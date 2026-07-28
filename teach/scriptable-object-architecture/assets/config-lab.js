/**
 * Classify: Config SO / Field SO / Not a good SO Field.
 */
(function (global) {
  const ITEMS = [
    {
      id: "knockback",
      prompt: "击退系数、闪白 delay、是否绑死亡回调（遭遇预设）",
      answer: "config",
      why: "相对稳定的表现配方，换一场战斗不必靠 Reset 清掉。你们已有 BattleEncounterProfileSO。",
    },
    {
      id: "selected-ability",
      prompt: "玩家此刻选中的技能是火球还是治疗",
      answer: "field",
      why: "当前事实，会变、要通知别人。作者用 Field SO；你们放在运行时 System/输入上下文里。",
    },
    {
      id: "tween-lunge",
      prompt: "某张卡的冲刺 Punch 曲线、时长、方向变体元数据",
      answer: "config",
      why: "CardEffectSO 族：可拖到装配槽的配方；PlayAsync 是策略，不是『当前选中哪张牌』。",
    },
    {
      id: "mainline-busy",
      prompt: "主线表演此刻是否 Busy（门禁用）",
      answer: "neither",
      why: "会话级互斥真相，权威在 PresentationDirector。做成可拖 Field.asset 易出现双资产两套 Busy，且和 ADR-0004 单一真相冲突。",
    },
    {
      id: "slot-registry",
      prompt: "卡面槽位名 → 绑定路径的注册表",
      answer: "config",
      why: "CardFaceSlotRegistrySO：编辑期配方，多处引用同一份注册表是配置共享，不是运行时黑板。",
    },
    {
      id: "hover-cell",
      prompt: "鼠标当前悬停的格子坐标",
      answer: "field",
      why: "作者总结里典型的共享运行时字段。你们更多由指针路由/局部状态持有，而不是 HoverCell.asset。",
    },
  ];

  function mount(root) {
    if (!root) return;
    let index = 0;
    let correct = 0;

    const prompt = document.createElement("div");
    prompt.style.marginBottom = "0.65rem";

    const choices = document.createElement("div");
    choices.style.display = "grid";
    choices.style.gap = "0.4rem";

    const feedback = document.createElement("div");
    feedback.className = "caption";
    feedback.style.marginTop = "0.65rem";
    feedback.style.minHeight = "2.8em";

    const progress = document.createElement("div");
    progress.className = "caption";
    progress.style.marginBottom = "0.55rem";

    const buttons = [
      { key: "config", label: "配置 SO（配方）" },
      { key: "field", label: "Field SO（当前事实）" },
      { key: "neither", label: "不宜当可拖运行时 SO" },
    ];

    function render() {
      const item = ITEMS[index];
      progress.textContent =
        "第 " + (index + 1) + " / " + ITEMS.length + " · 已对 " + correct;
      prompt.innerHTML = "<strong>" + item.prompt + "</strong>";
      feedback.textContent = "先选一类，再看讲解。";
      choices.innerHTML = "";
      buttons.forEach((b) => {
        const btn = document.createElement("button");
        btn.type = "button";
        btn.textContent = b.label;
        btn.style.textAlign = "left";
        btn.style.padding = "0.55rem 0.7rem";
        btn.style.border = "1px solid #444";
        btn.style.background = "#1c1c1c";
        btn.style.color = "#eee";
        btn.style.borderRadius = "4px";
        btn.style.cursor = "pointer";
        btn.style.font = "inherit";
        btn.addEventListener("click", () => onPick(b.key, btn));
        choices.appendChild(btn);
      });
    }

    function onPick(key, btn) {
      const item = ITEMS[index];
      const ok = key === item.answer;
      if (ok) correct += 1;
      Array.from(choices.querySelectorAll("button")).forEach((el) => {
        el.disabled = true;
        el.style.opacity = "0.7";
      });
      btn.style.borderColor = ok ? "#3ecf8e" : "#ff6b6b";
      btn.style.opacity = "1";
      feedback.textContent = (ok ? "对。 " : "偏了。 ") + item.why;

      setTimeout(() => {
        index += 1;
        if (index >= ITEMS.length) {
          progress.textContent = "完成 · " + correct + " / " + ITEMS.length;
          prompt.innerHTML =
            "<strong>边界钉住了就够：配方可资产化；当前事实若要 SO，那是 Field，不是 Config。</strong>";
          choices.innerHTML = "";
          feedback.textContent =
            "下一课才会深挖 Field；本课只要你不再把『当前选中』误叫成配置 SO。";
          return;
        }
        render();
      }, 1600);
    }

    root.appendChild(progress);
    root.appendChild(prompt);
    root.appendChild(choices);
    root.appendChild(feedback);
    render();
  }

  global.SoConfigLab = { mount: mount };
})(window);
