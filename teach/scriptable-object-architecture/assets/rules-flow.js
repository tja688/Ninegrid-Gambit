/**
 * Click-through: unit select / explore intent — SO Rules vs NineGrid.
 */
(function (global) {
  const FLOWS = {
    so: {
      title: "作者 · Rules Model SO",
      steps: [
        "Bridge 收到点击单位",
        "UnityEvent → RulesModel.ApplySelectedUnit(unit)",
        "RulesModel 内判：回合？友方？已行动？",
        "改 Field/State，PushState(Selected)",
        "State 广播 → Visual 画标记与范围",
      ],
      note: "规则服务本身是可拖的 .asset；多 Bridge 共用同一份。",
    },
    ng: {
      title: "你们 · Intake + Core",
      steps: [
        "HitProxy / Controller 收到探索或攻击",
        "SendCommand → IntentIntake.Submit（所有权×Busy）",
        "Allow/Buffer 后进 Director；写规则走 Core Command",
        "合法性可读 Query；占格真相在 BoardModel",
        "批次事件 → Present Channel / 卡面 Commit 画结果",
      ],
      note: "规则在 Core 程序集；Intake 是门禁收口，不是把规则做成 SO。",
    },
  };

  function mount(root) {
    if (!root) return;
    const tabs = document.createElement("div");
    tabs.className = "lab-controls";
    const ol = document.createElement("ol");
    ol.style.margin = "0.45rem 0 0";
    ol.style.paddingLeft = "1.2rem";
    ol.style.fontSize = "0.88rem";
    ol.style.lineHeight = "1.45";
    const note = document.createElement("div");
    note.className = "caption";

    function show(key) {
      const f = FLOWS[key];
      Array.from(tabs.querySelectorAll("button")).forEach((b) => {
        const on = b.dataset.key === key;
        b.style.borderColor = on ? "#6aa6ff" : "#444";
        b.style.background = on ? "#243044" : "#1c1c1c";
      });
      ol.innerHTML = "";
      f.steps.forEach((s) => {
        const li = document.createElement("li");
        li.textContent = s;
        ol.appendChild(li);
      });
      note.textContent = f.note;
    }

    Object.keys(FLOWS).forEach((key) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.dataset.key = key;
      btn.textContent = FLOWS[key].title;
      btn.style.padding = "0.35rem 0.7rem";
      btn.style.border = "1px solid #444";
      btn.style.background = "#1c1c1c";
      btn.style.color = "#eee";
      btn.style.borderRadius = "4px";
      btn.style.cursor = "pointer";
      btn.style.font = "inherit";
      btn.addEventListener("click", () => show(key));
      tabs.appendChild(btn);
    });

    root.appendChild(tabs);
    root.appendChild(ol);
    root.appendChild(note);
    show("so");
  }

  global.SoRulesFlow = { mount: mount };
})(window);
