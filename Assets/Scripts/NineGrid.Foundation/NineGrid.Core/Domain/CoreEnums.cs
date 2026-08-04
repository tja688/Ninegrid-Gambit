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
        UntilEnemyChanges
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
        OnFlip
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
        CardFaceChanged,
        AvatarMoved
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
        ResolvePostKillFill,
        ResolvePostKillRotate,
        RegisterEnemyActionPhase,
        ResolveNextEnemyAction,
        ResolveEnemyActionFinale,
        ResolveFusionRefill,
        ResolveDrainRefill,
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
        Navigation
    }
}
