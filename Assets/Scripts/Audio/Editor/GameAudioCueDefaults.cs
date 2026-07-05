using System.Collections.Generic;

namespace NineGrid.Audio.Editor
{
    /// <summary>
    /// Static metadata for all 106 audio hit-points from Assets/Docs/音效打点探查文档.md.
    /// Used by GameAudioCueDatabaseBuilder to populate GameAudioCueSO.
    /// </summary>
    internal static class GameAudioCueDefaults
    {
        internal readonly struct Def
        {
            public readonly AudioKey Key;
            public readonly string DisplayName;
            public readonly string TriggerContext;
            public readonly AudioPriority Priority;
            public readonly AudioCategory Category;
            public readonly LoopMode LoopMode;

            public Def(AudioKey key, string displayName, string triggerContext,
                AudioPriority priority, AudioCategory category, LoopMode loopMode = LoopMode.None)
            {
                Key = key;
                DisplayName = displayName;
                TriggerContext = triggerContext;
                Priority = priority;
                Category = category;
                LoopMode = loopMode;
            }
        }

        /// <summary>Maps AudioKey to audio file name (without extension) under Assets/Arts/Audios/.</summary>
        internal static readonly Dictionary<AudioKey, string> ClipFileMap = new()
        {
            { AudioKey.BgmBattle, "常规战斗配乐" },
            { AudioKey.BgmBoss, "Boss战斗配乐" },
            { AudioKey.BgmRoute, "路线选择音乐" },
            { AudioKey.BgmIsland, "平稳的海量音效" },
            { AudioKey.ChainSettling, "巨锚命中敌方" },
            { AudioKey.Impact, "猛烈的撞击" },
            { AudioKey.BoreShow, "电钻就绪" },
            { AudioKey.AmbientOcean, "平稳的海量音效" },
        };

        internal static readonly Def[] All =
        {
            // ===== 场景流转 & 全局状态机 (#1-7) =====
            new(AudioKey.SceneStateChanged, "场景状态切换", "GameFlowController.StateChanged(prev,next)：每次全局 FSM 状态切换时触发，可按目标状态分支不同 stinger。", AudioPriority.S, AudioCategory.SceneFlow),
            new(AudioKey.RunStarted, "新局开始", "GameFlowController.StartNewRun()：玩家开始新一局时触发。", AudioPriority.A, AudioCategory.SceneFlow),
            new(AudioKey.PlayerDefeated, "玩家战败", "GameFlowController.NotifyPlayerDefeated()：玩家战败结算时触发。", AudioPriority.S, AudioCategory.SceneFlow),
            new(AudioKey.VictorySettled, "胜利结算", "GameFlowController.NotifyVictorySettled()：胜利结算完成时触发。", AudioPriority.S, AudioCategory.SceneFlow),
            new(AudioKey.TransitionFadeOut, "场景过渡淡出", "SceneFlowDirector.TransitionRoutine() 黑屏淡入阶段（alpha→1, 0.35s）。", AudioPriority.A, AudioCategory.SceneFlow),
            new(AudioKey.TransitionFadeIn, "场景过渡淡入", "SceneFlowDirector.TransitionRoutine() 黑屏淡出阶段（alpha→0, 0.35s）。", AudioPriority.A, AudioCategory.SceneFlow),
            new(AudioKey.RunPassThroughVictory, "通关胜利展示", "SceneFlowDirector.RunPassThrough() 胜利文字展示 + 等待 2.5s。", AudioPriority.B, AudioCategory.SceneFlow),

            // ===== 战斗 - BattleController (#8-14) =====
            new(AudioKey.BattleEnter, "战斗开始", "BattleController.EnterCompleted：解锁权限、显示战斗 HUD、敌方面板滑入。", AudioPriority.S, AudioCategory.Battle),
            new(AudioKey.BattleExit, "战斗结束", "BattleController.ExitCompleted：锁定权限、关闭锻造、敌方面板滑出。", AudioPriority.A, AudioCategory.Battle),
            new(AudioKey.BattleFinished, "战斗结果", "BattleController.BattleFinished(won)：战斗胜负判定。", AudioPriority.S, AudioCategory.Battle),
            new(AudioKey.EnterForgeMode, "进入锻造模式", "BattleController.TryEnterForgeMode()：点击己方船进入锻造。", AudioPriority.A, AudioCategory.Battle),
            new(AudioKey.EnterAnchorMode, "进入撞击模式", "BattleController.TryEnterAnchorMode()：点击敌方船发射锚链。", AudioPriority.A, AudioCategory.Battle),
            new(AudioKey.NoticeWarning, "操作警告", "BattleController.ShowNotice(\"先锻造\")：未锻造就点敌方的警告。", AudioPriority.B, AudioCategory.Battle),
            new(AudioKey.RamSucceeded, "撞击成功", "BattleController.OnRamSucceeded()：一次撞击完成确认。", AudioPriority.A, AudioCategory.Battle),

            // ===== 战斗 - AnchorRammingController (#15-20) =====
            new(AudioKey.RamBegin, "撞击序列启动", "AnchorRammingController.onRamBegin：撞击序列启动。", AudioPriority.S, AudioCategory.Battle),
            new(AudioKey.CrankBegin, "绞盘启动", "AnchorRammingController.onCrankBegin：绞盘开始转动。", AudioPriority.S, AudioCategory.Battle),
            new(AudioKey.PreImpact, "撞击前紧张", "AnchorRammingController.onPreImpact：慢动作 0.22x + 相机拉近。", AudioPriority.S, AudioCategory.Battle),
            new(AudioKey.Impact, "重型撞击", "AnchorRammingController.onImpact：屏幕震动 + 顿帧 + 碰撞，音量可按冲击力缩放。", AudioPriority.S, AudioCategory.Battle),
            new(AudioKey.Bounce, "撞击回弹", "AnchorRammingController.onBounce：船只弹开 OutBack 缓动。", AudioPriority.A, AudioCategory.Battle),
            new(AudioKey.RamEnd, "撞击结束", "AnchorRammingController.onRamEnd：撞击序列结束、归位。", AudioPriority.A, AudioCategory.Battle),

            // ===== 战斗 - WinchCrankController (#21-24) =====
            new(AudioKey.WinchClicked, "绞盘点击", "WinchCrankController.Clicked：每次成功点击，音高/音量随 Intensity01 递增。", AudioPriority.S, AudioCategory.Battle),
            new(AudioKey.WinchBeginCrank, "绞盘连打开始", "WinchCrankController.BeginCrank()：开始连打阶段。", AudioPriority.A, AudioCategory.Battle),
            new(AudioKey.WinchEndCrank, "绞盘连打结束", "WinchCrankController.EndCrank()：连打结束。", AudioPriority.A, AudioCategory.Battle),
            new(AudioKey.WinchIntensityDecay, "绞盘空转衰减", "WinchCrankController Update：强度自然衰减期间的空转 whir（可选持续音）。", AudioPriority.C, AudioCategory.Battle),

            // ===== 战斗 - AnchorChainLauncher (#25-31) =====
            new(AudioKey.ChainFire, "锚链发射", "AnchorChainLauncher.Fire()：锚链发射。", AudioPriority.S, AudioCategory.Battle),
            new(AudioKey.ChainFlying, "锚链飞行", "AnchorChainLauncher Phase.Flying：锁链在空中飞行。", AudioPriority.A, AudioCategory.Battle),
            new(AudioKey.ChainSettling, "锚链命中", "AnchorChainLauncher Phase.Settling：锚命中敌船船体。", AudioPriority.S, AudioCategory.Battle),
            new(AudioKey.ChainTightening, "锚链绷紧", "AnchorChainLauncher Phase.Tightening：锁链绷紧吱嘎渐强。", AudioPriority.A, AudioCategory.Battle),
            new(AudioKey.ChainAttached, "锚链锁定", "AnchorChainLauncher Phase.Attached：锁链锁定卡扣咬合。", AudioPriority.A, AudioCategory.Battle),
            new(AudioKey.ChainReset, "锚链回收", "AnchorChainLauncher.ResetToIdle()：锁链回收。", AudioPriority.B, AudioCategory.Battle),
            new(AudioKey.ChainHoldTaut, "锚链保持张力", "AnchorChainLauncher.HoldTaut()：撞击期间保持绷紧。", AudioPriority.B, AudioCategory.Battle),

            // ===== 战斗 - AnchorHpTracker (#32-34) =====
            new(AudioKey.AnchorHpResetFull, "锚 HP 满", "AnchorHpTracker.ResetFull()：3 个图标全亮。", AudioPriority.B, AudioCategory.Battle),
            new(AudioKey.AnchorHpConsume, "锚 HP 消耗", "AnchorHpTracker.ConsumeOne()：一个图标熄灭。", AudioPriority.S, AudioCategory.Battle),
            new(AudioKey.AnchorHpEmpty, "锚 HP 耗尽", "AnchorHpTracker IsEmpty：所有锚耗尽危急警告。", AudioPriority.A, AudioCategory.Battle),

            // ===== 战斗 - BoreManager (#35-36) =====
            new(AudioKey.BoreShow, "钻头部署", "BoreManager.ShowBore(slot)：钻头伸出船身。", AudioPriority.A, AudioCategory.Battle),
            new(AudioKey.BoreHideAll, "钻头缩回", "BoreManager.HideAll()：所有钻头缩回。", AudioPriority.B, AudioCategory.Battle),

            // ===== 战斗 - 相机效果 (#37-38) =====
            new(AudioKey.ScreenShakePlay, "屏幕震动", "ScreenShakeEffect.Play()：屏幕震动低频 rumble。", AudioPriority.A, AudioCategory.Battle),
            new(AudioKey.CameraZoomBegin, "相机拉近", "CameraJuice.BeginZoom()：相机拉近聚焦 whoosh。", AudioPriority.C, AudioCategory.Battle),

            // ===== 液压锻造 - HydraulicSceneController (#39-47) =====
            new(AudioKey.HydraulicEnterStart, "锻造进入开始", "HydraulicSceneController.EnterStarted：BG→展示台→管道交错淡入。", AudioPriority.S, AudioCategory.Forge),
            new(AudioKey.HydraulicEnterComplete, "锻造进入完成", "HydraulicSceneController.EnterCompleted：锻造完全可见可交互。", AudioPriority.A, AudioCategory.Forge),
            new(AudioKey.HydraulicExit, "锻造退出", "HydraulicSceneController.ExitCompleted：管道→展示台→BG 交错淡出。", AudioPriority.A, AudioCategory.Forge),
            new(AudioKey.HydraulicPreForge, "冲压预锁", "HydraulicSceneController.PlayHydraulic/NotifyForgeCommitted：锤子准备下落。", AudioPriority.A, AudioCategory.Forge),
            new(AudioKey.HydraulicHammerFall, "锤头下落", "HydraulicRoutine 锤下落 DOTween InExpo 0.22s。", AudioPriority.S, AudioCategory.Forge),
            new(AudioKey.HydraulicHammerImpact, "锤头撞击", "HydraulicRoutine 锤头撞击瞬间 + ScreenShakeEffect，锻造最大音效时刻。", AudioPriority.S, AudioCategory.Forge),
            new(AudioKey.HydraulicHammerHold, "锤头保持", "HydraulicRoutine 锤保持 0.5s：持续压力 hiss。", AudioPriority.A, AudioCategory.Forge),
            new(AudioKey.HydraulicHammerRise, "锤头上升", "HydraulicRoutine 锤上升 DOTween InCubic 0.32s。", AudioPriority.A, AudioCategory.Forge),
            new(AudioKey.HydraulicCompleted, "冲压完成", "HydraulicSceneController.HydraulicCompleted：冲压周期完成。", AudioPriority.B, AudioCategory.Forge),

            // ===== 液压锻造 - 材料管道 (#48-50) =====
            new(AudioKey.LaneDeliver, "管道输送", "HydraulicMaterialLane.TryDeliver() 管道滑行 DOTween 0.55s。", AudioPriority.A, AudioCategory.Forge),
            new(AudioKey.LaneDeliverLand, "材料落桌", "HydraulicMaterialLane.TryDeliver() 落桌 DOTween 0.7s。", AudioPriority.A, AudioCategory.Forge),
            new(AudioKey.LaneReset, "管道重置", "HydraulicMaterialLane.ResetLane()：所有材料归位。", AudioPriority.C, AudioCategory.Forge),

            // ===== 液压锻造 - 铁砧拖放 (#51-56) =====
            new(AudioKey.BoardBeginDrag, "开始拖拽材料", "HydraulicMaterialBoard.BeginDrag()：拖拽开始材料抬起。", AudioPriority.A, AudioCategory.Forge),
            new(AudioKey.BoardPlaceOnAnvil, "放置到铁砧", "HydraulicMaterialBoard.TryPlaceOnAnvil()：放到铁砧上。", AudioPriority.S, AudioCategory.Forge),
            new(AudioKey.BoardRelayoutAnvil, "铁砧重排", "HydraulicMaterialBoard.RelayoutAnvil(animate:true)：铁砧堆叠重排。", AudioPriority.B, AudioCategory.Forge),
            new(AudioKey.BoardPlaceOnTable, "放回台面", "HydraulicMaterialBoard.TryPlaceOnFreeTableSlot()：材料放回台面。", AudioPriority.B, AudioCategory.Forge),
            new(AudioKey.BoardCancelDrag, "取消拖拽", "HydraulicMaterialBoard.CancelDragToOrigin()：取消拖拽归位。", AudioPriority.C, AudioCategory.Forge),
            new(AudioKey.BoardAnvilFull, "铁砧已满", "铁砧满 stack>=10 拒绝放置。", AudioPriority.B, AudioCategory.Forge),

            // ===== 液压锻造 - 材料个体 (#57) =====
            new(AudioKey.PieceHoverAnim, "材料悬浮", "HydraulicMaterialPiece 铁砧上正弦波悬浮动画（可选持续 hum）。", AudioPriority.C, AudioCategory.Forge),

            // ===== 液压锻造 - ForgeStationController (#58-61) =====
            new(AudioKey.ForgeTryForge, "确认锻造", "ForgeStationController.TryForge()：点击开始锻造。", AudioPriority.A, AudioCategory.Forge),
            new(AudioKey.ForgeExit, "退出锻造台", "ForgeStationController 点击退出。", AudioPriority.B, AudioCategory.Forge),
            new(AudioKey.ForgeTogglePanel, "切换矿石面板", "ForgeStationController.Toggle()：矿石仓库面板展开/收起。", AudioPriority.B, AudioCategory.Forge),
            new(AudioKey.ForgeEmptyWarning, "空锻造警告", "ForgeStationController 空锻造警告提示。", AudioPriority.B, AudioCategory.Forge),

            // ===== 开场演出 - ProloguePerformance (#62-65) =====
            new(AudioKey.PrologueSeaHold, "序章海面等待", "ProloguePerformance initialSeaHold 等待 1s 空海面。", AudioPriority.S, AudioCategory.Prologue),
            new(AudioKey.ProloguePlayerSailIn, "玩家船驶入", "ProloguePerformance.SailIn(player) DOTween 1s。", AudioPriority.S, AudioCategory.Prologue),
            new(AudioKey.PrologueDialogueStart, "序章对话开始", "ProloguePerformance.PlayDialogue()：对话系统开启。", AudioPriority.A, AudioCategory.Prologue),
            new(AudioKey.PrologueEnemySailIn, "敌船驶入", "ProloguePerformance.SailIn(enemy) DOTween 1s。", AudioPriority.S, AudioCategory.Prologue),

            // ===== 对话系统 (#66-71) =====
            new(AudioKey.DialogueOpen, "对话面板展开", "DialogueSystem.OpenWithPresentation → DialogueOpened。", AudioPriority.A, AudioCategory.Prologue),
            new(AudioKey.DialogueTextTyping, "打字机音效", "DialogueSystem.PresentText → LineStarted 文字逐字显现。", AudioPriority.A, AudioCategory.Prologue),
            new(AudioKey.DialogueCharacterSwitch, "角色切换", "DialogueSystem 角色立绘更换。", AudioPriority.C, AudioCategory.Prologue),
            new(AudioKey.DialogueAdvance, "对话推进", "DialogueSystem.Advance() 点击推进下一句。", AudioPriority.B, AudioCategory.Prologue),
            new(AudioKey.DialogueClose, "对话面板收起", "DialogueSystem.CloseAnimated → DialogueClosed。", AudioPriority.A, AudioCategory.Prologue),
            new(AudioKey.DialogueSequenceComplete, "对话序列完成", "DialogueSystem.SequenceCompleted。", AudioPriority.B, AudioCategory.Prologue),

            // ===== 通知系统 (#72-73) =====
            new(AudioKey.NoticeShown, "通知显示", "NoticeSystem.Show → NoticeShown。", AudioPriority.B, AudioCategory.UI),
            new(AudioKey.NoticeHidden, "通知隐藏", "NoticeSystem.Hide/自动隐藏 → NoticeHidden。", AudioPriority.C, AudioCategory.UI),

            // ===== UI - 敌方面板 (#74-76) =====
            new(AudioKey.EnemyPanelShow, "敌方面板滑入", "EnemyIntroducePanelController.EnterCompleted。", AudioPriority.A, AudioCategory.UI),
            new(AudioKey.EnemyPanelHide, "敌方面板滑出", "EnemyIntroducePanelController.ExitCompleted。", AudioPriority.B, AudioCategory.UI),
            new(AudioKey.EnemyPanelHoverDescription, "敌方描述悬停", "EnemyIntroducePanelController.ShowDescriptionImmediate 悬停触发。", AudioPriority.C, AudioCategory.UI),

            // ===== UI - 岛屿 (#77-80) =====
            new(AudioKey.IslandOpenRefinery, "打开精炼厂", "IslandController.OpenRefinery() 面板滑入。", AudioPriority.B, AudioCategory.UI),
            new(AudioKey.IslandOpenShipyard, "打开船坞", "IslandController.OpenShipyard() 面板滑入。", AudioPriority.B, AudioCategory.UI),
            new(AudioKey.IslandPanelSwitch, "岛屿面板切换", "IslandController 旧面板滑出。", AudioPriority.C, AudioCategory.UI),
            new(AudioKey.IslandLeave, "离开岛屿", "IslandController.Leave()。", AudioPriority.A, AudioCategory.UI),

            // ===== UI - 路线选择 (#81-82) =====
            new(AudioKey.RouteHover, "路线悬停", "RouteController 悬停事件点揭示描述。", AudioPriority.B, AudioCategory.UI),
            new(AudioKey.RouteSelect, "路线确认", "RouteController.SelectEvent() 确认选择。", AudioPriority.A, AudioCategory.UI),

            // ===== UI - 主菜单 (#83) =====
            new(AudioKey.MainMenuStart, "开始游戏", "MainMenuController.StartGame() 点击开始。", AudioPriority.A, AudioCategory.UI),

            // ===== UI - 矿石仓库 (#84-85) =====
            new(AudioKey.OreInventoryOpen, "矿石面板打开", "OreInventoryPanel.Open()。", AudioPriority.B, AudioCategory.UI),
            new(AudioKey.OreInventoryClose, "矿石面板关闭", "OreInventoryPanel.Close()。", AudioPriority.C, AudioCategory.UI),

            // ===== UI - 悬停通知 (#86) =====
            new(AudioKey.HoverNoticeShow, "悬停通知", "HoverNoticePresenter.Show/HideIfMine。", AudioPriority.C, AudioCategory.UI),

            // ===== 选中 & 视觉反馈 (#87-90) =====
            new(AudioKey.SelectableHoverEnter, "可选元素悬停进入", "SelectableSceneElement.SetHovered(true)。", AudioPriority.B, AudioCategory.Selection),
            new(AudioKey.SelectableHoverExit, "可选元素悬停退出", "SelectableSceneElement.SetHovered(false)。", AudioPriority.C, AudioCategory.Selection),
            new(AudioKey.SelectableSelected, "可选元素选中", "SelectableSceneElement.SetSelected(true)。", AudioPriority.A, AudioCategory.Selection),
            new(AudioKey.PointerHoverChange, "指针悬停切换", "SceneElementPointerSelector 悬停目标切换。", AudioPriority.C, AudioCategory.Selection),

            // ===== 环境音 & BGM (#91-98) =====
            new(AudioKey.BgmBattle, "战斗 BGM", "战斗场景期间循环播放。现有素材：常规战斗配乐.mp3 / Boss战斗配乐.mp3。", AudioPriority.S, AudioCategory.Environment, LoopMode.Loop),
            new(AudioKey.AmbientBattle, "战斗环境音", "战斗场景海洋+风声 loop 叠加在 BGM 下层。", AudioPriority.A, AudioCategory.Environment, LoopMode.Loop),
            new(AudioKey.BgmIsland, "岛屿环境循环", "IslandScene 期间循环。素材：平稳的海量音效.mp3。", AudioPriority.A, AudioCategory.Environment, LoopMode.Loop),
            new(AudioKey.BgmBoss, "Boss 战 BGM", "MainScene BossBattle 状态专属。素材：Boss战斗配乐.mp3。", AudioPriority.S, AudioCategory.Environment, LoopMode.Loop),
            new(AudioKey.BgmRoute, "路线选择 BGM", "路线选择期间 loop。现有素材：路线选择音乐.mp3。", AudioPriority.B, AudioCategory.Environment, LoopMode.Loop),
            new(AudioKey.BgmMainMenu, "主菜单 BGM", "主菜单期间 loop。", AudioPriority.A, AudioCategory.Environment, LoopMode.Loop),
            new(AudioKey.AmbientForge, "锻造环境音", "锻造 Active 期间蒸汽/机械嗡鸣 loop。", AudioPriority.A, AudioCategory.Environment, LoopMode.Loop),
            new(AudioKey.LoopChainTaut, "锁链张力 loop", "AnchorChainLauncher HoldTaut/Phase.Tightening 期间持续。", AudioPriority.B, AudioCategory.Environment, LoopMode.Loop),
            new(AudioKey.AmbientOcean, "海面环境底噪", "InfiniteScrollingBackground 始终播放的极轻 loop。现有素材：平稳的海量音效.mp3。", AudioPriority.C, AudioCategory.Environment, LoopMode.Loop),

            // ===== 数据驱动 - 未来 (#99-106) =====
            new(AudioKey.OreTraitTriggered, "矿石特性触发", "OreDataSO traits 激活（淬火/余烬/双生/核心等）。", AudioPriority.A, AudioCategory.DataDriven),
            new(AudioKey.HullModPurchased, "船体改造购买", "HullModDataSO 购买改造。", AudioPriority.A, AudioCategory.DataDriven),
            new(AudioKey.HullModEffectActivated, "改造效果激活", "HullModType 效果生效。", AudioPriority.A, AudioCategory.DataDriven),
            new(AudioKey.CoinGained, "获得金币", "经济系统获得金币（未实现）。", AudioPriority.B, AudioCategory.DataDriven),
            new(AudioKey.CoinSpent, "花费金币", "经济系统花费金币（未实现）。", AudioPriority.B, AudioCategory.DataDriven),
            new(AudioKey.EventChoiceSelected, "事件选择", "事件系统做出选择（未实现）。", AudioPriority.A, AudioCategory.DataDriven),
            new(AudioKey.RewardGranted, "获得奖励", "奖励结算（未实现）。", AudioPriority.A, AudioCategory.DataDriven),
            new(AudioKey.NodeEntered, "进入节点", "地图节点进入（未实现）。", AudioPriority.A, AudioCategory.DataDriven),
        };
    }
}
