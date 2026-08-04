using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Tools;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace NineGrid.Flow.Transitions
{
    /// <summary>
    /// 局内节点过场：同层 Directional（伪随机偏置），跨层 Round（洞心追 Avatar）。
    /// 支持 Cover 挂起：进消费房时先盖住，等货架刷完再 Reveal，避免揭开后跳切。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunSceneTransitionService : MonoBehaviour
    {
        public static RunSceneTransitionService InstanceOrNull { get; private set; }

        [SerializeField] private RunSceneTransitionSettingsSO settings;
        [SerializeField] private MMFaderRound roundFader;
        [SerializeField] private MMFaderDirectional directionalFader;
        [SerializeField] private Image roundMaskImage;
        [SerializeField] private Image roundBackgroundImage;

        private readonly DirectionalBiasPicker mDirectionPicker = new DirectionalBiasPicker();
        private bool mBusy;
        private bool mCoverHeld;
        private bool mHeldCrossFloor;

        public bool IsBusy => mBusy;
        public bool IsCoverHeld => mCoverHeld;

        public RunSceneTransitionSettingsSO Settings =>
            settings != null
                ? settings
                : settings = Resources.Load<RunSceneTransitionSettingsSO>(
                    RunSceneTransitionSettingsSO.ResourcePath);

        public bool IsEnabled => Settings != null && Settings.Enabled;

        private void Awake()
        {
            InstanceOrNull = this;
            EnsureSettings();
            EnsureRoundHierarchy();
            ApplySettingsToFaders();
        }

        private void OnDestroy()
        {
            if (InstanceOrNull == this)
            {
                InstanceOrNull = null;
            }
        }

        public void BindForTests(
            RunSceneTransitionSettingsSO so,
            MMFaderRound round,
            MMFaderDirectional directional)
        {
            settings = so;
            roundFader = round;
            directionalFader = directional;
            EnsureRoundHierarchy();
            ApplySettingsToFaders();
        }

        /// <summary>
        /// 进房后若进入商店/卡店/特殊奖励场地板，应挂起 Cover，等刷板后再 <see cref="CompleteRevealAsync"/>。
        /// </summary>
        public static bool ShouldDeferRevealForInRoomBoard(IArchitecture arch)
        {
            if (arch == null)
            {
                return false;
            }

            var phase = arch.GetSystem<IPhaseSystem>();
            var pending = arch.GetModel<PendingChoiceModel>();
            return phase != null
                   && pending != null
                   && phase.CurrentPhase == GamePhase.RewardItemChoice
                   && pending.Kind.Value == PendingChoiceKind.Reward
                   && PendingChoiceModel.IsConsumerBoardPool(pending.PoolId.Value);
        }

        public static bool WillCrossFloor(IArchitecture arch)
        {
            var run = arch?.GetModel<RunModel>();
            if (run == null)
            {
                return false;
            }

            return run.NodeIndex.Value + 1 >= RunModel.NodesPerFloor
                   && run.Floor.Value < RunModel.FinalFloor;
        }

        /// <summary>Cover → mid → Reveal（完整两段）。</summary>
        public async UniTask PlayCoverRevealAsync(
            bool crossFloor,
            Func<CancellationToken, UniTask> midAction,
            CancellationToken ct)
        {
            if (midAction == null)
            {
                return;
            }

            if (!IsEnabled || (mBusy && !mCoverHeld))
            {
                await midAction(ct);
                return;
            }

            if (crossFloor && roundFader == null)
            {
                await midAction(ct);
                return;
            }

            if (!crossFloor && directionalFader == null)
            {
                await midAction(ct);
                return;
            }

            await BeginCoverAsync(crossFloor, ct);
            try
            {
                await midAction(CancellationToken.None);
            }
            finally
            {
                await CompleteRevealAsync(CancellationToken.None);
            }
        }

        /// <summary>只播 Cover；成功后 <see cref="IsCoverHeld"/> 为 true，须再调 Reveal。</summary>
        public async UniTask BeginCoverAsync(bool crossFloor, CancellationToken ct)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (mCoverHeld)
            {
                return;
            }

            if (mBusy)
            {
                return;
            }

            if (crossFloor && roundFader == null)
            {
                return;
            }

            if (!crossFloor && directionalFader == null)
            {
                return;
            }

            mBusy = true;
            mHeldCrossFloor = crossFloor;
            try
            {
                EnsureSettings();
                EnsureRoundHierarchy();
                ApplySettingsToFaders();
                await PlayFadeInAsync(crossFloor, ct);
                mCoverHeld = true;
            }
            catch (OperationCanceledException)
            {
                ForceClearFaders();
                mCoverHeld = false;
                mBusy = false;
                throw;
            }
        }

        /// <summary>播 Reveal 并放下 Cover 挂起。</summary>
        public async UniTask CompleteRevealAsync(CancellationToken ct)
        {
            if (!mCoverHeld)
            {
                return;
            }

            try
            {
                await PlayFadeOutAsync(mHeldCrossFloor, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                ForceClearFaders();
                throw;
            }
            finally
            {
                mCoverHeld = false;
                mBusy = false;
            }
        }

        /// <summary>异常/取消时强制收掉遮罩，避免全屏黑死。</summary>
        public void ForceClearFaders()
        {
            if (roundFader != null)
            {
                MMFadeStopEvent.Trigger(RunSceneTransitionSettingsSO.RoundFaderId, restore: false);
                if (roundFader.FaderMask != null)
                {
                    var open = roundFader.MaskScale.y;
                    roundFader.FaderMask.localScale = open * Vector3.one;
                }

                var cg = roundFader.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 0f;
                    cg.blocksRaycasts = false;
                }
            }

            if (directionalFader != null)
            {
                MMFadeStopEvent.Trigger(RunSceneTransitionSettingsSO.DirectionalFaderId, restore: false);
                var cg = directionalFader.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 0f;
                    cg.blocksRaycasts = false;
                }

                directionalFader.enabled = false;
            }

            mCoverHeld = false;
            mBusy = false;
        }

        private async UniTask PlayFadeInAsync(bool crossFloor, CancellationToken ct)
        {
            var so = Settings;
            if (crossFloor)
            {
                PrepareRoundCamera();
                PrepareRoundForIrisClose();
                var duration = Mathf.Max(0.05f, so.RoundFadeInDuration);
                var tween = ToMmTween(so.RoundTween);
                var worldPos = ResolveAvatarWorldPosition();
                MMFadeInEvent.Trigger(
                    duration,
                    tween,
                    RunSceneTransitionSettingsSO.RoundFaderId,
                    so.IgnoreTimeScale,
                    worldPos);
                await WaitFadeAsync(duration, so.IgnoreTimeScale, ct);
                // 合上后停一帧，确保洞完全闭合再硬切。
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                return;
            }

            var dir = mDirectionPicker.Next();
            ApplyDirectional(dir);
            var dirDuration = Mathf.Max(0.05f, so.DirectionalFadeInDuration);
            MMFadeInEvent.Trigger(
                dirDuration,
                ToMmTween(so.DirectionalTween),
                RunSceneTransitionSettingsSO.DirectionalFaderId,
                so.IgnoreTimeScale);
            await WaitFadeAsync(dirDuration, so.IgnoreTimeScale, ct);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        private async UniTask PlayFadeOutAsync(bool crossFloor, CancellationToken ct)
        {
            var so = Settings;
            if (crossFloor)
            {
                PrepareRoundCamera();
                PrepareRoundForIrisOpen();
                var duration = Mathf.Max(0.05f, so.RoundFadeOutDuration);
                var tween = ToMmTween(so.RoundTween);
                var worldPos = ResolveAvatarWorldPosition();
                MMFadeOutEvent.Trigger(
                    duration,
                    tween,
                    RunSceneTransitionSettingsSO.RoundFaderId,
                    so.IgnoreTimeScale,
                    worldPos);
                await WaitFadeAsync(duration, so.IgnoreTimeScale, ct);
                // Feel 用 float== 判断是否 DisableFader，不可靠；收尾强制透明。
                ForceRoundHidden();
                return;
            }

            var dirDuration = Mathf.Max(0.05f, so.DirectionalFadeOutDuration);
            MMFadeOutEvent.Trigger(
                dirDuration,
                ToMmTween(so.DirectionalTween),
                RunSceneTransitionSettingsSO.DirectionalFaderId,
                so.IgnoreTimeScale);
            await WaitFadeAsync(dirDuration, so.IgnoreTimeScale, ct);
        }

        private void PrepareRoundCamera()
        {
            if (roundFader == null)
            {
                return;
            }

            if (roundFader.CameraMode == MMFaderRound.CameraModes.Main
                || roundFader.TargetCamera == null)
            {
                roundFader.TargetCamera = Camera.main;
            }
        }

        /// <summary>
        /// FadeIn = 洞从大缩到小。先把 mask 放到「全开」再触发，避免从已闭合态瞬间变黑。
        /// </summary>
        private void PrepareRoundForIrisClose()
        {
            if (roundFader == null || roundFader.FaderMask == null)
            {
                return;
            }

            var open = roundFader.MaskScale.y;
            roundFader.FaderMask.localScale = open * Vector3.one;
            var cg = roundFader.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
            }
        }

        /// <summary>FadeOut = 洞从小扩到大。确保处于合上且不透明。</summary>
        private void PrepareRoundForIrisOpen()
        {
            if (roundFader == null || roundFader.FaderMask == null)
            {
                return;
            }

            var closed = roundFader.MaskScale.x;
            roundFader.FaderMask.localScale = closed * Vector3.one;
            var cg = roundFader.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = Settings == null || Settings.BlockRaycastsDuringFade;
            }
        }

        private void ForceRoundHidden()
        {
            if (roundFader == null)
            {
                return;
            }

            if (roundFader.FaderMask != null)
            {
                roundFader.FaderMask.localScale = roundFader.MaskScale.y * Vector3.one;
            }

            var cg = roundFader.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// Feel stencil：Mask 须先于 Background 绘制（同级更小 sibling index）。
        /// </summary>
        private void EnsureRoundHierarchy()
        {
            if (roundFader == null)
            {
                return;
            }

            var mask = roundFader.FaderMask;
            var bg = roundFader.FaderBackground;
            if (mask == null || bg == null)
            {
                return;
            }

            if (mask.GetSiblingIndex() > bg.GetSiblingIndex())
            {
                mask.SetSiblingIndex(bg.GetSiblingIndex());
            }

            if (roundMaskImage == null)
            {
                roundMaskImage = mask.GetComponent<Image>();
            }

            if (roundBackgroundImage == null)
            {
                roundBackgroundImage = bg.GetComponent<Image>();
            }
        }

        private void ApplyDirectional(DirectionalBiasPicker.Direction direction)
        {
            if (directionalFader == null)
            {
                return;
            }

            var mm = ToMmDirection(direction);
            directionalFader.FadeInDirection = mm;
            directionalFader.FadeOutDirection = mm;
        }

        private void EnsureSettings()
        {
            if (settings == null)
            {
                settings = Resources.Load<RunSceneTransitionSettingsSO>(
                    RunSceneTransitionSettingsSO.ResourcePath);
            }
        }

        private void ApplySettingsToFaders()
        {
            var so = Settings;
            if (so == null)
            {
                return;
            }

            if (roundFader != null)
            {
                roundFader.ID = RunSceneTransitionSettingsSO.RoundFaderId;
                roundFader.MaskScale = so.RoundMaskScale;
                roundFader.DefaultDuration = so.RoundFadeInDuration;
                roundFader.IgnoreTimescale = so.IgnoreTimeScale;
                roundFader.ShouldBlockRaycasts = so.BlockRaycastsDuringFade;
                roundFader.DefaultTween = ToMmTween(so.RoundTween);
                PrepareRoundCamera();
            }

            if (directionalFader != null)
            {
                directionalFader.ID = RunSceneTransitionSettingsSO.DirectionalFaderId;
                directionalFader.DefaultDuration = so.DirectionalFadeInDuration;
                directionalFader.IgnoreTimescale = so.IgnoreTimeScale;
                directionalFader.ShouldBlockRaycasts = so.BlockRaycastsDuringFade;
                directionalFader.DefaultTween = ToMmTween(so.DirectionalTween);
            }

            if (roundMaskImage != null && so.RoundMaskSprite != null)
            {
                roundMaskImage.sprite = so.RoundMaskSprite;
            }

            if (roundBackgroundImage != null)
            {
                roundBackgroundImage.color = so.RoundBackgroundColor;
            }
        }

        private static Vector3 ResolveAvatarWorldPosition()
        {
            var arch = NineGridArchitecture.Current;
            var board = arch?.GetModel<BoardModel>();
            if (board != null)
            {
                var uid = board.AvatarUid.Value;
                if (uid > 0
                    && CardEntityLifecycleHook.TryGetCard(uid, out var card)
                    && card?.Transform != null)
                {
                    return card.Transform.position;
                }

                var slot = board.AvatarSlot.Value;
                if (slot.IsBoardSlot)
                {
                    var geometry = arch.GetSystem<NineGrid.Presentation.Systems.IGroundFieldGeometrySystem>();
                    var anchor = geometry?.GetGroundAnchor(slot.Index);
                    if (anchor != null)
                    {
                        return anchor.position;
                    }
                }
            }

            var cam = Camera.main;
            if (cam != null)
            {
                var mid = new Vector3(
                    Screen.width * 0.5f,
                    Screen.height * 0.5f,
                    Mathf.Abs(cam.transform.position.z));
                return cam.ScreenToWorldPoint(mid);
            }

            return Vector3.zero;
        }

        private static async UniTask WaitFadeAsync(float duration, bool ignoreTimeScale, CancellationToken ct)
        {
            if (duration <= 0f)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                return;
            }

            var ms = Mathf.CeilToInt(duration * 1000f) + 32;
            await UniTask.Delay(ms, DelayType.Realtime, cancellationToken: ct);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        private static MMTweenType ToMmTween(TransitionTweenCurve curve)
        {
            return new MMTweenType((MMTween.MMTweenCurve)(int)curve);
        }

        private static MMFaderDirectional.Directions ToMmDirection(DirectionalBiasPicker.Direction direction)
        {
            return direction switch
            {
                DirectionalBiasPicker.Direction.TopToBottom => MMFaderDirectional.Directions.TopToBottom,
                DirectionalBiasPicker.Direction.LeftToRight => MMFaderDirectional.Directions.LeftToRight,
                DirectionalBiasPicker.Direction.RightToLeft => MMFaderDirectional.Directions.RightToLeft,
                DirectionalBiasPicker.Direction.BottomToTop => MMFaderDirectional.Directions.BottomToTop,
                _ => MMFaderDirectional.Directions.LeftToRight,
            };
        }
    }
}
