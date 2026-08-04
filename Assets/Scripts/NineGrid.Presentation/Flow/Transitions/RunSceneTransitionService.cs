using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Tools;
using NineGrid.Cards;
using NineGrid.Core;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace NineGrid.Flow.Transitions
{
    /// <summary>
    /// 局内节点过场：同层 Directional（伪随机偏置），跨层 Round（洞心追 Avatar）。
    /// 驱动 Feel <see cref="MMFaderRound"/> / <see cref="MMFaderDirectional"/> 事件。
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

        public bool IsBusy => mBusy;

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
            ApplySettingsToFaders();
        }

        /// <summary>Cover（FadeIn）→ 调用方硬切 → Reveal（FadeOut）。</summary>
        public async UniTask PlayCoverRevealAsync(
            bool crossFloor,
            System.Func<CancellationToken, UniTask> midAction,
            CancellationToken ct)
        {
            if (midAction == null)
            {
                return;
            }

            if (!IsEnabled || mBusy)
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

            mBusy = true;
            try
            {
                EnsureSettings();
                ApplySettingsToFaders();

                await PlayFadeInAsync(crossFloor, ct);
                if (ct.IsCancellationRequested)
                {
                    ForceClearFaders();
                    return;
                }

                try
                {
                    await midAction(ct);
                }
                finally
                {
                    // midAction（进房硬切）可能取消外部 token；Reveal 必须仍执行，否则黑屏卡死。
                    await PlayFadeOutAsync(crossFloor, CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                ForceClearFaders();
                throw;
            }
            finally
            {
                mBusy = false;
            }
        }

        /// <summary>异常/取消时强制收掉遮罩，避免全屏黑死。</summary>
        private void ForceClearFaders()
        {
            if (roundFader != null)
            {
                MMFadeStopEvent.Trigger(RunSceneTransitionSettingsSO.RoundFaderId, restore: false);
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

        private async UniTask PlayFadeInAsync(bool crossFloor, CancellationToken ct)
        {
            var so = Settings;
            if (crossFloor)
            {
                var duration = so.RoundFadeInDuration;
                var tween = ToMmTween(so.RoundTween);
                var worldPos = ResolveAvatarWorldPosition();
                MMFadeInEvent.Trigger(
                    duration,
                    tween,
                    RunSceneTransitionSettingsSO.RoundFaderId,
                    so.IgnoreTimeScale,
                    worldPos);
                await WaitFadeAsync(duration, so.IgnoreTimeScale, ct);
                return;
            }

            var dir = mDirectionPicker.Next();
            ApplyDirectional(dir);
            var dirDuration = so.DirectionalFadeInDuration;
            MMFadeInEvent.Trigger(
                dirDuration,
                ToMmTween(so.DirectionalTween),
                RunSceneTransitionSettingsSO.DirectionalFaderId,
                so.IgnoreTimeScale);
            await WaitFadeAsync(dirDuration, so.IgnoreTimeScale, ct);
        }

        private async UniTask PlayFadeOutAsync(bool crossFloor, CancellationToken ct)
        {
            var so = Settings;
            if (crossFloor)
            {
                var duration = so.RoundFadeOutDuration;
                var tween = ToMmTween(so.RoundTween);
                var worldPos = ResolveAvatarWorldPosition();
                MMFadeOutEvent.Trigger(
                    duration,
                    tween,
                    RunSceneTransitionSettingsSO.RoundFaderId,
                    so.IgnoreTimeScale,
                    worldPos);
                await WaitFadeAsync(duration, so.IgnoreTimeScale, ct);
                return;
            }

            var dirDuration = so.DirectionalFadeOutDuration;
            MMFadeOutEvent.Trigger(
                dirDuration,
                ToMmTween(so.DirectionalTween),
                RunSceneTransitionSettingsSO.DirectionalFaderId,
                so.IgnoreTimeScale);
            await WaitFadeAsync(dirDuration, so.IgnoreTimeScale, ct);
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
                var mid = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, Mathf.Abs(cam.transform.position.z));
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

            var ms = Mathf.CeilToInt(duration * 1000f);
            await UniTask.Delay(ms, DelayType.Realtime, cancellationToken: ct);
            // Feel fader 在 Update 里收尾；多等一帧避免硬切露馅。
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
