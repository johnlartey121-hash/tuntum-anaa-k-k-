// Plays a short 4-player game against the live server. Usage: node tools/live-check.js wss://tuntum-anaa-kokoo.fly.dev
'use strict';
const WebSocket = require('ws');
const url = process.argv[2];
const names = ['Ama', 'Kofi', 'Esi', 'Yaw'];
function client(name) {
  const ws = new WebSocket(url); const inbox = []; const waits = [];
  ws.on('message', d => { const m = JSON.parse(d); const w = waits.findIndex(x => x.t === m.t); if (w >= 0) waits.splice(w, 1)[0].r(m); else inbox.push(m); });
  return { ws, name, open: () => new Promise((r, j) => { ws.once('open', r); ws.once('error', j); }), send: m => ws.send(JSON.stringify(m)),
    next: t => new Promise((r, j) => { const k = inbox.findIndex(x => x.t === t); if (k >= 0) return r(inbox.splice(k, 1)[0]);
      const to = setTimeout(() => j(new Error(name + ' timed out waiting for ' + t + ' (inbox: ' + JSON.stringify(inbox) + ')')), 15000); waits.push({ t, r: m => { clearTimeout(to); r(m); } }); }) };
}
(async () => {
  const cs = names.map(client);
  await Promise.all(cs.map(c => c.open()));
  console.log('4 connections open');
  cs[0].send({ t: 'create', name: 'Ama' });
  const { code } = await cs[0].next('created');
  console.log('room', code);
  for (const c of cs.slice(1)) { c.send({ t: 'join', code, name: c.name }); const l = await c.next('lobby'); console.log(c.name, 'joined, lobby:', l.names.join(', ')); }
  cs[0].send({ t: 'start' });
  const st = await Promise.all(cs.map(c => c.next('start')));
  console.log('started for', st.map(s => s.you).join(','), st[0].names.join(', '));
  let turn = 0;
  for (let i = 0; i < 12; i++) { cs[turn].send({ t: 'guess', i, col: 'r' }); const rs = await Promise.all(cs.map(c => c.next('result'))); turn = rs[0].turn; }
  console.log('12 guesses played, all phones agreed. LIVE SERVER OK');
  cs.forEach(c => c.ws.close()); process.exit(0);
})().catch(e => { console.error('LIVE CHECK FAILED:', e.message); process.exit(1); });
