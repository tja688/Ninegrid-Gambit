(() => {
  const UI_KEY = "NineGrid.AudioWorkbench.Ui.v1";
  const ABNORMAL = new Set(["Suppressed", "Cooldown", "Unbound", "BackendFailure"]);
  const DEFAULT_OUTCOMES = ["Played", "Suppressed", "Cooldown", "Unbound", "BackendFailure", "Scheduled", "Cancelled"];
  const BURST_WINDOW_MS = 1000;
  const BURST_THRESHOLD = 4;
  const ROW_H = 28;
  const OVERSCAN = 8;

  const token = new URLSearchParams(location.hash.replace(/^#/, "")).get("token") || "";
  const els = {
    badge: document.getElementById("connectionBadge"),
    status: document.getElementById("statusLine"),
    dirty: document.getElementById("dirtyLine"),
    error: document.getElementById("errorLine"),
    panel: document.getElementById("modePanel"),
    actions: document.getElementById("globalActions"),
    conflict: document.getElementById("conflictBanner"),
    tabs: Array.from(document.querySelectorAll(".tab")),
  };

  let state = null;
  let mode = null;
  let revision = 0;
  let useLongPoll = false;
  let selectedSequence = null;
  let selectedBindingKey = null;
  let selectedMusicState = null;
  let streamPaused = false;
  let pendingHistory = [];
  let streamRows = [];
  let flashUntil = new Map();
  let lastPreviewSourceId = "";
  let requestSeq = 0;

  const ui = loadUi();
  const pins = new Map((ui.pins || []).map((p) => [p.sequence, p]));
  const filters = Object.assign({
    text: "",
    module: "",
    outcomes: DEFAULT_OUTCOMES.slice(),
    onlyAbnormal: false,
    aggregateKey: "",
    staticText: "",
    staticFlags: { dirty: false, unbound: false, broken: false, aiDraft: false, disabled: false },
  }, ui.filters || {});
  const cols = Object.assign({
    left: "1.2fr", mid: "0.9fr", right: "1.1fr", static: "1.6fr", bgm: "1.2fr",
  }, ui.cols || {});

  function loadUi() {
    try { return JSON.parse(localStorage.getItem(UI_KEY) || "{}"); } catch { return {}; }
  }
  function saveUi(patch) {
    const next = Object.assign(loadUi(), patch);
    localStorage.setItem(UI_KEY, JSON.stringify(next));
  }
  function persistUi() {
    saveUi({
      mode,
      pins: Array.from(pins.values()),
      filters,
      cols,
    });
  }

  function esc(s) {
    return String(s == null ? "" : s)
      .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  async function api(path, options = {}) {
    const headers = Object.assign({ Authorization: "Bearer " + token }, options.headers || {});
    const res = await fetch(path, Object.assign({}, options, { headers }));
    const text = await res.text();
    let json = null;
    try { json = text ? JSON.parse(text) : null; } catch { /* ignore */ }
    if (!res.ok) {
      const msg = (json && (json.error || json.message)) || (path + " -> " + res.status);
      throw new Error(msg);
    }
    return json;
  }

  async function command(name, payload = {}) {
    const requestId = "r" + (++requestSeq);
    const result = await api("/api/command", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ requestId, command: name, payload }),
    });
    if (!result.ok) throw new Error(result.error || (name + " failed"));
    if (typeof result.revision === "number") revision = result.revision;
    return result;
  }

  function setConnected(ok, label) {
    els.badge.className = "badge " + (ok ? "ok" : "bad");
    els.badge.textContent = label;
  }

  function declByKey(key) {
    return (state.declarations || []).find((d) => d.bindingKey === key) || null;
  }
  function declByCue(cueId) {
    return (state.declarations || []).find((d) => d.cueId === cueId) || null;
  }
  function findDeclForEvent(ev) {
    if (ev && ev.bindingKey) {
      const byKey = declByKey(ev.bindingKey);
      if (byKey) return byKey;
    }
    return ev && ev.cueId ? declByCue(ev.cueId) : null;
  }

  function muteLabel(decl) {
    if (!decl || !decl.dto) return "";
    const enabled = !!decl.dto.enabled;
    const saved = decl.savedEnabled !== false;
    if (!enabled && decl.isDirty) return "临时静音（未保存）";
    if (!enabled && !decl.isDirty) return "已永久禁用";
    if (enabled && decl.isDirty && !saved) return "临时恢复（未保存）";
    return enabled ? "已启用" : "已永久禁用";
  }

  function isHot(agg) {
    const stamps = (agg && agg.recentTimestamps) || [];
    if (stamps.length < BURST_THRESHOLD) return false;
    const ms = stamps.map((t) => t > 1e12 ? t : t * 1000).sort((a, b) => a - b);
    for (let i = BURST_THRESHOLD - 1; i < ms.length; i++) {
      if (ms[i] - ms[i - (BURST_THRESHOLD - 1)] <= BURST_WINDOW_MS) return true;
    }
    return false;
  }

  function abnormalCount(agg) {
    return (agg.suppressed || 0) + (agg.cooldown || 0) + (agg.unbound || 0) + (agg.backendFailure || 0);
  }

  function playingSourceIds() {
    return new Set(((state.runtime && state.runtime.playingSources) || []).map((s) => s.sourceId));
  }

  function findDeclByCueOrClip(cueId, clipKey) {
    if (cueId) {
      const byCue = declByCue(cueId);
      if (byCue) return byCue;
    }
    if (!clipKey) return null;
    return (state.declarations || []).find((d) => d.dto && d.dto.clipKey === clipKey) || null;
  }

  async function muteBindingTemp(decl) {
    if (!decl || !decl.dto) return;
    const replacement = Object.assign({}, decl.dto, { enabled: false });
    await run("replaceSfxBinding", {
      originalBindingKey: decl.bindingKey,
      replacement,
    });
    selectedBindingKey = state.focusedBindingKey || decl.bindingKey;
  }

  async function saveBindingPermanent(decl) {
    if (!decl) return;
    if (decl.dto && decl.dto.enabled) {
      await muteBindingTemp(decl);
      decl = declByKey(selectedBindingKey) || decl;
    }
    await run("saveBinding", { bindingKey: decl.bindingKey, channel: "sfx" });
  }

  function hintShortcuts() {
    return `<div class="hint-line">快捷键：Space 固定 · <b>E</b> 临时禁 · <b>Ctrl+S</b> 永久禁 · P 试听 · Esc 取消选中</div>`;
  }

  function syncStreamFromState() {
    if (!state || !state.playMode || !state.runtime) {
      streamRows = [];
      pendingHistory = [];
      flashUntil.clear();
      return;
    }
    const hist = ((state.runtime && state.runtime.history) || []).slice();
    if (streamPaused) {
      const known = new Set(streamRows.map((r) => r.sequence).concat(pendingHistory.map((r) => r.sequence)));
      hist.forEach((row) => {
        if (!known.has(row.sequence)) pendingHistory.push(row);
      });
      return;
    }
    if (pendingHistory.length) {
      hist.push(...pendingHistory);
      pendingHistory = [];
    }
    const serverSeqs = new Set(hist.map((r) => r.sequence));
    // Drop rows that vanished from the runtime ring (new Play session / eviction).
    const bySeq = new Map();
    streamRows.forEach((r) => {
      if (serverSeqs.has(r.sequence) || pins.has(r.sequence)) bySeq.set(r.sequence, r);
    });
    hist.forEach((r) => {
      if (!bySeq.has(r.sequence)) flashUntil.set(r.sequence, Date.now() + 600);
      bySeq.set(r.sequence, r);
    });
    streamRows = Array.from(bySeq.values()).sort((a, b) => b.sequence - a.sequence);
  }

  function isEditingDetail() {
    const el = document.activeElement;
    if (!el) return false;
    if (isTypingTarget(el)) return true;
    return !!(el.closest && el.closest("#sfxForm, #bgmForm, #detailHost, #bgmDetail"));
  }

  function filteredStream() {
    const text = (filters.text || "").trim().toLowerCase();
    const module = filters.module || "";
    const outcomes = new Set(filters.outcomes || []);
    return streamRows.filter((row) => {
      if (row.outcome === "Requested" && !outcomes.has("Requested")) return false;
      if (!outcomes.has(row.outcome) && row.outcome !== "Requested") return false;
      if (filters.onlyAbnormal && !ABNORMAL.has(row.outcome)) return false;
      if (filters.aggregateKey) {
        const key = row.bindingKey || row.cueId || "";
        if (key !== filters.aggregateKey && row.cueId !== filters.aggregateKey && row.bindingKey !== filters.aggregateKey) {
          const agg = ((state.runtime && state.runtime.aggregates) || []).find((a) => a.aggregateKey === filters.aggregateKey);
          if (!agg) return false;
          const match = (agg.bindingKey && agg.bindingKey === row.bindingKey)
            || (agg.cueId && agg.cueId === row.cueId);
          if (!match) return false;
        }
      }
      if (module) {
        const decl = findDeclForEvent(row);
        if (!decl || decl.module !== module) return false;
      }
      if (text) {
        const hay = [row.cueNote, row.cueId, row.bindingKey, row.diagnosticSource, row.actualClipKey, row.failureReason]
          .join(" ").toLowerCase();
        if (!hay.includes(text)) return false;
      }
      return true;
    });
  }

  function filteredDeclarations() {
    const text = (filters.staticText || "").trim().toLowerCase();
    const f = filters.staticFlags || {};
    return (state.declarations || []).filter((d) => {
      if (f.dirty && !d.isDirty) return false;
      if (f.unbound && !d.isUnbound) return false;
      if (f.broken && !d.isBroken) return false;
      if (f.disabled && !d.isDisabled) return false;
      if (f.aiDraft) {
        const st = d.authoringStatus || "";
        const isAi = !st || st === "aiDraft";
        if (!isAi) return false;
      }
      if (text) {
        const hay = [d.note, d.cueId, d.module, d.bindingKey, d.dto && d.dto.clipKey].join(" ").toLowerCase();
        if (!hay.includes(text)) return false;
      }
      return true;
    });
  }

  function latestOutcome(decl) {
    const hist = (state.runtime && state.runtime.history) || [];
    for (let i = hist.length - 1; i >= 0; i--) {
      const h = hist[i];
      if ((decl.bindingKey && h.bindingKey === decl.bindingKey) || h.cueId === decl.cueId) {
        return h.outcome;
      }
    }
    return "—";
  }

  function selectedEvent() {
    if (selectedSequence != null) {
      const pinned = pins.get(selectedSequence);
      if (pinned) return pinned;
      return streamRows.find((r) => r.sequence === selectedSequence) || null;
    }
    return null;
  }

  function focusBinding(key) {
    selectedBindingKey = key || null;
    if (!key) {
      render();
      return;
    }
    run("focusBinding", { bindingKey: key });
  }

  async function run(name, payload) {
    try {
      els.error.textContent = "";
      await command(name, payload);
      const snap = await api("/api/snapshot");
      applyEnvelope(snap);
    } catch (err) {
      els.error.textContent = String(err.message || err);
      render();
    }
  }

  function pinEvent(ev) {
    if (!ev) return;
    const existing = pins.get(ev.sequence);
    if (existing) {
      selectedSequence = ev.sequence;
      selectedBindingKey = existing.bindingKey || findDeclForEvent(existing)?.bindingKey || null;
      persistUi();
      render();
      return;
    }
    const copy = Object.assign({}, ev);
    pins.set(ev.sequence, copy);
    selectedSequence = ev.sequence;
    selectedBindingKey = copy.bindingKey || findDeclForEvent(copy)?.bindingKey || null;
    persistUi();
    render();
  }

  function unpinSequence(seq) {
    pins.delete(seq);
    if (selectedSequence === seq) selectedSequence = null;
    persistUi();
    render();
  }

  function clearUnpinned() {
    const pinnedSeq = new Set(pins.keys());
    streamRows = streamRows.filter((r) => pinnedSeq.has(r.sequence));
    pendingHistory = [];
    render();
  }

  function buildSparkline(stamps) {
    const vals = (stamps || []).map((t) => t > 1e12 ? t : t * 1000);
    if (!vals.length) return "";
    const min = Math.min(...vals);
    const max = Math.max(...vals);
    const span = Math.max(1, max - min);
    const w = 140, h = 22;
    const pts = vals.map((t, i) => {
      const x = (i / Math.max(1, vals.length - 1)) * (w - 2) + 1;
      const y = h - 2 - ((t - min) / span) * (h - 4);
      return x.toFixed(1) + "," + y.toFixed(1);
    }).join(" ");
    return `<svg class="spark" viewBox="0 0 ${w} ${h}" preserveAspectRatio="none"><polyline fill="none" stroke="#d9a441" stroke-width="1.5" points="${pts}"/></svg>`;
  }

  function renderFreqRail(host) {
    const aggs = ((state.runtime && state.runtime.aggregates) || []).slice().sort((a, b) => {
      const aa = abnormalCount(a), ba = abnormalCount(b);
      if (ba !== aa) return ba - aa;
      if ((b.requested || 0) !== (a.requested || 0)) return (b.requested || 0) - (a.requested || 0);
      return (b.lastTime || 0) - (a.lastTime || 0);
    });
    if (!aggs.length) {
      host.innerHTML = `<div class="empty">暂无频率聚合（进入 Play 后触发声音可见）</div>`;
      return;
    }
    host.innerHTML = aggs.map((a) => {
      const decl = (a.bindingKey && declByKey(a.bindingKey)) || declByCue(a.cueId);
      const title = (decl && decl.note) || a.cueId || a.aggregateKey;
      const hot = isHot(a);
      return `<div class="freq-card ${hot ? "hot" : ""}" data-agg="${esc(a.aggregateKey)}" data-bkey="${esc(a.bindingKey || "")}" data-cue="${esc(a.cueId || "")}">
        <div class="title">${esc(title)}${hot ? '<span class="badge-hot">高频</span>' : ""}</div>
        <div class="meta">req ${a.requested || 0} · play ${a.played || 0} · supp ${a.suppressed || 0} · cd ${a.cooldown || 0} · unb ${a.unbound || 0} · fail ${a.backendFailure || 0}</div>
        ${buildSparkline(a.recentTimestamps)}
      </div>`;
    }).join("");
    host.querySelectorAll(".freq-card").forEach((card) => {
      card.addEventListener("click", () => {
        const key = card.dataset.agg;
        filters.aggregateKey = key;
        filters.text = "";
        const cue = card.dataset.cue;
        const bkey = card.dataset.bkey;
        const newest = streamRows.find((r) =>
          (bkey && r.bindingKey === bkey) || (cue && r.cueId === cue));
        if (newest) {
          selectedSequence = newest.sequence;
          selectedBindingKey = newest.bindingKey || bkey || null;
        }
        persistUi();
        render();
      });
    });
  }

  function attachVirtualList(scroller, rows, paintRow, onClick) {
    const inner = document.createElement("div");
    inner.className = "vlist-inner";
    inner.style.height = (rows.length * ROW_H) + "px";
    scroller.innerHTML = "";
    scroller.appendChild(inner);
    const pool = [];
    function ensurePool(n) {
      while (pool.length < n) {
        const row = document.createElement("div");
        row.className = "vrow";
        row.addEventListener("click", () => onClick && onClick(row._data));
        pool.push(row);
        inner.appendChild(row);
      }
    }
    function paint() {
      const top = scroller.scrollTop;
      const view = scroller.clientHeight;
      const start = Math.max(0, Math.floor(top / ROW_H) - OVERSCAN);
      const end = Math.min(rows.length, Math.ceil((top + view) / ROW_H) + OVERSCAN);
      ensurePool(end - start);
      for (let i = 0; i < pool.length; i++) {
        const idx = start + i;
        const el = pool[i];
        if (idx >= end) {
          el.style.display = "none";
          continue;
        }
        el.style.display = "grid";
        el.style.top = (idx * ROW_H) + "px";
        el._data = rows[idx];
        paintRow(el, rows[idx], idx);
      }
    }
    scroller.addEventListener("scroll", paint, { passive: true });
    paint();
    return { paint, scroller, rows };
  }

  function renderDetail(host) {
    const ev = selectedEvent();
    const decl = selectedBindingKey
      ? declByKey(selectedBindingKey)
      : (ev ? findDeclForEvent(ev) : null);
    if (!ev && !decl) {
      host.innerHTML = `<div class="empty">选择一条事件或绑定以检查与调音<br/>${hintShortcuts()}</div>`;
      return;
    }
    const playing = playingSourceIds();
    const sourceId = (ev && ev.sourceId) || lastPreviewSourceId;
    const canStop = !!(sourceId && playing.has(sourceId));
    const dto = (decl && decl.dto) || null;
    const clips = state.clipOptions || [];
    const enabled = !!(dto && dto.enabled);

    let html = `<div class="section kill-panel"><h3>灭火</h3>
      <div class="actions kill-actions">
        <button type="button" class="danger" data-act="stop-source" ${canStop ? "" : "disabled"} title="${canStop ? "" : "该次 SourceId 已不在播放中；请用下方「正在播放」停源"}">立刻停播（本次）</button>
        <button type="button" class="danger" data-act="mute-temp" ${dto ? "" : "disabled"}>${enabled ? "临时禁用绑定" : "已临时/永久禁用"}</button>
        <button type="button" class="danger primary" data-act="mute-save" ${decl ? "" : "disabled"}>保存永久禁用</button>
      </div>
      ${hintShortcuts()}
      ${!canStop && sourceId ? `<div class="meta">SourceId 已结束：${esc(sourceId)}</div>` : ""}
    </div>`;

    html += `<div class="section"><h3>检查</h3><div class="kv">`;
    if (ev) {
      html += `
        <b>outcome</b><span class="outcome-${esc(ev.outcome)}">${esc(ev.outcome)}</span>
        <b>cue</b><span>${esc(ev.cueNote || "")} · ${esc(ev.cueId || "")}</span>
        <b>BindingKey</b><span class="mono">${esc(ev.bindingKey || "")}</span>
        <b>素材/变体</b><span>${esc(ev.actualClipKey || "—")} / ${esc(ev.variantId || "—")}</span>
        <b>诊断来源</b><span>${esc(ev.diagnosticSource || "")}</span>
        <b>发射所有者</b><span>${esc((decl && decl.authoritativeEmitter) || "")}</span>
        <b>上下文</b><span>card=${esc(ev.cardDefId||"")} skill=${esc(ev.skillId||"")} room=${esc(ev.roomId||"")} item=${esc(ev.itemDefId||"")} content=${esc(ev.contentId||"")}</span>
        <b>SourceId</b><span class="mono">${esc(ev.sourceId || "")}</span>
        <b>reason</b><span>${esc(ev.failureReason || "")}</span>
        <b>time</b><span>${esc(ev.time)}</span>`;
    } else if (decl) {
      html += `
        <b>cue</b><span>${esc(decl.note || "")} · ${esc(decl.cueId || "")}</span>
        <b>BindingKey</b><span class="mono">${esc(decl.bindingKey || "")}</span>
        <b>模块</b><span>${esc(decl.module || "")}</span>
        <b>发射所有者</b><span>${esc(decl.authoritativeEmitter || "")}</span>
        <b>运行时</b><span>${esc(latestOutcome(decl))}</span>`;
    }
    html += `</div><div class="actions" style="margin-top:8px">`;
    if (ev) {
      const pinned = pins.has(ev.sequence);
      html += `<button type="button" data-act="toggle-pin">${pinned ? "取消固定" : "固定"}</button>`;
    }
    html += `
      <button type="button" data-act="preview" ${dto ? "" : "disabled"}>试听当前绑定</button>
      <button type="button" data-act="stop-preview">停本次试听</button>
      <button type="button" data-act="ping-clip" ${dto && dto.clipKey ? "" : "disabled"}>定位素材</button>
      <button type="button" data-act="copy-src" ${ev ? "" : "disabled"}>复制触发源</button>
    </div></div>`;

    if (dto && decl) {
      html += `<div class="section"><h3>调音</h3>
        <div class="mute-label">${esc(muteLabel(decl))}</div>
        <div class="form-grid" id="sfxForm">
          <label>启用</label><input type="checkbox" data-f="enabled" ${dto.enabled ? "checked" : ""} />
          <label>素材</label><select data-f="clipKey">${clips.map((c) =>
            `<option value="${esc(c.resourcesKey)}" ${c.resourcesKey === dto.clipKey ? "selected" : ""}>${esc(c.resourcesKey)}</option>`).join("")}</select>
          <label>音量 dB</label><input type="number" step="0.1" data-f="volumeDb" value="${esc(dto.volumeDb)}" />
          <label>起播点</label><input type="number" step="0.01" min="0" data-f="startOffsetSeconds" value="${esc(dto.startOffsetSeconds)}" />
          <label>绑定延迟</label><input type="number" step="0.01" min="0" data-f="bindingDelaySeconds" value="${esc(dto.bindingDelaySeconds)}" />
          <label>最短间隔</label><input type="number" step="0.01" min="0" data-f="minimumIntervalSeconds" value="${esc(dto.minimumIntervalSeconds)}" />
          <label>cardDef</label><input type="text" data-f="selectorCardDefId" value="${esc(dto.selectorCardDefId || "")}" />
          <label>skillId</label><input type="text" data-f="selectorSkillId" value="${esc(dto.selectorSkillId || "")}" />
          <label>roomId</label><input type="text" data-f="selectorRoomId" value="${esc(dto.selectorRoomId || "")}" />
          <label>itemDef</label><input type="text" data-f="selectorItemDefId" value="${esc(dto.selectorItemDefId || "")}" />
          <label>contentId</label><input type="text" data-f="selectorContentId" value="${esc(dto.selectorContentId || "")}" />
          <label>变体 JSON</label><textarea data-f="variantsJson" rows="3">${esc(JSON.stringify(dto.variants || [], null, 0))}</textarea>
        </div>
        <div class="actions" style="margin-top:8px">
          <button type="button" class="primary" data-act="apply-patch">应用补丁</button>
          <button type="button" data-act="save-one">保存此条</button>
          <button type="button" data-act="revert-one">回撤此条</button>
          ${!decl.hasBinding ? `<button type="button" data-act="create-draft">创建草稿</button>` : ""}
        </div>
      </div>`;
    } else if (ev && !decl) {
      html += `<div class="section"><button type="button" data-act="create-draft">为 ${esc(ev.cueId)} 创建草稿</button></div>`;
    }

    host.innerHTML = html;
    host.querySelectorAll("[data-act]").forEach((btn) => {
      btn.addEventListener("click", () => onDetailAction(btn.dataset.act, ev, decl));
    });
  }

  function readSfxForm(root, baseDto) {
    const get = (f) => root.querySelector(`[data-f="${f}"]`);
    let variants = baseDto.variants || [];
    try { variants = JSON.parse(get("variantsJson").value || "[]"); } catch { /* keep */ }
    return {
      enabled: !!get("enabled").checked,
      clipKey: get("clipKey").value,
      volumeDb: Number(get("volumeDb").value || 0),
      startOffsetSeconds: Number(get("startOffsetSeconds").value || 0),
      bindingDelaySeconds: Number(get("bindingDelaySeconds").value || 0),
      minimumIntervalSeconds: Number(get("minimumIntervalSeconds").value || 0),
      selectorCardDefId: get("selectorCardDefId").value,
      selectorSkillId: get("selectorSkillId").value,
      selectorRoomId: get("selectorRoomId").value,
      selectorItemDefId: get("selectorItemDefId").value,
      selectorContentId: get("selectorContentId").value,
      variants,
    };
  }

  async function onDetailAction(act, ev, decl) {
    if (act === "toggle-pin") {
      if (pins.has(ev.sequence)) unpinSequence(ev.sequence);
      else pinEvent(ev);
      return;
    }
    if (act === "copy-src") {
      const text = [
        ev.diagnosticSource || "",
        ev.cueId || "",
        ev.bindingKey || "",
        "card=" + (ev.cardDefId || ""),
        "skill=" + (ev.skillId || ""),
        "room=" + (ev.roomId || ""),
        "item=" + (ev.itemDefId || ""),
        "content=" + (ev.contentId || ""),
      ].join("\n");
      try { await navigator.clipboard.writeText(text); els.status.textContent = "已复制触发源"; }
      catch { els.error.textContent = "复制失败"; }
      return;
    }
    if (act === "stop-source") {
      const sid = (ev && ev.sourceId) || lastPreviewSourceId;
      if (!sid) return;
      await run("stopSfxSource", { sourceId: sid });
      return;
    }
    if (act === "mute-temp") {
      await muteBindingTemp(decl);
      return;
    }
    if (act === "mute-save") {
      await saveBindingPermanent(decl);
      return;
    }
    if (act === "stop-preview") {
      if (lastPreviewSourceId) {
        try { await command("stopSfxSource", { sourceId: lastPreviewSourceId }); } catch { /* ignore */ }
        lastPreviewSourceId = "";
      }
      await run("stopPreview", {});
      return;
    }
    if (act === "preview") {
      const res = await command("previewBinding", { bindingKey: decl.bindingKey, includeBindingDelay: false });
      lastPreviewSourceId = (res.payload && (res.payload.SourceId || res.payload.sourceId)) || "";
      const snap = await api("/api/snapshot");
      applyEnvelope(snap);
      return;
    }
    if (act === "ping-clip") {
      await run("pingClipAsset", { clipKey: decl.dto.clipKey });
      return;
    }
    if (act === "create-draft") {
      const cueId = (decl && decl.cueId) || (ev && ev.cueId);
      await run("createDraft", { cueId });
      return;
    }
    if (act === "save-one") {
      await run("saveBinding", { bindingKey: decl.bindingKey, channel: "sfx" });
      return;
    }
    if (act === "revert-one") {
      await run("revertBinding", { bindingKey: decl.bindingKey, channel: "sfx" });
      return;
    }
    if (act === "apply-patch") {
      const form = document.getElementById("sfxForm");
      const replacement = readSfxForm(form, decl.dto);
      await run("replaceSfxBinding", {
        originalBindingKey: decl.bindingKey,
        replacement,
      });
      selectedBindingKey = state.focusedBindingKey || decl.bindingKey;
    }
  }

  function renderNowPlaying(host) {
    if (!host) return;
    const playMode = !!(state && state.playMode);
    if (!playMode) {
      host.innerHTML = `<div class="empty">进入 Play Mode 后显示正在播放源（Sfx / Music / 场景旁路）</div>`;
      return;
    }

    const sfx = ((state.runtime && state.runtime.playingSources) || []).slice();
    const music = ((state.musicRuntime && state.musicRuntime.playingSources) || []).slice();
    const orphans = ((state.runtime && state.runtime.sceneOrphans) || []).slice();
    const persist = ((state.runtime && state.runtime.persistAnomalies) || []).slice().slice(-6);

    const rows = [];
    sfx.forEach((s) => {
      rows.push({
        track: "Sfx",
        sourceId: s.sourceId,
        clipKey: s.clipKey,
        cueId: s.cueId,
        loop: !!s.loop,
        pos: s.playbackPositionSeconds,
        claimed: s.claimed !== false,
        kind: "sfx",
      });
    });
    music.forEach((s) => {
      rows.push({
        track: "Music",
        sourceId: s.sourceId,
        clipKey: s.clipKey,
        cueId: "",
        loop: s.loop !== false,
        pos: s.playbackPositionSeconds,
        claimed: !!s.claimed,
        kind: "music",
      });
    });
    orphans.forEach((o) => {
      rows.push({
        track: "Scene",
        sourceId: "",
        clipKey: o.clipName || "(no clip)",
        cueId: o.gameObjectPath,
        loop: !!o.loop,
        pos: o.playbackPositionSeconds,
        claimed: false,
        kind: "orphan",
      });
    });

    const head = `<div class="now-head"><h2>正在播放</h2>
        <span class="meta">${rows.length} 源 · Sfx ${sfx.length} · Music ${music.length} · Scene ${orphans.length}</span>
        <button type="button" class="danger" id="npStopAllSfx">紧急：停全部 SFX</button>
        <button type="button" class="danger" id="npStopUnknownMusic">紧急：停全部未知 Music</button>
      </div>`;

    if (!rows.length) {
      host.innerHTML = `${head}
      <div class="empty">当前无在播源。若仍听到声音，旁路会出现在 Scene 行；也可切 BGM 页。</div>
      ${persist.length ? `<div class="persist-list"><h3>近期持续异常</h3>${persist.map((a) =>
        `<div class="persist-row"><span class="badge-unclaimed">Persist</span> ${esc(a.clipKey || a.sourceId)} · ${esc(a.reason || "")}</div>`
      ).join("")}</div>` : ""}`;
      wireNowPlayingActions(host);
      return;
    }

    host.innerHTML = `${head}
      <div class="now-list">${rows.map((r) => {
        const decl = findDeclByCueOrClip(r.cueId, r.clipKey);
        const canStop = (r.kind === "sfx" || r.kind === "music") && !!r.sourceId;
        return `<div class="now-row ${r.claimed ? "" : "unclaimed"}">
          <span class="track track-${esc(r.track.toLowerCase())}">${esc(r.track)}</span>
          <span class="claim ${r.claimed ? "ok" : "bad"}">${r.claimed ? "认领" : "未认领"}${r.loop ? " · loop" : ""}</span>
          <span class="ellipsis mono" title="${esc(r.clipKey)}">${esc(r.clipKey || "—")}</span>
          <span class="ellipsis meta" title="${esc(r.cueId || r.sourceId)}">${esc(r.cueId || r.sourceId || "")}</span>
          <span class="meta">${Number(r.pos || 0).toFixed(2)}s</span>
          <span class="now-acts">
            <button type="button" data-np="stop" data-kind="${esc(r.kind)}" data-sid="${esc(r.sourceId)}" ${canStop ? "" : "disabled"}>停</button>
            <button type="button" data-np="mute" data-bkey="${esc((decl && decl.bindingKey) || "")}" ${decl ? "" : "disabled"}>临时禁</button>
            <button type="button" data-np="save" data-bkey="${esc((decl && decl.bindingKey) || "")}" ${decl ? "" : "disabled"}>永久禁</button>
          </span>
        </div>`;
      }).join("")}</div>
      ${persist.length ? `<div class="persist-list"><h3>近期持续异常</h3>${persist.map((a) =>
        `<div class="persist-row"><span class="badge-unclaimed">Persist</span> ${esc(a.clipKey || a.sourceId)} · ${esc(a.reason || "")}
          <button type="button" data-np="stop" data-kind="sfx" data-sid="${esc(a.sourceId)}" ${a.sourceId ? "" : "disabled"}>停</button>
        </div>`
      ).join("")}</div>` : ""}`;
    wireNowPlayingActions(host);
  }

  function wireNowPlayingActions(host) {
    const stopAll = host.querySelector("#npStopAllSfx");
    if (stopAll) stopAll.onclick = () => run("stopAllSfxSources", {});
    const stopMusic = host.querySelector("#npStopUnknownMusic");
    if (stopMusic) stopMusic.onclick = () => run("stopUnknownMusic", {});
    host.querySelectorAll("[data-np]").forEach((btn) => {
      btn.addEventListener("click", async () => {
        const op = btn.dataset.np;
        if (op === "stop") {
          const kind = btn.dataset.kind;
          const sid = btn.dataset.sid;
          if (!sid) return;
          if (kind === "music") await run("stopMusicSource", { sourceId: sid });
          else await run("stopSfxSource", { sourceId: sid });
          return;
        }
        const bkey = btn.dataset.bkey;
        const decl = declByKey(bkey);
        if (!decl) return;
        selectedBindingKey = bkey;
        if (op === "mute") await muteBindingTemp(decl);
        else if (op === "save") await saveBindingPermanent(decl);
      });
    });
  }

  function renderCapture() {
    syncStreamFromState();
    const modules = Array.from(new Set((state.declarations || []).map((d) => d.module).filter(Boolean))).sort();
    els.panel.innerHTML = `
      <div id="freqRail" class="freq-rail"></div>
      <div id="nowPlaying" class="now-playing"></div>
      <div class="capture-layout" style="--col-left:${cols.left};--col-mid:${cols.mid};--col-right:${cols.right}">
        <section class="col" id="colStream">
          <div class="col-head">
            <h2>实时流</h2>
            <div class="filters">
              <input type="text" id="fltText" placeholder="文本过滤" value="${esc(filters.text)}" />
              <select id="fltModule"><option value="">模块</option>${modules.map((m) =>
                `<option value="${esc(m)}" ${m === filters.module ? "selected" : ""}>${esc(m)}</option>`).join("")}</select>
              <button type="button" class="chip ${filters.onlyAbnormal ? "on" : ""}" id="fltAbn">只看异常</button>
              <button type="button" class="chip ${streamPaused ? "on" : ""}" id="fltPause">${streamPaused ? "恢复流入" : "暂停流入视图"}</button>
              <button type="button" id="fltClear">清空未固定</button>
              ${filters.aggregateKey ? `<button type="button" id="fltClearAgg">清除频率筛选</button>` : ""}
            </div>
            <div class="filters" id="outcomeChips"></div>
          </div>
          <div class="vlist" id="streamList"></div>
        </section>
        <div class="splitter" data-split="left-mid"></div>
        <section class="col" id="colPins">
          <div class="col-head"><h2>已固定</h2><span class="meta">${pins.size}</span></div>
          <div id="pinList" style="overflow:auto;flex:1"></div>
        </section>
        <div class="splitter" data-split="mid-right"></div>
        <section class="col" id="colDetail">
          <div class="col-head"><h2>检查与调音</h2></div>
          <div class="detail" id="detailHost"></div>
        </section>
      </div>`;

    renderFreqRail(document.getElementById("freqRail"));
    renderNowPlaying(document.getElementById("nowPlaying"));
    const chipHost = document.getElementById("outcomeChips");
    ["Requested", ...DEFAULT_OUTCOMES].forEach((o) => {
      const on = (filters.outcomes || []).includes(o) || (o === "Requested" && (filters.outcomes || []).includes("Requested"));
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "chip " + ((filters.outcomes || []).includes(o) ? "on" : "");
      btn.textContent = o;
      btn.addEventListener("click", () => {
        const set = new Set(filters.outcomes || []);
        if (set.has(o)) set.delete(o); else set.add(o);
        filters.outcomes = Array.from(set);
        persistUi();
        render();
      });
      chipHost.appendChild(btn);
    });

    document.getElementById("fltText").addEventListener("change", (e) => {
      filters.text = e.target.value; persistUi(); render();
    });
    document.getElementById("fltModule").addEventListener("change", (e) => {
      filters.module = e.target.value; persistUi(); render();
    });
    document.getElementById("fltAbn").addEventListener("click", () => {
      filters.onlyAbnormal = !filters.onlyAbnormal; persistUi(); render();
    });
    document.getElementById("fltPause").addEventListener("click", () => {
      streamPaused = !streamPaused;
      if (!streamPaused) syncStreamFromState();
      render();
    });
    document.getElementById("fltClear").addEventListener("click", clearUnpinned);
    const clearAgg = document.getElementById("fltClearAgg");
    if (clearAgg) clearAgg.addEventListener("click", () => {
      filters.aggregateKey = ""; persistUi(); render();
    });

    const rows = filteredStream();
    attachVirtualList(document.getElementById("streamList"), rows, (el, row) => {
      el.classList.toggle("selected", row.sequence === selectedSequence);
      el.classList.toggle("flash", (flashUntil.get(row.sequence) || 0) > Date.now());
      el.style.gridTemplateColumns = "52px 96px 1fr 1fr";
      el.innerHTML = `<span class="seq">#${row.sequence}</span><span class="outcome outcome-${esc(row.outcome)}">${esc(row.outcome)}</span><span class="ellipsis">${esc(row.cueNote || row.cueId || "")}</span><span class="ellipsis mono">${esc(row.bindingKey || row.actualClipKey || "")}</span>`;
    }, (row) => {
      selectedSequence = row.sequence;
      selectedBindingKey = row.bindingKey || findDeclForEvent(row)?.bindingKey || null;
      render();
    });

    const pinHost = document.getElementById("pinList");
    if (!pins.size) pinHost.innerHTML = `<div class="empty">空格或点击固定一条记录</div>`;
    else {
      pinHost.innerHTML = Array.from(pins.values()).sort((a, b) => b.sequence - a.sequence).map((p) => `
        <div class="pin-card ${p.sequence === selectedSequence ? "selected" : ""}" data-seq="${p.sequence}">
          <div class="title">#${p.sequence} · ${esc(p.outcome)} · ${esc(p.cueNote || p.cueId || "")}</div>
          <div class="meta mono">${esc(p.bindingKey || "")}</div>
        </div>`).join("");
      pinHost.querySelectorAll(".pin-card").forEach((card) => {
        card.addEventListener("click", () => {
          const seq = Number(card.dataset.seq);
          const p = pins.get(seq);
          selectedSequence = seq;
          selectedBindingKey = p.bindingKey || findDeclForEvent(p)?.bindingKey || null;
          render();
        });
      });
    }

    renderDetail(document.getElementById("detailHost"));
    wireSplitters(els.panel.querySelector(".capture-layout"), "capture");
  }

  function renderStatic() {
    const rows = filteredDeclarations();
    const f = filters.staticFlags;
    els.panel.innerHTML = `
      <div class="static-layout" style="--col-static:${cols.static};--col-right:${cols.right}">
        <section class="col">
          <div class="col-head">
            <h2>静态绑定库</h2>
            <input type="text" id="staticText" placeholder="搜索 note/cue/clip" value="${esc(filters.staticText)}" />
            <button type="button" class="chip ${f.dirty ? "on" : ""}" data-flag="dirty">脏</button>
            <button type="button" class="chip ${f.unbound ? "on" : ""}" data-flag="unbound">未绑</button>
            <button type="button" class="chip ${f.broken ? "on" : ""}" data-flag="broken">断链</button>
            <button type="button" class="chip ${f.aiDraft ? "on" : ""}" data-flag="aiDraft">AI草稿</button>
            <button type="button" class="chip ${f.disabled ? "on" : ""}" data-flag="disabled">禁用</button>
            <button type="button" id="btnDraft">建草稿</button>
            <button type="button" id="btnAi">AI初绑</button>
          </div>
          <div class="table-head">
            <span>说明</span><span>cueId</span><span>模块</span><span>素材/池</span><span>选择器</span><span>作者态</span><span>启</span><span>脏</span><span>断</span><span>运行时</span>
          </div>
          <div class="vlist" id="staticList"></div>
        </section>
        <div class="splitter" data-split="static-right"></div>
        <section class="col">
          <div class="col-head"><h2>检查与调音</h2></div>
          <div class="detail" id="detailHost"></div>
        </section>
      </div>`;

    document.getElementById("staticText").addEventListener("change", (e) => {
      filters.staticText = e.target.value; persistUi(); render();
    });
    els.panel.querySelectorAll("[data-flag]").forEach((btn) => {
      btn.addEventListener("click", () => {
        f[btn.dataset.flag] = !f[btn.dataset.flag];
        persistUi();
        render();
      });
    });
    document.getElementById("btnDraft").addEventListener("click", async () => {
      const cueId = prompt("新建草稿 cueId");
      if (cueId) await run("createDraft", { cueId });
    });
    document.getElementById("btnAi").addEventListener("click", () => run("runAiBindPreserve", {}));

    attachVirtualList(document.getElementById("staticList"), rows, (el, d) => {
      el.className = "table-row" + (d.bindingKey === selectedBindingKey ? " selected" : "");
      el.style.position = "absolute";
      el.style.left = "0";
      el.style.right = "0";
      el.style.height = ROW_H + "px";
      const pool = (d.dto && d.dto.variants && d.dto.variants.length) ? ("池" + d.dto.variants.length) : (d.dto && d.dto.clipKey) || "—";
      const sel = [d.dto && d.dto.selectorCardDefId, d.dto && d.dto.selectorSkillId, d.dto && d.dto.selectorRoomId]
        .filter(Boolean).join(",") || "—";
      const auth = d.authoringStatus === "humanConfirmed" ? "人工确认" : (d.hasBinding ? "AI草稿" : "未绑");
      el.innerHTML = `
        <span class="ellipsis">${esc(d.note || "")}</span>
        <span class="ellipsis mono">${esc(d.cueId || "")}</span>
        <span class="ellipsis">${esc(d.module || "")}</span>
        <span class="ellipsis">${esc(pool)}</span>
        <span class="ellipsis">${esc(sel)}</span>
        <span>${esc(auth)}</span>
        <span>${d.isDisabled ? "否" : "是"}</span>
        <span>${d.isDirty ? "●" : ""}</span>
        <span>${d.isBroken ? "!" : ""}</span>
        <span class="outcome-${esc(latestOutcome(d))}">${esc(latestOutcome(d))}</span>`;
    }, (d) => {
      selectedBindingKey = d.bindingKey;
      selectedSequence = null;
      render();
    });

    renderDetail(document.getElementById("detailHost"));
    wireSplitters(els.panel.querySelector(".static-layout"), "static");
  }

  function renderBgm() {
    const entries = state.musicEntries || [];
    const rt = state.musicRuntime || {};
    const focused = selectedMusicState || (entries[0] && entries[0].state);
    selectedMusicState = focused;
    const entry = entries.find((e) => e.state === focused) || entries[0];
    els.panel.innerHTML = `
      <div class="bgm-layout" style="--col-bgm:${cols.bgm};--col-right:${cols.right}">
        <section class="col">
          <div class="col-head">
            <h2>BGM</h2>
            <span class="meta">期望 ${esc(rt.desired || "—")} · 当前 ${esc(rt.current || "—")} · 代数 ${esc(rt.currentMusicGeneration || 0)} · 源 ${esc(rt.currentSourceCount || 0)}/${esc(rt.retiringSourceCount || 0)}</span>
            <button type="button" id="btnStopUnknown">停未知源</button>
          </div>
          <div class="bgm-list" id="bgmList"></div>
          <div class="section" style="margin:8px">
            <h3>审计 / 异常</h3>
            <div class="kv">
              <b>lastAudit</b><span>${esc(rt.lastAudit ? JSON.stringify(rt.lastAudit) : "—")}</span>
              <b>anomalies</b><span>${esc((rt.anomalies || []).length)}</span>
            </div>
            <div class="mono" style="max-height:120px;overflow:auto;margin-top:6px">${esc(JSON.stringify(rt.history || [], null, 2))}</div>
          </div>
        </section>
        <div class="splitter" data-split="bgm-right"></div>
        <section class="col">
          <div class="col-head"><h2>字段编辑</h2></div>
          <div class="detail" id="bgmDetail"></div>
        </section>
      </div>`;

    const list = document.getElementById("bgmList");
    list.innerHTML = entries.map((e) => `
      <div class="bgm-row ${e.state === focused ? "selected" : ""}" data-state="${esc(e.state)}">
        <div class="title">${esc(e.state)} ${e.isDirty ? "●" : ""}</div>
        <div class="meta">${esc(e.dto && e.dto.clipKey)} · ${e.dto && e.dto.enabled ? "启用" : "禁用"} · ${esc(e.dto && e.dto.volumeDb)} dB</div>
      </div>`).join("") || `<div class="empty">无 BGM 条目</div>`;
    list.querySelectorAll(".bgm-row").forEach((row) => {
      row.addEventListener("click", () => { selectedMusicState = row.dataset.state; render(); });
    });
    document.getElementById("btnStopUnknown").addEventListener("click", () => run("stopUnknownMusic", {}));

    const host = document.getElementById("bgmDetail");
    if (!entry || !entry.dto) {
      host.innerHTML = `<div class="empty">选择一条 BGM</div>`;
    } else {
      const d = entry.dto;
      const clips = state.clipOptions || [];
      host.innerHTML = `
        <div class="form-grid" id="bgmForm">
          <label>启用</label><input type="checkbox" data-f="enabled" ${d.enabled ? "checked" : ""} />
          <label>素材</label><select data-f="clipKey">${clips.map((c) =>
            `<option value="${esc(c.resourcesKey)}" ${c.resourcesKey === d.clipKey ? "selected" : ""}>${esc(c.resourcesKey)}</option>`).join("")}</select>
          <label>音量 dB</label><input type="number" step="0.1" data-f="volumeDb" value="${esc(d.volumeDb)}" />
          <label>起播点</label><input type="number" step="0.01" min="0" data-f="startOffsetSeconds" value="${esc(d.startOffsetSeconds)}" />
          <label>淡入</label><input type="number" step="0.01" min="0" data-f="fadeInSeconds" value="${esc(d.fadeInSeconds)}" />
          <label>淡出</label><input type="number" step="0.01" min="0" data-f="fadeOutSeconds" value="${esc(d.fadeOutSeconds)}" />
          <label>循环</label><input type="checkbox" data-f="loop" ${d.loop ? "checked" : ""} />
        </div>
        <div class="actions" style="margin-top:8px">
          <button type="button" class="primary" id="bgmApply">应用补丁</button>
          <button type="button" id="bgmSave">保存此条</button>
          <button type="button" id="bgmRevert">回撤此条</button>
          <button type="button" id="bgmPreview">预览</button>
          <button type="button" id="bgmEndPreview">结束预览</button>
        </div>
        <p class="meta">BGM 不 live 热换 music catalog；保存后下次运行时安装生效。</p>`;
      document.getElementById("bgmApply").addEventListener("click", async () => {
        const root = document.getElementById("bgmForm");
        const get = (f) => root.querySelector(`[data-f="${f}"]`);
        await run("replaceMusicBinding", {
          state: entry.state,
          replacement: {
            state: entry.state,
            enabled: !!get("enabled").checked,
            clipKey: get("clipKey").value,
            volumeDb: Number(get("volumeDb").value || 0),
            startOffsetSeconds: Number(get("startOffsetSeconds").value || 0),
            fadeInSeconds: Number(get("fadeInSeconds").value || 0),
            fadeOutSeconds: Number(get("fadeOutSeconds").value || 0),
            loop: !!get("loop").checked,
          },
        });
      });
      document.getElementById("bgmSave").addEventListener("click", () =>
        run("saveBinding", { bindingKey: entry.state, channel: "music" }));
      document.getElementById("bgmRevert").addEventListener("click", () =>
        run("revertBinding", { bindingKey: entry.state, channel: "music" }));
      document.getElementById("bgmPreview").addEventListener("click", () =>
        run("previewMusic", { state: entry.state }));
      document.getElementById("bgmEndPreview").addEventListener("click", () =>
        run("endMusicPreview", {}));
    }
    wireSplitters(els.panel.querySelector(".bgm-layout"), "bgm");
  }

  function wireSplitters(layout, kind) {
    if (!layout) return;
    layout.querySelectorAll(".splitter").forEach((sp) => {
      sp.addEventListener("mousedown", (ev) => {
        ev.preventDefault();
        const startX = ev.clientX;
        const rect = layout.getBoundingClientRect();
        const onMove = (e) => {
          const x = (e.clientX - rect.left) / rect.width;
          if (kind === "capture") {
            if (sp.dataset.split === "left-mid") {
              cols.left = Math.max(0.2, Math.min(0.55, x)) * 2 + "fr";
            } else {
              cols.right = Math.max(0.2, Math.min(0.55, 1 - x)) * 2 + "fr";
            }
            layout.style.setProperty("--col-left", cols.left);
            layout.style.setProperty("--col-mid", cols.mid);
            layout.style.setProperty("--col-right", cols.right);
          } else if (kind === "static") {
            cols.static = Math.max(0.4, Math.min(2.2, x * 2.5)) + "fr";
            layout.style.setProperty("--col-static", cols.static);
          } else {
            cols.bgm = Math.max(0.4, Math.min(2.2, x * 2.5)) + "fr";
            layout.style.setProperty("--col-bgm", cols.bgm);
          }
        };
        const onUp = () => {
          window.removeEventListener("mousemove", onMove);
          window.removeEventListener("mouseup", onUp);
          persistUi();
        };
        window.addEventListener("mousemove", onMove);
        window.addEventListener("mouseup", onUp);
      });
    });
  }

  function renderGlobal() {
    const play = !!(state && state.playMode);
    setConnected(true, play ? "已连接 · 运行时" : "已连接 · 非运行时");
    els.status.textContent = (state && state.statusMessage) || "";
    els.dirty.textContent = "SFX脏 " + ((state && state.sfxDirtyCount) || 0)
      + " · BGM脏 " + ((state && state.musicDirtyCount) || 0)
      + ((state && state.blocksMutations) ? " · 冲突门禁中" : "");
    els.error.textContent = (state && state.errorMessage) || "";

    els.actions.innerHTML = `
      <button type="button" id="gSaveAll">保存全部</button>
      <button type="button" id="gRevertAll">回撤全部</button>
      <button type="button" id="gReload">重载磁盘</button>`;
    document.getElementById("gSaveAll").onclick = () => run("saveAll", {});
    document.getElementById("gRevertAll").onclick = () => run("revertAll", {});
    document.getElementById("gReload").onclick = () => run("reloadFromDisk", {});

    if (state && state.blocksMutations) {
      els.conflict.classList.remove("hidden");
      els.conflict.innerHTML = `
        <div class="msg">${esc(state.errorMessage || state.statusMessage || "磁盘冲突")}</div>
        <button type="button" class="danger" id="cRecover">恢复临时版覆盖磁盘</button>
        <button type="button" id="cDiscard">丢弃临时版用磁盘</button>`;
      document.getElementById("cRecover").onclick = () => run("recoverTransient", {});
      document.getElementById("cDiscard").onclick = () => run("discardTransient", {});
    } else {
      els.conflict.classList.add("hidden");
      els.conflict.innerHTML = "";
    }
  }

  function render() {
    if (!state) return;
    if (!mode) mode = state.defaultMode || (state.playMode ? "实时抓音" : "静态绑定库");
    // Play Mode 默认抓音：仅首次无 local mode 时
    els.tabs.forEach((tab) => tab.classList.toggle("active", tab.dataset.mode === mode));
    persistUi();
    renderGlobal();
    if (mode === "实时抓音") renderCapture();
    else if (mode === "静态绑定库") renderStatic();
    else renderBgm();
  }

  function applyEnvelope(envelope) {
    if (!envelope) return;
    if (envelope.type === "snapshot") {
      if (typeof envelope.revision === "number") revision = envelope.revision;
      state = envelope.payload || null;
      if (state && state.focusedBindingKey) selectedBindingKey = state.focusedBindingKey;
      syncStreamFromState();
      render();
      return;
    }
    if (envelope.type === "delta") {
      if (typeof envelope.revision !== "number" || envelope.revision !== revision + 1) {
        api("/api/snapshot").then(applyEnvelope).catch((err) => {
          setConnected(false, "snapshot 重拉失败");
          els.error.textContent = String(err);
        });
        return;
      }
      revision = envelope.revision;
      state = Object.assign({}, state || {}, envelope.payload || {});
      if (state.focusedBindingKey) selectedBindingKey = state.focusedBindingKey;
      syncStreamFromState();
      // Avoid wiping an in-progress tuning form on high-frequency runtime deltas.
      if (isEditingDetail() && mode === "实时抓音") {
        renderGlobal();
        const freq = document.getElementById("freqRail");
        if (freq) renderFreqRail(freq);
        const now = document.getElementById("nowPlaying");
        if (now) renderNowPlaying(now);
        const list = document.getElementById("streamList");
        if (list) {
          const rows = filteredStream();
          attachVirtualList(list, rows, (el, row) => {
            el.classList.toggle("selected", row.sequence === selectedSequence);
            el.classList.toggle("flash", (flashUntil.get(row.sequence) || 0) > Date.now());
            el.style.gridTemplateColumns = "52px 96px 1fr 1fr";
            el.innerHTML = `<span class="seq">#${row.sequence}</span><span class="outcome outcome-${esc(row.outcome)}">${esc(row.outcome)}</span><span class="ellipsis">${esc(row.cueNote || row.cueId || "")}</span><span class="ellipsis mono">${esc(row.bindingKey || row.actualClipKey || "")}</span>`;
          }, (row) => {
            selectedSequence = row.sequence;
            selectedBindingKey = row.bindingKey || findDeclForEvent(row)?.bindingKey || null;
            render();
          });
        }
        return;
      }
      render();
    }
  }

  function isTypingTarget(el) {
    if (!el) return false;
    const tag = (el.tagName || "").toLowerCase();
    if (tag === "input" || tag === "textarea" || tag === "select") return true;
    return !!el.isContentEditable;
  }

  function onKey(ev) {
    if (isTypingTarget(ev.target)) return;
    if (ev.key === " " || ev.code === "Space") {
      ev.preventDefault();
      const row = selectedEvent() || filteredStream()[0];
      if (row) pinEvent(row);
      return;
    }
    if (ev.key === "e" || ev.key === "E") {
      ev.preventDefault();
      const decl = selectedBindingKey ? declByKey(selectedBindingKey) : null;
      if (!decl || !decl.dto) return;
      const replacement = Object.assign({}, decl.dto, { enabled: !decl.dto.enabled });
      run("replaceSfxBinding", { originalBindingKey: decl.bindingKey, replacement });
      return;
    }
    if (ev.key === "p" || ev.key === "P") {
      ev.preventDefault();
      const decl = selectedBindingKey ? declByKey(selectedBindingKey) : null;
      if (decl) onDetailAction("preview", selectedEvent(), decl);
      return;
    }
    if ((ev.ctrlKey || ev.metaKey) && (ev.key === "s" || ev.key === "S")) {
      ev.preventDefault();
      const decl = selectedBindingKey ? declByKey(selectedBindingKey) : null;
      if (decl) run("saveBinding", { bindingKey: decl.bindingKey, channel: "sfx" });
      return;
    }
    if (ev.key === "Escape") {
      ev.preventDefault();
      selectedSequence = null;
      filters.aggregateKey = "";
      persistUi();
      render();
    }
  }

  async function bootstrap() {
    if (!token) {
      setConnected(false, "缺少 token");
      return;
    }
    if (ui.mode) mode = ui.mode;
    els.tabs.forEach((tab) => tab.addEventListener("click", () => {
      mode = tab.dataset.mode;
      persistUi();
      render();
    }));
    window.addEventListener("keydown", onKey);

    try {
      const snap = await api("/api/snapshot");
      applyEnvelope(snap);
    } catch (err) {
      setConnected(false, "鉴权失败");
      els.error.textContent = String(err);
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
      els.error.textContent = String(err);
    }
    setTimeout(longPoll, 250);
  }

  bootstrap();
})();
