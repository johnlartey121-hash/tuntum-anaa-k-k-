'use strict';
const test = require('node:test');
const assert = require('node:assert');
const WebSocket = require('ws');
const { createServer, shuffledDeck, cleanName, isRed } = require('../server');

function client(port) {
  const ws = new WebSocket(`ws://127.0.0.1:${port}`);
  const inbox = [];
  const waiters = [];
  ws.on('message', d => {
    const m = JSON.parse(d.toString());
    const w = waiters.findIndex(x => x.t === m.t);
    if (w >= 0) waiters.splice(w, 1)[0].resolve(m); else inbox.push(m);
  });
  return {
    ws,
    open: () => new Promise(r => ws.once('open', r)),
    send: m => ws.send(JSON.stringify(m)),
    next: t => new Promise((resolve, reject) => {
      const k = inbox.findIndex(x => x.t === t);
      if (k >= 0) return resolve(inbox.splice(k, 1)[0]);
      const timer = setTimeout(() => reject(new Error('timeout waiting for ' + t)), 2000);
      waiters.push({ t, resolve: m => { clearTimeout(timer); resolve(m); } });
    }),
    inbox,
  };
}

async function room(port, names) {
  const cs = names.map(() => client(port));
  await Promise.all(cs.map(c => c.open()));
  cs[0].send({ t: 'create', name: names[0] });
  const { code } = await cs[0].next('created');
  for (let k = 1; k < cs.length; k++) { cs[k].send({ t: 'join', code: code.toLowerCase(), name: names[k] }); await cs[k].next('lobby'); }
  return { cs, code };
}
async function startRoom(cs) {
  cs[0].send({ t: 'start' });
  return Promise.all(cs.map(c => c.next('start')));
}
async function pair(port) {
  const { cs, code } = await room(port, ['Ama', 'Kofi']);
  const [sa, sb] = await startRoom(cs);
  return { a: cs[0], b: cs[1], code, sa, sb };
}

test('deck has 52 unique cards, 26 red', () => {
  const d = shuffledDeck();
  assert.strictEqual(new Set(d).size, 52);
  assert.strictEqual(d.filter(isRed).length, 26);
});

test('names are cleaned', () => {
  assert.strictEqual(cleanName('<b>Kofi</b>'), 'bKofib');
  assert.strictEqual(cleanName(''), 'Player');
  assert.strictEqual(cleanName('Ɔkɔɔ Ama'), 'Ɔkɔɔ Ama');
  assert.strictEqual(cleanName('x'.repeat(40)).length, 12);
});

test('a full online game: both phones see the same results and the game ends after 52 cards', async () => {
  const s = await createServer({ port: 0, log: () => {} });
  try {
    const { a, b, sa, sb } = await pair(s.port);
    assert.deepStrictEqual(sa.names, ['Ama', 'Kofi']);
    assert.strictEqual(sa.you, 0); assert.strictEqual(sb.you, 1);
    let turn = 0, scores = [0, 0];
    for (let i = 0; i < 52; i++) {
      const who = turn === 0 ? a : b;
      who.send({ t: 'guess', i, col: i % 3 ? 'r' : 'k' });
      const [ra, rb] = await Promise.all([a.next('result'), b.next('result')]);
      assert.deepStrictEqual(ra, rb);
      assert.strictEqual(ra.i, i);
      assert.strictEqual(ra.ok, isRed(ra.card) === (ra.col === 'r'));
      if (ra.ok) scores[turn]++;
      if (!ra.ok && i < 51) assert.strictEqual(ra.turn, 1 - turn);
      turn = ra.turn;
    }
    const room = [...s.rooms.values()][0];
    assert.ok(room.over);
    assert.deepStrictEqual(room.scores, scores);
    a.ws.close(); b.ws.close();
  } finally { await s.close(); }
});

test('out-of-turn and stale guesses are ignored', async () => {
  const s = await createServer({ port: 0, log: () => {} });
  try {
    const { a, b } = await pair(s.port);
    b.send({ t: 'guess', i: 0, col: 'r' });        // not Kofi's turn
    a.send({ t: 'guess', i: 5, col: 'r' });        // wrong card index
    a.send({ t: 'guess', i: 0, col: 'purple' });   // nonsense colour
    await new Promise(r => setTimeout(r, 150));
    assert.strictEqual(a.inbox.filter(m => m.t === 'result').length, 0);
    a.send({ t: 'guess', i: 0, col: 'k' });
    const r = await a.next('result');
    assert.strictEqual(r.i, 0);
    a.ws.close(); b.ws.close();
  } finally { await s.close(); }
});

test('joining a missing, full or started room gives a clear error', async () => {
  const s = await createServer({ port: 0, log: () => {} });
  try {
    const { cs, code } = await room(s.port, ['Ama', 'Kofi', 'Esi', 'Yaw']);
    const c = client(s.port); await c.open();
    c.send({ t: 'join', code: 'ZZZZ', name: 'Abena' });
    assert.match((await c.next('error')).msg, /No room/);
    c.send({ t: 'join', code, name: 'Abena' });
    assert.match((await c.next('error')).msg, /full/);
    const p = await pair(s.port);
    c.send({ t: 'join', code: p.code, name: 'Abena' });
    assert.match((await c.next('error')).msg, /already started/);
    [...cs, c, p.a, p.b].forEach(x => x.ws.close());
  } finally { await s.close(); }
});

test('a room made for 3 players turns away a 4th', async () => {
  const s = await createServer({ port: 0, log: () => {} });
  try {
    const cs = [client(s.port), client(s.port), client(s.port), client(s.port)];
    await Promise.all(cs.map(c => c.open()));
    cs[0].send({ t: 'create', name: 'Ama', max: 3 });
    const { code } = await cs[0].next('created');
    cs[1].send({ t: 'join', code, name: 'Kofi' }); assert.strictEqual((await cs[1].next('lobby')).max, 3);
    cs[2].send({ t: 'join', code, name: 'Esi' }); await cs[2].next('lobby');
    cs[3].send({ t: 'join', code, name: 'Yaw' }); assert.match((await cs[3].next('error')).msg, /full \(3 players\)/);
    cs.forEach(c => c.ws.close());
  } finally { await s.close(); }
});

test('four friends: lobby fills up, only the creator can start, turns go round all four', async () => {
  const s = await createServer({ port: 0, log: () => {} });
  try {
    const { cs } = await room(s.port, ['Ama', 'Kofi', 'Esi', 'Yaw']);
    const last = await cs[0].next('lobby'); // creator's latest lobby view
    cs[1].send({ t: 'start' });            // not the creator: ignored
    await new Promise(r => setTimeout(r, 100));
    assert.strictEqual(cs[1].inbox.filter(m => m.t === 'start').length, 0);
    const starts = await startRoom(cs);
    starts.forEach((st, k) => { assert.deepStrictEqual(st.names, ['Ama', 'Kofi', 'Esi', 'Yaw']); assert.strictEqual(st.you, k); });
    const seen = new Set([0]);
    let turn = 0;
    for (let i = 0; i < 52; i++) {
      cs[turn].send({ t: 'guess', i, col: i % 2 ? 'r' : 'k' });
      const rs = await Promise.all(cs.map(c => c.next('result')));
      rs.forEach(r => assert.deepStrictEqual(r, rs[0]));
      if (!rs[0].ok && i < 51) assert.strictEqual(rs[0].turn, (turn + 1) % 4);
      turn = rs[0].turn; seen.add(turn);
    }
    assert.strictEqual(seen.size, 4);
    cs.forEach(c => c.ws.close());
  } finally { await s.close(); }
});

test('a friend leaving the lobby updates everyone; leaving mid-game ends it', async () => {
  const s = await createServer({ port: 0, log: () => {} });
  try {
    const { cs } = await room(s.port, ['Ama', 'Kofi', 'Esi']);
    while (cs[0].inbox.length) cs[0].inbox.pop();
    cs[2].ws.close();
    let l;
    do { l = await cs[0].next('lobby'); } while (l.names.length !== 2);
    assert.deepStrictEqual(l.names, ['Ama', 'Kofi']);
    await startRoom([cs[0], cs[1]]);
    cs[1].ws.close();
    assert.strictEqual((await cs[0].next('left')).name, 'Kofi');
    cs[0].ws.close();
  } finally { await s.close(); }
});

test('when one player leaves, the other is told and the room closes', async () => {
  const s = await createServer({ port: 0, log: () => {} });
  try {
    const { a, b } = await pair(s.port);
    b.ws.close();
    const m = await a.next('left');
    assert.strictEqual(m.name, 'Kofi');
    await new Promise(r => setTimeout(r, 50));
    assert.strictEqual(s.rooms.size, 0);
    a.ws.close();
  } finally { await s.close(); }
});

test('a guest asks for a rematch, the creator starts it', async () => {
  const s = await createServer({ port: 0, log: () => {} });
  try {
    const { a, b } = await pair(s.port);
    const room = [...s.rooms.values()][0];
    room.i = 52; room.over = true;
    b.send({ t: 'rematch' });
    await a.next('rematch');
    a.send({ t: 'rematch' });
    const [sa, sb] = await Promise.all([a.next('start'), b.next('start')]);
    assert.strictEqual(sa.you, 0); assert.strictEqual(sb.you, 1);
    assert.strictEqual(room.i, 0);
    a.ws.close(); b.ws.close();
  } finally { await s.close(); }
});
