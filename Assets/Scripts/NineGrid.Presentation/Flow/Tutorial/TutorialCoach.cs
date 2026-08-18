using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow.InfoNotice;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
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
    /// 教程教练步骤枚举。
    /// </summary>
    public enum TutorialCoachStep
    {
        None = 0,
        Step1_Welcome = 1,
        Step2_WarriorCleave = 2,
        Step3_Opening8Cards = 3,
        Step4_AttackMonster = 4,
        Step5_RefillAndRotate = 5,
        Step6_PickupPotion = 6,
        Step7_MonsterCountdowns = 7,
        Step8_InspectCards = 8,
        Step9_RuleBook = 9,
        Step10_LeaveDoor = 10,
        Completed = 11,
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
    /// 消费节拍（角色就位、开局铺场就位、推进点击、击杀后盘面稳定、道具拾取、离开机关就位等），
    /// 产出覆层意图（弹窗文案、保持方式、框选目标、白名单格位、是否卡住主流程）。
    /// </summary>
    public static class TutorialCoach
    {
        public const string Step1Text = "欢迎来到九宫地下城，在开始之前，让我们了解一些基础操作";
        public const string Step2Text = "这是你的初始角色，战士可以使用自带的顺劈斧进行范围攻击";
        public const string Step3Text = "每次开局都会发8张牌铺满场地";
        public const string Step4Text = "点击你交互范围内的敌方卡牌可以进行攻击并准备迎接敌方的反击";
        public const string Step5Text = "击杀后若场地缺牌会从卡组补充，并触发一次旋转";
        public const string Step6Text = "可以拾取道具卡，放入手牌并随时使用，手牌可以跨战斗保存";
        public const string Step7Text = "当心，待怪物行动计数归零后他们会主动发起效果或攻击";
        public const string Step8Text = "可以右键点击场地卡牌、手牌或者遗物来查看它们的详细描述";
        public const string Step9Text = "你可以点击规则书了解更多内容，祝你游戏愉快！";
        public const string Step10Text = "可以打破门来离开，清理掉所有怪物再离开会自动变卖场上的道具";

        public const string Steps1To9HoldReason = "TutorialCoachSteps1To9";
        public const string DoorExternalHoldReason = "TutorialCoachStep10Door";

        public const int FirstBattleTargetMonsterSlot = 6;
        public const int FirstBattleCollectiblePotionSlot = 6;

        private static TutorialEntryKind sEntryKind = TutorialEntryKind.Natural;
        private static bool sIsSteps1To9Active;
        private static TutorialCoachStep sCurrentStep = TutorialCoachStep.None;

        private static bool sIsDoorTutorialActive;
        private static bool sDoorSettledTriggeredThisBattle;
        private static int sActiveDoorSlot = -1;
        private static ManagedCard sActiveDoorCard;

        private static int sTargetSlot = -1;
        private static ManagedCard sTargetCard;

        private static UniTaskCompletionSource sAvatarIntroTcs;
        private static UniTaskCompletionSource sBoardIntroTcs;

        /// <summary>当前教程入口种类。</summary>
        public static TutorialEntryKind EntryKind => sEntryKind;

        /// <summary>当前步骤 1–9 是否处于激活中。</summary>
        public static bool IsSteps1To9Active => sIsSteps1To9Active;

        /// <summary>当前教练步骤。</summary>
        public static TutorialCoachStep CurrentStep => sCurrentStep;

        /// <summary>当前是否处于门教学（第 10 步）卡点状态。</summary>
        public static bool IsDoorTutorialActive => sIsDoorTutorialActive;

        /// <summary>当前是否正在卡住主流程（阻断玩家常规棋盘动作）。</summary>
        public static bool IsBlockingMainline
        {
            get
            {
                if (sIsDoorTutorialActive)
                {
                    return true;
                }

                if (!sIsSteps1To9Active)
                {
                    return false;
                }

                return IsStepBlockingMainline(sCurrentStep);
            }
        }

        /// <summary>当前右键详述是否允许打开（步骤 8 之前完全禁用）。</summary>
        public static bool IsRightClickInspectAllowed =>
            EvaluateRightClickInspectAllowed(sCurrentStep, sIsSteps1To9Active);

        /// <summary>当前规则书按钮是否允许打开（步骤 9 之前完全禁用）。</summary>
        public static bool IsRulebookAllowed =>
            EvaluateRulebookAllowed(sCurrentStep, sIsSteps1To9Active);

        /// <summary>当前目标格（有框选时）。</summary>
        public static int TargetSlot => sTargetSlot;

        /// <summary>当前目标卡（有框选时）。</summary>
        public static ManagedCard TargetCard => sTargetCard;

        /// <summary>设置或重置当前战斗入口模式。</summary>
        public static void SetEntryKind(TutorialEntryKind entryKind)
        {
            sEntryKind = entryKind;
        }

        /// <summary>
        /// 纯策略判定：步骤 1–9 是否应在当前对局激活。
        /// </summary>
        public static bool EvaluateShouldRunSteps1To9(
            TutorialEntryKind entryKind,
            bool isSteps1To9Completed,
            int nodeIndex)
        {
            if (entryKind == TutorialEntryKind.Menu)
            {
                return true;
            }

            if (entryKind == TutorialEntryKind.Natural)
            {
                return !isSteps1To9Completed && nodeIndex <= 1;
            }

            return false;
        }

        /// <summary>
        /// 纯策略判定：给定步骤输出对应的覆层意图。
        /// </summary>
        public static bool EvaluateStepIntent(
            TutorialCoachStep step,
            int targetSlot,
            ManagedCard targetCard,
            out TutorialOverlayIntent intent)
        {
            switch (step)
            {
                case TutorialCoachStep.Step1_Welcome:
                    intent = new TutorialOverlayIntent
                    {
                        Text = Step1Text,
                        HoldMode = InfoNoticeHoldMode.ClickToAdvance,
                        TargetSlot = targetSlot > 0 ? targetSlot : GroundSlotTopology.AvatarReservedSlot,
                        TargetCard = targetCard,
                        IsBlockingMainline = true,
                    };
                    return true;

                case TutorialCoachStep.Step2_WarriorCleave:
                    intent = new TutorialOverlayIntent
                    {
                        Text = Step2Text,
                        HoldMode = InfoNoticeHoldMode.ClickToAdvance,
                        TargetSlot = targetSlot > 0 ? targetSlot : GroundSlotTopology.AvatarReservedSlot,
                        TargetCard = targetCard,
                        IsBlockingMainline = true,
                    };
                    return true;

                case TutorialCoachStep.Step3_Opening8Cards:
                    intent = new TutorialOverlayIntent
                    {
                        Text = Step3Text,
                        HoldMode = InfoNoticeHoldMode.ClickToAdvance,
                        TargetSlot = -1,
                        TargetCard = null,
                        IsBlockingMainline = true,
                    };
                    return true;

                case TutorialCoachStep.Step4_AttackMonster:
                    intent = new TutorialOverlayIntent
                    {
                        Text = Step4Text,
                        HoldMode = InfoNoticeHoldMode.ClickToAdvance,
                        TargetSlot = targetSlot > 0 ? targetSlot : FirstBattleTargetMonsterSlot,
                        TargetCard = targetCard,
                        IsBlockingMainline = false,
                    };
                    return true;

                case TutorialCoachStep.Step5_RefillAndRotate:
                    intent = new TutorialOverlayIntent
                    {
                        Text = Step5Text,
                        HoldMode = InfoNoticeHoldMode.ClickToAdvance,
                        TargetSlot = -1,
                        TargetCard = null,
                        IsBlockingMainline = true,
                    };
                    return true;

                case TutorialCoachStep.Step6_PickupPotion:
                    intent = new TutorialOverlayIntent
                    {
                        Text = Step6Text,
                        HoldMode = InfoNoticeHoldMode.ClickToAdvance,
                        TargetSlot = targetSlot > 0 ? targetSlot : FirstBattleCollectiblePotionSlot,
                        TargetCard = targetCard,
                        IsBlockingMainline = false,
                    };
                    return true;

                case TutorialCoachStep.Step7_MonsterCountdowns:
                    intent = new TutorialOverlayIntent
                    {
                        Text = Step7Text,
                        HoldMode = InfoNoticeHoldMode.ClickToAdvance,
                        TargetSlot = -1,
                        TargetCard = null,
                        IsBlockingMainline = true,
                    };
                    return true;

                case TutorialCoachStep.Step8_InspectCards:
                    intent = new TutorialOverlayIntent
                    {
                        Text = Step8Text,
                        HoldMode = InfoNoticeHoldMode.ClickToAdvance,
                        TargetSlot = -1,
                        TargetCard = null,
                        IsBlockingMainline = true,
                    };
                    return true;

                case TutorialCoachStep.Step9_RuleBook:
                    intent = new TutorialOverlayIntent
                    {
                        Text = Step9Text,
                        HoldMode = InfoNoticeHoldMode.ClickToAdvance,
                        TargetSlot = -1,
                        TargetCard = null,
                        IsBlockingMainline = true,
                    };
                    return true;

                case TutorialCoachStep.Step10_LeaveDoor:
                    intent = new TutorialOverlayIntent
                    {
                        Text = Step10Text,
                        HoldMode = InfoNoticeHoldMode.ClickToAdvance,
                        TargetSlot = targetSlot,
                        TargetCard = targetCard,
                        IsBlockingMainline = true,
                    };
                    return true;

                default:
                    intent = TutorialOverlayIntent.Empty;
                    return false;
            }
        }

        /// <summary>
        /// 纯策略判定：当前步骤是否卡住主流程。
        /// </summary>
        public static bool IsStepBlockingMainline(TutorialCoachStep step)
        {
            switch (step)
            {
                case TutorialCoachStep.Step1_Welcome:
                case TutorialCoachStep.Step2_WarriorCleave:
                case TutorialCoachStep.Step3_Opening8Cards:
                case TutorialCoachStep.Step5_RefillAndRotate:
                case TutorialCoachStep.Step7_MonsterCountdowns:
                case TutorialCoachStep.Step8_InspectCards:
                case TutorialCoachStep.Step9_RuleBook:
                case TutorialCoachStep.Step10_LeaveDoor:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 纯策略判定：右键详述是否放行。
        /// </summary>
        public static bool EvaluateRightClickInspectAllowed(
            TutorialCoachStep step,
            bool isSteps1To9Active)
        {
            if (!isSteps1To9Active)
            {
                return true;
            }

            return step >= TutorialCoachStep.Step8_InspectCards
                   || step == TutorialCoachStep.Step10_LeaveDoor
                   || step == TutorialCoachStep.Completed;
        }

        /// <summary>
        /// 纯策略判定：规则书按钮是否放行。
        /// </summary>
        public static bool EvaluateRulebookAllowed(
            TutorialCoachStep step,
            bool isSteps1To9Active)
        {
            if (!isSteps1To9Active)
            {
                return true;
            }

            return step >= TutorialCoachStep.Step9_RuleBook
                   || step == TutorialCoachStep.Step10_LeaveDoor
                   || step == TutorialCoachStep.Completed;
        }

        /// <summary>
        /// 纯策略判定：意图是否在当前教练步骤合法。
        /// </summary>
        public static bool EvaluateIntentLegality(
            TutorialCoachStep step,
            bool isSteps1To9Active,
            InputIntent intent,
            out string rejectReason)
        {
            rejectReason = null;
            if (!isSteps1To9Active)
            {
                return true;
            }

            if (step == TutorialCoachStep.Step4_AttackMonster)
            {
                if (intent.TargetId == FirstBattleTargetMonsterSlot
                    && string.Equals(intent.Kind, InputIntentKinds.Attack, StringComparison.Ordinal))
                {
                    return true;
                }

                rejectReason = "tutorialRestrictedTarget";
                return false;
            }

            if (step == TutorialCoachStep.Step6_PickupPotion)
            {
                if (intent.TargetId == FirstBattleCollectiblePotionSlot
                    && string.Equals(intent.Kind, InputIntentKinds.Pickup, StringComparison.Ordinal))
                {
                    return true;
                }

                rejectReason = "tutorialRestrictedTarget";
                return false;
            }

            if (IsStepBlockingMainline(step))
            {
                rejectReason = "tutorialMainlineBlocked";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 供 IntentIntake 调用的运行时意图过滤入口。
        /// </summary>
        public static bool IsIntentAllowed(InputIntent intent, out string rejectReason)
        {
            return EvaluateIntentLegality(sCurrentStep, sIsSteps1To9Active, intent, out rejectReason);
        }

        /// <summary>战斗开始或重开时复位单局内状态。</summary>
        public static void OnBattleStarted(TutorialEntryKind entryKind, int nodeIndex = 1)
        {
            sEntryKind = entryKind;
            sDoorSettledTriggeredThisBattle = false;
            var is1To9Done = TutorialProgressStore.IsSteps1To9Completed();
            sIsSteps1To9Active = EvaluateShouldRunSteps1To9(entryKind, is1To9Done, nodeIndex);
            sCurrentStep = sIsSteps1To9Active ? TutorialCoachStep.None : TutorialCoachStep.Completed;
            sTargetSlot = -1;
            sTargetCard = null;

            DismissActiveCoachStep(releaseInput: true, silent: true);
            Debug.Log($"[TutorialCoach] 战斗开始：entryKind={entryKind}, nodeIndex={nodeIndex}, 1-9激活={sIsSteps1To9Active}");
        }

        /// <summary>战斗结束时清理状态。</summary>
        public static void OnBattleEnded()
        {
            sDoorSettledTriggeredThisBattle = false;
            DismissActiveCoachStep(releaseInput: true, silent: true);
        }

        /// <summary>
        /// 节拍：Avatar 在格 5 揭示落位后调用（发 8 张牌之前）。
        /// 若步骤 1–9 激活，触发步骤 1（欢迎并框玩家），点击推进到步骤 2（顺劈斧说明），再点击放行后续发牌。
        /// </summary>
        public static async UniTask NotifyAvatarSettledAsync(ManagedCard avatarCard, CancellationToken ct)
        {
            if (!sIsSteps1To9Active)
            {
                return;
            }

            sCurrentStep = TutorialCoachStep.Step1_Welcome;
            sTargetSlot = GroundSlotTopology.AvatarReservedSlot;
            sTargetCard = avatarCard;

            // 1. 顶部信息弹窗：第 1 句
            InfoNoticePresenter.ShowClickToAdvance(Step1Text);

            // 2. 教学提示框：框住玩家
            if (avatarCard != null)
            {
                TutorialPromptBoxPresenter.ShowTarget(avatarCard);
            }
            else
            {
                TutorialPromptBoxPresenter.ShowTarget(sTargetSlot);
            }

            // 3. 卡住主流程输入
            PresentationInputGates.TryBeginExternalHold(Steps1To9HoldReason);

            sAvatarIntroTcs = new UniTaskCompletionSource();
            Debug.Log("[TutorialCoach] 步骤 1 触发：欢迎来到九宫地下城");

            try
            {
                await sAvatarIntroTcs.Task.AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            finally
            {
                sAvatarIntroTcs = null;
            }
        }

        /// <summary>
        /// 节拍：开局 8 张卡牌环发着陆后调用。
        /// 触发步骤 3（铺满场地说明），点击推进到步骤 4（框 Slot 6 邻格真怪，放行 Slot 6 攻击白名单）。
        /// </summary>
        public static async UniTask NotifyOpeningBoardDealtAsync(CancellationToken ct)
        {
            if (!sIsSteps1To9Active || sCurrentStep != TutorialCoachStep.Step2_WarriorCleave)
            {
                return;
            }

            sCurrentStep = TutorialCoachStep.Step3_Opening8Cards;
            sTargetSlot = -1;
            sTargetCard = null;

            TutorialPromptBoxPresenter.HidePrompt();
            InfoNoticePresenter.ShowClickToAdvance(Step3Text);
            PresentationInputGates.TryBeginExternalHold(Steps1To9HoldReason);

            sBoardIntroTcs = new UniTaskCompletionSource();
            Debug.Log("[TutorialCoach] 步骤 3 触发：发8张牌铺满场地");

            try
            {
                await sBoardIntroTcs.Task.AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            finally
            {
                sBoardIntroTcs = null;
            }
        }

        /// <summary>
        /// 节拍：击杀 Slot 6 怪物后，补牌与顺时针旋转完成且盘面稳定后调用。
        /// 触发步骤 5（补牌与旋转说明），点击推进到步骤 6（框 Slot 6 药水并放行拾取白名单）。
        /// </summary>
        public static void NotifyPostKillBoardSettled()
        {
            if (!sIsSteps1To9Active || sCurrentStep != TutorialCoachStep.Step4_AttackMonster)
            {
                return;
            }

            sCurrentStep = TutorialCoachStep.Step5_RefillAndRotate;
            sTargetSlot = -1;
            sTargetCard = null;

            TutorialPromptBoxPresenter.HidePrompt();
            InfoNoticePresenter.ShowClickToAdvance(Step5Text);
            PresentationInputGates.TryBeginExternalHold(Steps1To9HoldReason);

            Debug.Log("[TutorialCoach] 步骤 5 触发：击杀后补牌与旋转");
        }

        /// <summary>
        /// 节拍：道具卡拾取并加入手牌完成后调用。
        /// 触发步骤 7（点了才走），之后由推进点击进入步骤 8、9。
        /// </summary>
        public static void NotifyItemPickedUp(int slot, ManagedCard card = null)
        {
            if (!sIsSteps1To9Active || sCurrentStep != TutorialCoachStep.Step6_PickupPotion)
            {
                return;
            }

            sTargetSlot = -1;
            sTargetCard = null;
            TutorialPromptBoxPresenter.HidePrompt();

            sCurrentStep = TutorialCoachStep.Step7_MonsterCountdowns;
            InfoNoticePresenter.ShowClickToAdvance(Step7Text);
            PresentationInputGates.TryBeginExternalHold(Steps1To9HoldReason);
            Debug.Log("[TutorialCoach] 步骤 7 触发：怪物行动计数");
        }

        /// <summary>
        /// 纯策略判定：输入入口种类与生涯已见标记，计算是否应触发门教学及对应覆层意图。
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
        /// 推进点击不会落地攻击或棋盘移动。
        /// </summary>
        public static bool TryConsumeAdvance()
        {
            if (sIsDoorTutorialActive)
            {
                CompleteDoorTutorial();
                return true;
            }

            if (!sIsSteps1To9Active)
            {
                return false;
            }

            switch (sCurrentStep)
            {
                case TutorialCoachStep.Step1_Welcome:
                    // 推进到步骤 2：顺劈斧
                    sCurrentStep = TutorialCoachStep.Step2_WarriorCleave;
                    InfoNoticePresenter.ShowClickToAdvance(Step2Text);
                    if (sTargetCard != null)
                    {
                        TutorialPromptBoxPresenter.ShowTarget(sTargetCard);
                    }
                    else
                    {
                        TutorialPromptBoxPresenter.ShowTarget(sTargetSlot);
                    }
                    Debug.Log("[TutorialCoach] 推进到步骤 2：战士顺劈斧");
                    return true;

                case TutorialCoachStep.Step2_WarriorCleave:
                    // 步骤 2 推进：放行发 8 张牌
                    TutorialPromptBoxPresenter.HidePrompt();
                    sAvatarIntroTcs?.TrySetResult();
                    Debug.Log("[TutorialCoach] 步骤 2 推进完成，开始发牌");
                    return true;

                case TutorialCoachStep.Step3_Opening8Cards:
                    // 步骤 3 推进到步骤 4：攻击邻格怪
                    sCurrentStep = TutorialCoachStep.Step4_AttackMonster;
                    InfoNoticePresenter.ShowClickToAdvance(Step4Text);
                    ShowPromptForSlot(FirstBattleTargetMonsterSlot);
                    PresentationInputGates.EndExternalHold(Steps1To9HoldReason);
                    sBoardIntroTcs?.TrySetResult();
                    Debug.Log("[TutorialCoach] 推进到步骤 4：攻击邻格怪（白名单放行 Slot 6）");
                    return true;

                case TutorialCoachStep.Step5_RefillAndRotate:
                    // 步骤 5 推进到步骤 6：拾取药水
                    sCurrentStep = TutorialCoachStep.Step6_PickupPotion;
                    InfoNoticePresenter.ShowClickToAdvance(Step6Text);
                    ShowPromptForSlot(FirstBattleCollectiblePotionSlot);
                    PresentationInputGates.EndExternalHold(Steps1To9HoldReason);
                    Debug.Log("[TutorialCoach] 推进到步骤 6：拾取道具卡（白名单放行 Slot 6）");
                    return true;

                case TutorialCoachStep.Step7_MonsterCountdowns:
                    sCurrentStep = TutorialCoachStep.Step8_InspectCards;
                    InfoNoticePresenter.ShowClickToAdvance(Step8Text);
                    Debug.Log("[TutorialCoach] 推进到步骤 8：右键详细描述（右键已启用）");
                    return true;

                case TutorialCoachStep.Step8_InspectCards:
                    sCurrentStep = TutorialCoachStep.Step9_RuleBook;
                    InfoNoticePresenter.ShowClickToAdvance(Step9Text);
                    Debug.Log("[TutorialCoach] 推进到步骤 9：规则书（规则书已启用）");
                    return true;

                case TutorialCoachStep.Step9_RuleBook:
                    CompleteSteps1To9();
                    return true;

                default:
                    return false;
            }
        }

        private static void ShowPromptForSlot(int slot)
        {
            sTargetSlot = slot;
            sTargetCard = null;
            TutorialPromptBoxPresenter.ShowTarget(slot);
        }

        private static bool TryGetAuthoritativeCardAtSlot(int slot, out ManagedCard card)
        {
            card = null;
            var board = NineGridArchitecture.Current?.GetModel<BoardModel>();
            if (board != null)
            {
                var uid = board.GetCardUid(SlotId.Board(slot));
                if (uid > 0 && CardEntityLifecycleHook.TryGetCard(uid, out card) && card != null)
                {
                    return true;
                }
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field != null
                && field.TryGetCardAt(slot, out card)
                && card != null
                && field.TryGetSlotOf(card.Uid, out var liveSlot)
                && liveSlot == slot)
            {
                return true;
            }

            card = null;
            return false;
        }

        private static void CompleteSteps1To9()
        {
            TutorialProgressStore.MarkSteps1To9Completed();
            sCurrentStep = TutorialCoachStep.Completed;
            sIsSteps1To9Active = false;
            sTargetSlot = -1;
            sTargetCard = null;
            TutorialPromptBoxPresenter.HidePrompt();
            PresentationInputGates.EndExternalHold(Steps1To9HoldReason);
            InfoNoticePresenter.DismissHold();
            Debug.Log("[TutorialCoach] 步骤 1–9 全部完成，已写入进度标记并释放主流程。");
        }

        private static void CompleteDoorTutorial()
        {
            sIsDoorTutorialActive = false;
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
            sActiveDoorSlot = -1;
            sActiveDoorCard = null;

            sAvatarIntroTcs?.TrySetCanceled();
            sAvatarIntroTcs = null;

            sBoardIntroTcs?.TrySetCanceled();
            sBoardIntroTcs = null;

            if (!silent)
            {
                InfoNoticePresenter.DismissHold();
                TutorialPromptBoxPresenter.HidePrompt();
            }

            if (releaseInput)
            {
                PresentationInputGates.ForceEndExternalHold(DoorExternalHoldReason);
                PresentationInputGates.ForceEndExternalHold(Steps1To9HoldReason);
            }
        }

        /// <summary>测试复位。</summary>
        public static void ResetForTests()
        {
            sEntryKind = TutorialEntryKind.Natural;
            sIsSteps1To9Active = false;
            sCurrentStep = TutorialCoachStep.None;
            sDoorSettledTriggeredThisBattle = false;
            sTargetSlot = -1;
            sTargetCard = null;
            DismissActiveCoachStep(releaseInput: true, silent: true);
        }
    }
}
