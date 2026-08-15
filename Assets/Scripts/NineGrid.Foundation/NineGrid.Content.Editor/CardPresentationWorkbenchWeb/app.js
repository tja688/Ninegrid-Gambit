(() => {
  const UI_KEY = "NineGrid.CardPresentationWorkbench.Ui.v1";
  const token = new URLSearchParams(location.hash.replace(/^#/, "")).get("token") || "";

  const els = {
    badge: document.getElementById("connectionBadge"),
    status: document.getElementById("statusLine"),
    dirty: document.getElementById("dirtyBadge"),
    nav: document.getElementById("navTree"),
    navSearch: document.getElementById("navSearch"),
    main: document.getElementById("mainPane"),
    modalHost: document.getElementById("modalHost"),
    toastHost: document.getElementById("toastHost"),
    btnSaveAll: document.getElementById("btnSaveAll"),
    btnReload: document.getElementById("btnReload"),
    btnExportIndex: document.getElementById("btnExportIndex"),
    btnReconnect: document.getElementById("btnReconnect"),
  };

  let snap = null;
  let revision = 0;
  let ws = null;
  let streamActive = false;
  let suppressClose = false;
  let wsBroken = false;
  let longPollActive = false;
  let reconnectTimer = null;
  let reconnectDelay = 1500;
  let requestSeq = 0;
  let currentPage = null;
  let navFilter = "";

  const ui = loadUi();
  let sel = ui.sel || null;
  const openState = ui.open || {};

  function loadUi() {
    try { return JSON.parse(localStorage.getItem(UI_KEY) || "{}"); } catch { return {}; }
  }
  function persistUi() {
    try { localStorage.setItem(UI_KEY, JSON.stringify({ sel, open: openState })); } catch { /* ignore */ }
  }

  /* ── 基础工具 ─────────────────────────── */

  function esc(s) {
    return String(s == null ? "" : s)
      .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
  }
  function el(tag, cls, text) {
    const node = document.createElement(tag);
    if (cls) node.className = cls;
    if (text != null) node.textContent = text;
    return node;
  }
  function debounce(fn, ms) {
    let timer = null;
    return (...args) => {
      clearTimeout(timer);
      timer = setTimeout(() => fn(...args), ms);
    };
  }
  function toast(message, type) {
    const node = el("div", "toast" + (type ? " " + type : ""), message);
    els.toastHost.appendChild(node);
    setTimeout(() => node.remove(), type === "err" ? 5200 : 2600);
  }
  function copyText(text) {
    navigator.clipboard?.writeText(text).then(
      () => toast("已复制 " + text, "ok"),
      () => toast("复制失败", "err"));
  }

  /* ── 通信 ─────────────────────────────── */

  async function api(path, options = {}) {
    const headers = Object.assign({ Authorization: "Bearer " + token }, options.headers || {});
    const res = await fetch(path, Object.assign({}, options, { headers }));
    const text = await res.text();
    let json = null;
    try { json = text ? JSON.parse(text) : null; } catch { /* ignore */ }
    if (!res.ok) {
      throw new Error((json && (json.error || json.message)) || (path + " -> " + res.status));
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
    if (!result.ok) throw new Error(result.error || (name + " 失败"));
    // revision 只由 applyEnvelope 推进：命令回执不改本地基准，
    // 否则长轮询带回的同版本快照会被丢弃、界面不刷新。
    return result;
  }

  async function run(name, payload) {
    try {
      return await command(name, payload);
    } catch (err) {
      toast(String(err.message || err), "err");
      return null;
    }
  }

  function setConnected(ok, label) {
    els.badge.className = "badge " + (ok ? "ok" : "bad");
    els.badge.textContent = label;
    els.btnReconnect.classList.toggle("hidden", ok);
  }

  function applyEnvelope(envelope) {
    if (!envelope || (envelope.type !== "snapshot" && envelope.type !== "delta")) return;
    if (typeof envelope.revision === "number") revision = envelope.revision;
    snap = envelope.payload || null;
    setConnected(true, "已连接 · r" + revision);
    onSnapshot();
  }

  function startStream() {
    if (streamActive) return;
    if (wsBroken || !window.WebSocket) {
      longPoll();
      return;
    }
    streamActive = true;
    let gotMessage = false;
    try {
      ws = new WebSocket("ws://" + location.host + "/stream");
      ws.addEventListener("open", () => {
        reconnectDelay = 1500;
        ws.send(JSON.stringify({ type: "hello", token, afterRevision: revision }));
      });
      ws.addEventListener("message", (ev) => {
        try {
          const parsed = JSON.parse(ev.data);
          if (parsed.type === "snapshot" || parsed.type === "delta") {
            gotMessage = true;
            applyEnvelope(parsed);
          }
        } catch { /* ignore */ }
      });
      ws.addEventListener("close", () => {
        ws = null;
        streamActive = false;
        if (suppressClose) { suppressClose = false; return; }
        if (!gotMessage) {
          // 编辑器侧 HttpListener 不支持 WS 升级时回退长轮询。
          wsBroken = true;
          longPoll();
          return;
        }
        setConnected(false, "已断开");
        scheduleReconnect();
      });
    } catch {
      streamActive = false;
      wsBroken = true;
      longPoll();
    }
  }

  async function longPoll() {
    if (longPollActive) return;
    longPollActive = true;
    setConnected(true, "已连接 · r" + revision);
    while (longPollActive) {
      try {
        const envelope = await api("/api/events?afterRevision=" + revision + "&timeoutMs=25000");
        if (!longPollActive) return;
        if (envelope && (envelope.type === "snapshot" || envelope.type === "delta")
          && envelope.revision > revision) {
          applyEnvelope(envelope);
        }
      } catch {
        longPollActive = false;
        setConnected(false, "已断开");
        scheduleReconnect();
        return;
      }
    }
  }

  function scheduleReconnect() {
    if (reconnectTimer != null) return;
    setConnected(false, "重连中…");
    reconnectTimer = setTimeout(async () => {
      reconnectTimer = null;
      await connect();
    }, reconnectDelay);
    reconnectDelay = Math.min(reconnectDelay * 1.6, 10000);
  }

  async function connect() {
    if (!token) {
      setConnected(false, "缺少 token");
      els.main.innerHTML = "";
      const box = el("div", "empty-state");
      box.appendChild(el("div", "mark"));
      box.appendChild(el("h2", null, "缺少访问令牌"));
      box.appendChild(el("div", null, "请从 Unity 菜单「NineGrid/表现层配置工作台（网页）」打开本页。"));
      els.main.appendChild(box);
      return;
    }
    try {
      const envelope = await api("/api/snapshot");
      applyEnvelope(envelope);
      if (ws) { suppressClose = true; try { ws.close(); } catch { /* ignore */ } ws = null; streamActive = false; }
      longPollActive = false;
      startStream();
    } catch {
      scheduleReconnect();
    }
  }

  /* ── 图片加载 ─────────────────────────── */

  const blobCache = new Map();
  let inflight = 0;
  const imageQueue = [];

  function pumpImageQueue() {
    while (inflight < 6 && imageQueue.length > 0) {
      const job = imageQueue.shift();
      inflight++;
      job().finally(() => { inflight--; pumpImageQueue(); });
    }
  }

  function fetchBlobUrl(url, cache = true) {
    if (cache && blobCache.has(url)) return blobCache.get(url);
    const promise = new Promise((resolve, reject) => {
      imageQueue.push(async () => {
        try {
          const res = await fetch(url, { headers: { Authorization: "Bearer " + token } });
          if (!res.ok) throw new Error(String(res.status));
          resolve(URL.createObjectURL(await res.blob()));
        } catch (err) {
          if (cache) blobCache.delete(url);
          reject(err);
        }
      });
      pumpImageQueue();
    });
    if (cache) blobCache.set(url, promise);
    return promise;
  }

  function spriteThumbUrl(ref) {
    return "/api/asset?kind=sprite&path=" + encodeURIComponent(ref);
  }

  async function setImage(img, url, cache = true) {
    try {
      const objUrl = await fetchBlobUrl(url, cache);
      if (!cache && img.dataset.blobUrl) URL.revokeObjectURL(img.dataset.blobUrl);
      if (!cache) img.dataset.blobUrl = objUrl;
      img.src = objUrl;
      img.style.visibility = "visible";
      return true;
    } catch {
      img.style.visibility = "hidden";
      return false;
    }
  }

  /* ── 更新队列（按条目去抖合并） ─────────── */

  const patchTimers = new Map();
  const patchBodies = new Map();
  const lastEditAt = new Map();

  function markEdited(key) { lastEditAt.set(key, Date.now()); }
  function recentlyEdited(key, windowMs = 900) {
    return Date.now() - (lastEditAt.get(key) || 0) < windowMs;
  }

  function queuePatch(commandName, key, identity, patch, onDone, delay = 320) {
    markEdited(key);
    const existing = patchBodies.get(key) || {};
    for (const [k, v] of Object.entries(patch)) {
      if (v !== null && typeof v === "object" && !Array.isArray(v)
        && existing[k] && typeof existing[k] === "object" && !Array.isArray(existing[k])) {
        Object.assign(existing[k], v);
      } else {
        existing[k] = v;
      }
    }
    patchBodies.set(key, existing);
    clearTimeout(patchTimers.get(key));
    patchTimers.set(key, setTimeout(async () => {
      const body = patchBodies.get(key);
      patchBodies.delete(key);
      patchTimers.delete(key);
      const result = await run(commandName, Object.assign({}, identity, body));
      if (result && onDone) onDone(result);
    }, delay));
  }

  /* ── 快照访问 ─────────────────────────── */

  function faceById(id) { return (snap?.faces || []).find((f) => f.contentId === id) || null; }
  function deckById(id) { return (snap?.decks || []).find((d) => d.contentId === id) || null; }
  function templateById(id) { return (snap?.templates || []).find((t) => t.id === id) || null; }
  function vfxById(id) { return (snap?.visualEffects || []).find((v) => v.id === id) || null; }

  const KIND_LABELS = {
    Monster: "怪物", HelpCard: "道具卡", PlayerCard: "道具卡", Item: "道具卡",
    Relic: "遗物", Skill: "技能", Avatar: "玩家", Trap: "机关",
    Room: "房间", ChoiceOption: "房间选项", Deck: "卡组",
  };
  const RARITY_LABELS = { White: "白", Blue: "蓝", Gold: "金", Red: "红", None: "无" };

  function kindLabel(kind) { return KIND_LABELS[kind] || kind || "?"; }
  function isMonsterKind(kind) { return /^monster$/i.test(kind || ""); }
  function isCombatKind(kind) { return /^(avatar|monster|trap)$/i.test(kind || ""); }
  function isRoomKind(kind) { return /^room$/i.test(kind || ""); }
  function isChoiceKind(kind) { return /^choiceoption$/i.test(kind || ""); }
  function isItemLikeKind(kind) { return /^(helpcard|playercard|item)$/i.test(kind || ""); }

  function containerCategoryForKind(kind) {
    if (isMonsterKind(kind)) return "MonsterSkill";
    if (/^trap$/i.test(kind || "")) return "TrapSkill";
    if (/^relic$/i.test(kind || "")) return "Relic";
    if (isItemLikeKind(kind)) return "Item";
    return "";
  }

  function deckLabel(deckId) {
    if (!deckId) return "(未分组)";
    const choice = (snap?.deckChoices || []).find((c) => c.deckId === deckId);
    return choice ? choice.label : deckId;
  }

  /* ── 快照到达 ─────────────────────────── */

  function onSnapshot() {
    if (!snap) return;
    els.status.textContent = snap.error ? snap.error : (snap.status || "");
    els.status.style.color = snap.error ? "#ffb19a" : "";
    const dirty = snap.dirtyCount || 0;
    els.dirty.classList.toggle("hidden", dirty === 0);
    els.dirty.textContent = "未保存 " + dirty;
    renderSidebar();
    renderMain(false);
  }

  /* ── 侧栏 ─────────────────────────────── */

  function isOpen(key, fallback = false) {
    return openState[key] != null ? !!openState[key] : fallback;
  }
  function setOpen(key, value) {
    openState[key] = value;
    persistUi();
  }

  function select(kind, id) {
    sel = { kind, id };
    persistUi();
    renderSidebar();
    renderMain(true);
    if (kind === "face" && id) run("openFace", { contentId: id });
  }

  function matches(...texts) {
    if (!navFilter) return true;
    const needle = navFilter.toLowerCase();
    return texts.some((t) => (t || "").toLowerCase().includes(needle));
  }

  function navItem(kind, id, label, sub, dirty) {
    const item = el("div", "nav-item");
    if (sel && sel.kind === kind && sel.id === id) item.classList.add("selected");
    if (dirty) item.appendChild(el("span", "dot"));
    const labelEl = el("span", "label", label);
    item.appendChild(labelEl);
    if (sub) item.appendChild(el("span", "sub", sub));
    item.addEventListener("click", () => select(kind, id));
    return item;
  }

  function navSection(key, title, count, buildBody, defaultOpen = false) {
    const section = el("div", "nav-section");
    const forced = !!navFilter;
    if (forced || isOpen(key, defaultOpen)) section.classList.add("open");
    const head = el("div", "nav-section-head");
    head.appendChild(el("span", "chev", "▶"));
    head.appendChild(el("span", null, title));
    head.appendChild(el("span", "count", String(count)));
    head.addEventListener("click", () => {
      const next = !section.classList.contains("open");
      section.classList.toggle("open", next);
      setOpen(key, next);
    });
    section.appendChild(head);
    const body = el("div", "nav-section-body");
    buildBody(body);
    section.appendChild(body);
    return section;
  }

  function navGroup(key, title, count, buildBody) {
    const group = el("div", "nav-group");
    if (navFilter || isOpen(key)) group.classList.add("open");
    const head = el("div", "nav-group-head");
    head.appendChild(el("span", "chev", "▶"));
    head.appendChild(el("span", null, title));
    head.appendChild(el("span", "count", String(count)));
    head.addEventListener("click", () => {
      const next = !group.classList.contains("open");
      group.classList.toggle("open", next);
      setOpen(key, next);
    });
    group.appendChild(head);
    const body = el("div", "nav-group-body");
    buildBody(body);
    group.appendChild(body);
    return group;
  }

  function faceGroups() {
    const groups = new Map();
    for (const face of snap.faces || []) {
      let key;
      let title;
      if (isRoomKind(face.kind)) { key = "__rooms__"; title = "房间图标"; }
      else if (isChoiceKind(face.kind)) { key = "__choices__"; title = "房间选项"; }
      else { key = face.deckId || "__ungrouped__"; title = deckLabel(face.deckId); }
      if (!groups.has(key)) groups.set(key, { key, title, faces: [] });
      groups.get(key).faces.push(face);
    }
    const rank = (g) => g.key === "__rooms__" ? 0 : g.key === "__choices__" ? 1 : g.key === "__ungrouped__" ? 3 : 2;
    return [...groups.values()].sort((a, b) => rank(a) - rank(b) || a.title.localeCompare(b.title, "zh-CN"));
  }

  function faceTitle(face) {
    const name = (face.displayName || "").trim();
    const slot = (face.designSlotName || "").trim();
    if (slot && slot !== name) return name ? name + " · " + slot : slot;
    return name || face.contentId;
  }

  function renderSidebar() {
    if (!snap) return;
    const scrollTop = els.nav.scrollTop;
    els.nav.innerHTML = "";

    els.nav.appendChild(navSection("sec:faces", "卡面", (snap.faces || []).length, (body) => {
      for (const group of faceGroups()) {
        const visible = group.faces.filter((f) => matches(faceTitle(f), f.contentId));
        if (navFilter && visible.length === 0) continue;
        body.appendChild(navGroup("faceGroup:" + group.key, group.title, group.faces.length, (groupBody) => {
          for (const face of (navFilter ? visible : group.faces)) {
            groupBody.appendChild(navItem("face", face.contentId, faceTitle(face), face.contentId, face.isDirty));
          }
        }));
      }
    }, true));

    els.nav.appendChild(navSection("sec:glossary", "词条", (snap.glossary?.entries || []).length, (body) => {
      if (!navFilter || matches("词条")) {
        body.appendChild(navItem("glossary", "glossary", "词条编辑", "名字 · 图标 · 颜色", snap.glossary?.dirty));
      }
    }));

    els.nav.appendChild(navSection("sec:templates", "效果池", (snap.templates || []).length, (body) => {
      const byCategory = new Map();
      for (const template of snap.templates || []) {
        if (!byCategory.has(template.categoryTitle)) byCategory.set(template.categoryTitle, []);
        byCategory.get(template.categoryTitle).push(template);
      }
      const order = ["道具效果", "遗物效果", "怪物技能效果", "机关技能效果", "其他效果"];
      const keys = [...byCategory.keys()].sort((a, b) =>
        (order.indexOf(a) + 99) - (order.indexOf(b) + 99) || a.localeCompare(b, "zh-CN"));
      for (const key of keys) {
        const rows = byCategory.get(key);
        const visible = rows.filter((t) => matches(t.paramText, t.id));
        if (navFilter && visible.length === 0) continue;
        body.appendChild(navGroup("tplGroup:" + key, key, rows.length, (groupBody) => {
          for (const template of (navFilter ? visible : rows)) {
            const label = template.paramText || template.id;
            groupBody.appendChild(navItem(
              "template", template.id,
              label.length > 30 ? label.slice(0, 30) + "…" : label,
              null, template.isDirty));
          }
        }));
      }
    }));

    els.nav.appendChild(navSection("sec:vfx", "特效库", (snap.visualEffects || []).length, (body) => {
      const byCategory = new Map();
      for (const vfx of snap.visualEffects || []) {
        if (!byCategory.has(vfx.categoryTitle)) byCategory.set(vfx.categoryTitle, new Map());
        const variants = byCategory.get(vfx.categoryTitle);
        if (!variants.has(vfx.variantId)) variants.set(vfx.variantId, []);
        variants.get(vfx.variantId).push(vfx);
      }
      for (const [categoryTitle, variants] of byCategory) {
        const variantIds = [...variants.keys()].sort((a, b) => a.localeCompare(b));
        const visibleIds = variantIds.filter((v) => matches(v, categoryTitle));
        if (navFilter && visibleIds.length === 0) continue;
        body.appendChild(navGroup("vfxGroup:" + categoryTitle, categoryTitle, variantIds.length, (groupBody) => {
          for (const variantId of (navFilter ? visibleIds : variantIds)) {
            const leaves = variants.get(variantId);
            const preferred = preferredVfxLeaf(leaves);
            const selected = sel && sel.kind === "vfx" && leaves.some((l) => l.id === sel.id);
            const item = navItem("vfx", preferred.id, variantId, null, leaves.some((l) => l.isDirty));
            if (selected) item.classList.add("selected");
            groupBody.appendChild(item);
          }
        }));
      }
    }));

    els.nav.appendChild(navSection("sec:decks", "卡组·卡背", (snap.decks || []).length, (body) => {
      for (const deck of snap.decks || []) {
        if (!matches(deck.displayName, deck.contentId)) continue;
        body.appendChild(navItem("deck", deck.contentId, deck.displayName || deck.contentId, deck.contentId, deck.isDirty));
      }
      const addBtn = el("button", "small nav-add", "＋ 新建卡组");
      addBtn.addEventListener("click", openCreateDeckDialog);
      body.appendChild(addBtn);
    }));

    els.nav.scrollTop = scrollTop;
  }

  function preferredVfxLeaf(leaves) {
    const sorted = [...leaves].sort((a, b) => {
      const aLarge = /^large$/i.test(a.size) ? 0 : 1;
      const bLarge = /^large$/i.test(b.size) ? 0 : 1;
      return aLarge - bLarge || a.id.localeCompare(b.id);
    });
    return sorted[0];
  }

  /* ── 主区渲染骨架 ─────────────────────── */

  function renderMain(force) {
    if (!snap) return;
    if (sel) {
      const exists =
        (sel.kind === "face" && faceById(sel.id))
        || (sel.kind === "deck" && deckById(sel.id))
        || (sel.kind === "template" && templateById(sel.id))
        || (sel.kind === "vfx" && vfxById(sel.id))
        || sel.kind === "glossary";
      if (!exists) sel = null;
    }

    if (!sel) {
      currentPage?.dispose?.();
      currentPage = null;
      els.main.innerHTML = "";
      const box = el("div", "empty-state");
      box.appendChild(el("div", "mark"));
      box.appendChild(el("h2", null, "表现层配置"));
      box.appendChild(el("div", null, "从左侧选择卡面、词条、效果模板、特效或卡组开始编辑。"));
      els.main.appendChild(box);
      return;
    }

    if (!force && currentPage && currentPage.kind === sel.kind && currentPage.id === sel.id) {
      currentPage.sync && currentPage.sync();
      return;
    }

    currentPage?.dispose?.();
    els.main.innerHTML = "";
    els.main.scrollTop = 0;
    switch (sel.kind) {
      case "face": currentPage = buildFacePage(sel.id); break;
      case "deck": currentPage = buildDeckPage(sel.id); break;
      case "template": currentPage = buildTemplatePage(sel.id); break;
      case "vfx": currentPage = buildVfxPage(sel.id); break;
      case "glossary": currentPage = buildGlossaryPage(); break;
      default: currentPage = null;
    }
  }

  /* ── 表单控件工厂 ─────────────────────── */

  function field(labelText, control, options = {}) {
    const wrap = el("div", "field" + (options.span === 2 ? " span2" : options.span === "full" ? " full" : ""));
    if (labelText) wrap.appendChild(el("label", null, labelText));
    wrap.appendChild(control);
    return wrap;
  }

  function textInput(value, onChange, options = {}) {
    const input = el("input");
    input.type = "text";
    if (options.mono) input.classList.add("mono");
    if (options.placeholder) input.placeholder = options.placeholder;
    input.value = value ?? "";
    input.addEventListener("input", () => onChange(input.value));
    return input;
  }

  function numberInput(value, onChange, options = {}) {
    const input = el("input");
    input.type = "number";
    if (options.step != null) input.step = String(options.step);
    if (options.min != null) input.min = String(options.min);
    if (options.width) input.style.width = options.width;
    input.value = value ?? 0;
    input.addEventListener("input", () => {
      const parsed = options.float ? parseFloat(input.value) : parseInt(input.value, 10);
      if (!Number.isNaN(parsed)) onChange(parsed);
    });
    return input;
  }

  function selectInput(options, value, onChange) {
    const select = el("select");
    for (const option of options) {
      const opt = el("option", null, option.label);
      opt.value = option.value;
      select.appendChild(opt);
    }
    select.value = value ?? "";
    if (select.selectedIndex < 0 && options.length > 0) select.selectedIndex = 0;
    select.addEventListener("change", () => onChange(select.value));
    return select;
  }

  function checkboxInput(labelText, value, onChange) {
    const wrap = el("label", "field-inline");
    const input = el("input");
    input.type = "checkbox";
    input.checked = !!value;
    input.addEventListener("change", () => onChange(input.checked));
    wrap.appendChild(input);
    wrap.appendChild(el("span", null, labelText));
    wrap._checkbox = input;
    return wrap;
  }

  function textArea(value, onChange, options = {}) {
    const area = el("textarea");
    if (options.mono) area.classList.add("mono");
    if (options.minHeight) area.style.minHeight = options.minHeight;
    area.value = value ?? "";
    area.addEventListener("input", () => onChange(area.value));
    return area;
  }

  function sectionCard(title, hint, buildBody, options = {}) {
    const card = el("div", "card" + (options.collapsible ? " collapsible" : ""));
    if (options.collapsible && options.open) card.classList.add("open");
    const head = el("div", "card-head");
    if (options.collapsible) head.appendChild(el("span", "chev", "▶"));
    head.appendChild(el("span", null, title));
    if (hint) head.appendChild(el("span", "hint", hint));
    const tail = el("div", "tail");
    head.appendChild(tail);
    card.appendChild(head);
    const body = el("div", "card-body");
    buildBody(body, tail);
    card.appendChild(body);
    if (options.collapsible) {
      head.addEventListener("click", (ev) => {
        if (ev.target.closest("button, input, select")) return;
        card.classList.toggle("open");
      });
    }
    return card;
  }

  function syncValue(input, value) {
    if (document.activeElement === input) return;
    const text = value == null ? "" : String(value);
    if (input.value !== text) input.value = text;
  }

  /* ── 搜索下拉 ─────────────────────────── */

  function combo(choices, value, onPick) {
    const wrap = el("div", "combo");
    const btn = el("button", "combo-btn");
    const textSpan = el("span", "text");
    btn.appendChild(textSpan);
    btn.appendChild(el("span", "arrow", "▼"));
    wrap.appendChild(btn);

    function labelFor(v) {
      const found = choices.find((c) => c.value === v);
      return found ? found.label : (v || "（未选择）");
    }
    textSpan.textContent = labelFor(value);

    btn.addEventListener("click", () => {
      const existing = wrap.querySelector(".combo-pop");
      if (existing) { existing.remove(); return; }
      const pop = el("div", "combo-pop");
      const search = el("input");
      search.type = "text";
      search.placeholder = "搜索…";
      pop.appendChild(search);
      const optionsHost = el("div", "options");
      pop.appendChild(optionsHost);

      function renderOptions() {
        optionsHost.innerHTML = "";
        const needle = search.value.trim().toLowerCase();
        let shown = 0;
        for (const choice of choices) {
          if (needle && !(choice.label + " " + choice.value).toLowerCase().includes(needle)) continue;
          if (++shown > 120) break;
          const option = el("div", "combo-option");
          option.appendChild(el("div", null, choice.label));
          if (choice.sub) option.appendChild(el("div", "sub", choice.sub));
          option.addEventListener("click", () => {
            pop.remove();
            textSpan.textContent = choice.label;
            onPick(choice.value);
          });
          optionsHost.appendChild(option);
        }
        if (shown === 0) optionsHost.appendChild(el("div", "combo-option", "（无匹配）"));
      }
      search.addEventListener("input", renderOptions);
      renderOptions();
      wrap.appendChild(pop);
      search.focus();

      const dismiss = (ev) => {
        if (!wrap.contains(ev.target)) {
          pop.remove();
          document.removeEventListener("mousedown", dismiss);
        }
      };
      document.addEventListener("mousedown", dismiss);
    });

    wrap._setLabel = (v) => { textSpan.textContent = labelFor(v); };
    return wrap;
  }

  /* ── 精灵槽 tile ──────────────────────── */

  function spriteTile(name, getPath, onPick) {
    const tile = el("div", "sprite-tile");
    const thumb = el("div", "thumb");
    tile.appendChild(thumb);
    tile.appendChild(el("div", "name", name));
    const pathEl = el("div", "path");
    tile.appendChild(pathEl);

    function refresh() {
      const path = getPath() || "";
      pathEl.textContent = path || "—";
      pathEl.title = path;
      thumb.innerHTML = "";
      if (!path) {
        thumb.appendChild(el("span", "none", "未设置"));
        return;
      }
      const img = el("img");
      thumb.appendChild(img);
      setImage(img, spriteThumbUrl(path));
    }
    tile.addEventListener("click", () => {
      openSpritePicker({
        title: name,
        current: getPath() || "",
        onPick: (ref) => { onPick(ref); refresh(); },
      });
    });
    refresh();
    tile._refresh = refresh;
    return tile;
  }

  /* ── 精灵选择器 ───────────────────────── */

  function closeModal() {
    els.modalHost.classList.add("hidden");
    els.modalHost.innerHTML = "";
  }

  function openModal(node) {
    els.modalHost.innerHTML = "";
    els.modalHost.appendChild(node);
    els.modalHost.classList.remove("hidden");
    els.modalHost.onmousedown = (ev) => { if (ev.target === els.modalHost) closeModal(); };
  }

  function openSpritePicker({ title, current, onPick }) {
    const modal = el("div", "modal");
    const head = el("div", "modal-head");
    head.appendChild(el("span", null, "选择精灵 · " + title));
    const closeBtn = el("button", "ghost close", "✕");
    closeBtn.addEventListener("click", closeModal);
    head.appendChild(closeBtn);
    modal.appendChild(head);

    const body = el("div", "modal-body");
    const toolbar = el("div", "picker-toolbar");
    const rootSelect = selectInput([
      { value: snap.contentArtRoot || "Assets/Resources/ContentArt", label: "ContentArt 素材根" },
      { value: "Assets", label: "整个 Assets" },
    ], snap.contentArtRoot || "Assets/Resources/ContentArt", () => refresh());
    const search = el("input");
    search.type = "text";
    search.placeholder = "按名字搜索…";
    toolbar.appendChild(rootSelect);
    toolbar.appendChild(search);
    body.appendChild(toolbar);

    const manualRow = el("div", "picker-toolbar");
    const manual = el("input");
    manual.type = "text";
    manual.classList.add("mono");
    manual.placeholder = "或直接粘贴资产路径（可含 #精灵名）";
    manual.value = current || "";
    const useBtn = el("button", null, "使用路径");
    useBtn.addEventListener("click", () => { onPick(manual.value.trim()); closeModal(); });
    const clearBtn = el("button", "danger", "清空槽位");
    clearBtn.addEventListener("click", () => { onPick(""); closeModal(); });
    manualRow.appendChild(manual);
    manualRow.appendChild(useBtn);
    manualRow.appendChild(clearBtn);
    body.appendChild(manualRow);

    const note = el("div", "picker-note", "");
    body.appendChild(note);
    const grid = el("div", "picker-grid");
    body.appendChild(grid);
    modal.appendChild(body);
    openModal(modal);
    search.focus();

    let generation = 0;
    async function refresh() {
      const gen = ++generation;
      note.textContent = "搜索中…";
      grid.innerHTML = "";
      const result = await run("listSprites", { query: search.value.trim(), root: rootSelect.value });
      if (!result || gen !== generation) return;
      const sprites = result.payload?.sprites || [];
      note.textContent = sprites.length === 0
        ? "无匹配精灵"
        : "共 " + sprites.length + " 个" + (result.payload.truncated ? "（已截断，请细化搜索词）" : "");
      for (const sprite of sprites.slice(0, 200)) {
        const cell = el("div", "picker-cell");
        const thumb = el("div", "thumb");
        const img = el("img");
        thumb.appendChild(img);
        cell.appendChild(thumb);
        const nameEl = el("div", "name", sprite.name);
        nameEl.title = sprite.ref;
        cell.appendChild(nameEl);
        cell.addEventListener("click", () => { onPick(sprite.ref); closeModal(); });
        grid.appendChild(cell);
        setImage(img, spriteThumbUrl(sprite.ref));
      }
    }
    search.addEventListener("input", debounce(refresh, 320));
    refresh();
  }

  function openCreateDeckDialog() {
    const modal = el("div", "modal narrow");
    const head = el("div", "modal-head");
    head.appendChild(el("span", null, "新建卡组"));
    const closeBtn = el("button", "ghost close", "✕");
    closeBtn.addEventListener("click", closeModal);
    head.appendChild(closeBtn);
    modal.appendChild(head);

    const body = el("div", "modal-body");
    let deckId = "";
    let deckName = "";
    body.appendChild(field("deckId（自动补 deck. 前缀）", textInput("", (v) => { deckId = v; }, { mono: true })));
    body.appendChild(field("显示名", textInput("", (v) => { deckName = v; })));
    modal.appendChild(body);

    const foot = el("div", "modal-foot");
    const cancel = el("button", null, "取消");
    cancel.addEventListener("click", closeModal);
    const create = el("button", "primary", "创建");
    create.addEventListener("click", async () => {
      const result = await run("createDeck", { contentId: deckId, displayName: deckName });
      if (result) {
        closeModal();
        toast("已创建卡组", "ok");
      }
    });
    foot.appendChild(cancel);
    foot.appendChild(create);
    modal.appendChild(foot);
    openModal(modal);
  }

  function confirmDialog(message, onConfirm) {
    const modal = el("div", "modal narrow");
    const head = el("div", "modal-head");
    head.appendChild(el("span", null, "确认"));
    modal.appendChild(head);
    const body = el("div", "modal-body");
    body.appendChild(el("div", null, message));
    modal.appendChild(body);
    const foot = el("div", "modal-foot");
    const cancel = el("button", null, "取消");
    cancel.addEventListener("click", closeModal);
    const ok = el("button", "danger", "确定");
    ok.addEventListener("click", () => { closeModal(); onConfirm(); });
    foot.appendChild(cancel);
    foot.appendChild(ok);
    modal.appendChild(foot);
    openModal(modal);
  }

  /* ── 卡面预览组件 ─────────────────────── */

  function facePreviewPanel(contentId, opts = {}) {
    let faceUp = !opts.startBack;
    let loadSeq = 0;
    const card = el("div", "card");
    const head = el("div", "card-head");
    head.appendChild(el("span", null, "预览"));
    card.appendChild(head);

    const stage = el("div", "preview-stage");
    const flip = el("div", "preview-flip");
    if (!faceUp) flip.classList.add("back");
    const frontFace = el("div", "preview-face");
    const frontImg = el("img");
    frontFace.appendChild(frontImg);
    const backFace = el("div", "preview-face back-side");
    const backImg = el("img");
    backFace.appendChild(backImg);
    flip.appendChild(frontFace);
    flip.appendChild(backFace);
    stage.appendChild(flip);
    stage.appendChild(el("div", "preview-loading", "渲染中…"));
    card.appendChild(stage);

    const toolbar = el("div", "preview-toolbar");
    const flipBtn = el("button", null, faceUp ? "翻到背面" : "翻到正面");
    flipBtn.addEventListener("click", () => {
      faceUp = !faceUp;
      flip.classList.toggle("back", !faceUp);
      flipBtn.textContent = faceUp ? "翻到背面" : "翻到正面";
    });
    const refreshBtn = el("button", null, "刷新");
    refreshBtn.addEventListener("click", () => reload());
    const note = el("span", "preview-note");
    toolbar.appendChild(flipBtn);
    toolbar.appendChild(refreshBtn);
    toolbar.appendChild(el("span", "spacer"));
    toolbar.appendChild(note);
    card.appendChild(toolbar);

    async function reload() {
      const seq = ++loadSeq;
      stage.classList.add("loading");
      const bust = "&t=" + Date.now();
      const base = "/api/asset?kind=facePreview&contentId=" + encodeURIComponent(contentId) + "&w=520&h=640";
      const okFront = await setImage(frontImg, base + "&face=front" + bust, false);
      const okBack = await setImage(backImg, base + "&face=back" + bust, false);
      if (seq !== loadSeq) return;
      stage.classList.remove("loading");
      note.textContent = okFront || okBack ? "" : "预览不可用";
    }
    const reloadDebounced = debounce(reload, 420);
    reload();
    card._reload = reloadDebounced;
    card._setNote = (t) => { note.textContent = t; };
    return card;
  }

  /* ── 卡面页 ───────────────────────────── */

  function buildFacePage(contentId) {
    const face = faceById(contentId);
    if (!face) return null;
    const key = "face:" + contentId;
    let preview = null;

    function patch(body, refreshPreview = true) {
      queuePatch("updateFace", key, { contentId }, body, (result) => {
        const payload = result.payload || {};
        if (payload.description != null) {
          face.description = payload.description;
          face.descriptionLocked = !!payload.descriptionLocked;
          syncDescription(face);
        }
        if (refreshPreview && preview) preview._reload();
      });
    }

    const pageHead = el("div", "page-head");
    const title = el("h1", null, faceTitle(face));
    pageHead.appendChild(title);
    const chips = el("div", "chips");
    chips.appendChild(el("span", "chip mono", face.contentId));
    chips.appendChild(el("span", "chip", kindLabel(face.kind)));
    const rarityText = RARITY_LABELS[face.rarity] || face.rarity || "无";
    if (rarityText !== "无") chips.appendChild(el("span", "chip accent", "稀有度 " + rarityText));
    const dirtyChip = el("span", "chip warn hidden", "未保存");
    chips.appendChild(dirtyChip);
    pageHead.appendChild(chips);
    const actions = el("div", "actions");
    const saveBtn = el("button", "primary", "保存本卡");
    saveBtn.addEventListener("click", async () => {
      const result = await run("saveFace", { contentId });
      if (result) toast("已保存 " + contentId, "ok");
    });
    actions.appendChild(saveBtn);
    pageHead.appendChild(actions);
    els.main.appendChild(pageHead);
    els.main.appendChild(el("p", "page-sub", "改动实时暂存于编辑会话，点「保存」写入 JSON（Authoring + StreamingAssets）。"));

    const grid = el("div", "page-grid");
    const leftCol = el("div", "col");
    const rightCol = el("div", "col");
    grid.appendChild(leftCol);
    grid.appendChild(rightCol);
    els.main.appendChild(grid);

    /* 左列：预览 + 动画试播 */
    preview = facePreviewPanel(contentId);
    leftCol.appendChild(preview);
    updateBackFollowNote();

    function updateBackFollowNote() {
      const local = faceById(contentId);
      if (!local || !preview) return;
      const own = local.sprites.backBorder || local.sprites.backShirt || local.sprites.backLogo;
      if (local.deckId) {
        preview._setNote("卡背跟随 " + deckLabel(local.deckId) + (own ? "（覆盖自有槽）" : ""));
      } else {
        preview._setNote(own ? "卡背：自有背图" : "卡背：模板兜底");
      }
    }

    const animCard = buildAnimPlayerCard(contentId);
    leftCol.appendChild(animCard);

    /* 右列：表单 */
    const inputs = {};

    rightCol.appendChild(sectionCard("基本信息", null, (body) => {
      const form = el("div", "form-grid");
      inputs.displayName = textInput(face.displayName, (v) => { face.displayName = v; title.textContent = faceTitle(face); patch({ displayName: v }); });
      form.appendChild(field("显示名", inputs.displayName));

      const deckOptions = [{ value: "", label: "(未分组)" }]
        .concat((snap.deckChoices || []).map((c) => ({ value: c.deckId, label: c.label })));
      if (face.deckId && !deckOptions.some((o) => o.value === face.deckId)) {
        deckOptions.splice(1, 0, { value: face.deckId, label: face.deckId + "（缺失卡组）" });
      }
      inputs.deckId = selectInput(deckOptions, face.deckId, (v) => {
        face.deckId = v;
        patch({ deckId: v });
        updateBackFollowNote();
      });
      form.appendChild(field("所属卡组", inputs.deckId));

      if (!isRoomKind(face.kind)) {
        inputs.gold = numberInput(face.gold, (v) => { face.gold = v; patch({ gold: v }, false); });
        form.appendChild(field(isChoiceKind(face.kind) ? "金币（展示价）" : "金币", inputs.gold));
      }

      if (isMonsterKind(face.kind)) {
        inputs.designSlotName = textInput(face.designSlotName, (v) => { face.designSlotName = v; patch({ designSlotName: v }, false); });
        form.appendChild(field("策划槽位名", inputs.designSlotName));

        inputs.sequence = selectInput([
          { value: "0", label: "未设置" },
          { value: "1", label: "1" }, { value: "2", label: "2" }, { value: "3", label: "3" },
          { value: "4", label: "4" }, { value: "5", label: "5（层主）" },
        ], String(face.sequence || 0), (v) => { face.sequence = parseInt(v, 10); patch({ sequence: face.sequence }, false); });
        form.appendChild(field("发牌序列", inputs.sequence));

        inputs.level = textInput(face.level, (v) => { face.level = v; patch({ level: v }, false); });
        form.appendChild(field("等级（普通 / 层主）", inputs.level));

        inputs.isReserve = checkboxInput("储备怪（不参与遭遇抽选）", face.isReserve, (v) => { face.isReserve = v; patch({ isReserve: v }, false); });
        form.appendChild(field("", inputs.isReserve));
      }

      body.appendChild(form);

      const introField = el("div", "field full");
      introField.appendChild(el("label", null, "卡面介绍（右键详述用，可含 {param} 与 [词条]）"));
      inputs.faceIntro = textArea(face.faceIntro, (v) => { face.faceIntro = v; patch({ faceIntro: v }, false); }, { minHeight: "54px" });
      introField.appendChild(inputs.faceIntro);
      introField.style.marginTop = "10px";
      body.appendChild(introField);
    }));

    if (isCombatKind(face.kind)) {
      rightCol.appendChild(sectionCard("数值", null, (body) => {
        const form = el("div", "form-grid");
        inputs.attack = numberInput(face.stats.attack, (v) => { face.stats.attack = v; patch({ stats: { attack: v } }); });
        inputs.armor = numberInput(face.stats.armor, (v) => { face.stats.armor = v; patch({ stats: { armor: v } }); });
        inputs.hp = numberInput(face.stats.hp, (v) => { face.stats.hp = v; patch({ stats: { hp: v } }); });
        form.appendChild(field("攻击", inputs.attack));
        form.appendChild(field("护甲", inputs.armor));
        form.appendChild(field("生命", inputs.hp));

        if (isMonsterKind(face.kind)) {
          inputs.attackPattern = selectInput(
            (snap.attackPatterns || []).map((p) => ({ value: p, label: p })),
            face.attackPattern || (snap.attackPatterns || [])[0],
            (v) => { face.attackPattern = v; patch({ attackPattern: v }, false); });
          form.appendChild(field("攻击模式", inputs.attackPattern));

          inputs.rhythmSource = selectInput(
            (snap.rhythmSources || []).map((r) => ({ value: r, label: r })),
            face.rhythmSource || (snap.rhythmSources || [])[0],
            (v) => { face.rhythmSource = v; patch({ rhythmSource: v }, false); });
          form.appendChild(field("节奏源", inputs.rhythmSource));

          inputs.rhythmPeriod = numberInput(face.rhythmPeriod, (v) => { face.rhythmPeriod = v; patch({ rhythmPeriod: v }); }, { min: 0 });
          form.appendChild(field("节奏周期", inputs.rhythmPeriod));
        }
        body.appendChild(form);
      }));
    }

    if (isRoomKind(face.kind)) {
      rightCol.appendChild(sectionCard("房间配置", null, (body) => {
        const form = el("div", "form-grid");
        inputs.iconPrefab = textInput(face.iconPrefab, (v) => { face.iconPrefab = v; patch({ iconPrefab: v }); }, { mono: true });
        form.appendChild(field("图标预制体路径", inputs.iconPrefab, { span: 2 }));
        inputs.boardSlot = numberInput(face.boardSlot, (v) => { face.boardSlot = v; patch({ boardSlot: v }, false); }, { min: 0 });
        form.appendChild(field("格位（1–9，0=未配置）", inputs.boardSlot));
        inputs.weight = numberInput(face.weight, (v) => { face.weight = v; patch({ weight: v }, false); });
        form.appendChild(field("权重", inputs.weight));
        inputs.shopOfferCount = numberInput(face.shopOfferCount, (v) => { face.shopOfferCount = v; patch({ shopOfferCount: v }, false); });
        form.appendChild(field("商店货数", inputs.shopOfferCount));
        inputs.rewardPoolId = textInput(face.rewardPoolId, (v) => { face.rewardPoolId = v; patch({ rewardPoolId: v }, false); }, { mono: true });
        form.appendChild(field("奖励池", inputs.rewardPoolId));
        body.appendChild(form);

        const injectField = el("div", "field full");
        injectField.style.marginTop = "10px";
        injectField.appendChild(el("label", null, "开局注入 JSON"));
        inputs.openingInjects = textArea(face.openingInjectsJson, (v) => { face.openingInjectsJson = v; patch({ openingInjectsJson: v }, false); }, { mono: true, minHeight: "72px" });
        injectField.appendChild(inputs.openingInjects);
        body.appendChild(injectField);
      }));
    }

    rightCol.appendChild(sectionCard("图像", null, (body) => {
      const grid2 = el("div", "sprite-grid");
      const slotDefs = [
        ["mainIcon", "主图标"], ["faceBackground", "卡面背景"], ["cardFrame", "卡框"], ["banner", "横幅"],
        ["backBorder", "卡背边框"], ["backShirt", "卡背背纹"], ["backLogo", "卡背 Logo"],
      ];
      inputs.spriteTiles = {};
      for (const [slot, label] of slotDefs) {
        const tile = spriteTile(label, () => face.sprites[slot], (ref) => {
          face.sprites[slot] = ref;
          patch({ sprites: { [slot]: ref } });
          updateBackFollowNote();
        });
        inputs.spriteTiles[slot] = tile;
        grid2.appendChild(tile);
      }
      body.appendChild(grid2);

      const mvForm = el("div", "form-grid");
      mvForm.style.marginTop = "12px";
      inputs.offsetX = numberInput(face.mainVisual.offsetX, (v) => { face.mainVisual.offsetX = v; patch({ mainVisual: { offsetX: v } }); }, { float: true, step: 0.01 });
      inputs.offsetY = numberInput(face.mainVisual.offsetY, (v) => { face.mainVisual.offsetY = v; patch({ mainVisual: { offsetY: v } }); }, { float: true, step: 0.01 });
      inputs.uniformScale = numberInput(face.mainVisual.uniformScale, (v) => { face.mainVisual.uniformScale = v; patch({ mainVisual: { uniformScale: v } }); }, { float: true, step: 0.05 });
      mvForm.appendChild(field("主图 Offset X", inputs.offsetX));
      mvForm.appendChild(field("主图 Offset Y", inputs.offsetY));
      mvForm.appendChild(field("主图 Scale", inputs.uniformScale));
      body.appendChild(mvForm);
    }));

    rightCol.appendChild(sectionCard("动画", "来源 none / folder / atlas", (body) => {
      const fpsRow = el("div", "form-grid");
      inputs.defaultFps = numberInput(face.animations.defaultFps, (v) => {
        face.animations.defaultFps = v;
        patch({ animations: { defaultFps: v } }, false);
      }, { float: true, step: 0.5, min: 0.1 });
      fpsRow.appendChild(field("默认 FPS", inputs.defaultFps));
      body.appendChild(fpsRow);

      const table = el("table", "anim-table");
      const thead = el("thead");
      const headRow = el("tr");
      for (const text of ["槽", "来源", "路径", "ΔX", "ΔY"]) headRow.appendChild(el("th", null, text));
      thead.appendChild(headRow);
      table.appendChild(thead);
      const tbody = el("tbody");
      inputs.animRows = {};

      function sendSlots() {
        patch({ animations: { slots: face.animations.slots } }, false);
      }

      for (const slot of face.animations.slots) {
        const row = el("tr");
        row.appendChild(el("td", null, slot.id));

        const sourceCell = el("td");
        const sourceSelect = selectInput(
          [{ value: "none", label: "none" }, { value: "folder", label: "folder" }, { value: "atlas", label: "atlas" }],
          slot.sourceType || "none",
          (v) => { slot.sourceType = v; sendSlots(); });
        sourceCell.appendChild(sourceSelect);
        row.appendChild(sourceCell);

        const pathCell = el("td");
        const pathInput = textInput(slot.path, (v) => { slot.path = v; sendSlots(); }, { mono: true });
        pathInput.classList.add("path");
        pathCell.appendChild(pathInput);
        row.appendChild(pathCell);

        const oxCell = el("td");
        const oxInput = numberInput(slot.offsetX, (v) => { slot.offsetX = v; sendSlots(); }, { float: true, step: 0.01 });
        oxInput.classList.add("num");
        oxCell.appendChild(oxInput);
        row.appendChild(oxCell);

        const oyCell = el("td");
        const oyInput = numberInput(slot.offsetY, (v) => { slot.offsetY = v; sendSlots(); }, { float: true, step: 0.01 });
        oyInput.classList.add("num");
        oyCell.appendChild(oyInput);
        row.appendChild(oyCell);

        inputs.animRows[slot.id] = { sourceSelect, pathInput, oxInput, oyInput };
        tbody.appendChild(row);
      }
      table.appendChild(tbody);
      body.appendChild(table);
    }));

    const assemblyCard = buildAssemblyCard(face, contentId, key, (payload) => {
      if (payload && payload.description != null) {
        face.description = payload.description;
        face.descriptionLocked = !!payload.descriptionLocked;
        syncDescription(face);
      }
      if (preview) preview._reload();
    });
    rightCol.appendChild(assemblyCard);

    /* 检查描述 */
    let descArea = null;
    let descModeChip = null;
    rightCol.appendChild(sectionCard("检查描述", "右键检查的静态规则概括", (body) => {
      const modeRow = el("div", "desc-mode");
      descModeChip = el("span", "chip");
      modeRow.appendChild(descModeChip);
      const modeHint = el("span", "preview-note", "清空描述可恢复自动跟随效果装配");
      modeRow.appendChild(modeHint);
      body.appendChild(modeRow);
      descArea = textArea(face.description, (v) => {
        face.description = v;
        patch({ description: v });
        inputs.renderGlossaryTerms && inputs.renderGlossaryTerms();
      }, { minHeight: "72px" });
      descArea.classList.add("desc-textarea");
      body.appendChild(descArea);
      syncDescription(face);
    }));

    function syncDescription(local) {
      if (!descArea || !descModeChip) return;
      descModeChip.textContent = local.descriptionLocked ? "自定义（手改锁定）" : "自动同步";
      descModeChip.className = "chip " + (local.descriptionLocked ? "accent" : "ok");
      syncValue(descArea, local.description);
    }

    /* 词条配置（右键详情） */
    rightCol.appendChild(sectionCard("词条配置（右键详情）", "检查描述中的 [[词条]] 自动作为基础填充；可搜索词条表追加隐藏词条（仅右键详情多展）", (body) => {
      face.extraGlossaryTerms = face.extraGlossaryTerms || [];
      const derivedHost = el("div", "term-chips");
      const extraHost = el("div", "term-chips");
      body.appendChild(el("div", "term-group-label", "基础填充（检查描述自动抽取，删描述词条后保存刷新本区同步消失）"));
      body.appendChild(derivedHost);
      body.appendChild(el("div", "term-group-label", "追加词条（右键详情多展，不占卡面描述）"));
      body.appendChild(extraHost);
      const addRow = el("div", "term-add-row");
      const addBtn = el("button", "small", "＋ 添加词条");
      addRow.appendChild(addBtn);
      body.appendChild(addRow);

      function extractDerived() {
        const out = [];
        const seen = new Set();
        const re = /\[\[([^\]]+)\]\]/g;
        let m;
        while ((m = re.exec(face.description || ""))) {
          const name = m[1].trim();
          if (name && !seen.has(name)) { seen.add(name); out.push(name); }
        }
        return out;
      }

      function glossaryChoices() {
        return (snap.glossary?.entries || [])
          .filter((e) => (e.displayNameZh || "").trim())
          .map((e) => ({ value: e.displayNameZh.trim(), label: e.displayNameZh.trim(), sub: e.explanation || "" }));
      }

      function isTermRegistered(name) {
        const needle = name.trim();
        return (snap.glossary?.entries || []).some((e) => (e.displayNameZh || "").trim() === needle);
      }

      function sendExtras() {
        markEdited(key);
        queuePatch("updateFace", key, { contentId }, { extraGlossaryTerms: face.extraGlossaryTerms.slice() }, () => {
          if (preview) preview._reload();
        });
      }

      function chip(name, registered, onRemove) {
        const c = el("span", "term-chip" + (registered ? "" : " unregistered"), name);
        if (!registered) c.title = "未命中词条表：详情行仅显示名字";
        if (onRemove) {
          const x = el("button", "term-remove", "×");
          x.title = "移除该追加词条";
          x.addEventListener("click", onRemove);
          c.appendChild(x);
        }
        return c;
      }

      function renderTerms() {
        derivedHost.innerHTML = "";
        const derived = extractDerived();
        if (derived.length === 0) {
          derivedHost.appendChild(el("span", "preview-note", "（无；在检查描述里写 [[名字]] 即自动作为基础填充）"));
        }
        for (const name of derived) {
          derivedHost.appendChild(chip(name, isTermRegistered(name), null));
        }

        extraHost.innerHTML = "";
        const extras = face.extraGlossaryTerms;
        const kept = [];
        for (let i = 0; i < extras.length; i++) {
          const name = (extras[i] || "").trim();
          if (!name || derived.includes(name)) continue;
          kept.push(name);
          const target = name;
          extraHost.appendChild(chip(name, isTermRegistered(name), () => {
            face.extraGlossaryTerms = face.extraGlossaryTerms.filter((n) => (n || "").trim() !== target);
            sendExtras();
            renderTerms();
          }));
        }
        face.extraGlossaryTerms.length = 0;
        for (const k of kept) face.extraGlossaryTerms.push(k);
        if (kept.length === 0) {
          extraHost.appendChild(el("span", "preview-note", "（暂无追加词条）"));
        }
      }

      function addTerm(name) {
        const trimmed = name.trim();
        if (!trimmed) return;
        if (extractDerived().includes(trimmed)) {
          toast("该词条已由检查描述自动填充", "err");
          return;
        }
        const extras = face.extraGlossaryTerms;
        if (extras.includes(trimmed)) return;
        extras.push(trimmed);
        sendExtras();
        renderTerms();
      }

      addBtn.addEventListener("click", () => {
        const existing = addRow.querySelector(".combo-pop");
        if (existing) { existing.remove(); return; }
        const pop = el("div", "combo-pop");
        const search = el("input");
        search.type = "text";
        search.placeholder = "搜索词条…";
        pop.appendChild(search);
        const optionsHost = el("div", "options");
        pop.appendChild(optionsHost);
        addRow.appendChild(pop);

        function renderOptions() {
          optionsHost.innerHTML = "";
          const needle = search.value.trim().toLowerCase();
          let shown = 0;
          for (const choice of glossaryChoices()) {
            if (needle && !(choice.label + " " + (choice.sub || "")).toLowerCase().includes(needle)) continue;
            if (++shown > 120) break;
            const option = el("div", "combo-option");
            option.appendChild(el("div", null, choice.label));
            if (choice.sub) option.appendChild(el("div", "sub", choice.sub));
            option.addEventListener("click", () => {
              pop.remove();
              document.removeEventListener("mousedown", dismiss);
              addTerm(choice.value);
            });
            optionsHost.appendChild(option);
          }
          if (shown === 0) optionsHost.appendChild(el("div", "combo-option", "（无匹配）"));
        }
        search.addEventListener("input", renderOptions);
        renderOptions();
        search.focus();
        const dismiss = (ev) => {
          if (!addRow.contains(ev.target)) {
            pop.remove();
            document.removeEventListener("mousedown", dismiss);
          }
        };
        document.addEventListener("mousedown", dismiss);
      });

      inputs.renderGlossaryTerms = renderTerms;
      renderTerms();
    }));

    /* 其他配置 */
    rightCol.appendChild(sectionCard("其他配置", "extraSlots", (body) => {
      const rows = el("div", "extra-rows");
      body.appendChild(rows);

      function commit() {
        patch({ extraSlots: face.extraSlots }, false);
        renderRows();
      }
      inputs.renderExtraRows = () => renderRows();
      function renderRows() {
        rows.innerHTML = "";
        face.extraSlots.forEach((slot, index) => {
          const row = el("div", "extra-row");
          const codeInput = textInput(slot.code, (v) => { slot.code = v; patch({ extraSlots: face.extraSlots }, false); }, { mono: true, placeholder: "code" });
          codeInput.classList.add("code");
          const pathInput = textInput(slot.path, (v) => { slot.path = v; patch({ extraSlots: face.extraSlots }, false); }, { mono: true, placeholder: "Assets/…" });
          pathInput.classList.add("path");
          const removeBtn = el("button", "danger small", "删");
          removeBtn.addEventListener("click", () => { face.extraSlots.splice(index, 1); commit(); });
          row.appendChild(codeInput);
          row.appendChild(pathInput);
          row.appendChild(removeBtn);
          rows.appendChild(row);
        });
      }
      renderRows();

      const addBtn = el("button", "small", "＋ 添加 extraSlot");
      addBtn.style.marginTop = "8px";
      addBtn.addEventListener("click", () => { face.extraSlots.push({ code: "", path: "" }); commit(); });
      body.appendChild(addBtn);
    }, { collapsible: true, open: (face.extraSlots || []).length > 0 }));

    function mergeDraft(local) {
      const scalars = [
        "displayName", "designSlotName", "faceIntro", "deckId", "gold", "level", "sequence",
        "isReserve", "attackPattern", "rhythmSource", "rhythmPeriod", "iconPrefab", "boardSlot",
        "weight", "shopOfferCount", "rewardPoolId", "openingInjectsJson",
        "description", "descriptionLocked", "isDirty",
      ];
      for (const name of scalars) face[name] = local[name];
      Object.assign(face.stats, local.stats);
      Object.assign(face.mainVisual, local.mainVisual);
      Object.assign(face.sprites, local.sprites);
      face.animations.defaultFps = local.animations.defaultFps;
      for (const slot of face.animations.slots) {
        const fresh = (local.animations.slots || []).find((s) => s.id === slot.id);
        if (fresh) Object.assign(slot, fresh);
      }
      if (JSON.stringify(local.extraSlots) !== JSON.stringify(face.extraSlots)) {
        face.extraSlots.length = 0;
        for (const slot of local.extraSlots) face.extraSlots.push({ code: slot.code, path: slot.path });
        inputs.renderExtraRows && inputs.renderExtraRows();
      }
      if (JSON.stringify(local.extraGlossaryTerms || []) !== JSON.stringify(face.extraGlossaryTerms || [])) {
        face.extraGlossaryTerms.length = 0;
        for (const name of local.extraGlossaryTerms || []) face.extraGlossaryTerms.push(name);
        inputs.renderGlossaryTerms && inputs.renderGlossaryTerms();
      }
    }

    function sync() {
      const local = faceById(contentId);
      if (!local) return;
      dirtyChip.classList.toggle("hidden", !local.isDirty);
      if (recentlyEdited(key)) return;
      mergeDraft(local);
      assemblyCard._sync(local);
      title.textContent = faceTitle(face);
      syncValue(inputs.displayName, face.displayName);
      if (inputs.gold) syncValue(inputs.gold, face.gold);
      if (inputs.faceIntro) syncValue(inputs.faceIntro, face.faceIntro);
      if (inputs.designSlotName) syncValue(inputs.designSlotName, face.designSlotName);
      if (inputs.level) syncValue(inputs.level, face.level);
      if (inputs.sequence) syncValue(inputs.sequence, String(face.sequence || 0));
      if (inputs.deckId) syncValue(inputs.deckId, face.deckId);
      if (inputs.isReserve && document.activeElement !== inputs.isReserve._checkbox) {
        inputs.isReserve._checkbox.checked = !!face.isReserve;
      }
      if (inputs.attack) { syncValue(inputs.attack, face.stats.attack); syncValue(inputs.armor, face.stats.armor); syncValue(inputs.hp, face.stats.hp); }
      if (inputs.attackPattern) syncValue(inputs.attackPattern, face.attackPattern);
      if (inputs.rhythmSource) syncValue(inputs.rhythmSource, face.rhythmSource);
      if (inputs.rhythmPeriod) syncValue(inputs.rhythmPeriod, face.rhythmPeriod);
      if (inputs.offsetX) { syncValue(inputs.offsetX, face.mainVisual.offsetX); syncValue(inputs.offsetY, face.mainVisual.offsetY); syncValue(inputs.uniformScale, face.mainVisual.uniformScale); }
      if (inputs.defaultFps) syncValue(inputs.defaultFps, face.animations.defaultFps);
      if (inputs.animRows) {
        for (const slot of face.animations.slots) {
          const row = inputs.animRows[slot.id];
          if (!row) continue;
          syncValue(row.sourceSelect, slot.sourceType || "none");
          syncValue(row.pathInput, slot.path);
          syncValue(row.oxInput, slot.offsetX);
          syncValue(row.oyInput, slot.offsetY);
        }
      }
      if (inputs.iconPrefab) syncValue(inputs.iconPrefab, face.iconPrefab);
      if (inputs.boardSlot) syncValue(inputs.boardSlot, face.boardSlot);
      if (inputs.weight) syncValue(inputs.weight, face.weight);
      if (inputs.shopOfferCount) syncValue(inputs.shopOfferCount, face.shopOfferCount);
      if (inputs.rewardPoolId) syncValue(inputs.rewardPoolId, face.rewardPoolId);
      if (inputs.openingInjects) syncValue(inputs.openingInjects, face.openingInjectsJson);
      if (inputs.spriteTiles) {
        for (const tile of Object.values(inputs.spriteTiles)) tile._refresh();
      }
      syncDescription(face);
      if (inputs.renderGlossaryTerms) inputs.renderGlossaryTerms();
      updateBackFollowNote();
    }

    return {
      kind: "face",
      id: contentId,
      sync,
      dispose: () => { animCard._stop && animCard._stop(); },
    };
  }

  /* ── 动画试播卡 ───────────────────────── */

  function buildAnimPlayerCard(contentId) {
    let frames = [];
    let frameUrls = [];
    let timer = null;
    let index = 0;
    let fps = 8;

    const card = el("div", "card");
    const head = el("div", "card-head");
    head.appendChild(el("span", null, "动画试播"));
    const tail = el("div", "tail");
    const slotSelect = selectInput(
      (snap.animSlotIds || ["idle"]).map((s) => ({ value: s, label: s })),
      "idle",
      () => load());
    tail.appendChild(slotSelect);
    const reloadBtn = el("button", "small", "重载");
    reloadBtn.addEventListener("click", () => load());
    tail.appendChild(reloadBtn);
    head.appendChild(tail);
    card.appendChild(head);

    const body = el("div", "card-body");
    const player = el("div", "anim-player");
    const frameBox = el("div", "frame");
    const img = el("img");
    frameBox.appendChild(img);
    player.appendChild(frameBox);
    const meta = el("div", "meta", "选择槽位后试播");
    player.appendChild(meta);
    body.appendChild(player);
    card.appendChild(body);

    function stop() {
      if (timer != null) { clearInterval(timer); timer = null; }
    }

    async function load() {
      stop();
      img.style.visibility = "hidden";
      meta.textContent = "加载中…";
      const result = await run("animFrames", { contentId, slotId: slotSelect.value });
      if (!result) { meta.textContent = "该槽位无帧"; return; }
      frames = result.payload?.frames || [];
      fps = Math.max(0.5, result.payload?.fps || 8);
      if (frames.length === 0) {
        meta.textContent = "该槽位无帧（来源 none 或路径无效）";
        return;
      }
      frameUrls = await Promise.all(frames.map((f) => fetchBlobUrl(spriteThumbUrl(f.ref)).catch(() => null)));
      index = 0;
      meta.textContent = frames.length + " 帧 · " + fps.toFixed(1) + " FPS 循环";
      img.style.visibility = "visible";
      timer = setInterval(() => {
        const url = frameUrls[index % frameUrls.length];
        if (url) img.src = url;
        index++;
      }, 1000 / fps);
    }

    card._stop = stop;
    return card;
  }

  /* ── 效果装配卡 ───────────────────────── */

  function buildAssemblyCard(face, contentId, key, onAfterChange) {
    const card = el("div", "card");
    const head = el("div", "card-head");
    head.appendChild(el("span", null, "效果装配"));
    head.appendChild(el("span", "hint", "数值改 argsJson；描述未锁定时自动跟随"));
    const tail = el("div", "tail");
    head.appendChild(tail);
    card.appendChild(head);
    const body = el("div", "card-body");
    card.appendChild(body);

    const listHost = el("div", "assembly-list");
    body.appendChild(listHost);

    const btnRow = el("div", "field-row");
    btnRow.style.marginTop = "10px";
    const addBtn = el("button", null, "＋ 添加效果");
    const clearBtn = el("button", "danger", "清空全部");
    btnRow.appendChild(addBtn);
    btnRow.appendChild(clearBtn);
    body.appendChild(btnRow);

    function templateChoicesForFace() {
      const category = containerCategoryForKind(face.kind);
      const rows = (snap.templates || []).filter((t) => !category || t.category === category);
      const labelCounts = new Map();
      for (const row of rows) {
        labelCounts.set(row.paramText, (labelCounts.get(row.paramText) || 0) + 1);
      }
      return rows
        .map((row) => ({
          value: row.id,
          label: (labelCounts.get(row.paramText) > 1 ? row.paramText + " · " + row.id : row.paramText) || row.id,
          sub: row.id,
        }))
        .sort((a, b) => a.label.localeCompare(b.label, "zh-CN"));
    }

    function sendAssemblies() {
      markEdited(key);
      queuePatch("updateFace", key, { contentId },
        { effectAssemblies: face.effectAssemblies.map((a) => ({ id: a.id, templateId: a.templateId, containerType: a.containerType, argsJson: a.argsJson })) },
        (result) => {
          onAfterChange && onAfterChange(result.payload || {});
        });
    }

    function renderList() {
      listHost.innerHTML = "";
      if (face.effectAssemblies.length === 0) {
        listHost.appendChild(el("div", "preview-note", "无效果装配（白板）"));
      }
      const choices = templateChoicesForFace();
      face.effectAssemblies.forEach((assembly, index) => {
        const allChoices = [...choices];
        if (assembly.templateId && !allChoices.some((c) => c.value === assembly.templateId)) {
          const orphan = templateById(assembly.templateId);
          allChoices.unshift({
            value: assembly.templateId,
            label: (orphan ? orphan.paramText + "（" + orphan.categoryTitle + "）" : assembly.templateId),
            sub: assembly.templateId,
          });
        }

        const row = el("div", "assembly-row");
        const row1 = el("div", "row1");
        const idInput = textInput(assembly.id, (v) => { assembly.id = v; sendAssembliesDebounced(); }, { mono: true, placeholder: "挂载 id" });
        idInput.classList.add("id-input");
        row1.appendChild(idInput);

        const tplCombo = combo(allChoices, assembly.templateId, async (picked) => {
          assembly.templateId = picked;
          const suggestion = await run("suggestArgs", { templateId: picked });
          assembly.argsJson = suggestion?.payload?.argsJson || "{}";
          sendAssemblies();
          renderList();
        });
        row1.appendChild(tplCombo);

        const removeBtn = el("button", "danger small", "删除");
        removeBtn.addEventListener("click", () => {
          face.effectAssemblies.splice(index, 1);
          sendAssemblies();
          renderList();
        });
        row1.appendChild(removeBtn);
        row.appendChild(row1);

        if (assembly.templateId) {
          const brief = el("div", "brief", assembly.brief || "");
          row.appendChild(brief);
        }

        const argsRow = el("div", "args-row");
        argsRow.appendChild(el("span", "lbl chip mono", "args"));
        const argsInput = textInput(assembly.argsJson, (v) => { assembly.argsJson = v; sendAssembliesDebounced(); }, { mono: true });
        argsRow.appendChild(argsInput);
        row.appendChild(argsRow);

        if ((assembly.sendableKeys || []).length > 0 && assembly.id) {
          const vars = el("div", "assembly-vars");
          vars.appendChild(el("span", "lbl", "发送变量到描述："));
          for (const pair of assembly.sendableKeys) {
            const chip = el("span", "var-chip", pair.key + "=" + pair.value);
            chip.title = "把 {" + assembly.id + "." + pair.key + "} 追加到检查描述末尾";
            chip.addEventListener("click", () => {
              const tokenText = "{" + assembly.id.trim() + "." + pair.key + "}";
              const current = (face.description || "").trim();
              face.description = current ? current.replace(/\s+$/, "") + "；" + tokenText : tokenText;
              queuePatch("updateFace", key, { contentId }, { description: face.description }, (result) => {
                onAfterChange && onAfterChange(result.payload || {});
              }, 60);
              toast("已追加 " + tokenText, "ok");
            });
            vars.appendChild(chip);
          }
          row.appendChild(vars);
        }

        listHost.appendChild(row);
      });
    }

    const sendAssembliesDebounced = debounce(sendAssemblies, 420);

    addBtn.addEventListener("click", () => {
      face.effectAssemblies.push({
        id: "fx." + Math.random().toString(16).slice(2, 10),
        templateId: "",
        containerType: "",
        argsJson: "{}",
        sendableKeys: [],
        brief: "",
      });
      renderList();
      sendAssemblies();
    });

    clearBtn.addEventListener("click", () => {
      confirmDialog("清空本卡全部效果装配？", () => {
        face.effectAssemblies.length = 0;
        renderList();
        sendAssemblies();
      });
    });

    renderList();
    card._sync = (local) => {
      const fresh = local.effectAssemblies || [];
      if (JSON.stringify(fresh) === JSON.stringify(face.effectAssemblies)) return;
      face.effectAssemblies.length = 0;
      for (const assembly of fresh) face.effectAssemblies.push(assembly);
      renderList();
    };
    return card;
  }

  /* ── 卡组页 ───────────────────────────── */

  function buildDeckPage(contentId) {
    const deck = deckById(contentId);
    if (!deck) return null;
    const key = "deck:" + contentId;
    let preview = null;

    function patch(body) {
      queuePatch("updateDeck", key, { contentId }, body, () => {
        preview && preview._reload();
      });
    }

    const pageHead = el("div", "page-head");
    const title = el("h1", null, deck.displayName || deck.contentId);
    pageHead.appendChild(title);
    const chips = el("div", "chips");
    chips.appendChild(el("span", "chip mono", deck.contentId));
    chips.appendChild(el("span", "chip", "卡组"));
    const dirtyChip = el("span", "chip warn hidden", "未保存");
    chips.appendChild(dirtyChip);
    pageHead.appendChild(chips);
    const actions = el("div", "actions");
    const saveBtn = el("button", "primary", "保存卡组");
    saveBtn.addEventListener("click", async () => {
      const result = await run("saveDeck", { contentId });
      if (result) toast("已保存 " + contentId, "ok");
    });
    actions.appendChild(saveBtn);
    pageHead.appendChild(actions);
    els.main.appendChild(pageHead);
    els.main.appendChild(el("p", "page-sub", "卡背三槽；卡面 deckId 归属本组后，预览与运行时跟随本组卡背。"));

    const grid = el("div", "page-grid");
    const leftCol = el("div", "col");
    const rightCol = el("div", "col");
    grid.appendChild(leftCol);
    grid.appendChild(rightCol);
    els.main.appendChild(grid);

    preview = facePreviewPanel(contentId, { startBack: true });
    leftCol.appendChild(preview);

    const inputs = {};
    rightCol.appendChild(sectionCard("卡组配置", null, (body) => {
      const form = el("div", "form-grid");
      inputs.displayName = textInput(deck.displayName, (v) => {
        deck.displayName = v;
        title.textContent = v || deck.contentId;
        patch({ displayName: v });
      });
      form.appendChild(field("显示名", inputs.displayName, { span: 2 }));
      body.appendChild(form);

      const spriteGrid = el("div", "sprite-grid");
      spriteGrid.style.marginTop = "12px";
      inputs.tiles = {};
      for (const [slot, label] of [["backBorder", "卡背边框"], ["backShirt", "卡背背纹"], ["backLogo", "卡背 Logo"]]) {
        const tile = spriteTile(label, () => deck[slot], (ref) => {
          deck[slot] = ref;
          patch({ [slot]: ref });
        });
        inputs.tiles[slot] = tile;
        spriteGrid.appendChild(tile);
      }
      body.appendChild(spriteGrid);
    }));

    rightCol.appendChild(sectionCard("遭遇接线（只读）", null, (body) => {
      const panel = el("div", "usage-panel");
      const headline = el("div", "headline");
      const detail = el("div", "detail");
      panel.appendChild(headline);
      panel.appendChild(detail);
      body.appendChild(panel);
      inputs.usageHeadline = headline;
      inputs.usageDetail = detail;
      applyUsage(deck);
    }));

    function applyUsage(local) {
      const usage = local.usage || {};
      inputs.usageHeadline.textContent = usage.headline || "";
      inputs.usageHeadline.className = "headline " + (
        usage.status === "Wired" ? "ok" : usage.status === "PresentationOnly" ? "plain" : "warn");
      inputs.usageDetail.textContent = usage.detail || "";
    }

    function sync() {
      const local = deckById(contentId);
      if (!local) return;
      dirtyChip.classList.toggle("hidden", !local.isDirty);
      applyUsage(local);
      if (recentlyEdited(key)) return;
      title.textContent = local.displayName || local.contentId;
      syncValue(inputs.displayName, local.displayName);
      Object.assign(deck, local);
      for (const tile of Object.values(inputs.tiles)) tile._refresh();
    }

    return { kind: "deck", id: contentId, sync };
  }

  /* ── 效果模板页 ───────────────────────── */

  function buildTemplatePage(id) {
    const template = templateById(id);
    if (!template) return null;
    const key = "template:" + id;
    let previewImg = null;
    let previewStage = null;

    function patch(body) {
      queuePatch("updateTemplate", key, { id }, body, () => reloadPreview());
    }

    const pageHead = el("div", "page-head");
    pageHead.appendChild(el("h1", null, "效果模板"));
    const chips = el("div", "chips");
    const idChip = el("span", "chip mono", template.id);
    idChip.style.cursor = "pointer";
    idChip.title = "点击复制";
    idChip.addEventListener("click", () => copyText(template.id));
    chips.appendChild(idChip);
    chips.appendChild(el("span", "chip", template.categoryTitle));
    const dirtyChip = el("span", "chip warn hidden", "未保存");
    chips.appendChild(dirtyChip);
    pageHead.appendChild(chips);
    const actions = el("div", "actions");
    const saveBtn = el("button", "primary", "保存模板表");
    saveBtn.addEventListener("click", async () => {
      const result = await run("saveTemplates", {});
      if (result) toast("已保存效果模板表", "ok");
    });
    actions.appendChild(saveBtn);
    pageHead.appendChild(actions);
    els.main.appendChild(pageHead);
    els.main.appendChild(el("p", "page-sub", "design_text 是作者备注文案；body 为效果 DSL（只读）。"));

    const grid = el("div", "page-grid");
    const leftCol = el("div", "col");
    const rightCol = el("div", "col");
    grid.appendChild(leftCol);
    grid.appendChild(rightCol);
    els.main.appendChild(grid);

    leftCol.appendChild(sectionCard("描述预览", null, (body) => {
      previewStage = el("div", "preview-single preview-stage");
      previewImg = el("img");
      previewStage.appendChild(previewImg);
      previewStage.appendChild(el("div", "preview-loading", "渲染中…"));
      body.appendChild(previewStage);
    }));

    async function reloadPreview() {
      previewStage.classList.add("loading");
      await setImage(previewImg,
        "/api/asset?kind=templatePreview&templateId=" + encodeURIComponent(id) + "&w=520&h=640&t=" + Date.now(),
        false);
      previewStage.classList.remove("loading");
    }
    reloadPreview();

    const inputs = {};
    rightCol.appendChild(sectionCard("模板内容", null, (body) => {
      const form = el("div", "form-grid");
      inputs.state = textInput(template.state, (v) => patch({ state: v }), { mono: true });
      form.appendChild(field("state", inputs.state));
      body.appendChild(form);

      const designField = el("div", "field full");
      designField.style.marginTop = "10px";
      designField.appendChild(el("label", null, "design_text"));
      inputs.designText = textArea(template.designText, (v) => patch({ designText: v }), { minHeight: "84px" });
      designField.appendChild(inputs.designText);
      body.appendChild(designField);
    }));

    rightCol.appendChild(sectionCard("元数据（只读）", null, (body) => {
      const kv = el("div", "kv-readonly");
      for (const [k, v] of [["requires", template.requiresJson], ["conditions", template.conditionsJson]]) {
        const row = el("div", "kv");
        row.appendChild(el("span", "k", k));
        row.appendChild(el("span", "v", v));
        kv.appendChild(row);
      }
      body.appendChild(kv);
    }));

    rightCol.appendChild(sectionCard("body（效果 DSL，只读）", null, (body) => {
      const area = el("textarea", "mono");
      area.readOnly = true;
      area.style.minHeight = "180px";
      area.value = template.body || "";
      body.appendChild(area);
      inputs.body = area;
    }, { collapsible: true, open: false }));

    function sync() {
      const local = templateById(id);
      if (!local) return;
      dirtyChip.classList.toggle("hidden", !local.isDirty);
      if (recentlyEdited(key)) return;
      syncValue(inputs.state, local.state);
      syncValue(inputs.designText, local.designText);
    }

    return { kind: "template", id, sync };
  }

  /* ── 特效页 ───────────────────────────── */

  function buildVfxPage(id) {
    const vfx = vfxById(id);
    if (!vfx) return null;
    const key = "vfx:" + id;
    let timer = null;
    let frameUrls = [];
    let frameDims = [];
    let frameIndex = 0;
    let playing = true;

    function patch(body) {
      queuePatch("updateVisualEffect", key, { id }, body);
    }

    const pageHead = el("div", "page-head");
    pageHead.appendChild(el("h1", null, vfx.displayName || vfx.variantId));
    const chips = el("div", "chips");
    chips.appendChild(el("span", "chip mono", vfx.id));
    chips.appendChild(el("span", "chip", vfx.categoryTitle));
    const dirtyChip = el("span", "chip warn hidden", "未保存");
    chips.appendChild(dirtyChip);
    pageHead.appendChild(chips);
    const actions = el("div", "actions");
    const saveBtn = el("button", "primary", "保存特效库");
    saveBtn.addEventListener("click", async () => {
      const result = await run("saveVisualEffects", {});
      if (result) toast("已保存 visual_effects.json", "ok");
    });
    actions.appendChild(saveBtn);
    pageHead.appendChild(actions);
    els.main.appendChild(pageHead);
    els.main.appendChild(el("p", "page-sub", "播放速度与大小写回 visual_effects.json；预览为逐帧循环。"));

    const grid = el("div", "page-grid");
    const leftCol = el("div", "col");
    const rightCol = el("div", "col");
    grid.appendChild(leftCol);
    grid.appendChild(rightCol);
    els.main.appendChild(grid);

    /* 播放台 */
    let img = null;
    let metaNote = null;
    leftCol.appendChild(sectionCard("预览", null, (body, tail) => {
      const stage = el("div", "vfx-stage");
      img = el("img");
      stage.appendChild(img);
      body.appendChild(stage);
      const toolbar = el("div", "preview-toolbar");
      const pauseBtn = el("button", null, "暂停");
      pauseBtn.addEventListener("click", () => {
        playing = !playing;
        pauseBtn.textContent = playing ? "暂停" : "继续";
      });
      const replayBtn = el("button", null, "重播");
      replayBtn.addEventListener("click", () => { frameIndex = 0; playing = true; pauseBtn.textContent = "暂停"; });
      metaNote = el("span", "preview-note");
      toolbar.appendChild(pauseBtn);
      toolbar.appendChild(replayBtn);
      toolbar.appendChild(el("span", "spacer"));
      toolbar.appendChild(metaNote);
      body.appendChild(toolbar);
    }));

    function applyFrame() {
      if (frameUrls.length === 0) return;
      const url = frameUrls[frameIndex % frameUrls.length];
      const dims = frameDims[frameIndex % frameDims.length] || { w: 64, h: 64 };
      if (url) {
        img.src = url;
        const scale = currentScale();
        img.style.width = Math.max(8, Math.round(dims.w * scale * 3)) + "px";
        img.style.height = "auto";
      }
    }

    function currentScale() {
      const local = vfxById(id);
      return Math.max(0.05, local ? local.defaultScale : 1);
    }

    function restartTimer() {
      if (timer != null) clearInterval(timer);
      const local = vfxById(id);
      const fps = Math.max(0.5, local ? local.defaultFps : 12);
      timer = setInterval(() => {
        if (!playing || frameUrls.length === 0) return;
        applyFrame();
        frameIndex++;
      }, 1000 / fps);
    }

    async function loadFrames() {
      const result = await run("vfxFrames", { id });
      if (!result) return;
      const frames = result.payload?.frames || [];
      metaNote.textContent = frames.length + " 帧";
      frameDims = frames.map((f) => ({ w: f.w, h: f.h }));
      frameUrls = await Promise.all(frames.map((f) => fetchBlobUrl(spriteThumbUrl(f.ref)).catch(() => null)));
      frameIndex = 0;
      restartTimer();
    }

    /* 变体尺寸/颜色 */
    const leaves = (snap.visualEffects || []).filter((v) =>
      v.category === vfx.category && v.variantId === vfx.variantId);
    if (leaves.length > 1) {
      leftCol.appendChild(sectionCard("尺寸 / 颜色", null, (body) => {
        const chipsHost = el("div", "leaf-chips");
        for (const leaf of leaves) {
          const chip = el("span", "leaf-chip" + (leaf.id === id ? " selected" : ""), leaf.size + " · " + leaf.color);
          chip.addEventListener("click", () => select("vfx", leaf.id));
          chipsHost.appendChild(chip);
        }
        body.appendChild(chipsHost);
      }));
    }

    const inputs = {};
    rightCol.appendChild(sectionCard("参数", null, (body) => {
      const form = el("div", "form-grid");
      inputs.displayName = textInput(vfx.displayName, (v) => patch({ displayName: v }));
      form.appendChild(field("显示名", inputs.displayName, { span: 2 }));
      body.appendChild(form);

      const fpsRow = el("div", "field");
      fpsRow.style.marginTop = "10px";
      fpsRow.appendChild(el("label", null, "播放速度 (FPS)"));
      const fpsSlider = el("div", "slider-row");
      inputs.fpsRange = el("input");
      inputs.fpsRange.type = "range";
      inputs.fpsRange.min = "1"; inputs.fpsRange.max = "40"; inputs.fpsRange.step = "0.5";
      inputs.fpsRange.value = vfx.defaultFps;
      inputs.fpsNum = numberInput(vfx.defaultFps, (v) => {
        inputs.fpsRange.value = v;
        applyFps(v);
      }, { float: true, step: 0.5, min: 0.5 });
      inputs.fpsNum.classList.add("val");
      inputs.fpsRange.addEventListener("input", () => {
        const v = parseFloat(inputs.fpsRange.value);
        inputs.fpsNum.value = v;
        applyFps(v);
      });
      fpsSlider.appendChild(inputs.fpsRange);
      fpsSlider.appendChild(inputs.fpsNum);
      fpsRow.appendChild(fpsSlider);
      body.appendChild(fpsRow);

      const scaleRow = el("div", "field");
      scaleRow.style.marginTop = "10px";
      scaleRow.appendChild(el("label", null, "大小 (Scale)"));
      const scaleSlider = el("div", "slider-row");
      inputs.scaleRange = el("input");
      inputs.scaleRange.type = "range";
      inputs.scaleRange.min = "0.1"; inputs.scaleRange.max = "4"; inputs.scaleRange.step = "0.05";
      inputs.scaleRange.value = vfx.defaultScale;
      inputs.scaleNum = numberInput(vfx.defaultScale, (v) => {
        inputs.scaleRange.value = v;
        applyScale(v);
      }, { float: true, step: 0.05, min: 0.05 });
      inputs.scaleNum.classList.add("val");
      inputs.scaleRange.addEventListener("input", () => {
        const v = parseFloat(inputs.scaleRange.value);
        inputs.scaleNum.value = v;
        applyScale(v);
      });
      scaleSlider.appendChild(inputs.scaleRange);
      scaleSlider.appendChild(inputs.scaleNum);
      scaleRow.appendChild(scaleSlider);
      body.appendChild(scaleRow);

      const pathRow = el("div", "kv-readonly");
      pathRow.style.marginTop = "12px";
      const kv = el("div", "kv");
      kv.appendChild(el("span", "k", "sheetPath"));
      kv.appendChild(el("span", "v", vfx.sheetPath));
      pathRow.appendChild(kv);
      body.appendChild(pathRow);
    }));

    function applyFps(v) {
      const local = vfxById(id);
      if (local) local.defaultFps = v;
      patch({ defaultFps: v });
      restartTimer();
    }
    function applyScale(v) {
      const local = vfxById(id);
      if (local) local.defaultScale = v;
      patch({ defaultScale: v });
      applyFrame();
    }

    loadFrames();

    function sync() {
      const local = vfxById(id);
      if (!local) return;
      dirtyChip.classList.toggle("hidden", !local.isDirty);
      if (recentlyEdited(key)) return;
      syncValue(inputs.displayName, local.displayName);
      syncValue(inputs.fpsNum, local.defaultFps);
      syncValue(inputs.scaleNum, local.defaultScale);
    }

    const page = { kind: "vfx", id, sync };
    page.dispose = () => { if (timer != null) clearInterval(timer); };
    return page;
  }

  /* ── 词条页 ───────────────────────────── */

  function buildGlossaryPage() {
    const key = "glossary";
    let sampleKind = "Monster";
    let sampleText = "在[Action_Icon]后攻击玩家；施加[Poison]";
    let previewImg = null;
    let previewStage = null;

    const pageHead = el("div", "page-head");
    pageHead.appendChild(el("h1", null, "词条"));
    const chips = el("div", "chips");
    chips.appendChild(el("span", "chip", "[[名字]] 文字词条"));
    chips.appendChild(el("span", "chip", "[code] 图标词条"));
    const dirtyChip = el("span", "chip warn hidden", "未保存");
    chips.appendChild(dirtyChip);
    pageHead.appendChild(chips);
    const actions = el("div", "actions");
    const addBtn = el("button", null, "＋ 添加词条");
    addBtn.addEventListener("click", async () => {
      const result = await run("glossaryAdd", {});
      if (result) toast("已添加词条", "ok");
    });
    const saveBtn = el("button", "primary", "保存词条表");
    saveBtn.addEventListener("click", async () => {
      const result = await run("saveGlossary", {});
      if (result) toast("已保存词条表与图标样式", "ok");
    });
    actions.appendChild(addBtn);
    actions.appendChild(saveBtn);
    pageHead.appendChild(actions);
    els.main.appendChild(pageHead);
    els.main.appendChild(el("p", "page-sub", "名字供 [[名字]] 引用；代号 + 精灵供 [code] 内联图标；介绍进右键详情。"));

    const grid = el("div", "page-grid");
    const leftCol = el("div", "col");
    const rightCol = el("div", "col");
    grid.appendChild(leftCol);
    grid.appendChild(rightCol);
    els.main.appendChild(grid);

    /* 样例预览 */
    leftCol.appendChild(sectionCard("样例预览", null, (body) => {
      const form = el("div", "form-grid");
      form.appendChild(field("卡种", selectInput([
        { value: "Avatar", label: "玩家卡" },
        { value: "Monster", label: "怪物卡" },
        { value: "HelpCard", label: "道具卡" },
        { value: "Relic", label: "遗物卡" },
      ], sampleKind, (v) => { sampleKind = v; reloadPreview(); })));
      body.appendChild(form);

      const sampleField = el("div", "field full");
      sampleField.style.margin = "10px 0";
      sampleField.appendChild(el("label", null, "样例描述"));
      sampleField.appendChild(textArea(sampleText, (v) => { sampleText = v; reloadPreviewDebounced(); }, { minHeight: "54px" }));
      body.appendChild(sampleField);

      previewStage = el("div", "preview-single preview-stage");
      previewImg = el("img");
      previewStage.appendChild(previewImg);
      previewStage.appendChild(el("div", "preview-loading", "渲染中…"));
      body.appendChild(previewStage);
    }));

    async function reloadPreview() {
      previewStage.classList.add("loading");
      await setImage(previewImg,
        "/api/asset?kind=samplePreview&cardKind=" + sampleKind
        + "&text=" + encodeURIComponent(sampleText) + "&w=520&h=640&t=" + Date.now(),
        false);
      previewStage.classList.remove("loading");
    }
    const reloadPreviewDebounced = debounce(reloadPreview, 500);
    reloadPreview();

    /* 装配槽图标（只读 sprite + 布局） */
    leftCol.appendChild(sectionCard("装配槽图标布局", "模板内建图标；改 Sprite 走预制体", (body) => {
      const host = el("div", "insertable-list");
      for (const icon of snap.glossary?.insertable || []) {
        const row = el("div", "insertable-row");
        row.appendChild(el("span", "name", icon.displayNameZh));
        const tokenChip = el("span", "token-chip", "[" + icon.code + "]");
        tokenChip.title = "点击复制";
        tokenChip.addEventListener("click", () => copyText("[" + icon.code + "]"));
        row.appendChild(tokenChip);
        row.appendChild(layoutFields(icon.code, icon, () => reloadPreviewDebounced()));
        host.appendChild(row);
      }
      body.appendChild(host);
    }, { collapsible: true, open: false }));

    function layoutFields(code, layout, onChanged) {
      const wrap = el("div", "layout-fields");
      const mk = (labelText, prop, step) => {
        const label = el("label", null, labelText);
        const input = numberInput(layout[prop], (v) => {
          layout[prop] = v;
          markEdited(key);
          queuePatch("glossaryLayout", "glossaryLayout:" + code, { code },
            { bearingX: layout.bearingX, bearingY: layout.bearingY, baseScale: layout.baseScale },
            () => onChanged && onChanged());
        }, { float: true, step });
        wrap.appendChild(label);
        wrap.appendChild(input);
      };
      mk("ΔX", "bearingX", 0.01);
      mk("ΔY", "bearingY", 0.01);
      mk("Scale", "baseScale", 0.05);
      return wrap;
    }

    /* 词条列表 */
    const listHost = el("div", "glossary-list");
    rightCol.appendChild(sectionCard("词条列表", null, (body) => {
      body.appendChild(listHost);
    }));

    function renderEntries() {
      const entries = snap.glossary?.entries || [];
      listHost.innerHTML = "";
      if (entries.length === 0) {
        listHost.appendChild(el("div", "preview-note", "表空，点右上「添加词条」。"));
        return;
      }
      for (const entry of entries) {
        listHost.appendChild(glossaryRow(entry));
      }
    }

    function gPatch(index, body, extraDelay) {
      markEdited(key);
      queuePatch("glossaryUpdate", "glossaryEntry:" + index, { index }, body,
        () => reloadPreviewDebounced(), extraDelay || 380);
    }

    function glossaryRow(entry) {
      const row = el("div", "glossary-row");
      const head = el("div", "head");
      head.appendChild(el("span", "title", entry.displayNameZh || "（未命名）"));
      if (entry.displayNameZh) {
        const nameToken = el("span", "token-chip", "[[" + entry.displayNameZh + "]]");
        nameToken.title = "点击复制";
        nameToken.addEventListener("click", () => copyText("[[" + entry.displayNameZh + "]]"));
        head.appendChild(nameToken);
      }
      if (entry.code) {
        const codeToken = el("span", "token-chip", "[" + entry.code + "]");
        codeToken.title = "点击复制";
        codeToken.addEventListener("click", () => copyText("[" + entry.code + "]"));
        head.appendChild(codeToken);
      }
      const tail = el("div", "tail");
      const removeBtn = el("button", "danger small", "删除");
      removeBtn.addEventListener("click", () => {
        confirmDialog("删除词条「" + (entry.displayNameZh || entry.code || "?") + "」？", async () => {
          await run("glossaryRemove", { index: entry.index });
        });
      });
      tail.appendChild(removeBtn);
      head.appendChild(tail);
      row.appendChild(head);

      const form = el("div", "form-grid");
      form.appendChild(field("名字", textInput(entry.displayNameZh, (v) => gPatch(entry.index, { displayNameZh: v }))));
      form.appendChild(field("代号（可选，供 [code]）", textInput(entry.code, (v) => gPatch(entry.index, { code: v }), { mono: true })));

      /* 颜色 */
      const colorWrap = el("div", "field-row");
      const colorToggle = el("input");
      colorToggle.type = "checkbox";
      colorToggle.checked = !!entry.colorHex;
      const colorInput = el("input");
      colorInput.type = "color";
      colorInput.value = entry.colorHex || "#ffffff";
      colorInput.disabled = !entry.colorHex;
      colorToggle.addEventListener("change", () => {
        colorInput.disabled = !colorToggle.checked;
        gPatch(entry.index, { colorHex: colorToggle.checked ? colorInput.value : "" }, 80);
      });
      colorInput.addEventListener("input", () => {
        if (colorToggle.checked) gPatch(entry.index, { colorHex: colorInput.value });
      });
      colorWrap.appendChild(colorToggle);
      colorWrap.appendChild(colorInput);
      form.appendChild(field("覆盖文字颜色", colorWrap));

      row.appendChild(form);

      const explField = el("div", "field full");
      explField.style.marginTop = "8px";
      explField.appendChild(el("label", null, "详细介绍（右键详情正文）"));
      explField.appendChild(textArea(entry.explanation, (v) => gPatch(entry.index, { explanation: v }), { minHeight: "44px" }));
      row.appendChild(explField);

      const bottomRow = el("div", "field-row");
      bottomRow.style.marginTop = "10px";
      const tile = spriteTile("图标 Sprite", () => entry.spritePath, (ref) => {
        entry.spritePath = ref;
        gPatch(entry.index, { spritePath: ref }, 80);
      });
      tile.style.width = "150px";
      bottomRow.appendChild(tile);
      if (entry.code) {
        bottomRow.appendChild(layoutFields(entry.code, entry, () => reloadPreviewDebounced()));
      }
      row.appendChild(bottomRow);
      return row;
    }

    renderEntries();

    function sync() {
      dirtyChip.classList.toggle("hidden", !snap.glossary?.dirty);
      if (recentlyEdited(key) || document.activeElement?.closest?.(".glossary-list")) return;
      renderEntries();
    }

    return { kind: "glossary", id: "glossary", sync };
  }

  /* ── 全局动作 ─────────────────────────── */

  async function saveAll() {
    const result = await run("saveAll", {});
    if (result) toast("已保存全部改动", "ok");
  }

  async function saveScope() {
    if (!sel) return saveAll();
    switch (sel.kind) {
      case "face": {
        const result = await run("saveFace", { contentId: sel.id });
        if (result) toast("已保存 " + sel.id, "ok");
        return;
      }
      case "deck": {
        const result = await run("saveDeck", { contentId: sel.id });
        if (result) toast("已保存 " + sel.id, "ok");
        return;
      }
      case "template": {
        const result = await run("saveTemplates", {});
        if (result) toast("已保存效果模板表", "ok");
        return;
      }
      case "vfx": {
        const result = await run("saveVisualEffects", {});
        if (result) toast("已保存特效库", "ok");
        return;
      }
      case "glossary": {
        const result = await run("saveGlossary", {});
        if (result) toast("已保存词条表", "ok");
        return;
      }
      default:
        return saveAll();
    }
  }

  els.btnSaveAll.addEventListener("click", saveAll);
  els.btnReload.addEventListener("click", () => {
    const dirty = snap?.dirtyCount || 0;
    if (dirty > 0) {
      confirmDialog("有 " + dirty + " 处未保存改动，丢弃并从磁盘重载？", async () => {
        blobCache.clear();
        const result = await run("reload", {});
        if (result) { toast("已重新加载", "ok"); renderMain(true); }
      });
    } else {
      blobCache.clear();
      run("reload", {}).then((r) => { if (r) { toast("已重新加载", "ok"); renderMain(true); } });
    }
  });
  els.btnExportIndex.addEventListener("click", async () => {
    const result = await run("exportIndex", {});
    if (result) toast(result.payload?.message || "已导出索引", "ok");
  });
  els.btnReconnect.addEventListener("click", () => {
    reconnectDelay = 1200;
    connect();
  });
  els.navSearch.addEventListener("input", debounce(() => {
    navFilter = els.navSearch.value.trim();
    renderSidebar();
  }, 180));

  document.addEventListener("keydown", (ev) => {
    if ((ev.ctrlKey || ev.metaKey) && ev.key.toLowerCase() === "s") {
      ev.preventDefault();
      saveScope();
    }
    if (ev.key === "Escape" && !els.modalHost.classList.contains("hidden")) {
      closeModal();
    }
  });

  setConnected(false, "连接中…");
  connect();
})();
