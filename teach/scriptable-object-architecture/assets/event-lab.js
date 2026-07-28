/**
 * Discriminate: Field fact vs Event pulse vs Hook seam.
 */
(function (global) {
  const CASES = [
    {
      prompt: "『当前输入所有者是 ChoiceOverlay』——随时可查询",
      answer: "field",
      why: "持续事实。你们：CurrentOwner / BindableProperty。作者：Field SO。",
    },
    {
      prompt: "Explore 合法性被拒，Reason=…，旁路 UI 需要知道这一下",
      answer: "event",
      why: "一次性通知。你们：SendEvent(ExploreIntentRejectedEvent)。作者：Event Channel Raise。",
    },
    {
      prompt: "Cards 目录代码要通知 Presentation 装配 AttackController，避免程序集环",
      answer: "hook",
      why: "装配缝 *Hook（静态委托），不是业务事件总线，也不是 Field。",
    },
    {
      prompt: "流程壳相位从 A 变到 B，除了属性更新还要广播一下",
      answer: "event",
      why: "GameFlowShellStateChangedEvent 注释已写：BindableProperty 之外的一次性通知。",
    },
    {
      prompt: "BoardSelect / Opening / Overlay 三个索取布尔的当前值",
      answer: "field",
      why: "可查询的当前事实；变化可用 BindableProperty，不必每次靠事件才能知道『现在是什么』。",
    },
    {
      prompt: "玩家点击了火球按钮——若有人只关心『点过一下』、不关心事后当前技能是否仍是火球",
      answer: "event",
      why: "发生了。若还要让后加入的监听者读到『现在是火球』，那是 Field（或 Field+Event 组合）。",
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
    feedback.style.minHeight = "2.8em";
    feedback.style.marginTop = "0.55rem";

    const opts = [
      { key: "field", label: "Field / 当前事实" },
      { key: "event", label: "Event / 发生了一次" },
      { key: "hook", label: "Hook 装配缝（非业务总线）" },
    ];

    function render() {
      const c = CASES[i];
      progress.textContent = "第 " + (i + 1) + " / " + CASES.length + " · 已对 " + score;
      prompt.innerHTML = "<strong>" + c.prompt + "</strong>";
      feedback.textContent = "先归类。";
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
      const c = CASES[i];
      const ok = key === c.answer;
      if (ok) score += 1;
      Array.from(choices.querySelectorAll("button")).forEach((el) => {
        el.disabled = true;
        el.style.opacity = "0.65";
      });
      btn.style.borderColor = ok ? "#3ecf8e" : "#ff6b6b";
      btn.style.opacity = "1";
      feedback.textContent = (ok ? "对。 " : "偏了。 ") + c.why;
      setTimeout(() => {
        i += 1;
        if (i >= CASES.length) {
          progress.textContent = "完成 · " + score + " / " + CASES.length;
          prompt.innerHTML = "<strong>三件事：事实、脉冲、装配缝——别揉进一个万能 SO。</strong>";
          choices.innerHTML = "";
          feedback.textContent = "下一课才碰 Rules Model；本课只要 Event ≠ Field ≠ Hook。";
          return;
        }
        render();
      }, 1500);
    }

    root.appendChild(progress);
    root.appendChild(prompt);
    root.appendChild(choices);
    root.appendChild(feedback);
    render();
  }

  global.SoEventLab = { mount: mount };
})(window);
