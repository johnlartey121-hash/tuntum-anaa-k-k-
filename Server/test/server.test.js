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

async function pair(port) {
  const a = client(port), b = client(port);
  await Promise.all([a.open(), b.open()]);
  a.send({ t: 'create', name: 'Ama' });
  const { code } = await a.next('created');
  b.send({ t: 'join', code: code.toLowerCase(), name: 'Kofi' });
  const [sa, sb] = await Promise.all([a.next('start'), b.next('start')]);
  return { a, b, code, sa, sb };
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

test('joining a missing or full room gives a clear error', async () => {
  const s = await createServer({ port: 0, log: () => {} });
  try {
    const { a, b, code } = await pair(s.port);
    const c = client(s.port); await c.open();
    c.send({ t: 'join', code: 'ZZZZ', name: 'Yaw' });
    assert.match((await c.next('error')).msg, /No room/);
    c.send({ t: 'join', code, name: 'Yaw' });
    assert.match((await c.next('error')).msg, /full/);
    a.ws.close(); b.ws.close(); c.ws.close();
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

test('rematch starts a fresh deck only when both ask', async () => {
  const s = await createServer({ port: 0, log: () => {} });
  try {
    const { a, b } = await pair(s.port);
    const room = [...s.rooms.values()][0];
    room.i = 52; room.over = true;
    a.send({ t: 'rematch' });
    await b.next('rematch');
    b.send({ t: 'rematch' });
    const [sa, sb] = await Promise.all([a.next('start'), b.next('start')]);
    assert.strictEqual(sa.you, 0); assert.strictEqual(sb.you, 1);
    assert.strictEqual(room.i, 0);
    a.ws.close(); b.ws.close();
  } finally { await s.close(); }
});
