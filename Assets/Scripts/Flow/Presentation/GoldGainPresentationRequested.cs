using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>金币增减的单向表现请求；UI/FX 只消费，不改 PlayerModel。</summary>
    public struct GoldGainPresentationRequested
    {
        public int Delta;
        public int AmountAfter;
        public string Reason;
        public string SourceDefId;
        public string ActionName;
        public Vector3? OriginWorld;
        public bool IsSpend;
    }
}
