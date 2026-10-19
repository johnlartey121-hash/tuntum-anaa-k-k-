// Tuntum anaa Kɔkɔɔ room server.
// Two phones share a room by a 4-letter code. The server shuffles the deck and decides every guess,
// so neither phone ever knows the next card before it is turned over.
'use strict';
const http = require('http');
const crypto = require('crypto');
const { WebSocketServer } = require('ws');

const SUITS = 'SCHD';
const RANKS = ['A', '2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K'];
const CODE_CHARS = 'ABCDEFGHJKMNPQRSTUVWXYZ23456789';
const ROOM_IDLE_MS = 30 * 60 * 1000;
const MAX_MSG_BYTES = 512;
const MAX_MSGS_PER_10S = 60;

const isRed = card => card.endsWith('H') || card.endsWith('D');

function shuffledDeck(rand = crypto.randomInt) {
  const d = [];
  for (const s of SUITS) for (const r of RANKS) d.push(r + s);
  for (let i = d.length - 1; i > 0; i--) { const j = rand(i + 1); [d[i], d[j]] = [d[j], d[i]]; }
  return d;
}

function cleanName(s) {
  const n = String(s ?? '').normalize('NFC').replace(/[^\p{L}\p{N} '\-]/gu, '').trim().slice(0, 12);
  return n || 'Player';
}

function createServer({ port = 8080, log = console.log } = {}) {
  const rooms = new Map();

  const httpServer = http.createServer((req, res) => {
    if (req.url === '/health') { res.writeHead(200, { 'content-type': 'text/plain' }); res.end('ok'); return; }
    res.writeHead(200, { 'content-type': 'text/plain' });
    res.end(`Tuntum anaa Kɔkɔɔ server. Rooms open: ${rooms.size}\n`);
  });
  const wss = new WebSocketServer({ server: httpServer, maxPayload: MAX_MSG_BYTES });

  const send = (ws, msg) => { if (ws && ws.readyState === 1) ws.send(JSON.stringify(msg)); };
  const both = (room, msg) => room.seats.forEach(ws => send(ws, msg));

  function newCode() {
    for (;;) {
      let c = '';
      for (let k = 0; k < 4; k++) c += CODE_CHARS[crypto.randomInt(CODE_CHARS.length)];
      if (!rooms.has(c)) return c;
    }
  }

  function startMatch(room) {
    Object.assign(room, { deck: shuffledDeck(), i: 0, turn: 0, scores: [0, 0], over: false, rematch: [false, false] });
    room.seats.forEach((ws, k) => send(ws, { t: 'start', code: room.code, names: room.names, you: k, turn: 0 }));
  }

  function leave(ws) {
    const room = ws.room;
    if (!room) return;
    ws.room = null;
    const k = room.seats.indexOf(ws);
    if (k >= 0) room.seats[k] = null;
    const other = room.seats.find(s => s);
    if (other) send(other, { t: 'left', name: room.names[k] });
    rooms.delete(room.code); // a room ends when either player leaves
    if (other) other.room = null;
  }

  function handle(ws, m) {
    if (m.t === 'ping') return send(ws, { t: 'pong' });

    if (m.t === 'create') {
      leave(ws);
      const room = { code: newCode(), seats: [ws, null], names: [cleanName(m.name), ''], touched: Date.now() };
      rooms.set(room.code, room);
      ws.room = room;
      return send(ws, { t: 'created', code: room.code });
    }

    if (m.t === 'join') {
      const code = String(m.code ?? '').toUpperCase().replace(/[^A-Z0-9]/g, '');
      const room = rooms.get(code);
      if (!room) return send(ws, { t: 'error', msg: 'No room with that code is open.' });
      if (room.seats[1]) return send(ws, { t: 'error', msg: 'That room is already full.' });
      if (room.seats[0] === ws) return send(ws, { t: 'error', msg: "That's your own room." });
      leave(ws);
      room.seats[1] = ws;
      room.names[1] = cleanName(m.name);
      ws.room = room;
      return startMatch(room);
    }

    const room = ws.room;
    if (!room) return send(ws, { t: 'error', msg: 'You are not in a room.' });
    room.touched = Date.now();
    const seat = room.seats.indexOf(ws);

    if (m.t === 'guess') {
      if (!room.deck || room.over || !room.seats[1]) return;
      if (seat !== room.turn || m.i !== room.i || (m.col !== 'r' && m.col !== 'k')) return; // stale or out of turn: ignore
      const card = room.deck[room.i];
      const ok = isRed(card) === (m.col === 'r');
      const i = room.i++;
      if (ok) room.scores[seat]++;
      if (room.i >= room.deck.length) room.over = true;
      else if (!ok) room.turn = 1 - room.turn;
      return both(room, { t: 'result', i, col: m.col, card, ok, turn: room.turn });
    }

    if (m.t === 'rematch') {
      if (!room.over) return;
      room.rematch[seat] = true;
      if (room.rematch[0] && room.rematch[1]) return startMatch(room);
      const other = room.seats[1 - seat];
      return send(other, { t: 'rematch', name: room.names[seat] });
    }
  }

  wss.on('connection', ws => {
    ws.isAlive = true;
    ws.msgTimes = [];
    ws.on('pong', () => { ws.isAlive = true; });
    ws.on('message', raw => {
      const now = Date.now();
      ws.msgTimes = ws.msgTimes.filter(t => now - t < 10000);
      if (ws.msgTimes.push(now) > MAX_MSGS_PER_10S) return send(ws, { t: 'error', msg: 'Too many messages. Slow down.' });
      let m;
      try { m = JSON.parse(raw.toString()); } catch { return; }
      if (!m || typeof m.t !== 'string') return;
      try { handle(ws, m); } catch (e) { log('handler error', e); }
    });
    ws.on('close', () => leave(ws));
  });

  const sweep = setInterval(() => {
    for (const ws of wss.clients) {
      if (!ws.isAlive) { ws.terminate(); continue; }
      ws.isAlive = false;
      ws.ping();
    }
    const now = Date.now();
    for (const [code, room] of rooms) if (now - room.touched > ROOM_IDLE_MS) { room.seats.forEach(s => s && s.close()); rooms.delete(code); }
  }, 30000);
  sweep.unref();

  return new Promise(resolve => httpServer.listen(port, () => {
    resolve({
      port: httpServer.address().port,
      rooms,
      close: () => new Promise(r => { clearInterval(sweep); for (const c of wss.clients) c.terminate(); wss.close(); httpServer.close(() => r()); }),
    });
  }));
}

module.exports = { createServer, shuffledDeck, cleanName, isRed };

if (require.main === module) {
  createServer({ port: Number(process.env.PORT) || 8080 }).then(s => console.log(`Tuntum server on :${s.port}`));
}
