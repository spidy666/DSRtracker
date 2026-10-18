// DSR Tracker – Web-Version ohne exe.
// Ersetzt den Tracker-Server im Browser: dieselben Seiten (Steuerung, Overlay) laufen unverändert,
// aber "/events" und "/api/..." werden hier lokal beantwortet. Der Stand liegt im localStorage
// dieses Browsers; Steuerseite (OBS-Dock) und Overlays (OBS-Browserquellen) teilen ihn.
// Kein Netzwerkverkehr außer dem Laden der Gegnerliste von derselben Adresse.
(function () {
  "use strict";
  const STORE = "dsr-web-state-v1";   // Haken, letzte Kills, Gebiet, Uhr
  const EVENT = "dsr-web-event-v1";   // letzte Kill-Meldung (für Einblendungen in anderen Fenstern)
  const BASE = document.currentScript ? document.currentScript.src.replace(/[^/]*$/, "") : "";
  let file = null;          // Gegnerliste
  const sources = [];       // offene "EventSource"-Ersatzobjekte dieses Fensters
  let bc = null; try { bc = new BroadcastChannel("dsr-web"); } catch { }

  // ---------------------------------------------------------------- gespeicherter Stand
  const empty = () => ({ v: 1, stamp: 0, manualDone: [], recentKills: [], areaOverride: -2,
                         clock: { runningSince: null, acc: 0 }, variant: "original" });
  function load() {
    try {
      const s = JSON.parse(localStorage.getItem(STORE));
      if (s && s.v === 1 && Array.isArray(s.manualDone)) return Object.assign(empty(), s);
    } catch { }
    return empty();
  }
  function save(s) {
    s.stamp = Date.now() + Math.random();
    try { localStorage.setItem(STORE, JSON.stringify(s)); } catch { }
    try { bc && bc.postMessage({ t: "state" }); } catch { }
    pushState();
  }
  const igtOf = s => s.clock.acc + (s.clock.runningSince ? Date.now() - s.clock.runningSince : 0);

  // ---------------------------------------------------------------- Zustand wie beim Tracker (GetStateJson)
  // Ansicht der Liste wie ListView.Build im Tracker: "original" oder "rando" (Randomizer-Namen + Zusatzplätze,
  // gleiche Schlüssel für die Originalplätze, Zusatzplätze als "Gebiet-(Anzahl+n)" hinter "after")
  function view(variant) {
    const r = variant === "rando" ? file.randomizer : null;
    const areas = file.areas.map((area, ai) => ({ area, enemies: area.enemies.map((e, ei) => {
      const key = `${ai}-${ei}`, n = r && r.names && r.names[key];
      return { key, e: n ? { de: n[0], en: n[1], pts: e.pts, tip: `Platz von ${e.de}: ${e.tip || ""}` } : e };
    }) }));
    const route = (file.route || []).map(rt => Object.assign({}, rt, { keys: rt.keys.slice() }));
    if (r) {
      const next = file.areas.map(a => a.enemies.length), lastAfter = {};
      for (const x of r.extra || []) {
        const key = `${x.area}-${next[x.area]++}`, anchor = lastAfter[x.after] || x.after;
        lastAfter[x.after] = key;
        const list = areas[x.area].enemies, i = list.findIndex(t => t.key === anchor);
        list.splice(i < 0 ? list.length : i + 1, 0, { key, e: { de: x.de, en: x.en, pts: x.pts, tip: x.tip || "" } });
        for (const rt of route) { const j = rt.keys.indexOf(anchor); if (j >= 0) { rt.keys.splice(j + 1, 0, key); break; } }
      }
    }
    return { areas, route };
  }
  const curVariant = s => s.variant === "rando" && file && file.randomizer ? "rando" : "original";
  function enemyAt(key, s) {
    if (!file) return null;
    for (const a of view(curVariant(s || load())).areas) for (const t of a.enemies) if (t.key === key) return t.e;
    return null;
  }
  function buildState() {
    const s = load(), done = new Set(s.manualDone), v = view(curVariant(s));
    let total = 0;
    const areas = v.areas.map(({ area, enemies: list }) => {
      const enemies = list.map(({ key, e }) => {
        const d = done.has(key);
        return { key, de: e.de, en: e.en, pts: e.pts, done: d, auto: false, manual: d, tip: e.tip || "", tracked: true };
      });
      const pts = enemies.filter(x => x.done).reduce((t, x) => t + x.pts, 0);
      total += pts;
      return { de: area.de, en: area.en, done: enemies.filter(x => x.done).length, count: enemies.length,
               pts, maxPts: enemies.reduce((t, x) => t + x.pts, 0), enemies };
    });
    const all = areas.flatMap(a => a.enemies);
    const igt = igtOf(s);
    return {
      type: "state", web: true,
      connected: false, playerLoaded: false,
      status: "Web-Version · Abhaken von Hand", gameVersion: null,
      total, maxTotal: all.reduce((t, x) => t + x.pts, 0),
      done: all.filter(x => x.done).length, count: all.length,
      areas, igt: igt > 0 || s.clock.runningSince ? igt : null,
      clockRunning: !!s.clock.runningSince,
      onboardingDone: true,
      currentArea: s.areaOverride, areaAuto: s.areaOverride === -2, autoArea: -2, pos: null,
      route: v.route,
      // Namen aus der aktuellen Ansicht; Zusatzplätze in der Original-Ansicht weglassen
      recentKills: s.recentKills.map(k => { const e = all.find(x => x.key === k.key); return e ? Object.assign({}, k, { de: e.de, en: e.en, pts: e.pts }) : null; }).filter(Boolean),
      flagLog: [],
      variant: curVariant(s), variantTitle: file.randomizer ? file.randomizer.title || "Randomizer" : null,
    };
  }

  // ---------------------------------------------------------------- Ersatz für EventSource("/events")
  function emit(obj) {
    const data = JSON.stringify(obj);
    for (const es of sources) if (!es.closed && es.onmessage) { try { es.onmessage({ data }); } catch (e) { console.error(e); } }
  }
  function pushState() { if (file) emit(buildState()); }
  function pushKill(k) { emit({ type: "kill", key: k.key, de: k.de, en: k.en, pts: k.pts }); }

  class LocalEventSource {
    constructor(url) {
      this.url = url; this.closed = false; this.onmessage = null; this.onerror = null; this.readyState = 1;
      sources.push(this);
      ready.then(() => pushState(), () => { if (this.onerror) this.onerror(new Event("error")); });
    }
    close() { this.closed = true; this.readyState = 2; const i = sources.indexOf(this); if (i >= 0) sources.splice(i, 1); }
    addEventListener(type, fn) { if (type === "message") this.onmessage = fn; else if (type === "error") this.onerror = fn; }
    removeEventListener() { }
  }
  window.EventSource = LocalEventSource;

  // ---------------------------------------------------------------- Ersatz für fetch("/api/...")
  function act(name, q) {
    const s = load();
    if (name === "toggle") {
      const key = q.get("key"), e = enemyAt(key, s);
      if (!e) return false;
      const i = s.manualDone.indexOf(key);
      if (i >= 0) { s.manualDone.splice(i, 1); s.recentKills = s.recentKills.filter(k => k.key !== key); save(s); return true; }
      s.manualDone.push(key);
      const t = new Date(), igt = igtOf(s);
      s.recentKills = s.recentKills.filter(k => k.key !== key);
      s.recentKills.unshift({ key, de: e.de, en: e.en, pts: e.pts,
        time: t.toTimeString().slice(0, 8), igt: s.clock.runningSince || s.clock.acc ? igt : null });
      save(s);
      announce({ key, de: e.de, en: e.en, pts: e.pts });
      return true;
    }
    if (name === "variant") {
      const v = q.get("v");
      if (v !== "original" && !(v === "rando" && file.randomizer)) return false;
      s.variant = v; save(s); return true;
    }
    if (name === "reset") { s.manualDone = []; s.recentKills = []; save(s); return true; }
    if (name === "area") { const i = parseInt(q.get("idx"), 10); s.areaOverride = i >= -1 && i < file.areas.length ? i : -2; save(s); return true; }
    if (name === "test-toast") { announce({ key: "test", de: "Test-Einblendung", en: "Test notification", pts: 5 }); return true; }
    if (name === "clock") {
      const c = s.clock, op = q.get("op");
      if (op === "start" && !c.runningSince) c.runningSince = Date.now();
      else if (op === "pause" && c.runningSince) { c.acc += Date.now() - c.runningSince; c.runningSince = null; }
      else if (op === "reset") { c.acc = 0; c.runningSince = null; }
      save(s); return true;
    }
    if (name === "clearlog" || name === "onboarding-done") return true;
    return false;
  }
  function announce(k) {
    const ev = Object.assign({ n: Date.now() + Math.random() }, k);
    try { localStorage.setItem(EVENT, JSON.stringify(ev)); } catch { }
    try { bc && bc.postMessage({ t: "kill", ev }); } catch { }
    lastEvent = ev.n;
    pushKill(k);
  }
  const realFetch = window.fetch.bind(window);
  window.fetch = function (input, init) {
    const url = typeof input === "string" ? input : (input && input.url) || "";
    const m = /(?:^|\/)api\/([a-z-]+)(\?.*)?$/.exec(url.replace(/#.*$/, ""));
    if (!m) return realFetch(input, init);
    return ready.then(() => {
      if (m[1] === "state") return new Response(JSON.stringify(buildState()), { headers: { "Content-Type": "application/json" } });
      const ok = act(m[1], new URLSearchParams(m[2] || ""));
      return new Response(ok ? "ok" : "unknown", { status: ok ? 200 : 404 });
    });
  };

  // ---------------------------------------------------------------- Änderungen aus anderen Fenstern (Dock ↔ Quellen)
  let lastStamp = load().stamp, lastEvent = (() => { try { return (JSON.parse(localStorage.getItem(EVENT)) || {}).n || 0; } catch { return 0; } })();
  function checkOthers() {
    const s = load();
    if (s.stamp !== lastStamp) { lastStamp = s.stamp; pushState(); }
    let ev = null; try { ev = JSON.parse(localStorage.getItem(EVENT)); } catch { }
    if (ev && ev.n && ev.n !== lastEvent) { lastEvent = ev.n; if (Date.now() - ev.n < 10000) pushKill(ev); }
  }
  window.addEventListener("storage", e => { if (e.key === STORE || e.key === EVENT) checkOthers(); });
  if (bc) bc.onmessage = m => {
    if (m.data && m.data.t === "kill" && m.data.ev && m.data.ev.n !== lastEvent) { lastEvent = m.data.ev.n; pushKill(m.data.ev); }
    else checkOthers();
  };
  // Sicherheitsnetz, falls OBS keine Ereignisse zwischen Dock und Quelle weiterreicht
  setInterval(checkOthers, 700);
  // Uhr: jede Sekunde die Zeit schicken, solange sie läuft
  setInterval(() => { const s = load(); if (s.clock.runningSince) emit({ type: "time", igt: igtOf(s) }); }, 1000);

  // ---------------------------------------------------------------- Gegnerliste laden
  const ready = realFetch(BASE + "dsr_enemies.json", { cache: "no-cache" })
    .then(r => { if (!r.ok) throw new Error("Gegnerliste nicht gefunden"); return r.json(); })
    .then(j => { file = j; });
  ready.catch(e => console.error("DSR Web:", e));

  // ---------------------------------------------------------------- Ergänzungen für die Steuerseite
  window.DSR_WEB = { act: (name, params) => act(name, new URLSearchParams(params || "")), state: () => file ? buildState() : null };
})();
