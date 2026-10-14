// DSR Tracker – Web-Version: passt die Steuerseite an (läuft nach dem Seiten-Skript).
// Blendet Exe-spezifische Teile aus und ergänzt Uhr sowie Sichern/Laden des Stands.
(function () {
  "use strict";
  if (!window.DSR_WEB) return;
  const $ = id => document.getElementById(id);
  const cardOf = el => el && el.closest("section.card");
  const hint = (card, html) => { const p = card && card.querySelector("p.hint"); if (p) p.innerHTML = html; };

  document.body.classList.add("web");
  const st = document.createElement("style");
  st.textContent = "body.web #dot{background:#7fa6d8}";
  document.head.appendChild(st);
  const nav = document.querySelector("header nav");
  if (nav) nav.innerHTML = `<a href="index.html" target="_blank" style="color:var(--muted)">Einrichtung</a><a href="guide.html" target="_blank" style="color:var(--muted)">Guide</a>`;

  // Legende: "auto" gibt es hier nicht
  const legend = document.querySelector(".legend");
  if (legend) legend.querySelectorAll(".tag.auto, .tag.only").forEach(t => {
    const n = t.nextSibling; if (n && n.nodeType === 3) n.remove(); t.remove();
  });

  hint(cardOf($("reset")), "Web-Version: Alle Gegner werden von Hand abgehakt. Der Stand bleibt in diesem Browser (bzw. in OBS) gespeichert, auch nach einem Neustart.");
  const reset = $("reset"); if (reset) reset.textContent = "Alle Haken zurücksetzen";
  hint(cardOf($("areaSel")), "Für die Overlay-Ansicht „Aktuelles Gebiet“. Ohne exe gibt es keine automatische Erkennung – hier das Gebiet einstellen, in dem du gerade spielst.");
  const auto = $("areaSel") && $("areaSel").querySelector('option[value="-2"]'); if (auto) auto.textContent = "– keins –";
  if ($("posInfo")) $("posInfo").style.display = "none";
  hint(cardOf($("splits")), "Jeder Kill mit der Zeit der Run-Uhr. Kills ohne Zeit wurden abgehakt, bevor die Uhr lief.");
  const flag = cardOf($("log")); if (flag) flag.style.display = "none";

  // ---- Run-Uhr (ersetzt die Spielzeit)
  const clock = document.createElement("section");
  clock.className = "card";
  clock.innerHTML = `<h2>Run-Uhr <span class="meta" id="w-clock">–</span></h2>
    <div class="body">
      <p class="hint">Ohne exe kennt die Web-Version die Spielzeit nicht. Diese Uhr ersetzt sie für Zeit und Punkte pro Stunde im Overlay.</p>
      <div class="row"><button id="w-start">Start</button><button id="w-pause">Pause</button><button class="danger" id="w-creset">Uhr auf 0</button></div>
    </div>`;
  const runCard = cardOf($("reset"));
  if (runCard) runCard.after(clock);
  const fmt = ms => { if (ms == null) return "–"; const t = Math.floor(ms / 1000); return `${Math.floor(t / 3600)}:${String(Math.floor(t / 60) % 60).padStart(2, "0")}:${String(t % 60).padStart(2, "0")}`; };
  const showClock = () => { const s = DSR_WEB.state(); if (!s) return;
    $("w-clock").textContent = fmt(s.igt) + (s.clockRunning ? " · läuft" : s.igt ? " · pausiert" : "");
    $("w-start").disabled = s.clockRunning; $("w-pause").disabled = !s.clockRunning; };
  $("w-start").onclick = () => { DSR_WEB.act("clock", "op=start"); showClock(); };
  $("w-pause").onclick = () => { DSR_WEB.act("clock", "op=pause"); showClock(); };
  $("w-creset").onclick = () => { if (confirm("Run-Uhr auf 0 setzen?")) { DSR_WEB.act("clock", "op=reset"); showClock(); } };
  setInterval(showClock, 1000); setTimeout(showClock, 300);

  // ---- Stand sichern / laden (z. B. für einen anderen PC oder eine andere Adresse)
  const io = document.createElement("section");
  io.className = "card";
  io.innerHTML = `<h2>Stand sichern</h2>
    <div class="body">
      <p class="hint">Speichert Haken, Kills und Uhr als Datei – zum Sichern oder um auf einem anderen PC weiterzumachen.</p>
      <div class="row"><button id="w-export">Als Datei speichern</button><label class="btnlike"><input type="file" id="w-import" accept=".json,application/json" style="display:none"><span id="w-importlbl" style="cursor:pointer;text-decoration:underline">Datei laden …</span></label></div>
      <div class="hint" id="w-iomsg"></div>
    </div>`;
  clock.after(io);
  const KEY = "dsr-web-state-v1";
  $("w-export").onclick = () => {
    const blob = new Blob([localStorage.getItem(KEY) || "{}"], { type: "application/json" });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = `dsr-stand-${new Date().toISOString().slice(0, 10)}.json`;
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(a.href), 2000);
  };
  $("w-importlbl").onclick = () => $("w-import").click();
  $("w-import").onchange = async ev => {
    const f = ev.target.files[0]; ev.target.value = ""; if (!f) return;
    try {
      if (f.size > 200000) throw new Error("Datei zu groß");
      const s = JSON.parse(await f.text());
      if (!s || s.v !== 1 || !Array.isArray(s.manualDone) || !s.manualDone.every(k => typeof k === "string" && /^\d+-\d+$/.test(k))
          || !Array.isArray(s.recentKills)) throw new Error("keine gültige Sicherung");
      if (!confirm("Aktuellen Stand durch die Datei ersetzen?")) return;
      const clean = {
        v: 1, stamp: Date.now(), manualDone: s.manualDone,
        recentKills: s.recentKills.filter(k => k && typeof k.key === "string").map(k => ({
          key: String(k.key), de: String(k.de || ""), en: String(k.en || ""), pts: Number(k.pts) || 0,
          time: String(k.time || ""), igt: typeof k.igt === "number" ? k.igt : null })),
        areaOverride: Number.isInteger(s.areaOverride) ? s.areaOverride : -2,
        clock: { runningSince: null, acc: s.clock && typeof s.clock.acc === "number" ? s.clock.acc : 0 },
      };
      localStorage.setItem(KEY, JSON.stringify(clean));
      $("w-iomsg").textContent = "Geladen ✓";
      setTimeout(() => location.reload(), 400);
    } catch (e) { $("w-iomsg").textContent = "Fehler: " + e.message; }
  };
})();
