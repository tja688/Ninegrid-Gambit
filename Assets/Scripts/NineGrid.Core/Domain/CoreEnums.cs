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
        Item
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
        FirstStrike,
        GoldArmorAbsorb,
        AttackTargetRestriction
    }

    public enum BoardMarkId
    {
        None,
        Blessed
    }

    public enum RoomKind
    {
        None,
        Battle,
        Elite,
        Boss,
        Shop,
        Tavern,
        Fountain,
        Gold,
        Treasure,
        Event
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
        OnNodeStart,
        OnNodeEnd,
        OnActionRejected
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
        SkillGranted,
        BaseStatModified,
        RelicGranted,
        RewardOffered,
        RoomResolved,
        BoardMarked,
        ContentLoaded
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
        NodeCompleted
    }

    public enum GameCommandKind
    {
        StartNode,
        Attack,
        PickupItem,
        ClickEmpty,
        UseItem
    }
}
