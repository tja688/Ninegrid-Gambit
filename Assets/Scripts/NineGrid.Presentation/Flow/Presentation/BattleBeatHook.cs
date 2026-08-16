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

        /// <summary>冲刷 Impact 但跳过指定 Kind（ADR-0018：Vacate 前飘字、TriggerEffect 延迟）。</summary>
        public static Action<PresentationInstructionKind> FlushImpactExcept;

        /// <summary>只冲刷指定 Kind 的 Impact，返回派发条数（ADR-0048 两相 Impact 节拍）。</summary>
        public static Func<PresentationInstructionKind, int> FlushImpactOnly;

        /// <summary>隔离满足谓词的 Impact 指令（神圣决斗惩罚留给决斗者攻击帧）。</summary>
        public static Action<Func<PresentationInstruction, bool>> QuarantineImpactWhere;

        /// <summary>放回隔离区指令（决斗者攻击命中帧报点前）。</summary>
        public static Action ReleaseQuarantined;

        /// <summary>按谓词放回隔离区中的部分指令（多决斗者逐个命中帧）。</summary>
        public static Action<Func<PresentationInstruction, bool>> ReleaseQuarantinedWhere;

        /// <summary>效果打击暂扣：把满足谓词的 Impact 指令移入打击暂扣区（ADR-0050）。</summary>
        public static Action<Func<PresentationInstruction, bool>> HoldStrikeImpactWhere;

        /// <summary>冲刷打击暂扣区中满足谓词的指令（打击组命中帧），返回派发条数（ADR-0050）。</summary>
        public static Func<Func<PresentationInstruction, bool>, int> FlushStrikeHeldWhere;

        public static void Reset()
        {
            OnBatchOpened = null;
            ReportBeat = null;
            PresentStandalone = null;
            FlushUpdateFaceUp = null;
            FlushImpactExcept = null;
            FlushImpactOnly = null;
            QuarantineImpactWhere = null;
            ReleaseQuarantined = null;
            ReleaseQuarantinedWhere = null;
            HoldStrikeImpactWhere = null;
            FlushStrikeHeldWhere = null;
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

        public static void NotifyFlushImpactExcept(PresentationInstructionKind excludedKind)
        {
            FlushImpactExcept?.Invoke(excludedKind);
        }

        public static int NotifyFlushImpactOnly(PresentationInstructionKind onlyKind)
        {
            return FlushImpactOnly != null ? FlushImpactOnly(onlyKind) : 0;
        }

        public static void NotifyQuarantineImpactWhere(Func<PresentationInstruction, bool> predicate)
        {
            QuarantineImpactWhere?.Invoke(predicate);
        }

        public static void NotifyReleaseQuarantined()
        {
            ReleaseQuarantined?.Invoke();
        }

        public static void NotifyReleaseQuarantinedWhere(Func<PresentationInstruction, bool> predicate)
        {
            ReleaseQuarantinedWhere?.Invoke(predicate);
        }

        public static void NotifyHoldStrikeImpactWhere(Func<PresentationInstruction, bool> predicate)
        {
            HoldStrikeImpactWhere?.Invoke(predicate);
        }

        public static int NotifyFlushStrikeHeldWhere(Func<PresentationInstruction, bool> predicate)
        {
            return FlushStrikeHeldWhere != null ? FlushStrikeHeldWhere(predicate) : 0;
        }
    }
}
