(() => {
  const UI_KEY = "NineGrid.VfxWorkbench.Ui.v1";
  const MATERIAL_PLAYER = "sprite-sheet";
  const ABNORMAL = new Set([
    "Suppressed", "Unbound", "InvalidBinding",
    "PlayerUnavailable", "DomainUnavailable", "BackendFailure",
  ]);
  const DEFAULT_OUTCOMES = [
    "Played", "Suppressed", "Unbound", "InvalidBinding",
    "PlayerUnavailable", "DomainUnavailable", "BackendFailure",
    "Scheduled", "Cancelled", "Requested",
  ];
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
  let selectedChannel = "cue";
  let staticChannel = "cue";
  let streamPaused = false;
  let pendingHistory = [];
  let streamRows = [];
  let flashUntil = new Map();
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
    staticFlags: { dirty: false, unbound: false, broken: false, disabled: false },
  }, ui.filters || {});
  const cols = Object.assign({
    left: "1.2fr", mid: "0.9fr", right: "1.1fr", static: "1.6fr", state: "1.2fr",
  }, ui.cols || {});
  let aggregateLevel = ui.aggregateLevel || "instance";

  function loadUi() {
    try { return JSON.parse(localStorage.getItem(UI_KEY) || "{}"); } catch { return {}; }
  }
  function saveUi(patch) {
    localStorage.setItem(UI_KEY, JSON.stringify(Object.assign(loadUi(), patch)));
  }
  function persistUi() {
    saveUi({ mode, pins: Array.from(pins.values()), filters, cols, aggregateLevel, staticChannel });
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
    if (!res.ok) throw new Error((json && (json.error || json.message)) || (path + " -> " + res.status));
    return json;
  }

  async function command(name, payload = {}) {
    const result = await api("/api/command", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ requestId: "r" + (++requestSeq), command: name, payload }),
    });
    if (!result.ok) throw new Error(result.error || (name + " failed"));
    if (typeof result.revision === "number") revision = result.revision;
    return result;
  }

  async function run(name, payload) {
    try {
      els.error.textContent = "";
      await command(name, payload);
      applyEnvelope(await api("/api/snapshot"));
    } catch (err) {
      els.error.textContent = String(err.message || err);
      render();
    }
  }

  function setConnected(ok, label) {
    els.badge.className = "badge " + (ok ? "ok" : "bad");
    els.badge.textContent = label;
  }

  function cueDecls() { return state.cueDeclarations || []; }
  function stateDecls() { return state.stateDeclarations || []; }
  function allDecls() { return cueDecls().concat(stateDecls()); }

  function declByKey(key) {
    return allDecls().find((d) => d.bindingKey === key) || null;
  }
  function declChannel(decl) {
    if (!decl) return selectedChannel;
    return decl.channel || (decl.stateId ? "state" : "cue");
  }
  function findDeclForEvent(ev) {
    if (ev && ev.bindingKey) {
      const byKey = declByKey(ev.bindingKey);
      if (byKey) return byKey;
    }
    if (ev && ev.cueId) {
      return cueDecls().find((d) => d.cueId === ev.cueId) || null;
    }
    return null;
  }
  function isMaterialPlayer(playerId) {
    return playerId === MATERIAL_PLAYER;
  }

  function disableLabel(decl) {
    if (!decl || !decl.dto) return "";
    const enabled = !!decl.dto.enabled;
    const saved = decl.savedEnabled !== false;
    if (!enabled && decl.isDirty) return "临时停用（未保存）";
    if (!enabled && !decl.isDirty) return "已永久禁用";
    if (enabled && decl.isDirty && !saved) return "临时恢复（未保存）";
    return enabled ? "已启用" : "已永久禁用";
  }

  function abnormalCount(a) {
    return (a.suppressed || 0) + (a.unbound || 0) + (a.invalidBinding || 0)
      + (a.playerUnavailable || 0) + (a.domainUnavailable || 0) + (a.backendFailure || 0);
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

  function syncStreamFromState() {
    if (!state || !state.playMode || !state.runtime) {
      streamRows = []; pendingHistory = []; flashUntil.clear(); return;
    }
    const hist = ((state.runtime.history) || []).slice();
    if (streamPaused) {
      const known = new Set(streamRows.map((r) => r.sequence).concat(pendingHistory.map((r) => r.sequence)));
      hist.forEach((row) => { if (!known.has(row.sequence)) pendingHistory.push(row); });
      return;
    }
    if (pendingHistory.length) { hist.push(...pendingHistory); pendingHistory = []; }
    const serverSeqs = new Set(hist.map((r) => r.sequence));
    const bySeq = new Map();
    streamRows.forEach((r) => { if (serverSeqs.has(r.sequence) || pins.has(r.sequence)) bySeq.set(r.sequence, r); });
    hist.forEach((r) => {
      if (!bySeq.has(r.sequence)) flashUntil.set(r.sequence, Date.now() + 600);
      bySeq.set(r.sequence, r);
    });
    streamRows = Array.from(bySeq.values()).sort((a, b) => b.sequence - a.sequence);
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
        const agg = ((state.runtime && state.runtime.aggregates) || [])
          .find((a) => a.aggregateKey === filters.aggregateKey);
        if (!agg) return false;
        if (aggregateLevel === "binding") {
          const match = (agg.bindingKey && agg.bindingKey === row.bindingKey)
            || (agg.cueOrStateId && (agg.cueOrStateId === row.cueId));
          if (!match) return false;
        } else if (row.instanceId && agg.aggregateKey !== row.instanceId) {
          if (row.bindingKey !== agg.bindingKey && row.cueId !== agg.cueOrStateId) return false;
        }
      }
      if (module) {
        const decl = findDeclForEvent(row);
        if (!decl || decl.module !== module) return false;
      }
      if (text) {
        const hay = [row.cueNote, row.cueId, row.bindingKey, row.diagnosticSource,
          row.playerId, row.materialKey, row.instanceId, row.failureReason].join(" ").toLowerCase();
        if (!hay.includes(text)) return false;
      }
      return true;
    });
  }

  function filteredStaticDecls() {
    const text = (filters.staticText || "").trim().toLowerCase();
    const f = filters.staticFlags || {};
    const list = staticChannel === "state" ? stateDecls() : cueDecls();
    return list.filter((d) => {
      if (f.dirty && !d.isDirty) return false;
      if (f.unbound && !d.isUnbound) return false;
      if (f.broken && !d.isBroken) return false;
      if (f.disabled && !d.isDisabled) return false;
      if (text) {
        const id = d.cueId || d.stateId || "";
        const hay = [d.note, id, d.module, d.bindingKey, d.dto && d.dto.playerId, d.dto && d.dto.materialKey]
          .join(" ").toLowerCase();
        if (!hay.includes(text)) return false;
      }
      return true;
    });
  }

  function latestOutcome(decl) {
    const hist = (state.runtime && state.runtime.history) || [];
    for (let i = hist.length - 1; i >= 0; i--) {
      const h = hist[i];
      if ((decl.bindingKey && h.bindingKey === decl.bindingKey)
        || (decl.cueId && h.cueId === decl.cueId)) return h.outcome;
    }
    return "—";
  }

  function selectedEvent() {
    if (selectedSequence == null) return null;
    return pins.get(selectedSequence) || streamRows.find((r) => r.sequence === selectedSequence) || null;
  }

  function focusBinding(key, channel) {
    selectedBindingKey = key || null;
    if (channel) selectedChannel = channel;
    if (!key) { render(); return; }
    run("focusBinding", { bindingKey: key });
  }

  function pinEvent(ev) {
    if (!ev) return;
    if (pins.has(ev.sequence)) {
      selectedSequence = ev.sequence;
      const p = pins.get(ev.sequence);
      selectedBindingKey = p.bindingKey || findDeclForEvent(p)?.bindingKey || null;
    } else {
      pins.set(ev.sequence, Object.assign({}, ev));
      selectedSequence = ev.sequence;
      selectedBindingKey = ev.bindingKey || findDeclForEvent(ev)?.bindingKey || null;
    }
    persistUi(); render();
  }

  function buildSparkline(stamps) {
    const vals = (stamps || []).map((t) => t > 1e12 ? t : t * 1000);
    if (!vals.length) return "";
    const min = Math.min(...vals), max = Math.max(...vals), span = Math.max(1, max - min);
    const w = 140, h = 22;
    const pts = vals.map((t, i) => {
      const x = (i / Math.max(1, vals.length - 1)) * (w - 2) + 1;
      const y = h - 2 - ((t - min) / span) * (h - 4);
      return x.toFixed(1) + "," + y.toFixed(1);
    }).join(" ");
    return `<svg class="spark" viewBox="0 0 ${w} ${h}" preserveAspectRatio="none"><polyline fill="none" stroke="#d9a441" stroke-width="1.5" points="${pts}"/></svg>`;
  }

  function renderAggRail(host) {
    const aggs = ((state.runtime && state.runtime.aggregates) || []).slice().sort((a, b) => {
      const aa = abnormalCount(a), ba = abnormalCount(b);
      if (ba !== aa) return ba - aa;
      return (b.lastTime || 0) - (a.lastTime || 0);
    });
    if (!aggs.length) {
      host.innerHTML = `<div class="empty">暂无聚合（进入 Play 后触发 VFX 可见）</div>`;
      return;
    }
    host.innerHTML = aggs.map((a) => {
      const decl = a.bindingKey ? declByKey(a.bindingKey) : null;
      const title = (decl && decl.note) || a.cueOrStateId || a.aggregateKey;
      const hot = isHot(a);
      const level = aggregateLevel === "binding" ? "Binding" : "实例";
      return `<div class="freq-card ${hot ? "hot" : ""}" data-agg="${esc(a.aggregateKey)}">
        <div class="title">${esc(title)}${hot ? '<span class="badge-hot">高频</span>' : ""}</div>
        <div class="meta">${level} · req ${a.requested || 0} · play ${a.played || 0} · fail ${abnormalCount(a)}</div>
        ${buildSparkline(a.recentTimestamps)}
      </div>`;
    }).join("");
    host.querySelectorAll(".freq-card").forEach((card) => {
      card.addEventListener("click", () => {
        filters.aggregateKey = card.dataset.agg;
        filters.text = "";
        const agg = aggs.find((a) => a.aggregateKey === filters.aggregateKey);
        if (agg && agg.bindingKey) {
          selectedBindingKey = agg.bindingKey;
          selectedChannel = declChannel(declByKey(agg.bindingKey));
        }
        const newest = streamRows.find((r) =>
          (agg && agg.bindingKey && r.bindingKey === agg.bindingKey)
          || (agg && agg.cueOrStateId && r.cueId === agg.cueOrStateId));
        if (newest) selectedSequence = newest.sequence;
        persistUi(); render();
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
      const start = Math.max(0, Math.floor(scroller.scrollTop / ROW_H) - OVERSCAN);
      const end = Math.min(rows.length, Math.ceil((scroller.scrollTop + scroller.clientHeight) / ROW_H) + OVERSCAN);
      ensurePool(end - start);
      for (let i = 0; i < pool.length; i++) {
        const idx = start + i, el = pool[i];
        if (idx >= end) { el.style.display = "none"; continue; }
        el.style.display = "grid";
        el.style.top = (idx * ROW_H) + "px";
        el._data = rows[idx];
        paintRow(el, rows[idx], idx);
      }
    }
    scroller.addEventListener("scroll", paint, { passive: true });
    paint();
    return { paint };
  }

  async function disableBindingTemp(decl) {
    if (!decl || !decl.dto) return;
    const ch = declChannel(decl);
    const cmd = ch === "state" ? "replaceStateBinding" : "replaceCueBinding";
    await run(cmd, {
      originalBindingKey: decl.bindingKey,
      replacement: Object.assign({}, decl.dto, { enabled: false }),
    });
    selectedBindingKey = state.focusedBindingKey || decl.bindingKey;
  }

  async function disableBindingPermanent(decl) {
    if (!decl) return;
    if (decl.dto && decl.dto.enabled) {
      await disableBindingTemp(decl);
      decl = declByKey(selectedBindingKey) || decl;
    }
    await run("saveBinding", { bindingKey: decl.bindingKey, channel: declChannel(decl) });
  }

  function readBindingForm(root, baseDto, channel) {
    const get = (f) => root.querySelector(`[data-f="${f}"]`);
    let variants = baseDto.variants || [];
    try { variants = JSON.parse(get("variantsJson").value || "[]"); } catch { /* keep */ }
    const dto = {
      enabled: !!get("enabled").checked,
      playerId: get("playerId").value,
      materialKey: get("materialKey").value,
      selectorCardDefId: get("selectorCardDefId").value,
      selectorSkillId: get("selectorSkillId").value,
      selectorRoomId: get("selectorRoomId").value,
      selectorItemDefId: get("selectorItemDefId").value,
      selectorContentId: get("selectorContentId").value,
      variants,
    };
    if (channel === "cue") {
      dto.cueId = baseDto.cueId;
      dto.bindingDelaySeconds = Number(get("bindingDelaySeconds").value || 0);
      dto.minimumIntervalSeconds = Number(get("minimumIntervalSeconds").value || 0);
    } else {
      dto.stateId = baseDto.stateId;
    }
    return dto;
  }

  function bindingFormHtml(decl) {
    const dto = decl.dto;
    const ch = declChannel(decl);
    const mats = state.materialOptions || [];
    const programmatic = !isMaterialPlayer(dto.playerId);
    if (programmatic) {
      return `<div class="section"><h3>程序化播放器</h3>
        <div class="prog-note">播放器 <b>${esc(dto.playerId)}</b> 自治内部参数与资源；工作台仅提供观察、预览与停用，不提供伪造调参表。</div>
        <div class="mute-label">${esc(disableLabel(decl))}</div>
        <div class="actions" style="margin-top:8px">
          <button type="button" data-act="preview">预览 Pulse</button>
          <button type="button" class="danger" data-act="disable-temp">临时停用</button>
          <button type="button" class="danger primary" data-act="disable-save">保存永久停用</button>
          <button type="button" data-act="save-one">保存此条</button>
          <button type="button" data-act="revert-one">回撤此条</button>
        </div></div>`;
    }
    return `<div class="section"><h3>序列帧绑定</h3>
      <div class="mute-label">${esc(disableLabel(decl))}</div>
      <div class="form-grid" id="bindingForm">
        <label>启用</label><input type="checkbox" data-f="enabled" ${dto.enabled ? "checked" : ""} />
        <label>播放器</label><input type="text" data-f="playerId" value="${esc(dto.playerId || MATERIAL_PLAYER)}" readonly />
        <label>素材</label><select data-f="materialKey">${mats.map((m) =>
          `<option value="${esc(m.resourcesKey || m.materialKey)}" ${(m.resourcesKey || m.materialKey) === dto.materialKey ? "selected" : ""}>${esc(m.resourcesKey || m.materialKey)}</option>`).join("")}</select>
        <label>绑定延迟</label><input type="number" step="0.01" min="0" data-f="bindingDelaySeconds" value="${esc(dto.bindingDelaySeconds || 0)}" ${ch === "state" ? "disabled" : ""} />
        <label>最短间隔</label><input type="number" step="0.01" min="0" data-f="minimumIntervalSeconds" value="${esc(dto.minimumIntervalSeconds || 0)}" ${ch === "state" ? "disabled" : ""} />
        <label>cardDef</label><input type="text" data-f="selectorCardDefId" value="${esc(dto.selectorCardDefId || "")}" />
        <label>skillId</label><input type="text" data-f="selectorSkillId" value="${esc(dto.selectorSkillId || "")}" />
        <label>roomId</label><input type="text" data-f="selectorRoomId" value="${esc(dto.selectorRoomId || "")}" />
        <label>itemDef</label><input type="text" data-f="selectorItemDefId" value="${esc(dto.selectorItemDefId || "")}" />
        <label>contentId</label><input type="text" data-f="selectorContentId" value="${esc(dto.selectorContentId || "")}" />
        <label>变体 JSON</label><textarea data-f="variantsJson" rows="3">${esc(JSON.stringify(dto.variants || [], null, 0))}</textarea>
      </div>
      <div class="actions" style="margin-top:8px">
        <button type="button" class="primary" data-act="apply-patch">应用补丁</button>
        <button type="button" data-act="preview">预览</button>
        <button type="button" data-act="save-one">保存此条</button>
        <button type="button" data-act="revert-one">回撤此条</button>
      </div></div>`;
  }

  async function onDetailAction(act, ev, decl) {
    if (act === "toggle-pin") {
      if (pins.has(ev.sequence)) { pins.delete(ev.sequence); if (selectedSequence === ev.sequence) selectedSequence = null; }
      else pinEvent(ev);
      persistUi(); render(); return;
    }
    if (act === "copy-src") {
      const text = [ev.diagnosticSource, ev.cueId, ev.bindingKey, ev.instanceId,
        "card=" + (ev.cardDefId || ""), "skill=" + (ev.skillId || ""), "uid=" + (ev.diagnosticCardUid || "")].join("\n");
      try { await navigator.clipboard.writeText(text); els.status.textContent = "已复制触发源"; }
      catch { els.error.textContent = "复制失败"; }
      return;
    }
    if (act === "disable-temp") { await disableBindingTemp(decl); return; }
    if (act === "disable-save") { await disableBindingPermanent(decl); return; }
    if (act === "preview") {
      await command("previewBinding", { bindingKey: decl.bindingKey, channel: declChannel(decl) });
      applyEnvelope(await api("/api/snapshot"));
      return;
    }
    if (act === "save-one") {
      await run("saveBinding", { bindingKey: decl.bindingKey, channel: declChannel(decl) });
      return;
    }
    if (act === "revert-one") {
      await run("revertBinding", { bindingKey: decl.bindingKey, channel: declChannel(decl) });
      return;
    }
    if (act === "apply-patch") {
      const form = document.getElementById("bindingForm");
      const ch = declChannel(decl);
      const cmd = ch === "state" ? "replaceStateBinding" : "replaceCueBinding";
      await run(cmd, {
        originalBindingKey: decl.bindingKey,
        replacement: readBindingForm(form, decl.dto, ch),
      });
      selectedBindingKey = state.focusedBindingKey || decl.bindingKey;
    }
    if (act === "drill-binding" && ev && ev.bindingKey) {
      selectedBindingKey = ev.bindingKey;
      selectedChannel = declChannel(declByKey(ev.bindingKey));
      render();
    }
  }

  function renderDetail(host) {
    const ev = selectedEvent();
    const decl = selectedBindingKey ? declByKey(selectedBindingKey) : (ev ? findDeclForEvent(ev) : null);
    if (!ev && !decl) {
      host.innerHTML = `<div class="empty">选择一条实例或绑定以检查<br/>
        <div class="hint-line">快捷键：Space 固定 · <b>E</b> 临时停 · <b>Ctrl+S</b> 永久停 · P 预览 · Esc 取消</div></div>`;
      return;
    }
    const dto = decl && decl.dto;
    let html = `<div class="section kill-panel"><h3>后续抑制</h3>
      <div class="kill-actions">
        <button type="button" class="danger" data-act="disable-temp" ${dto ? "" : "disabled"}>临时停用绑定</button>
        <button type="button" class="danger primary" data-act="disable-save" ${decl ? "" : "disabled"}>保存永久停用</button>
      </div>
      <div class="hint-line">Pulse 实例只观察，不强杀当前实例；停用 Binding 仅抑制后续请求。</div></div>`;

    html += `<div class="section"><h3>检查</h3><div class="kv">`;
    if (ev) {
      html += `
        <b>outcome</b><span class="outcome-${esc(ev.outcome)}">${esc(ev.outcome)}</span>
        <b>cue</b><span>${esc(ev.cueNote || "")} · ${esc(ev.cueId || "")}</span>
        <b>BindingKey</b><span class="mono">${esc(ev.bindingKey || "")}</span>
        <b>播放器/素材</b><span>${esc(ev.playerId || "—")} / ${esc(ev.materialKey || "—")}</span>
        <b>变体/实例</b><span>${esc(ev.variantId || "—")} / <span class="mono">${esc(ev.instanceId || "")}</span></span>
        <b>诊断来源</b><span>${esc(ev.diagnosticSource || "")}</span>
        <b>上下文</b><span>card=${esc(ev.cardDefId||"")} skill=${esc(ev.skillId||"")} uid=${esc(ev.diagnosticCardUid||"")}</span>
        <b>失败原因</b><span>${esc(ev.failureReason || "")}</span>
        <b>time</b><span>${esc(ev.time)}</span>`;
    } else if (decl) {
      const id = decl.cueId || decl.stateId || "";
      html += `
        <b>身份</b><span>${esc(decl.note || "")} · ${esc(id)}</span>
        <b>BindingKey</b><span class="mono">${esc(decl.bindingKey || "")}</span>
        <b>模块</b><span>${esc(decl.module || "")}</span>
        <b>发射者</b><span>${esc(decl.authoritativeEmitter || "")}</span>
        <b>运行时</b><span>${esc(latestOutcome(decl))}</span>`;
    }
    html += `</div><div class="actions" style="margin-top:8px">`;
    if (ev) {
      html += `<button type="button" data-act="toggle-pin">${pins.has(ev.sequence) ? "取消固定" : "固定"}</button>`;
      if (ev.bindingKey) html += `<button type="button" data-act="drill-binding">钻取 Binding</button>`;
      html += `<button type="button" data-act="copy-src">复制触发源</button>`;
    }
    html += `<button type="button" data-act="preview" ${decl ? "" : "disabled"}>预览绑定</button></div></div>`;
    if (decl && dto) html += bindingFormHtml(decl);
    host.innerHTML = html;
    host.querySelectorAll("[data-act]").forEach((btn) => {
      btn.addEventListener("click", () => onDetailAction(btn.dataset.act, ev, decl));
    });
  }

  function renderActivePulses(host) {
    if (!host) return;
    const playMode = !!(state && state.playMode);
    const pulses = playMode ? ((state.runtime && state.runtime.activeInstances) || []).slice() : [];
    host.innerHTML = `<div class="active-head"><h2>活跃 Pulse 实例</h2>
      <span class="meta">${pulses.length} 个</span></div>
      ${!pulses.length ? `<div class="empty">${playMode ? "当前无活跃 Pulse" : "进入 Play Mode 后显示"}</div>` :
        `<div class="active-list">${pulses.map((p) => `
          <div class="active-row">
            <span class="ellipsis mono" title="${esc(p.instanceId)}">${esc(p.instanceId || "—")}</span>
            <span class="ellipsis">${esc(p.cueId || "")}</span>
            <span class="ellipsis">${esc(p.playerId || "")}</span>
            <span class="ellipsis">${esc(p.materialKey || "")}</span>
            <span class="active-acts">
              <button type="button" data-inst="${esc(p.instanceId)}" data-bkey="${esc(p.bindingKey || "")}" data-act="focus">观察</button>
            </span>
          </div>`).join("")}</div>`}`;
    host.querySelectorAll("[data-act=focus]").forEach((btn) => {
      btn.addEventListener("click", () => {
        if (btn.dataset.bkey) { selectedBindingKey = btn.dataset.bkey; selectedChannel = "cue"; }
        render();
      });
    });
  }

  function renderRealtime() {
    syncStreamFromState();
    const modules = Array.from(new Set(allDecls().map((d) => d.module).filter(Boolean))).sort();
    els.panel.innerHTML = `
      <div id="aggRail" class="freq-rail"></div>
      <div id="activePanel" class="active-panel"></div>
      <div class="capture-layout" style="--col-left:${cols.left};--col-mid:${cols.mid};--col-right:${cols.right}">
        <section class="col">
          <div class="col-head">
            <h2>实例流</h2>
            <div class="filters">
              <input type="text" id="fltText" placeholder="文本过滤" value="${esc(filters.text)}" />
              <select id="fltModule"><option value="">模块</option>${modules.map((m) =>
                `<option value="${esc(m)}" ${m === filters.module ? "selected" : ""}>${esc(m)}</option>`).join("")}</select>
              <button type="button" class="chip ${filters.onlyAbnormal ? "on" : ""}" id="fltAbn">只看异常</button>
              <button type="button" class="chip ${streamPaused ? "on" : ""}" id="fltPause">${streamPaused ? "恢复流入" : "暂停视图"}</button>
              <button type="button" class="chip ${aggregateLevel === "instance" ? "on" : ""}" id="aggInst">实例聚合</button>
              <button type="button" class="chip ${aggregateLevel === "binding" ? "on" : ""}" id="aggBind">Binding 聚合</button>
              ${filters.aggregateKey ? `<button type="button" id="fltClearAgg">清除筛选</button>` : ""}
            </div>
            <div class="filters" id="outcomeChips"></div>
          </div>
          <div class="vlist" id="streamList"></div>
        </section>
        <div class="splitter" data-split="left-mid"></div>
        <section class="col">
          <div class="col-head"><h2>已固定</h2><span class="meta">${pins.size}</span></div>
          <div id="pinList" style="overflow:auto;flex:1"></div>
        </section>
        <div class="splitter" data-split="mid-right"></div>
        <section class="col">
          <div class="col-head"><h2>检查与编辑</h2></div>
          <div class="detail" id="detailHost"></div>
        </section>
      </div>`;

    renderAggRail(document.getElementById("aggRail"));
    renderActivePulses(document.getElementById("activePanel"));

    const chipHost = document.getElementById("outcomeChips");
    DEFAULT_OUTCOMES.forEach((o) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "chip " + ((filters.outcomes || []).includes(o) ? "on" : "");
      btn.textContent = o;
      btn.addEventListener("click", () => {
        const set = new Set(filters.outcomes || []);
        if (set.has(o)) set.delete(o); else set.add(o);
        filters.outcomes = Array.from(set);
        persistUi(); render();
      });
      chipHost.appendChild(btn);
    });

    document.getElementById("fltText").addEventListener("change", (e) => { filters.text = e.target.value; persistUi(); render(); });
    document.getElementById("fltModule").addEventListener("change", (e) => { filters.module = e.target.value; persistUi(); render(); });
    document.getElementById("fltAbn").addEventListener("click", () => { filters.onlyAbnormal = !filters.onlyAbnormal; persistUi(); render(); });
    document.getElementById("fltPause").addEventListener("click", () => { streamPaused = !streamPaused; if (!streamPaused) syncStreamFromState(); render(); });
    document.getElementById("aggInst").addEventListener("click", () => { aggregateLevel = "instance"; persistUi(); render(); });
    document.getElementById("aggBind").addEventListener("click", () => { aggregateLevel = "binding"; persistUi(); render(); });
    const clearAgg = document.getElementById("fltClearAgg");
    if (clearAgg) clearAgg.addEventListener("click", () => { filters.aggregateKey = ""; persistUi(); render(); });

    attachVirtualList(document.getElementById("streamList"), filteredStream(), (el, row) => {
      el.classList.toggle("selected", row.sequence === selectedSequence);
      el.classList.toggle("flash", (flashUntil.get(row.sequence) || 0) > Date.now());
      el.style.gridTemplateColumns = "52px 110px 1fr 1fr";
      el.innerHTML = `<span class="seq">#${row.sequence}</span><span class="outcome outcome-${esc(row.outcome)}">${esc(row.outcome)}</span><span class="ellipsis">${esc(row.cueNote || row.cueId || "")}</span><span class="ellipsis mono">${esc(row.instanceId || row.bindingKey || "")}</span>`;
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
          <div class="meta mono">${esc(p.instanceId || p.bindingKey || "")}</div>
        </div>`).join("");
      pinHost.querySelectorAll(".pin-card").forEach((card) => {
        card.addEventListener("click", () => {
          const p = pins.get(Number(card.dataset.seq));
          selectedSequence = p.sequence;
          selectedBindingKey = p.bindingKey || findDeclForEvent(p)?.bindingKey || null;
          render();
        });
      });
    }
    renderDetail(document.getElementById("detailHost"));
    wireSplitters(els.panel.querySelector(".capture-layout"), "capture");
  }

  function renderStatic() {
    const rows = filteredStaticDecls();
    const f = filters.staticFlags;
    els.panel.innerHTML = `
      <div class="static-layout" style="--col-static:${cols.static};--col-right:${cols.right}">
        <section class="col">
          <div class="col-head">
            <h2>静态绑定库</h2>
            <button type="button" class="chip ${staticChannel === "cue" ? "on" : ""}" id="chCue">Cue 绑定</button>
            <button type="button" class="chip ${staticChannel === "state" ? "on" : ""}" id="chState">State 绑定</button>
            <input type="text" id="staticText" placeholder="搜索 note/id/player" value="${esc(filters.staticText)}" />
            <button type="button" class="chip ${f.dirty ? "on" : ""}" data-flag="dirty">脏</button>
            <button type="button" class="chip ${f.unbound ? "on" : ""}" data-flag="unbound">未绑</button>
            <button type="button" class="chip ${f.broken ? "on" : ""}" data-flag="broken">断链</button>
            <button type="button" class="chip ${f.disabled ? "on" : ""}" data-flag="disabled">禁用</button>
          </div>
          <div class="table-head">
            <span>说明</span><span>ID</span><span>模块</span><span>播放器</span><span>素材/选择器</span><span>启</span><span>脏</span><span>断</span><span>运行时</span>
          </div>
          <div class="vlist" id="staticList"></div>
        </section>
        <div class="splitter" data-split="static-right"></div>
        <section class="col">
          <div class="col-head"><h2>检查与编辑</h2></div>
          <div class="detail" id="detailHost"></div>
        </section>
      </div>`;

    document.getElementById("chCue").onclick = () => { staticChannel = "cue"; persistUi(); render(); };
    document.getElementById("chState").onclick = () => { staticChannel = "state"; persistUi(); render(); };
    document.getElementById("staticText").addEventListener("change", (e) => { filters.staticText = e.target.value; persistUi(); render(); });
    els.panel.querySelectorAll("[data-flag]").forEach((btn) => {
      btn.addEventListener("click", () => { f[btn.dataset.flag] = !f[btn.dataset.flag]; persistUi(); render(); });
    });

    attachVirtualList(document.getElementById("staticList"), rows, (el, d) => {
      el.className = "table-row" + (d.bindingKey === selectedBindingKey ? " selected" : "");
      el.style.position = "absolute"; el.style.left = "0"; el.style.right = "0"; el.style.height = ROW_H + "px";
      const id = d.cueId || d.stateId || "";
      const mat = (d.dto && d.dto.materialKey) || (d.dto && d.dto.playerId) || "—";
      const sel = [d.dto && d.dto.selectorCardDefId, d.dto && d.dto.selectorSkillId].filter(Boolean).join(",") || "—";
      el.innerHTML = `
        <span class="ellipsis">${esc(d.note || "")}</span>
        <span class="ellipsis mono">${esc(id)}</span>
        <span class="ellipsis">${esc(d.module || "")}</span>
        <span class="ellipsis">${esc(d.dto && d.dto.playerId || "")}</span>
        <span class="ellipsis">${esc(mat)} / ${esc(sel)}</span>
        <span>${d.isDisabled ? "否" : "是"}</span>
        <span>${d.isDirty ? "●" : ""}</span>
        <span>${d.isBroken ? "!" : ""}</span>
        <span class="outcome-${esc(latestOutcome(d))}">${esc(latestOutcome(d))}</span>`;
    }, (d) => {
      selectedBindingKey = d.bindingKey;
      selectedChannel = staticChannel;
      selectedSequence = null;
      render();
    });

    renderDetail(document.getElementById("detailHost"));
    wireSplitters(els.panel.querySelector(".static-layout"), "static");
  }

  function renderPersistent() {
    const slots = (state.playMode && state.runtime && state.runtime.activeStateSlots) || [];
    const stateRows = stateDecls();
    els.panel.innerHTML = `
      <div class="state-layout" style="--col-state:${cols.state};--col-right:${cols.right}">
        <section class="col">
          <div class="col-head">
            <h2>活跃持续槽</h2>
            <span class="meta">${slots.length} 槽 · Play ${state.playMode ? "是" : "否"}</span>
          </div>
          <div class="state-list" id="slotList"></div>
        </section>
        <div class="splitter" data-split="state-right"></div>
        <section class="col">
          <div class="col-head"><h2>State 绑定控制</h2></div>
          <div class="detail" id="stateDetail"></div>
        </section>
      </div>`;

    const list = document.getElementById("slotList");
    list.innerHTML = slots.length ? slots.map((s) => `
      <div class="state-row" data-bkey="${esc(s.bindingKey || "")}">
        <div class="title">${esc(s.ownerLabel || "")} · ${esc(s.slot || "")}</div>
        <div class="meta">state=${esc(s.stateId || "")} · player=${esc(s.playerId || "")} · inst=${esc(s.instanceId || "")}</div>
        <div class="actions" style="margin-top:6px">
          <button type="button" data-clear="${esc(s.ownerLabel)}|${esc(s.slot)}">清除此槽</button>
          <button type="button" data-focus="${esc(s.bindingKey || "")}">聚焦 Binding</button>
        </div>
      </div>`).join("") : `<div class="empty">${state.playMode ? "当前无活跃持续槽" : "进入 Play Mode 后显示 activeStateSlots"}</div>`;

    list.querySelectorAll("[data-clear]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const parts = btn.dataset.clear.split("|");
        run("clearStateSlot", { ownerLabel: parts[0], slot: parts[1] });
      });
    });
    list.querySelectorAll("[data-focus]").forEach((btn) => {
      btn.addEventListener("click", () => {
        selectedBindingKey = btn.dataset.focus;
        selectedChannel = "state";
        render();
      });
    });

    const host = document.getElementById("stateDetail");
    const decl = selectedBindingKey ? declByKey(selectedBindingKey) : null;
    if (!decl || declChannel(decl) !== "state") {
      host.innerHTML = `<div class="empty">选择 State 绑定以全局停用或编辑<br/>
        <div class="state-list" style="margin-top:8px">${stateRows.slice(0, 20).map((d) => `
          <div class="state-row" data-pick="${esc(d.bindingKey)}">
            <div class="title">${esc(d.stateId || "")} ${d.isDirty ? "●" : ""}</div>
            <div class="meta">${esc(d.note || "")} · ${d.isDisabled ? "已禁用" : "启用"}</div>
          </div>`).join("") || "无 State 绑定"}</div></div>`;
      host.querySelectorAll("[data-pick]").forEach((row) => {
        row.addEventListener("click", () => {
          selectedBindingKey = row.dataset.pick;
          selectedChannel = "state";
          render();
        });
      });
    } else {
      host.innerHTML = `<div class="section kill-panel"><h3>全局 State 停用</h3>
        <div class="kill-actions">
          <button type="button" class="danger" id="stDisableTemp">临时停用（热应用）</button>
          <button type="button" class="danger primary" id="stDisableSave">保存永久停用并结束投影</button>
        </div>
        <div class="hint-line">停用 State Binding 会结束当前持续投影；单个 owner+slot 可用左侧「清除此槽」。</div></div>`
        + bindingFormHtml(decl);
      document.getElementById("stDisableTemp").onclick = () => disableBindingTemp(decl);
      document.getElementById("stDisableSave").onclick = () => disableBindingPermanent(decl);
      host.querySelectorAll("[data-act]").forEach((btn) => {
        btn.addEventListener("click", () => onDetailAction(btn.dataset.act, null, decl));
      });
    }
    wireSplitters(els.panel.querySelector(".state-layout"), "state");
  }

  function wireSplitters(layout, kind) {
    if (!layout) return;
    layout.querySelectorAll(".splitter").forEach((sp) => {
      sp.addEventListener("mousedown", (ev) => {
        ev.preventDefault();
        const rect = layout.getBoundingClientRect();
        const onMove = (e) => {
          const x = (e.clientX - rect.left) / rect.width;
          if (kind === "capture") {
            if (sp.dataset.split === "left-mid") cols.left = Math.max(0.2, Math.min(0.55, x)) * 2 + "fr";
            else cols.right = Math.max(0.2, Math.min(0.55, 1 - x)) * 2 + "fr";
            layout.style.setProperty("--col-left", cols.left);
            layout.style.setProperty("--col-right", cols.right);
          } else if (kind === "static") {
            cols.static = Math.max(0.4, Math.min(2.2, x * 2.5)) + "fr";
            layout.style.setProperty("--col-static", cols.static);
          } else {
            cols.state = Math.max(0.4, Math.min(2.2, x * 2.5)) + "fr";
            layout.style.setProperty("--col-state", cols.state);
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
    els.dirty.textContent = "Cue脏 " + ((state && state.cueDirtyCount) || 0)
      + " · State脏 " + ((state && state.stateDirtyCount) || 0)
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

  function isEditingDetail() {
    const el = document.activeElement;
    if (!el) return false;
    const tag = (el.tagName || "").toLowerCase();
    if (tag === "input" || tag === "textarea" || tag === "select") return true;
    return !!(el.closest && el.closest("#bindingForm, #detailHost, #stateDetail"));
  }

  function render() {
    if (!state) return;
    if (!mode) mode = state.defaultMode || (state.playMode ? "实时实例" : "静态绑定库");
    els.tabs.forEach((tab) => tab.classList.toggle("active", tab.dataset.mode === mode));
    persistUi();
    renderGlobal();
    if (mode === "实时实例") renderRealtime();
    else if (mode === "静态绑定库") renderStatic();
    else renderPersistent();
  }

  function applyEnvelope(envelope) {
    if (!envelope) return;
    if (envelope.type === "snapshot") {
      if (typeof envelope.revision === "number") revision = envelope.revision;
      state = envelope.payload || null;
      if (state && state.aggregateLevel) aggregateLevel = state.aggregateLevel;
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
      if (isEditingDetail() && mode === "实时实例") {
        renderGlobal();
        const rail = document.getElementById("aggRail");
        if (rail) renderAggRail(rail);
        const active = document.getElementById("activePanel");
        if (active) renderActivePulses(active);
        return;
      }
      render();
    }
  }

  function isTypingTarget(el) {
    if (!el) return false;
    const tag = (el.tagName || "").toLowerCase();
    return tag === "input" || tag === "textarea" || tag === "select" || !!el.isContentEditable;
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
      const ch = declChannel(decl);
      const cmd = ch === "state" ? "replaceStateBinding" : "replaceCueBinding";
      run(cmd, {
        originalBindingKey: decl.bindingKey,
        replacement: Object.assign({}, decl.dto, { enabled: !decl.dto.enabled }),
      });
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
      if (decl) run("saveBinding", { bindingKey: decl.bindingKey, channel: declChannel(decl) });
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
    if (!token) { setConnected(false, "缺少 token"); return; }
    if (ui.mode) mode = ui.mode;
    if (ui.staticChannel) staticChannel = ui.staticChannel;
    els.tabs.forEach((tab) => tab.addEventListener("click", () => { mode = tab.dataset.mode; persistUi(); render(); }));
    window.addEventListener("keydown", onKey);

    try {
      applyEnvelope(await api("/api/snapshot"));
    } catch (err) {
      setConnected(false, "鉴权失败");
      els.error.textContent = String(err);
      return;
    }

    if (location.protocol === "http:" && window.WebSocket) {
      try {
        const ws = new WebSocket((location.protocol === "https:" ? "wss://" : "ws://") + location.host + "/stream");
        ws.addEventListener("open", () => ws.send(JSON.stringify({ type: "hello", token, afterRevision: revision })));
        ws.addEventListener("message", (ev) => { try { applyEnvelope(JSON.parse(ev.data)); } catch { /* ignore */ } });
        ws.addEventListener("close", () => { useLongPoll = true; longPoll(); });
        ws.addEventListener("error", () => { useLongPoll = true; });
        return;
      } catch { useLongPoll = true; }
    } else useLongPoll = true;
    longPoll();
  }

  async function longPoll() {
    if (!useLongPoll) return;
    try { applyEnvelope(await api("/api/events?afterRevision=" + revision + "&timeoutMs=25000")); }
    catch (err) { setConnected(false, "长轮询中断"); els.error.textContent = String(err); }
    setTimeout(longPoll, 250);
  }

  bootstrap();
})();
