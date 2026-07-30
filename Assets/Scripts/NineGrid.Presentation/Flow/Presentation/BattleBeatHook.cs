using System;
using NineGrid.Core;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 战斗锚点报点桥：编排 / 命中帧报点 → 排期器，由组合根注入，禁止业务旁路写卡面。
    /// </summary>
    public static class BattleBeatHook
    {
        public static Action<PresentationBatch> OnBatchOpened;
        public static Action<PresentationBeat> ReportBeat;
        public static Action<PresentationBatch> PresentStandalone;

        /// <summary>只冲刷 pending 的 UpdateFaceUp（PresentStep 通道 Begin 前）。</summary>
        public static Action FlushUpdateFaceUp;

        public static void Reset()
        {
            OnBatchOpened = null;
            ReportBeat = null;
            PresentStandalone = null;
            FlushUpdateFaceUp = null;
        }

        public static void NotifyBatchOpened(PresentationBatch batch)
        {
            OnBatchOpened?.Invoke(batch);
        }

        public static void NotifyBeat(PresentationBeat beat)
        {
            ReportBeat?.Invoke(beat);
        }

        public static void NotifyPresentStandalone(PresentationBatch batch)
        {
            PresentStandalone?.Invoke(batch);
        }

        public static void NotifyFlushUpdateFaceUp()
        {
            FlushUpdateFaceUp?.Invoke();
        }
    }
}
