namespace NineGrid.Core
{
    public enum CardKind
    {
        Unknown,
        Avatar,
        Monster,
        PlayerCard,
        Relic,
        HelpCard,
        Item,
        Trap
    }

    public enum ZoneId
    {
        None,
        Avatar,
        Board,
        DrawPile,
        PlayerCardPool,
        EnemyCardPool,
        ItemSlots,
        Graveyard,
        Removed
    }

    public enum StatId
    {
        MaxHp,
        Hp,
        Attack,
        Armor,
        CurrentArmor,
        Recovery,
        InteractionRange
    }

    public enum ModifierOp
    {
        Add,
        Multiply,
        Override
    }

    public enum ModifierLayer
    {
        Persistent,
        Conditional,
        Temporary
    }

    public enum ModifierScope
    {
        Permanent,
        UntilBattleEnds,
        Once,
        UntilEnemyChanges,
        /// <summary>关卡级临时修正；在下一关 StartNode 前清除（#116 废物增幅器等）。</summary>
        UntilNodeEnds
    }

    public enum RuleId
    {
        RecoveryMultiplier,
        InteractionDistance,
        EnemyAttackDelta,
        GoldCostOffset,
        DamageMultiplier,
        DamageFlatDelta,
        /// <summary>
        /// 伤害减免（ADR-0028）：受击方多源累加；标准公式在乘区/平板之后、护甲吸收之前减去。
        /// 不是 <see cref="DamageFlatDelta"/>，不上卡面三围 / HUD。
        /// </summary>
        DamageReduction,
        FirstStrike,
        GoldArmorAbsorb,
        AttackTargetRestriction,
        VirtualAdjacency,
        /// <summary>禁行：&gt;0 时敌方行动阶段资格复核失败（ADR-0012）。</summary>
        ActionBanned,
        /// <summary>远程武器：&gt;0 时玩家交战该怪，怪不先手也不反击（齐射不受影响）。</summary>
        CounterAttackBanned,
        /// <summary>神圣决斗：&gt;0 标记本卡为决斗持有者（玩家交战记忆挂玩家侧状态）。</summary>
        HolyDuel,
        /// <summary>天涯若比邻：&gt;0 时场上怪物彼此视为正交相邻（仅技能邻接；不影响攻击距离/齐射）。</summary>
        GlobalMonsterAdjacency,
        /// <summary>
        /// 门（ADR-0026）：&gt;0 时仅交战中玩家出手可造成伤害；且直接 <c>RemoveCard</c> 无效。
        /// 不挡洗回卡组 / 翻面；Kill 路径仍可经致命伤害走击破。
        /// </summary>
        DoorProtection,
        /// <summary>
        /// 魔免（ADR-0026）：&gt;0 时外来效果以本卡为对象时直接失效（目标可被选中，效果管线过滤掉本卡）。
        /// 不挡玩家交战伤害；不挡持有者自身效果（门 / 离开 / 魔免挂载）。
        /// </summary>
        MagicImmunity
    }

    public enum BoardMarkId
    {
        None,
        Blessed
    }

    public enum RoomKind
    {
        None,
        Elite,
        Boss,
        Shop,
        Tavern,
        Fountain,
        Gold,
        Treasure,
        Attribute,
        TreasureReward,
        ItemReward
    }

    public enum TriggerTiming
    {
        Pre,
        Post
    }

    public enum TriggerPoint
    {
        BeforeAction,
        /// <summary>盘面位移动作（旋转/换位/移牌）Apply 之前；借甲图腾等在此结算贷款，不订 OnMove 以免倒刺位移前开火。</summary>
        BeforeBoardMotion,
        AfterAction,
        OnBattle,
        OnDamage,
        OnHeal,
        OnArmorGained,
        OnGoldChanged,
        OnRemove,
        OnKill,
        OnDeal,
        OnRotate,
        OnMove,
        OnMoveToSlot,
        OnSwap,
        OnInteract,
        OnUseHelpCard,
        OnEnter,
        OnArmorBreak,
        OnDamageTaken,
        OnFatalDamage,
        OnCumulative,
        OnActivate,
        OnNodeStart,
        OnNodeEnd,
        OnActionRejected,
        OnFlip,
        /// <summary>卡级共享开火窗口（ADR-0038）；技能同步触发订阅此点。</summary>
        OnCardRhythmFire
    }

    public enum CoreEventType
    {
        ActionStarted,
        ActionFinished,
        ActionRejected,
        DamageDealt,
        HpChanged,
        ArmorChanged,
        Healed,
        GoldModified,
        CardRemoved,
        CardKilled,
        CardMoved,
        CardSwapped,
        BoardRotated,
        CardDealt,
        DrawPileExhausted,
        SlotsFilled,
        InteractionChanged,
        PhaseChanged,
        NodeStarted,
        NodeCompleted,
        ItemPicked,
        EmptyClicked,
        ItemUsed,
        EffectTriggered,
        EffectModifierApplied,
        EffectDeactivated,
        CardSpawned,
        AvatarAppeared,
        BaseStatModified,
        RelicGranted,
        RewardOffered,
        RewardSelected,
        RewardSkipped,
        RoomChoicesOffered,
        RoomSelected,
        RoomResolved,
        NodeAdvanced,
        BoardMarked,
        ContentLoaded,
        ActionCountdownChanged,
        EffectCountdownChanged,
        EffectCountdownCleared,
        CardFaceChanged,
        AvatarMoved,
        /// <summary>卡级开火窗口开启（ADR-0038）；驱动 OnCardRhythmFire。</summary>
        CardRhythmFireOpened,
        /// <summary>诊断：敌方行动窗口裁决痕迹（roster/fired/void*/skip*，#206）；不进表现批次。</summary>
        EnemyActionResolved,
        /// <summary>
        /// 诊断（ADR-0047）：反应链深度/总量熔断——失控触发环被遏制、后续分支丢弃，
        /// 命令仍原子收尾（不再抛异常炸穿命令导致 Core/表现分叉）。不进表现批次。
        /// </summary>
        PipelineFaultContained,
        /// <summary>
        /// 诊断：遗物效果挂载审计——授予/自愈路径每次 ActivateRelic 后落一条
        /// 「申报装配数 vs 实挂实例数 vs 修饰符数」，corelog 可直接判定遗物是否真正生效。
        /// 不进表现批次。
        /// </summary>
        RelicEffectMountAudited,
        /// <summary>
        /// 诊断：条件修饰符激活态采样——Conditional 层修饰符按查询惰性求值、
        /// 本身不发事件，「挂上了但条件从未满足」在日志里完全隐形（血液暴力排查痛点）。
        /// 动作边界采样盘面 + Avatar，首见与翻转时各落一条。不进表现批次。
        /// </summary>
        ConditionalModifierAudited
    }

    public enum PresentationEventCategory
    {
        ActionLifecycle,
        Rejection,
        Damage,
        Stat,
        Economy,
        Remove,
        Kill,
        Move,
        Rotate,
        Deal,
        Interaction,
        Phase,
        Node,
        Item,
        Effect,
        Reward,
        Room,
        Board,
        Content
    }

    public enum PresentationInstructionKind
    {
        None,
        MarkActionStarted,
        MarkActionFinished,
        ShowRejectedIntent,
        ShowDamage,
        UpdateHp,
        UpdateArmor,
        UpdateGold,
        RemoveCard,
        KillCard,
        MoveCard,
        SwapCards,
        RotateBoard,
        DealCard,
        ShowDrawPileExhausted,
        FillSlots,
        UpdateInteractionCount,
        ChangePhase,
        StartNode,
        CompleteNode,
        PickItem,
        ClickEmpty,
        UseItem,
        TriggerEffect,
        ApplyModifier,
        DeactivateEffect,
        SpawnCard,
        ShowAvatar,
        ModifyBaseStat,
        GrantRelic,
        OfferReward,
        SelectReward,
        SkipReward,
        OfferRooms,
        SelectRoom,
        ResolveRoom,
        AdvanceNode,
        MarkBoard,
        LoadContent,
        UpdateActionCount,
        UpdateCountdownRemaining,
        ClearCountdownRemaining,
        UpdateFaceUp,
        MoveAvatar
    }

    public enum GamePhase
    {
        None,
        BuildEnemyPool,
        ResetNode,
        DealOpeningCards,
        InteractionLoop,
        ClearCheck,
        RewardItemChoice,
        RoomChoice,
        RoomEvent,
        NodeCompleted,
        Victory,
        Defeat
    }

    public enum GameCommandKind
    {
        StartNode,
        Attack,
        CombatHit,
        ResolvePostKillBoard,
        AdvanceInteractionCount,
        ResolveBoardStabilization,
        ResolvePostKillRotate,
        RegisterEnemyActionPhase,
        ResolveNextEnemyAction,
        ResolveEnemyActionFinale,
        PickupItem,
        ClickEmpty,
        UseItem,
        ApplyUseItem,
        SelectReward,
        SkipHelpChoice,
        DiscardRelic,
        RecycleItemSlot,
        ApplyRecycleItemSlot,
        RefreshShop,
        SelectRoom,
        EnterRoom,
        RevealFace,
        MoveAvatar,
        PresentationFinished
    }

    public enum PendingChoiceKind
    {
        None,
        Reward,
        Room,
        Navigation,
        /// <summary>属性房三选二会话（#136）：候选 3 选 2，选满后结束会话。</summary>
        AttributePick
    }
}
