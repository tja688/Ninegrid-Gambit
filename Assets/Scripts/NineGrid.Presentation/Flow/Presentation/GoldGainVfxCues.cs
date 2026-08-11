namespace NineGrid.Flow.Presentation
{
    /// <summary>金币 VFX 稳定声明（#203）：gold-flight 播放器消费的唯一 Cue。</summary>
    public static class GoldGainVfxCues
    {
        [VfxCue("economy.gold_flight", "金币飞入演出", "Economy", "GoldGainPresentationBinder", VfxCueContexts.None)]
        public const string FlyIn = "economy.gold_flight";
    }
}
