/**
 * Migration checklist: classify each proposal.
 */
(function (global) {
  const ITEMS = [
    {
      prompt: "新增一种卡面受击曲线，策划要在 Project 里调参",
      answer: "keep-so",
      why: "配置 SO 甜区。继续 CardEffectSO / EncounterProfile，不要扯进 Rules Model。",
    },
    {
      prompt: "把 MainlineBusy 做成可拖的 BusyField.asset，让 UI 都引用它",
      answer: "dont",
      why: "门禁单一真相。双资产会分裂；与 ADR-0004 / 装配单例冲突。",
    },
    {
      prompt: "未来做『移动预览→瞄准→确认』三层右键逐层退，想先在 System 里放栈",
      answer: "maybe",
      why: "栈的甜区。优先普通 C# 栈由 System 持有；只有真要 Inspector 可视化栈再考虑 SO。",
    },
    {
      prompt: "把 ExploreIntentRejected 改成 EventChannel.asset，Prefab 里拖订阅",
      answer: "maybe",
      why: "能做，但你们 struct+架构总线已可追踪。除非强需求可视化接线，否则收益有限、调用链变藏。",
    },
    {
      prompt: "AI 与玩家攻击都绕过 IntentIntake，直接调 Core",
      answer: "dont",
      why: "破坏『多入口单一收口』——这正是 Rules Model / Intake 要消灭的病。",
    },
    {
      prompt: "新 VFX 参数表做成 SO，仍由 Present Channel 播放",
      answer: "keep-so",
      why: "配方资产 + 代码编排。这是你们已经在走的混用正道。",
    },
  ];

  function mount(root) {
    if (!root) return;
    let i = 0;
    let score = 0;
    const progress = document.createElement("div");
    progress.className = "caption";
    const prompt = document.createElement("div");
    const choices = document.createElement("div");
    choices.style.display = "grid";
    choices.style.gap = "0.4rem";
    choices.style.marginTop = "0.55rem";
    const feedback = document.createElement("div");
    feedback.className = "caption";
    feedback.style.minHeight = "3em";
    feedback.style.marginTop = "0.55rem";

    const opts = [
      { key: "keep-so", label: "值得用 / 保持配置 SO" },
      { key: "maybe", label: "可考虑，但有条件" },
      { key: "dont", label: "不该迁 / 会打架" },
    ];

    function render() {
      const item = ITEMS[i];
      progress.textContent = "第 " + (i + 1) + " / " + ITEMS.length + " · 已对 " + score;
      prompt.innerHTML = "<strong>" + item.prompt + "</strong>";
      feedback.textContent = "按你们仓库的纪律判断。";
      choices.innerHTML = "";
      opts.forEach((o) => {
        const btn = document.createElement("button");
        btn.type = "button";
        btn.textContent = o.label;
        btn.style.textAlign = "left";
        btn.style.padding = "0.55rem 0.7rem";
        btn.style.border = "1px solid #444";
        btn.style.background = "#1c1c1c";
        btn.style.color = "#eee";
        btn.style.borderRadius = "4px";
        btn.style.cursor = "pointer";
        btn.style.font = "inherit";
        btn.addEventListener("click", () => pick(o.key, btn));
        choices.appendChild(btn);
      });
    }

    function pick(key, btn) {
      const item = ITEMS[i];
      const ok = key === item.answer;
      if (ok) score += 1;
      Array.from(choices.querySelectorAll("button")).forEach((el) => {
        el.disabled = true;
        el.style.opacity = "0.65";
      });
      btn.style.borderColor = ok ? "#3ecf8e" : "#ff6b6b";
      btn.style.opacity = "1";
      feedback.textContent = (ok ? "对。 " : "偏了。 ") + item.why;
      setTimeout(() => {
        i += 1;
        if (i >= ITEMS.length) {
          progress.textContent = "完成 · " + score + " / " + ITEMS.length;
          prompt.innerHTML =
            "<strong>口诀：配方可 SO；门禁与规则真相不迁资产；栈只加在真有多层取消的地方。</strong>";
          choices.innerHTML = "";
          feedback.textContent = "课程主线结束。有具体迁移提案，可以拿这条口诀来烤。";
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

  global.SoMigrateLab = { mount: mount };
})(window);
