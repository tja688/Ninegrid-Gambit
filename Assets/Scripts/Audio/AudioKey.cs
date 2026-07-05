namespace NineGrid.Audio
{
    /// <summary>
    /// 全部 106 个音效打点键值，按系统类别分组。
    /// 编号与 Assets/Docs/音效打点探查文档.md 一一对应。
    /// </summary>
    public enum AudioKey
    {
        // ===== 场景流转 & 全局状态机 (#1-7) =====
        SceneStateChanged = 1,
        RunStarted = 2,
        PlayerDefeated = 3,
        VictorySettled = 4,
        TransitionFadeOut = 5,
        TransitionFadeIn = 6,
        RunPassThroughVictory = 7,

        // ===== 战斗系统 - BattleController (#8-14) =====
        BattleEnter = 8,
        BattleExit = 9,
        BattleFinished = 10,
        EnterForgeMode = 11,
        EnterAnchorMode = 12,
        NoticeWarning = 13,
        RamSucceeded = 14,

        // ===== 战斗系统 - AnchorRammingController (#15-20) =====
        RamBegin = 15,
        CrankBegin = 16,
        PreImpact = 17,
        Impact = 18,
        Bounce = 19,
        RamEnd = 20,

        // ===== 战斗系统 - WinchCrankController (#21-24) =====
        WinchClicked = 21,
        WinchBeginCrank = 22,
        WinchEndCrank = 23,
        WinchIntensityDecay = 24,

        // ===== 战斗系统 - AnchorChainLauncher (#25-31) =====
        ChainFire = 25,
        ChainFlying = 26,
        ChainSettling = 27,
        ChainTightening = 28,
        ChainAttached = 29,
        ChainReset = 30,
        ChainHoldTaut = 31,

        // ===== 战斗系统 - AnchorHpTracker (#32-34) =====
        AnchorHpResetFull = 32,
        AnchorHpConsume = 33,
        AnchorHpEmpty = 34,

        // ===== 战斗系统 - BoreManager (#35-36) =====
        BoreShow = 35,
        BoreHideAll = 36,

        // ===== 战斗系统 - 相机效果 (#37-38) =====
        ScreenShakePlay = 37,
        CameraZoomBegin = 38,

        // ===== 液压锻造系统 - HydraulicSceneController (#39-47) =====
        HydraulicEnterStart = 39,
        HydraulicEnterComplete = 40,
        HydraulicExit = 41,
        HydraulicPreForge = 42,
        HydraulicHammerFall = 43,
        HydraulicHammerImpact = 44,
        HydraulicHammerHold = 45,
        HydraulicHammerRise = 46,
        HydraulicCompleted = 47,

        // ===== 液压锻造 - 材料管道 (#48-50) =====
        LaneDeliver = 48,
        LaneDeliverLand = 49,
        LaneReset = 50,

        // ===== 液压锻造 - 铁砧拖放 (#51-56) =====
        BoardBeginDrag = 51,
        BoardPlaceOnAnvil = 52,
        BoardRelayoutAnvil = 53,
        BoardPlaceOnTable = 54,
        BoardCancelDrag = 55,
        BoardAnvilFull = 56,

        // ===== 液压锻造 - 材料个体 (#57) =====
        PieceHoverAnim = 57,

        // ===== 液压锻造 - ForgeStationController (#58-61) =====
        ForgeTryForge = 58,
        ForgeExit = 59,
        ForgeTogglePanel = 60,
        ForgeEmptyWarning = 61,

        // ===== 开场演出 - ProloguePerformance (#62-65) =====
        PrologueSeaHold = 62,
        ProloguePlayerSailIn = 63,
        PrologueDialogueStart = 64,
        PrologueEnemySailIn = 65,

        // ===== 对话系统 - DialogueSystem (#66-71) =====
        DialogueOpen = 66,
        DialogueTextTyping = 67,
        DialogueCharacterSwitch = 68,
        DialogueAdvance = 69,
        DialogueClose = 70,
        DialogueSequenceComplete = 71,

        // ===== 通知系统 - NoticeSystem (#72-73) =====
        NoticeShown = 72,
        NoticeHidden = 73,

        // ===== UI - 敌方面板 (#74-76) =====
        EnemyPanelShow = 74,
        EnemyPanelHide = 75,
        EnemyPanelHoverDescription = 76,

        // ===== UI - 岛屿场景 (#77-80) =====
        IslandOpenRefinery = 77,
        IslandOpenShipyard = 78,
        IslandPanelSwitch = 79,
        IslandLeave = 80,

        // ===== UI - 路线选择 (#81-82) =====
        RouteHover = 81,
        RouteSelect = 82,

        // ===== UI - 主菜单 (#83) =====
        MainMenuStart = 83,

        // ===== UI - 矿石仓库面板 (#84-85) =====
        OreInventoryOpen = 84,
        OreInventoryClose = 85,

        // ===== UI - 悬停通知 (#86) =====
        HoverNoticeShow = 86,

        // ===== 选中 & 视觉反馈 (#87-90) =====
        SelectableHoverEnter = 87,
        SelectableHoverExit = 88,
        SelectableSelected = 89,
        PointerHoverChange = 90,

        // ===== 环境音 & BGM (#91-98) =====
        BgmBattle = 91,
        AmbientBattle = 92,
        BgmIsland = 93,
        BgmRoute = 94,
        BgmMainMenu = 95,
        AmbientForge = 96,
        LoopChainTaut = 97,
        AmbientOcean = 98,

        // ===== 数据驱动 - 未来接入 (#99-106) =====
        OreTraitTriggered = 99,
        HullModPurchased = 100,
        HullModEffectActivated = 101,
        CoinGained = 102,
        CoinSpent = 103,
        EventChoiceSelected = 104,
        RewardGranted = 105,
        NodeEntered = 106,
    }
}
