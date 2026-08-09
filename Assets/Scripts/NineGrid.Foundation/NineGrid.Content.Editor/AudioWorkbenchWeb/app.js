(() => {
  const UI_KEY = "NineGrid.AudioWorkbench.Ui.v1";
  const token = new URLSearchParams(location.hash.replace(/^#/, "")).get("token") || "";
  const badge = document.getElementById("connectionBadge");
  const statusLine = document.getElementById("statusLine");
  const dirtyLine = document.getElementById("dirtyLine");
  const errorLine = document.getElementById("errorLine");
  const modePanel = document.getElementById("modePanel");
  const tabs = Array.from(document.querySelectorAll(".tab"));

  let state = null;
  let mode = null;
  let revision = 0;
  let useLongPoll = false;

  function loadUi() {
    try { return JSON.parse(localStorage.getItem(UI_KEY) || "{}"); } catch { return {}; }
  }
  function saveUi(patch) {
    const next = Object.assign(loadUi(), patch);
    localStorage.setItem(UI_KEY, JSON.stringify(next));
  }

  async function api(path, options = {}) {
    const headers = Object.assign({ Authorization: "Bearer " + token }, options.headers || {});
    const res = await fetch(path, Object.assign({}, options, { headers }));
    if (!res.ok) throw new Error(path + " -> " + res.status);
    return res.json();
  }

  function setConnected(ok, label) {
    badge.className = "badge " + (ok ? "ok" : "bad");
    badge.textContent = label;
  }

  function render() {
    if (!state) return;
    const play = !!state.playMode;
    const conn = play ? "已连接 · 运行时" : "已连接 · 非运行时";
    setConnected(true, conn);
    statusLine.textContent = state.statusMessage || "";
    dirtyLine.textContent = "SFX脏 " + (state.sfxDirtyCount || 0) + " · BGM脏 " + (state.musicDirtyCount || 0)
      + (state.blocksMutations ? " · 冲突门禁中" : "");
    errorLine.textContent = state.errorMessage || "";

    if (!mode) {
      mode = state.defaultMode || (play ? "实时抓音" : "静态绑定库");
    }
    tabs.forEach((tab) => tab.classList.toggle("active", tab.dataset.mode === mode));
    saveUi({ mode });

    if (mode === "实时抓音") {
      modePanel.innerHTML = `
        <h2>实时抓音</h2>
        <p class="placeholder">本票壳层：完整抓音交互由后续票打磨。当前可观察连接态、revision 与运行时摘要。</p>
        <div class="kv">
          <b>revision</b><span>${revision}</span>
          <b>conflict</b><span>${state.conflict || "None"}</span>
          <b>runtime</b><span>${state.runtime ? ("hist=" + (state.runtime.history || []).length) : "（非 Play）"}</span>
        </div>`;
    } else if (mode === "静态绑定库") {
      const n = (state.declarations || []).length;
      modePanel.innerHTML = `
        <h2>静态绑定库</h2>
        <p class="placeholder">声明/绑定条目 ${n}。命令面已接 createDraft / replace / save / revert / reload。</p>
        <div class="kv">
          <b>focused</b><span>${state.focusedBindingKey || "—"}</span>
          <b>clips</b><span>${(state.clipOptions || []).length}</span>
        </div>`;
    } else {
      const n = (state.musicEntries || []).length;
      modePanel.innerHTML = `
        <h2>BGM</h2>
        <p class="placeholder">BGM 状态 ${n}。预览/停未知源命令已暴露；本票不热换 live music catalog。</p>
        <div class="kv">
          <b>desired</b><span>${(state.musicRuntime && state.musicRuntime.desired) || "—"}</span>
          <b>current</b><span>${(state.musicRuntime && state.musicRuntime.current) || "—"}</span>
        </div>`;
    }
  }

  function applyEnvelope(envelope) {
    if (!envelope) return;
    if (envelope.type === "snapshot") {
      if (typeof envelope.revision === "number") revision = envelope.revision;
      state = envelope.payload || null;
      render();
      return;
    }
    if (envelope.type === "delta") {
      if (typeof envelope.revision !== "number" || envelope.revision !== revision + 1) {
        api("/api/snapshot").then(applyEnvelope).catch((err) => {
          setConnected(false, "snapshot 重拉失败");
          errorLine.textContent = String(err);
        });
        return;
      }
      revision = envelope.revision;
      state = Object.assign({}, state || {}, envelope.payload || {});
      render();
    }
  }

  async function bootstrap() {
    if (!token) {
      setConnected(false, "缺少 token");
      return;
    }
    const ui = loadUi();
    if (ui.mode) mode = ui.mode;
    tabs.forEach((tab) => tab.addEventListener("click", () => { mode = tab.dataset.mode; render(); }));

    try {
      const snap = await api("/api/snapshot");
      applyEnvelope(snap);
    } catch (err) {
      setConnected(false, "鉴权失败");
      errorLine.textContent = String(err);
      return;
    }

    if (location.protocol === "http:" && window.WebSocket) {
      try {
        const ws = new WebSocket((location.protocol === "https:" ? "wss://" : "ws://") + location.host + "/stream");
        ws.addEventListener("open", () => {
          ws.send(JSON.stringify({ type: "hello", token, afterRevision: revision }));
        });
        ws.addEventListener("message", (ev) => {
          try { applyEnvelope(JSON.parse(ev.data)); } catch { /* ignore */ }
        });
        ws.addEventListener("close", () => { useLongPoll = true; longPoll(); });
        ws.addEventListener("error", () => { useLongPoll = true; });
        return;
      } catch {
        useLongPoll = true;
      }
    } else {
      useLongPoll = true;
    }
    longPoll();
  }

  async function longPoll() {
    if (!useLongPoll) return;
    try {
      const snap = await api("/api/events?afterRevision=" + revision + "&timeoutMs=25000");
      applyEnvelope(snap);
    } catch (err) {
      setConnected(false, "长轮询中断");
      errorLine.textContent = String(err);
    }
    setTimeout(longPoll, 250);
  }

  bootstrap();
})();
