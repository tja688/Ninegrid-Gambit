/**
 * Side-by-side flow: Field SO vs System-held fact.
 */
(function (global) {
  const STEPS = {
    so: [
      "技能按钮 SetValue(Fireball) → CurrentAbility.asset",
      "Field SO 存 Value，并 Raise ValueChanged",
      "MouseIndicator / Tooltip 各自订阅同一资产",
      "右键 Clear() → 所有监听者收到 null",
    ],
    ng: [
      "命中 / UI → Controller → IntentIntake（门禁）",
      "通过后进 Director / Core；共享事实在 System",
      "监听者经架构取 IPresentationInputStateSystem 等",
      "ResetGates / 会话结束显式清布尔与模式",
    ],
  };

  function mount(root) {
    if (!root) return;

    const tabs = document.createElement("div");
    tabs.className = "lab-controls";

    const list = document.createElement("ol");
    list.style.margin = "0.4rem 0 0";
    list.style.paddingLeft = "1.2rem";
    list.style.fontSize = "0.88rem";
    list.style.lineHeight = "1.45";

    const caption = document.createElement("div");
    caption.className = "caption";

    function show(side) {
      Array.from(tabs.querySelectorAll("button")).forEach((b) => {
        b.style.borderColor = b.dataset.side === side ? "#6aa6ff" : "#444";
        b.style.background = b.dataset.side === side ? "#243044" : "#1c1c1c";
      });
      list.innerHTML = "";
      STEPS[side].forEach((t) => {
        const li = document.createElement("li");
        li.textContent = t;
        list.appendChild(li);
      });
      caption.textContent =
        side === "so"
          ? "解耦靠：大家拖同一份 CurrentAbility.asset"
          : "解耦靠：组合根注册同一 System 实例；读口可再包一层门面";
    }

    [
      { side: "so", label: "作者 · Field SO" },
      { side: "ng", label: "你们 · System 事实" },
    ].forEach((t) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.dataset.side = t.side;
      btn.textContent = t.label;
      btn.style.padding = "0.35rem 0.7rem";
      btn.style.border = "1px solid #444";
      btn.style.background = "#1c1c1c";
      btn.style.color = "#eee";
      btn.style.borderRadius = "4px";
      btn.style.cursor = "pointer";
      btn.style.font = "inherit";
      btn.addEventListener("click", () => show(t.side));
      tabs.appendChild(btn);
    });

    root.appendChild(tabs);
    root.appendChild(list);
    root.appendChild(caption);
    show("so");
  }

  global.SoFieldFlow = { mount: mount };
})(window);
