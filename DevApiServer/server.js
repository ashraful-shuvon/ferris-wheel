// Minimal mock of the games backend, just enough to exercise
// Assets/Scripts/Net/ApiClient.cs and ApiModels.cs from Unity or a browser.
//
// Run:  npm install   then   npm start
// Serves on http://localhost:5002, API under /api/v1/...

const express = require("express");
const cors = require("cors");

const app = express();
app.use(cors());
app.use(express.json());
app.use(express.static("public"));

const PORT = 5002;
const GAME_KEY = "ferris_wheel";

// ── In-memory fake state ────────────────────────────────────────────────────

// ITEMS no longer have a hardcoded category. Category + multiplier are
// authoritative in Unity's BetButtonData ScriptableObjects. Unity pushes
// the real mapping to POST /register-items on startup so the server's
// payout logic always matches what the Editor has configured.
// This default is only used if Unity hasn't registered yet (e.g. hitting
// the API from a browser before the game connects).
let ITEMS = [
  { key: "B1", label: "x2", multiplier: 2, category: "Vegetable" },
  { key: "B2", label: "x3", multiplier: 3, category: "Vegetable" },
  { key: "B3", label: "x5", multiplier: 5, category: "Vegetable" },
  { key: "B4", label: "x10", multiplier: 10, category: "Vegetable" },
  { key: "B5", label: "x2", multiplier: 2, category: "Pizza" },
  { key: "B6", label: "x3", multiplier: 3, category: "Pizza" },
  { key: "B7", label: "x5", multiplier: 5, category: "Pizza" },
  { key: "B8", label: "x10", multiplier: 10, category: "Pizza" },
];

// Server decides everything — not the client. Flip these (or wire them to
// an admin endpoint) to test combinations without restarting the server.
let winMode = "single";       // "single" | "combo"
let winCategory = "Pizza";    // "Pizza" | "Vegetable" — which category wins each round
const MAX_ROUND_HISTORY = 20;
let roundHistory = [
  "B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8",
  "B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8",
  "B1", "B2", "B3", "B4",
];

const CONFIG = {
  gameKey: GAME_KEY,
  title: "Ferris Wheel",
  currency: "coins",
  rtpPercent: 95,
  get items() { return ITEMS; },
  betTiers: [10, 50, 100, 500],
  bettingMs: 15000,
  spinMs: 4000,
  intermissionMs: 0,      // legacy — unused in the 3-phase loop
  warmupMs: 3000,         // "Show Time" — the win/lose panel window (Bet Time → Drawing → Show Time)
  spinIntervalStartMs: 70,  // visual chase tick at the START of the spin (FAST — small value)
  spinIntervalEndMs: 350,   // visual chase tick at the END of the spin  (SLOW — large value)
  spinCurveType: "easeOut", // deceleration easing: linear | easeIn | easeOut | easeInOut
  get winMode() { return winMode; },
};

// `avatar` = the field the Unity BalanceDto reads (used for the "You" ranking
// row). `avatarUrl` kept too since the leaderboard code below references it.
let balance = { username: "You", coins: 1000, diamonds: 0, frozen: false, todaysWin: 0, avatar: "https://picsum.photos/id/64/100/100", avatarUrl: "https://picsum.photos/id/64/100/100" };

let serverJackpot = 123456788;
setInterval(() => {
  serverJackpot += Math.floor(Math.random() * 41) + 10; // ticks up by 10-50 per second on the server
}, 1000);

// Fake other players, just so the leaderboard has more than one row.
const otherPlayers = Array.from({ length: 24 }, (_, i) => ({
  username: `Bot_Player_${i + 2}`,
  avatarUrl: `https://picsum.photos/id/${(i + 10) % 1000}/100/100`,
  todaysWin: Math.round(10000 / (i + 1.2)) // ranging from 8333 down to 396 wins
}));

let currentRoundWinners = [];
let personalRecords = [];
let round = makeRound(1);

function makeRound(roundNumber) {
  const now = Date.now();

  // Hot Item: ~50/50 each new round whether ANY item is "hot". When it is,
  // pick one at random from whatever Unity has registered. Rolled once here
  // (not a live getter) so it stays fixed for the round's whole lifetime.
  const hotItem = (ITEMS.length > 0 && Math.random() < 0.5)
    ? ITEMS[Math.floor(Math.random() * ITEMS.length)].key
    : null;
  console.log(`[hotItem] round ${roundNumber}: ${hotItem || "(none this round)"}`);

  return {
    id: "round-" + roundNumber,
    gameKey: GAME_KEY,
    roundNumber,
    phase: "betting",
    startedAt: new Date(now).toISOString(),
    bettingClosesAt: new Date(now + CONFIG.bettingMs).toISOString(),
    spinEndsAt: new Date(now + CONFIG.bettingMs + CONFIG.spinMs).toISOString(),
    warmupEndsAt: null,     // set once the round enters "warmup" (Show Time)
    currency: "coins",
    get items() { return ITEMS; },
    rtpPercent: CONFIG.rtpPercent,
    betCount: 0,
    totalBet: 0,
    winningItem: null,
    winningItems: null,
    // Live getters — always reflect whatever admin has set, even mid-round.
    get winMode() { return winMode; },
    get winCategory() { return winCategory; },
    totalPayout: 0,
    hotItem,
    betsByItem: {},
    playerBetsByItem: {},
  };
}

// Decides which item(s) win this round from (winCategory, winMode) using
// the Unity-registered ITEMS mapping — never the old hardcoded values.
function pickWinners() {
  const pool = ITEMS.filter((i) => i.category === winCategory);
  if (pool.length === 0) {
    console.warn(`[pickWinners] No items found for category "${winCategory}". Check /register-items was called.`);
    return { winningItem: null, winningItems: [] };
  }

  if (winMode === "combo") {
    const items = pool.map((i) => i.key);
    return { winningItem: items[0], winningItems: items };
  }

  const chosen = pool[Math.floor(Math.random() * pool.length)].key;
  return { winningItem: chosen, winningItems: [chosen] };
}

// Advance the fake round through phases on a timer so /current looks "live".
setInterval(() => {
  const now = Date.now();
  
  // --- ADDED: Simulate other players (bots) betting randomly ---
  if (round.phase === "betting" && ITEMS && ITEMS.length > 0) {
    // ~20% chance to generate a fake bot bet every 500ms tick
    if (Math.random() < 0.20) {
      const item = ITEMS[Math.floor(Math.random() * ITEMS.length)];
      // Pick a random bet tier (10, 50, 100, 500)
      const amount = CONFIG.betTiers[Math.floor(Math.random() * CONFIG.betTiers.length)];
      
      round.betCount += 1;
      round.totalBet += amount;
      round.betsByItem[item.key] = (round.betsByItem[item.key] || 0) + amount;
      console.log(`[Bot] Simulated player bet ${amount} on ${item.key}`);
    }
  }
  // -------------------------------------------------------------

  if (round.phase === "betting" && now >= new Date(round.bettingClosesAt).getTime()) {
    round.phase = "spinning";
    const { winningItem, winningItems } = pickWinners();
    round.winningItem = winningItem;
    round.winningItems = winningItems;
  } else if (round.phase === "spinning" && now >= new Date(round.spinEndsAt).getTime()) {
    settleRound();
    // Enter "warmup" = Show Time (the WinLosePanel window). 3-phase loop:
    // Bet Time (betting) -> Drawing (spinning) -> Show Time (warmup) -> betting.
    round.phase = "warmup";
    round.warmupEndsAt = new Date(now + CONFIG.warmupMs).toISOString();
  } else if (round.phase === "warmup" && now >= new Date(round.warmupEndsAt).getTime()) {
    // 3-phase loop: Show Time (warmup) ends → straight to the next Bet Time.
    round = makeRound(round.roundNumber + 1);
  }
}, 500);

// Pays out the real player's winning bet(s) using the Unity-registered
// multipliers — always in sync with what the Editor has configured.
function settleRound() {
  console.log(`[settle] round ${round.roundNumber} winCategory=${winCategory} winMode=${winMode} winningItems=${round.winningItems}`);

  if (round.winningItem) {
    const historyKey = winMode === "combo" ? `combo_${winCategory}` : round.winningItem;
    roundHistory.unshift(historyKey);
    if (roundHistory.length > MAX_ROUND_HISTORY) {
      roundHistory.pop();
    }
  }

  let roundWinners = [];

  let payout = 0;
  for (const key of round.winningItems || []) {
    const item = ITEMS.find((i) => i.key === key);
    const stake = round.playerBetsByItem[key] || 0;
    if (item && stake > 0) payout += Math.round(stake * item.multiplier);
  }

  if (payout > 0) {
    balance.coins += payout;
    balance.todaysWin += payout;
    round.totalPayout = payout;
    roundWinners.push({ username: "You", wonAmount: payout, todaysWin: balance.todaysWin, avatar: balance.avatar });
  }

  for (const p of otherPlayers) {
    if (Math.random() < 0.6) {
      const botPayout = Math.round(Math.random() * 8000);
      p.todaysWin += botPayout;
      roundWinners.push({ username: p.username, wonAmount: botPayout, todaysWin: p.todaysWin, avatar: p.avatarUrl });
    }
  }

  roundWinners.sort((a, b) => b.wonAmount - a.wonAmount);
  currentRoundWinners = roundWinners.map((w, idx) => ({
    rank: idx + 1,
    username: w.username,
    wonAmount: w.wonAmount,
    todaysWin: w.todaysWin,
    avatar: w.avatar
  }));

  // ── RECORD GAME PLAYED FOR PLAYER ──
  let totalBetThisRound = Object.values(round.playerBetsByItem).reduce((a, b) => a + b, 0);
  if (totalBetThisRound > 0) {
    // ISO timestamp — matches the real backend so the records card renders the
    // same way in local dev as in prod.
    const timestamp = new Date().toISOString();

    let balanceBefore = balance.coins + totalBetThisRound;
    let balanceAfter = balance.coins + payout;

    personalRecords.unshift({
      roundNumber: round.roundNumber,
      timestamp: timestamp,
      selectedFood: Object.entries(round.playerBetsByItem).map(([key, amount]) => ({ key, amount })),
      winningFruits: round.winningItems || [],
      winCoins: payout,
      balanceBefore: balanceBefore,
      balanceAfter: balanceAfter
    });

    if (personalRecords.length > 20) {
      personalRecords.pop();
    }
  }
}

function envelope(dataKey, value) {
  // `meta.timestamp` mirrors the real backend's ResponseInterceptor — the Unity
  // client reads it off /current to sync its countdowns to the server clock.
  return {
    success: true,
    data: { [dataKey]: value },
    meta: { timestamp: new Date().toISOString() },
  };
}

function requireToken(req, res, next) {
  const auth = req.headers.authorization || "";
  console.log(`[${req.method}] ${req.originalUrl}  Authorization: ${auth || "(none)"}`);
  next(); // dev server accepts any token, or none — just logs it
}

app.use(["/api/v1", "/sandbox/v1"], requireToken);

// ── Endpoints Router (must match ApiClient.cs routes exactly) ────────────────
const gamesRouter = express.Router({ mergeParams: true });

gamesRouter.get("/config", (req, res) => {
  const configCopy = { ...CONFIG, gameKey: req.params.gameKey };
  res.json(envelope("config", configCopy));
});

gamesRouter.get("/jackpot", (req, res) => {
  const now = new Date();
  const nextMidnight = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() + 1, 0, 0, 0, 0));
  res.json(envelope("jackpot", { jackpot: serverJackpot, endsAt: nextMidnight.toISOString() }));
});

// Unity calls this once on startup (server mode) to push the authoritative
// button layout — buttonID, category, multiplier — from BetButtonData
// ScriptableObjects. This replaces the old hardcoded ITEMS categories so
// the server's pickWinners() and settleRound() always match the Editor.
gamesRouter.post("/register-items", (req, res) => {
  const { items } = req.body || {};
  if (!Array.isArray(items) || items.length === 0) {
    return res.status(400).json({
      success: false,
      error: { code: "BAD_REQUEST", message: "items must be a non-empty array" },
    });
  }

  // Validate each entry has the required fields.
  for (const item of items) {
    if (!item.key || !item.category || typeof item.multiplier !== "number") {
      return res.status(400).json({
        success: false,
        error: { code: "BAD_REQUEST", message: `Invalid item: ${JSON.stringify(item)}` },
      });
    }
  }

  ITEMS = items;
  console.log(`[register-items] Updated ${ITEMS.length} items from Unity:`);
  ITEMS.forEach((i) => console.log(`  ${i.key}  category=${i.category}  multiplier=${i.multiplier}`));

  res.json({ success: true, data: { registeredCount: ITEMS.length } });
});

// Dev/admin-only — flips win mode at runtime.
gamesRouter.post("/win-mode", (req, res) => {
  const { mode } = req.body || {};
  if (mode !== "single" && mode !== "combo") {
    return res.status(400).json({
      success: false,
      error: { code: "BAD_REQUEST", message: 'mode must be "single" or "combo"' },
    });
  }
  winMode = mode;
  res.json({ success: true, data: { winMode } });
});

// Dev/admin-only — picks which category wins each round.
gamesRouter.post("/win-category", (req, res) => {
  const { category } = req.body || {};
  const validCategories = [...new Set(ITEMS.map((i) => i.category))];
  if (!validCategories.includes(category)) {
    return res.status(400).json({
      success: false,
      error: { code: "BAD_REQUEST", message: `category must be one of: ${validCategories.join(", ")}` },
    });
  }
  winCategory = category;
  res.json({ success: true, data: { winCategory } });
});

gamesRouter.get("/current", (req, res) => {
  const roundCopy = { ...round, gameKey: req.params.gameKey };
  res.json(envelope("round", roundCopy));
});

gamesRouter.get("/history", (req, res) => {
  res.json(envelope("history", roundHistory));
});

gamesRouter.get("/balance", (req, res) => {
  res.json(envelope("balance", balance));
});

gamesRouter.get("/leaderboard/today", (req, res) => {
  const all = [{ username: "You", todaysWin: balance.todaysWin, avatarUrl: balance.avatarUrl || "https://picsum.photos/100?random=1" }, ...otherPlayers];
  const ranked = all
      .sort((a, b) => b.todaysWin - a.todaysWin)
      .map((p, i) => ({ rank: i + 1, username: p.username, wonAmount: p.todaysWin, todaysWin: p.todaysWin, avatar: p.avatarUrl }));
  res.json(envelope("entries", ranked));
});

// Fake yesterday's player data for the yesterday leaderboard.
const yesterdayPlayers = Array.from({ length: 25 }, (_, i) => ({
  username: i === 5 ? "You" : `BotYesterday${i + 2}`,
  avatarUrl: i === 5 ? "https://picsum.photos/id/64/100/100" : `https://picsum.photos/id/${(i + 100) % 1000}/100/100`,
  wonAmount: Math.round(15000000 / (i + 1))
}));

gamesRouter.get("/leaderboard/yesterday", (req, res) => {
  const ranked = yesterdayPlayers
      .sort((a, b) => b.wonAmount - a.wonAmount)
      .map((p, i) => ({ rank: i + 1, username: p.username, wonAmount: p.wonAmount, todaysWin: p.wonAmount, avatar: p.avatarUrl }));
  res.json(envelope("entries", ranked));
});

gamesRouter.get("/leaderboard/round", (req, res) => {
  res.json(envelope("entries", currentRoundWinners));
});

gamesRouter.get("/records", (req, res) => {
  res.json(envelope("records", personalRecords));
});

gamesRouter.post("/balance/add", (req, res) => {
  const { amount } = req.body || {};
  if (typeof amount !== "number" || amount === 0) {
    return res.status(400).json({
      success: false,
      error: { code: "BAD_REQUEST", message: "amount must be a non-zero number" },
    });
  }
  balance.coins = Math.max(0, balance.coins + amount);
  res.json(envelope("balance", balance));
});

gamesRouter.post("/bet", (req, res) => {
  const { item, amount } = req.body || {};
  if (!item || !amount || amount <= 0) {
    return res.status(400).json({
      success: false,
      error: { code: "BAD_REQUEST", message: "item and a positive amount are required" },
    });
  }
  if (round.phase !== "betting") {
    return res.status(409).json({
      success: false,
      error: { code: "BETTING_CLOSED", message: "Betting is closed for this round" },
    });
  }
  if (amount > balance.coins) {
    return res.status(402).json({
      success: false,
      error: { code: "INSUFFICIENT_FUNDS", message: "Not enough coins" },
    });
  }

  balance.coins -= amount;
  round.betCount += 1;
  round.totalBet += amount;
  round.betsByItem[item] = (round.betsByItem[item] || 0) + amount;
  round.playerBetsByItem[item] = (round.playerBetsByItem[item] || 0) + amount;

  const roundCopy = { ...round, gameKey: req.params.gameKey };
  res.json(envelope("round", roundCopy));
});

gamesRouter.post("/config/update", (req, res) => {
  const { bettingMs, spinMs, intermissionMs, warmupMs,
          spinIntervalStartMs, spinIntervalEndMs, spinCurveType } = req.body;

  // Update the CONFIG object values (use != null so 0 is accepted)
  if (bettingMs != null) CONFIG.bettingMs = bettingMs;
  if (spinMs != null) CONFIG.spinMs = spinMs;
  if (intermissionMs != null) CONFIG.intermissionMs = intermissionMs;
  if (warmupMs != null) CONFIG.warmupMs = warmupMs;
  if (spinIntervalStartMs != null) CONFIG.spinIntervalStartMs = spinIntervalStartMs;
  if (spinIntervalEndMs != null) CONFIG.spinIntervalEndMs = spinIntervalEndMs;
  if (spinCurveType != null) CONFIG.spinCurveType = spinCurveType;

  console.log(`[Config] Updated timings: Betting=${CONFIG.bettingMs}ms, Spin=${CONFIG.spinMs}ms, ` +
              `SpinInterval=${CONFIG.spinIntervalStartMs}->${CONFIG.spinIntervalEndMs}ms`);
  const configCopy = { ...CONFIG, gameKey: req.params.gameKey };
  res.json({ success: true, config: configCopy });
});

// Mount the router on both possible game prefixes
app.use(["/api/v1/games/:gameKey", "/sandbox/v1/games/:gameKey"], gamesRouter);

const HOST = "127.0.0.1";
app.listen(PORT, HOST, () => {
  console.log(`Ferris Wheel dev API running at http://${HOST}:${PORT}`);
  console.log(`Test page:           http://${HOST}:${PORT}/index.html`);
  console.log(`API base for Unity:  http://${HOST}:${PORT}/api/v1`);
});
