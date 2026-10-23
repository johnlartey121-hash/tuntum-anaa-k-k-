// Tuntum anaa Kɔkɔɔ room server.
// Up to four phones share a room by a 4-letter code. The creator starts the game when friends have joined. The server shuffles the deck and decides every guess,
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

const MAX_SEATS = 4;
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

  const lobby = room => both(room, { t: 'lobby', code: room.code, names: room.names });

  function startMatch(room) {
    const n = room.seats.length;
    Object.assign(room, { deck: shuffledDeck(), i: 0, turn: 0, scores: new Array(n).fill(0), over: false, playing: true });
    room.seats.forEach((ws, k) => send(ws, { t: 'start', code: room.code, names: room.names, you: k, turn: 0 }));
  }

  function leave(ws) {
    const room = ws.room;
    if (!room) return;
    ws.room = null;
    const k = room.seats.indexOf(ws);
    if (k < 0) return;
    const name = room.names[k];
    if (room.playing && !room.over) {
      // someone left mid-game: the game ends for everyone
      room.seats.forEach((s, j) => { if (j !== k) { send(s, { t: 'left', name }); s.room = null; } });
      rooms.delete(room.code);
      return;
    }
    room.seats.splice(k, 1);
    room.names.splice(k, 1);
    if (!room.seats.length || k === 0) {
      // the creator left the lobby: close the room
      room.seats.forEach(s => { send(s, { t: 'left', name }); s.room = null; });
      rooms.delete(room.code);
      return;
    }
    room.playing = false;
    lobby(room);
  }

  function handle(ws, m) {
    if (m.t === 'ping') return send(ws, { t: 'pong' });

    if (m.t === 'create') {
      leave(ws);
      const room = { code: newCode(), seats: [ws], names: [cleanName(m.name)], touched: Date.now(), playing: false };
      rooms.set(room.code, room);
      ws.room = room;
      send(ws, { t: 'created', code: room.code });
      return lobby(room);
    }

    if (m.t === 'join') {
      const code = String(m.code ?? '').toUpperCase().replace(/[^A-Z0-9]/g, '');
      const room = rooms.get(code);
      if (!room) return send(ws, { t: 'error', msg: 'No room with that code is open.' });
      if (room.seats.includes(ws)) return send(ws, { t: 'error', msg: "You're already in that room." });
      if (room.playing && !room.over) return send(ws, { t: 'error', msg: 'That game has already started.' });
      if (room.seats.length >= MAX_SEATS) return send(ws, { t: 'error', msg: 'That room is full (4 players).' });
      leave(ws);
      room.seats.push(ws);
      room.names.push(cleanName(m.name));
      ws.room = room;
      return lobby(room);
    }

    const room = ws.room;
    if (!room) return send(ws, { t: 'error', msg: 'You are not in a room.' });
    room.touched = Date.now();
    const seat = room.seats.indexOf(ws);

    if (m.t === 'start') {
      if (seat !== 0 || room.seats.length < 2 || (room.playing && !room.over)) return;
      return startMatch(room);
    }

    if (m.t === 'guess') {
      if (!room.playing || !room.deck || room.over) return;
      if (seat !== room.turn || m.i !== room.i || (m.col !== 'r' && m.col !== 'k')) return; // stale or out of turn: ignore
      const card = room.deck[room.i];
      const ok = isRed(card) === (m.col === 'r');
      const i = room.i++;
      if (ok) room.scores[seat]++;
      if (room.i >= room.deck.length) room.over = true;
      else if (!ok) room.turn = (room.turn + 1) % room.seats.length;
      return both(room, { t: 'result', i, col: m.col, card, ok, turn: room.turn });
    }

    if (m.t === 'rematch') {
      // the creator restarts; anyone else asks the creator
      if (!room.over) return;
      if (seat === 0) return startMatch(room);
      return send(room.seats[0], { t: 'rematch', name: room.names[seat] });
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
