namespace NineGrid.Cards
{
    /// <summary>
    /// 场地交战场景视图适配：仅暴露 Adapter 与 Encounter Catalog。
    /// busy / CTS / Present 编排由 <see cref="NineGrid.Presentation.Systems.IFieldBattlePresentationSystem"/> 持有。
    /// </summary>
    public interface IFieldBattleView
    {
        CardAttackBasicAdapter AttackAdapter { get; }

        BattleEncounterCatalogSO EncounterCatalog { get; }

        void EnsureAttackAdapter();
    }
}
