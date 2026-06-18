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
        GoldCostOffset
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
        Treasure,
        Event
    }
}
