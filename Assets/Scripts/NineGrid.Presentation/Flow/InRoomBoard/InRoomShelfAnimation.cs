using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using UnityEngine;

namespace NineGrid.Flow.InRoomBoard
{
    /// <summary>
    /// 房内货架 / 候选的补位与消耗表演（#92/#93/#94 美化）：
    /// 跳格复刻 Avatar 跳格（L2 收敛 + L4 hop 缩放脉冲）；能力卡购买走标准 Death 碎裂退场。
    /// 真卡与就地选项 GO 均适用；不 parent 到格位锚点、不写 localScale 绝对值（ADR-0024）。
    /// </summary>
    public static class InRoomShelfAnimation
    {
        public const string StandardDeathFxAssetPath =
            "Assets/Resources/Cards/Effects/Defaults/CardDeathBurnExitEffect.asset";

        /// <summary>与 Avatar 跳格同演出时长/质感（GroundMotionExecutor 默认 moveDuration / hop 强度）。</summary>
        public const float HopDuration = 0.35f;
        public const float HopPeakScaleIntensity = 0.06f;
        public const float HopLandScaleIntensity = 0.04f;
        private const float DropInHeight = 0.9f;

        private static CardSpriteSheetBurnExitEffectSO sStandardDeathFx;

        /// <summary>
        /// 标准 Death 碎裂特效（burn exit，卡面 Death 槽默认资产）。编辑器下从资产路径加载并缓存；
        /// Player 无该资产时回退 null（调用方降级为直接藏卡）。
        /// </summary>
        public static CardSpriteSheetBurnExitEffectSO ResolveStandardDeathFx()
        {
            if (sStandardDeathFx != null)
            {
                return sStandardDeathFx;
            }

            sStandardDeathFx = CardChassisPaths.LoadAsset<CardSpriteSheetBurnExitEffectSO>(
                StandardDeathFxAssetPath);
            return sStandardDeathFx;
        }

        /// <summary>标准跳格：卡从当前位置 hop 到目标格（与 Avatar 跳格同演出）。</summary>
        public static async UniTask HopToSlotAsync(
            ManagedCard card,
            IGroundFieldGeometrySystem geometry,
            int toSlot,
            CancellationToken cancellationToken = default)
        {
            if (card?.Transform == null || geometry == null)
            {
                return;
            }

            var toAnchor = geometry.GetGroundAnchor(toSlot);
            if (toAnchor == null)
            {
                return;
            }

            await ConvergeWithHopPulseAsync(card, toAnchor.position, cancellationToken);
        }

        /// <summary>补位入场：新卡从格上方落到目标格（hop 质感落地）。</summary>
        public static async UniTask DropInToSlotAsync(
            ManagedCard card,
            IGroundFieldGeometrySystem geometry,
            int toSlot,
            CancellationToken cancellationToken = default)
        {
            if (card?.Transform == null || geometry == null)
            {
                return;
            }

            var toAnchor = geometry.GetGroundAnchor(toSlot);
            if (toAnchor == null)
            {
                return;
            }

            card.Transform.position = toAnchor.position + Vector3.up * DropInHeight;
            await ConvergeWithHopPulseAsync(card, toAnchor.position, cancellationToken);
        }

        /// <summary>就地选项（能力卡 / 服务）跳格：DOTween 位移 + 根 hop 脉冲。</summary>
        public static async UniTask HopOptionGoAsync(
            GameObject go,
            IGroundFieldGeometrySystem geometry,
            int toSlot,
            CancellationToken cancellationToken = default)
        {
            if (go == null || geometry == null)
            {
                return;
            }

            var toAnchor = geometry.GetGroundAnchor(toSlot);
            if (toAnchor == null)
            {
                return;
            }

            await MoveOptionGoAsync(go, toAnchor.position, cancellationToken);
        }

        /// <summary>就地选项补位入场：从格上方落到目标格。</summary>
        public static async UniTask DropOptionGoInAsync(
            GameObject go,
            IGroundFieldGeometrySystem geometry,
            int toSlot,
            CancellationToken cancellationToken = default)
        {
            if (go == null || geometry == null)
            {
                return;
            }

            var toAnchor = geometry.GetGroundAnchor(toSlot);
            if (toAnchor == null)
            {
                return;
            }

            go.transform.position = toAnchor.position + Vector3.up * DropInHeight;
            await MoveOptionGoAsync(go, toAnchor.position, cancellationToken);
        }

        /// <summary>
        /// 能力卡 / 就地选项消耗退场：标准 Death 碎裂（burn exit FX），藏根后发射后不管。
        /// 无特效资产（Player）回退直接藏卡。
        /// </summary>
        public static void PlayConsumeDeath(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var fx = ResolveStandardDeathFx();
            if (fx == null)
            {
                root.gameObject.SetActive(false);
                return;
            }

            var context = new CardEffectPlayContext(
                card: null,
                root: root,
                view: null,
                invoke: CardEffectInvokeContext.ForDeath(0, CardBoardDirection.None),
                selfWorldPosition: root.position,
                otherWorldPosition: null,
                cancellationToken: CancellationToken.None);
            fx.PlayAsync(context).Forget();
        }

        /// <summary>真卡消耗退场：走卡面既有 Death 绑定（标准 burn exit），无绑定则回退根碎裂。</summary>
        public static void PlayConsumeDeath(ManagedCard card)
        {
            if (card == null)
            {
                return;
            }

            if (card.TryGetEffectManager(out var effectManager))
            {
                effectManager.PlayDeathAsync(
                    selfSlot: 0,
                    selfDirection: CardBoardDirection.None,
                    cancellationToken: CancellationToken.None,
                    audioCueId: CardLifecycleAudioCues.Exit).Forget();
                return;
            }

            CardLifecycleAudioCues.Pulse(
                CardLifecycleAudioCues.Exit,
                "InRoomShelfAnimation.PlayConsumeDeath",
                card.DefId);
            PlayConsumeDeath(card.Transform);
        }

        /// <summary>
        /// 单点移除容忍的索引匹配：newIndex i ↔ 旧索引 i 或 i+1（DefId 校验，避免错配）。
        /// 命中即标记 <paramref name="oldKept"/>，同一旧条目不会被二次保持。
        /// </summary>
        public static int TryMatchOldIndex(
            IReadOnlyList<string> oldDefIds,
            bool[] oldKept,
            string newDefId,
            int newIndex)
        {
            if (string.IsNullOrEmpty(newDefId) || oldDefIds == null || oldKept == null)
            {
                return -1;
            }

            for (var offset = 0; offset <= 1; offset++)
            {
                var j = newIndex + offset;
                if (j < 0 || j >= oldDefIds.Count || oldKept[j])
                {
                    continue;
                }

                if (!string.Equals(oldDefIds[j], newDefId, StringComparison.Ordinal))
                {
                    continue;
                }

                oldKept[j] = true;
                return j;
            }

            return -1;
        }

        private static async UniTask ConvergeWithHopPulseAsync(
            ManagedCard card,
            Vector3 targetWorld,
            CancellationToken cancellationToken)
        {
            if (card?.Transform == null)
            {
                return;
            }

            var hopScaleTask = UniTask.CompletedTask;
            if (SlotFrameConvergence.TryGetTower(card, out var hopTower)
                || SlotFrameConvergence.TryEnsureInfrastructure(
                    card,
                    out hopTower,
                    out _,
                    "InRoom.Hop"))
            {
                if (hopTower?.CardVisual != null)
                {
                    var visualDriver = card.View != null
                        ? card.View.GetComponent<CardVisualDriver>()
                        : null;
                    visualDriver?.InterruptFeedbackMotion();
                    hopScaleTask = CardDeckTween.PlayHopScalePulseAsync(
                        hopTower.CardVisual,
                        HopDuration,
                        HopPeakScaleIntensity,
                        HopLandScaleIntensity,
                        cancellationToken);
                }
            }

            await UniTask.WhenAll(
                SlotFrameConvergence.ConvergeVisualToWorldAsync(
                    card,
                    targetWorld,
                    HopDuration,
                    cancellationToken,
                    snapHomeOnComplete: true,
                    commitment: CommitmentKind.Sync),
                hopScaleTask);
        }

        private static async UniTask MoveOptionGoAsync(
            GameObject go,
            Vector3 targetWorld,
            CancellationToken cancellationToken)
        {
            if (go == null)
            {
                return;
            }

            CardDeckTween.MoveToWorld(go.transform, targetWorld, HopDuration, reason: "InRoom.OptionHop");
            // 根 hop 脉冲：不经 PlayHopScalePulseAsync（其 KillMotion 会掐掉同一根的 MoveToWorld）。
            var pulse = PlayRootHopPulseAsync(go.transform, cancellationToken);
            await UniTask.WhenAll(
                UniTask.Delay(TimeSpan.FromSeconds(HopDuration), cancellationToken: cancellationToken),
                pulse);
        }

        private static async UniTask PlayRootHopPulseAsync(Transform root, CancellationToken cancellationToken)
        {
            if (root == null)
            {
                return;
            }

            var baseScale = root.localScale;
            var peakScale = baseScale * (1f + HopPeakScaleIntensity);
            var landScale = baseScale * (1f - HopLandScaleIntensity);
            var halfDuration = HopDuration * 0.5f;
            var landDuration = halfDuration * 0.72f;
            var settleDuration = halfDuration - landDuration;

            var sequence = DOTween.Sequence()
                .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy);
            sequence.Append(root.DOScale(peakScale, halfDuration).SetEase(Ease.OutSine));
            sequence.Append(root.DOScale(landScale, landDuration).SetEase(Ease.InQuad));
            sequence.Append(root.DOScale(baseScale, settleDuration).SetEase(Ease.OutSine));
            sequence.OnComplete(() =>
            {
                if (root != null)
                {
                    root.localScale = baseScale;
                }
            });

            await UniTask.Delay(TimeSpan.FromSeconds(HopDuration), cancellationToken: cancellationToken);
        }
    }
}
