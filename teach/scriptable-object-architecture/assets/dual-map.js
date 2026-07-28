/**
 * Dual-map lab: click a concern cell to reveal SO vs NineGrid wiring.
 */
(function (global) {
  const ROWS = [
    {
      id: "input",
      label: "输入",
      so: {
        name: "Bridge MB",
        blurb: "收点击 / 动画结束，转成业务请求",
        detail:
          "SO 侧：UnitSelectionBridge 等 MonoBehaviour 吃场景回调，再 UnityEvent / 直接调 RulesModel。接线常在 Inspector 拖。",
      },
      ng: {
        name: "Controller + HitProxy",
        blurb: "PointerHitRouter → Hook → Controller",
        detail:
          "NineGrid：命中走 PointerHitRouter / IPointerHitTarget；Controller 收意图后 IntentIntake.Submit。场景 Host 经 PresentationSceneBindings 注入，不是拖同一个 State.asset。",
      },
    },
    {
      id: "rules",
      label: "规则",
      so: {
        name: "Rules Model SO",
        blurb: "合法性、应用、回滚；资产可共享",
        detail:
          "SO 侧：FelikeRulesModel 一类资产当规则中心。玩家 / AI / 教程 Bridge 都调同一份。规则活在 .asset 上。",
      },
      ng: {
        name: "Core + Query + Intake",
        blurb: "规则在 Core；门禁在 IntentIntake",
        detail:
          "NineGrid：规则真相在 NineGrid.Core；表现层用 Query 问合法性，写经 Command。IntentIntake（ADR-0004）是唯一意图收口——功能上像「规则入口门禁」，但是 C# System，不是 SO。",
      },
    },
    {
      id: "state",
      label: "状态",
      so: {
        name: "Field / State SO",
        blurb: "共享当前事实 + 变化通知",
        detail:
          "SO 侧：CurrentSelectedAbility 这类 Field SO，或多字段 BattleRuntimeStateSO。谁引用同一 .asset，谁就共享这份事实。",
      },
      ng: {
        name: "System 持有的运行时",
        blurb: "InputState / Director / GameFlow",
        detail:
          "NineGrid：当前输入所有权、MainlineBusy、流程相位由 System / Director 持有；只读投影给别人。共享靠组合根注入同一实例，不靠拖同一个 RuntimeField.asset。",
      },
    },
    {
      id: "event",
      label: "事件",
      so: {
        name: "Event Channel SO",
        blurb: "Raise / Register，不保存事实",
        detail:
          "SO 侧：AbilitySelectedEventSO 只转发「发生了」。发送者不知监听者。Hipple 的 GameEvent 是经典原型。",
      },
      ng: {
        name: "struct Event / Hook",
        blurb: "架构事件 + 装配缝委托",
        detail:
          "NineGrid：下→上多用 struct Event；Cards↔Controllers 用窄 *Hook 委托（装配缝，禁业务 Sink）。事件通道在代码与架构里，不在 Project 窗口的 Event.asset。",
      },
    },
    {
      id: "view",
      label: "表现",
      so: {
        name: "Visual SO / View MB",
        blurb: "格子高亮、预览；不判规则",
        detail:
          "SO 侧：VisualGrid 听状态/事件后画。Rules 决定『该显示哪些』，Visual 决定『怎么画』。",
      },
      ng: {
        name: "Presenter / Channel / Driver",
        blurb: "Timeline、PresentChannel、卡面驱动",
        detail:
          "NineGrid：PresentationDirector / BattleTimeline / IPresentChannel 编排；卡面 Commit、特效 SO（配置型）负责画。规则仍不进 View。",
      },
    },
    {
      id: "wire",
      label: "接线",
      so: {
        name: "拖同一份 .asset",
        blurb: "Prefab / 场景字段引用共享节点",
        detail:
          "SO 玩法的独特之处：解耦手段是『大家都引用同一个资产』。优点是可视化、可热换；代价是调用链藏在 Inspector，重命名易断。",
      },
      ng: {
        name: "CompositionRoot.Install",
        blurb: "代码装配 + SceneBindings",
        detail:
          "NineGrid：PresentationCompositionRoot.Install(bindings) 是唯一生产装配入口。解耦靠接口与 System，护栏靠结构测试。可拖拽主要留给配置/特效 SO，不留给运行时规则。",
      },
    },
  ];

  function mount(root) {
    if (!root) return;

    const map = document.createElement("div");
    map.className = "dual-map";
    map.setAttribute("role", "list");

    const head = document.createElement("div");
    head.className = "row";
    head.innerHTML =
      '<div class="label"></div>' +
      '<div class="cell so" style="cursor:default"><strong>SO 玩法</strong><span>资产当通信节点</span></div>' +
      '<div class="cell ng" style="cursor:default"><strong>NineGrid</strong><span>组合根 + QF</span></div>';
    map.appendChild(head);

    const detail = document.createElement("div");
    detail.className = "map-detail";
    detail.innerHTML =
      '<div class="title">点一格看对照</div>' +
      "<div>每一行是同一职责。左边是作者那套 SO 组织，右边是你们仓库里对应怎么玩。</div>";

    ROWS.forEach((row) => {
      const el = document.createElement("div");
      el.className = "row";
      el.setAttribute("role", "listitem");

      const label = document.createElement("div");
      label.className = "label";
      label.textContent = row.label;

      const so = document.createElement("button");
      so.type = "button";
      so.className = "cell so";
      so.innerHTML =
        "<strong>" +
        row.so.name +
        "</strong><span>" +
        row.so.blurb +
        "</span>";

      const ng = document.createElement("button");
      ng.type = "button";
      ng.className = "cell ng";
      ng.innerHTML =
        "<strong>" +
        row.ng.name +
        "</strong><span>" +
        row.ng.blurb +
        "</span>";

      function activate(side) {
        map.querySelectorAll(".cell.active").forEach((c) => c.classList.remove("active"));
        const cell = side === "so" ? so : ng;
        cell.classList.add("active");
        const payload = side === "so" ? row.so : row.ng;
        const other = side === "so" ? row.ng : row.so;
        detail.innerHTML =
          '<div class="title">' +
          row.label +
          " · " +
          payload.name +
          "</div><div>" +
          payload.detail +
          "</div><div style='margin-top:0.55rem;color:#bdbdbd'>对照：" +
          other.name +
          " — " +
          other.blurb +
          "</div>";
      }

      so.addEventListener("click", () => activate("so"));
      ng.addEventListener("click", () => activate("ng"));

      el.appendChild(label);
      el.appendChild(so);
      el.appendChild(ng);
      map.appendChild(el);
    });

    root.appendChild(map);
    root.appendChild(detail);
  }

  global.SoDualMap = { mount: mount };
})(window);
