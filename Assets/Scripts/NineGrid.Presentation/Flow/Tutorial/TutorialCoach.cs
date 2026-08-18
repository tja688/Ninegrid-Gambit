using System;
using NineGrid.Cards;
using NineGrid.Flow.InfoNotice;
using NineGrid.Presentation;
using UnityEngine;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教程入口种类。
    /// </summary>
    public enum TutorialEntryKind
    {
        /// <summary>自然对局（跑图开局）。按生涯标记进行门教学抑制。</summary>
        Natural = 0,

        /// <summary>主菜单「教程」入口。单场教学，不走生涯门教学抑制，每次都走完整流程。</summary>
        Menu = 1,
    }

    /// <summary>
    /// 教程教练覆层意图。
    /// </summary>
    public struct TutorialOverlayIntent
    {
        public string Text;
        public InfoNoticeHoldMode HoldMode;
        public int TargetSlot;
        public ManagedCard TargetCard;
        public bool IsBlockingMainline;

        public bool IsActive => !string.IsNullOrEmpty(Text);

        public static TutorialOverlayIntent Empty => default;
    }

    /// <summary>
    /// 教程教练：作为战斗节拍与教程覆层之间的唯一策略缝。
    /// 消费节拍（如离开机关就位、推进点击等），产出覆层意图（弹窗文案、保持方式、框选目标、是否卡住主流程）。
    /// 
    /// 门教学（第 10 步）：
    /// - 独立于步骤 1–9 线性状态机。
    /// - 自然对局生涯首次见到离开机关（trap.leave）就位后卡住、框门并打出第 10 句；点击推进即写已见标记。
    /// - 若在见到门前战败，标记保持 false，后续自然对局首次见门仍会提示。
    /// - 主菜单「教程」入口每次遇到离开机关就位均完整触发第 10 步。
    /// </summary>
    public static class TutorialCoach
    {
        public const string Step10Text = "可以打破门来离开，清理掉所有怪物再离开会自动变卖场上的道具";
        public const string DoorExternalHoldReason = "TutorialCoachStep10Door";

        private static TutorialEntryKind sEntryKind = TutorialEntryKind.Natural;
        private static bool sIsDoorTutorialActive;
        private static bool sIsBlockingMainline;
        private static bool sDoorSettledTriggeredThisBattle;
        private static int sActiveDoorSlot = -1;
        private static ManagedCard sActiveDoorCard;

        /// <summary>当前教程入口种类。</summary>
        public static TutorialEntryKind EntryKind => sEntryKind;

        /// <summary>当前是否处于门教学（第 10 步）卡点状态。</summary>
        public static bool IsDoorTutorialActive => sIsDoorTutorialActive;

        /// <summary>当前是否正在卡住主流程（阻断玩家常规棋盘动作）。</summary>
        public static bool IsBlockingMainline => sIsBlockingMainline;

        /// <summary>设置或重置当前战斗入口模式。</summary>
        public static void SetEntryKind(TutorialEntryKind entryKind)
        {
            sEntryKind = entryKind;
        }

        /// <summary>战斗开始或重开时复位单局内状态。</summary>
        public static void OnBattleStarted(TutorialEntryKind entryKind)
        {
            sEntryKind = entryKind;
            sDoorSettledTriggeredThisBattle = false;
            DismissActiveCoachStep(releaseInput: true, silent: true);
        }

        /// <summary>战斗结束时清理状态。</summary>
        public static void OnBattleEnded()
        {
            sDoorSettledTriggeredThisBattle = false;
            DismissActiveCoachStep(releaseInput: true, silent: true);
        }

        /// <summary>
        /// 纯策略判定：输入入口种类与生涯已见标记，计算是否应触发门教学及对应覆层意图。
        /// 供 EditMode 单元测试直接断言策略缝。
        /// </summary>
        public static bool EvaluateDoorTutorialPolicy(
            TutorialEntryKind entryKind,
            bool isDoorSeenInSave,
            int doorSlot,
            ManagedCard doorCard,
            out TutorialOverlayIntent intent)
        {
            var shouldTrigger = entryKind == TutorialEntryKind.Menu || !isDoorSeenInSave;
            if (shouldTrigger)
            {
                intent = new TutorialOverlayIntent
                {
                    Text = Step10Text,
                    HoldMode = InfoNoticeHoldMode.ClickToAdvance,
                    TargetSlot = doorSlot,
                    TargetCard = doorCard,
                    IsBlockingMainline = true,
                };
                return true;
            }

            intent = TutorialOverlayIntent.Empty;
            return false;
        }

        /// <summary>
        /// 节拍：离开机关在盘面上就位（开局发牌或补牌飞行着陆完成后触发）。
        /// </summary>
        public static bool NotifyLeaveTrapSettled(int slot, ManagedCard card = null)
        {
            if (sDoorSettledTriggeredThisBattle)
            {
                return false;
            }

            var isSeen = TutorialProgressStore.IsDoorTutorialSeen();
            if (!EvaluateDoorTutorialPolicy(sEntryKind, isSeen, slot, card, out var intent))
            {
                return false;
            }

            sDoorSettledTriggeredThisBattle = true;
            sIsDoorTutorialActive = true;
            sIsBlockingMainline = true;
            sActiveDoorSlot = slot;
            sActiveDoorCard = card;

            // 1. 顶部信息弹窗：点了才走
            InfoNoticePresenter.ShowClickToAdvance(intent.Text);

            // 2. 教学提示框：框住门
            if (card != null)
            {
                TutorialPromptBoxPresenter.ShowTarget(card);
            }
            else if (slot >= 1 && slot <= 9)
            {
                TutorialPromptBoxPresenter.ShowTarget(slot);
            }

            // 3. 卡住主流程输入
            PresentationInputGates.TryBeginExternalHold(DoorExternalHoldReason);

            Debug.Log($"[TutorialCoach] 触发门教学（第10步）：slot={slot}, entryKind={sEntryKind}, isSeen={isSeen}");
            return true;
        }

        /// <summary>
        /// 玩家主键点击推进：若当前处于卡点状态，则消耗本次点击推进教练并解除卡点。
        /// </summary>
        public static bool TryConsumeAdvance()
        {
            if (!sIsBlockingMainline && !sIsDoorTutorialActive)
            {
                return false;
            }

            if (sIsDoorTutorialActive)
            {
                CompleteDoorTutorial();
                return true;
            }

            return false;
        }

        private static void CompleteDoorTutorial()
        {
            sIsDoorTutorialActive = false;
            sIsBlockingMainline = false;
            sActiveDoorSlot = -1;
            sActiveDoorCard = null;

            InfoNoticePresenter.DismissHold();
            TutorialPromptBoxPresenter.HidePrompt();
            PresentationInputGates.EndExternalHold(DoorExternalHoldReason);

            // 提示结束即写门已见，不要求击破门
            TutorialProgressStore.MarkDoorTutorialSeen();
            Debug.Log("[TutorialCoach] 门教学完成，已写入门已见标记。");
        }

        private static void DismissActiveCoachStep(bool releaseInput, bool silent)
        {
            sIsDoorTutorialActive = false;
            sIsBlockingMainline = false;
            sActiveDoorSlot = -1;
            sActiveDoorCard = null;

            if (!silent)
            {
                InfoNoticePresenter.DismissHold();
                TutorialPromptBoxPresenter.HidePrompt();
            }

            if (releaseInput)
            {
                PresentationInputGates.ForceEndExternalHold(DoorExternalHoldReason);
            }
        }

        /// <summary>测试复位。</summary>
        public static void ResetForTests()
        {
            sEntryKind = TutorialEntryKind.Natural;
            sDoorSettledTriggeredThisBattle = false;
            DismissActiveCoachStep(releaseInput: true, silent: true);
        }
    }
}
