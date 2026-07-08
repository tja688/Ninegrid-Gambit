using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 单卡表现反馈管理器：桥接 CardEffectSO，由场地/手牌/战斗编排层被动驱动。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StandardCardView))]
    public sealed class CardEffectManager : MonoBehaviour
    {
        [Tooltip("五类效果 SO 装配；每项 kind 对应 Attack/Hit/Death/Use/HitFlash。")]
        [SerializeField] private List<CardEffectBinding> bindings = new()
        {
            new CardEffectBinding { kind = CardEffectKind.Attack },
            new CardEffectBinding { kind = CardEffectKind.Hit },
            new CardEffectBinding { kind = CardEffectKind.Death },
            new CardEffectBinding { kind = CardEffectKind.Use },
            new CardEffectBinding { kind = CardEffectKind.HitFlash },
        };

        private StandardCardView _view;
        private CardVisualDriver _visualDriver;
        private CardEffectSO _currentEffect;
        private CardEffectPlayContext _currentPlayContext;
        private CancellationTokenSource _playCts;
        private bool _isPlaying;
        private bool _suppressHover;

        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime")]
        public bool IsPlaying => _isPlaying;

        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime")]
        public string LastPlayedEffectName => _currentEffect != null ? _currentEffect.DisplayName : string.Empty;

        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime")]
        public CardBoardDirection LastResolvedDirection { get; private set; }

        [ShowInInspector, ReadOnly, FoldoutGroup("Runtime")]
        public bool SuppressHover => _suppressHover;

        private void Awake()
        {
            _view = GetComponent<StandardCardView>();
            _visualDriver = GetComponent<CardVisualDriver>();
        }

        public UniTask PlayAsync(CardEffectInvokeContext invoke, CancellationToken cancellationToken = default)
        {
            return PlayInternalAsync(invoke, cancellationToken);
        }

        public UniTask PlayAttackAsync(
            CardBoardDirection selfDirection,
            int selfSlot = 0,
            int? otherSlot = null,
            CancellationToken cancellationToken = default)
        {
            return PlayAsync(
                CardEffectInvokeContext.ForAttack(selfDirection, selfSlot, otherSlot),
                cancellationToken);
        }

        public UniTask PlayHitAsync(
            CardBoardDirection selfDirection,
            int selfSlot = 0,
            int? otherSlot = null,
            CancellationToken cancellationToken = default)
        {
            return PlayAsync(
                CardEffectInvokeContext.ForHit(selfDirection, selfSlot, otherSlot),
                cancellationToken);
        }

        public UniTask PlayDeathAsync(
            int selfSlot = 0,
            CardBoardDirection selfDirection = CardBoardDirection.None,
            CancellationToken cancellationToken = default)
        {
            return PlayAsync(
                CardEffectInvokeContext.ForDeath(selfSlot, selfDirection),
                cancellationToken);
        }

        public UniTask PlayUseAsync(
            CardBoardDirection selfDirection = CardBoardDirection.None,
            CancellationToken cancellationToken = default)
        {
            return PlayAsync(
                CardEffectInvokeContext.ForUse(selfDirection),
                cancellationToken);
        }

        public UniTask PlayHitFlashAsync(
            CardBoardDirection selfDirection = CardBoardDirection.None,
            int selfSlot = 0,
            int? otherSlot = null,
            CancellationToken cancellationToken = default)
        {
            return PlayAsync(
                CardEffectInvokeContext.ForHitFlash(selfDirection, selfSlot, otherSlot),
                cancellationToken);
        }

        /// <summary>
        /// Timeline / DOTween / Animation Event / UnityEvent 统一回调入口（整型）。
        /// <see cref="CardEffectCallbackAction"/> 的 Play 段与 <see cref="CardEffectKind"/> 数值一致；
        /// 未传方向时使用 <see cref="LastResolvedDirection"/>。
        /// </summary>
        public void Callback(int action)
        {
            if (!CardEffectCallbackActionUtility.TryParse(action, out var parsed))
            {
                Debug.LogWarning(
                    $"[CardEffectManager] 未知回调动作 id={action}，已忽略。",
                    this);
                return;
            }

            DispatchCallback(parsed, null);
        }

        /// <summary>
        /// 统一回调入口（整型 + 方向）。
        /// <paramref name="selfDirection"/> 为 <see cref="CardBoardDirection"/> 枚举整型值。
        /// </summary>
        public void Callback(int action, int selfDirection)
        {
            if (!CardEffectCallbackActionUtility.TryParse(action, out var parsed))
            {
                Debug.LogWarning(
                    $"[CardEffectManager] 未知回调动作 id={action}，已忽略。",
                    this);
                return;
            }

            DispatchCallback(parsed, (CardBoardDirection)selfDirection);
        }

        /// <summary>
        /// 统一回调入口（名称）。
        /// 支持枚举名、别名（如 HitFlash / flash / 闪白）及 Stop 类动作。
        /// </summary>
        public void Callback(string actionName)
        {
            if (!CardEffectCallbackActionUtility.TryParse(actionName, out var parsed))
            {
                Debug.LogWarning(
                    $"[CardEffectManager] 未知回调动作 name='{actionName}'，已忽略。",
                    this);
                return;
            }

            DispatchCallback(parsed, null);
        }

        /// <summary>
        /// 兼容旧绑定：播放受击闪白（等价于 <c>Callback(4)</c>）。
        /// </summary>
        public void CallbackPlayHitFlash()
        {
            Callback((int)CardEffectCallbackAction.PlayHitFlash);
        }

        /// <summary>
        /// 兼容旧绑定：按方向播放闪白（等价于 <c>Callback(4, selfDirection)</c>）。
        /// </summary>
        public void CallbackPlayHitFlashDirection(int selfDirection)
        {
            Callback((int)CardEffectCallbackAction.PlayHitFlash, selfDirection);
        }

        /// <summary>兼容旧绑定：停止闪白（等价于 <c>Callback(101)</c>）。</summary>
        public void CallbackStopHitFlash()
        {
            Callback((int)CardEffectCallbackAction.StopHitFlash);
        }

        private void DispatchCallback(CardEffectCallbackAction action, CardBoardDirection? directionOverride)
        {
            switch (action)
            {
                case CardEffectCallbackAction.StopCurrent:
                    StopCurrent();
                    return;
                case CardEffectCallbackAction.StopHitFlash:
                    StopOverlayFlash();
                    return;
            }

            if (!CardEffectCallbackActionUtility.IsPlayAction(action))
            {
                Debug.LogWarning(
                    $"[CardEffectManager] 未处理的回调动作 {action}，已忽略。",
                    this);
                return;
            }

            var direction = directionOverride ?? LastResolvedDirection;
            PlayCallbackEffectAsync(CardEffectCallbackActionUtility.ToPlayKind(action), direction).Forget();
        }

        private UniTask PlayCallbackEffectAsync(CardEffectKind kind, CardBoardDirection direction)
        {
            return kind switch
            {
                CardEffectKind.Attack => PlayAttackAsync(direction),
                CardEffectKind.Hit => PlayHitAsync(direction),
                CardEffectKind.Death => PlayDeathAsync(selfDirection: direction),
                CardEffectKind.Use => PlayUseAsync(direction),
                CardEffectKind.HitFlash => PlayHitFlashAsync(direction),
                _ => UniTask.CompletedTask,
            };
        }

        public void StopCurrent()
        {
            _playCts?.Cancel();
            _playCts?.Dispose();
            _playCts = null;

            if (_currentEffect != null && _currentPlayContext.Root != null)
            {
                _currentEffect.Stop(_currentPlayContext);
            }

            StopOverlayFlash();

            _currentEffect = null;
            _isPlaying = false;
            _suppressHover = false;
        }

        private void StopOverlayFlash()
        {
            if (_view != null
                && _view.TryGetComponent<CardSpriteHitFlashExecutor>(out var executor))
            {
                executor.Stop();
            }
        }

        public bool TryConsumeHoverSuppression()
        {
            if (!_suppressHover)
            {
                return false;
            }

            return true;
        }

        private async UniTask PlayInternalAsync(CardEffectInvokeContext invoke, CancellationToken cancellationToken)
        {
            if (_view == null)
            {
                _view = GetComponent<StandardCardView>();
            }

            var effect = ResolveEffect(invoke.Kind, invoke.SelfDirection);
            if (effect == null)
            {
                Debug.LogWarning(
                    $"[CardEffectManager] 未配置 {invoke.Kind} 效果 SO（方向={invoke.SelfDirection}），跳过。",
                    this);
                return;
            }

            if (invoke.Kind == CardEffectKind.HitFlash)
            {
                await PlayHitFlashOverlayAsync(effect, invoke, cancellationToken);
                return;
            }

            StopCurrent();

            _isPlaying = true;
            _suppressHover = true;
            _currentEffect = effect;
            LastResolvedDirection = invoke.SelfDirection;

            PrepareMotion();

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            _playCts = linkedCts;

            var card = ResolveManagedCard();
            var playContext = BuildPlayContext(card, invoke, linkedCts.Token);
            _currentPlayContext = playContext;

            try
            {
                await effect.PlayAsync(playContext);
            }
            catch (System.OperationCanceledException)
            {
                effect.Stop(playContext);
            }
            finally
            {
                _isPlaying = false;
                _suppressHover = false;
                _currentEffect = null;

                linkedCts.Dispose();
                if (_playCts == linkedCts)
                {
                    _playCts = null;
                }

                if (card != null
                    && invoke.Kind != CardEffectKind.Death
                    && invoke.Kind != CardEffectKind.Use)
                {
                    CardManagerSingleton.Instance.RefreshDisplayMode(card);
                }
            }
        }

        private async UniTask PlayHitFlashOverlayAsync(
            CardEffectSO effect,
            CardEffectInvokeContext invoke,
            CancellationToken cancellationToken)
        {
            var card = ResolveManagedCard();
            var playContext = BuildPlayContext(card, invoke, cancellationToken);

            try
            {
                await effect.PlayAsync(playContext);
            }
            catch (System.OperationCanceledException)
            {
                effect.Stop(playContext);
            }
        }

        private void PrepareMotion()
        {
            if (_view != null)
            {
                CardDeckTween.KillMotion(_view.transform);
            }

            _visualDriver ??= GetComponent<CardVisualDriver>();
            _visualDriver?.InterruptFeedbackMotion();
        }

        private CardEffectSO ResolveEffect(CardEffectKind kind, CardBoardDirection selfDirection)
        {
            if (bindings == null)
            {
                return null;
            }

            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding != null && binding.kind == kind)
                {
                    return binding.Resolve(selfDirection);
                }
            }

            return null;
        }

        private ManagedCard ResolveManagedCard()
        {
            _visualDriver ??= GetComponent<CardVisualDriver>();
            return _visualDriver?.BoundCard;
        }

        private CardEffectPlayContext BuildPlayContext(
            ManagedCard card,
            CardEffectInvokeContext invoke,
            CancellationToken cancellationToken)
        {
            var root = _view != null ? _view.transform : transform;
            var selfWorld = root.position;
            Vector3? otherWorld = null;

            var field = GroundFieldManagerSingleton.Instance;
            if (field != null)
            {
                if (invoke.SelfSlot > 0)
                {
                    var selfAnchor = field.GetGroundAnchor(invoke.SelfSlot);
                    if (selfAnchor != null)
                    {
                        selfWorld = selfAnchor.position;
                    }
                }

                if (invoke.OtherSlot.HasValue)
                {
                    var otherAnchor = field.GetGroundAnchor(invoke.OtherSlot.Value);
                    if (otherAnchor != null)
                    {
                        otherWorld = otherAnchor.position;
                    }
                }
            }

            return new CardEffectPlayContext(
                card,
                root,
                _view,
                invoke,
                selfWorld,
                otherWorld,
                cancellationToken);
        }

#if UNITY_EDITOR
        [FoldoutGroup("Debug")]
        [Tooltip("Odin 调试：模拟本卡表演方向（左右点测用 Right/Left）。")]
        [SerializeField] private CardBoardDirection debugSelfDirection = CardBoardDirection.Right;

        [FoldoutGroup("Debug")]
        [Tooltip("Odin 调试：模拟自身格位（1~9）。")]
        [SerializeField] private int debugSelfSlot = 1;

        [FoldoutGroup("Debug")]
        [Tooltip("Odin 调试：模拟关联格位（0 表示无）。")]
        [SerializeField] private int debugOtherSlot;

        [FoldoutGroup("Debug")]
        [Tooltip("Odin 调试：模拟强度参数。")]
        [SerializeField] private float debugMagnitude;

        [FoldoutGroup("Debug")]
        [Button("播放攻击", ButtonSizes.Medium)]
        private void DebugPlayAttack()
        {
            PlayAsync(BuildDebugInvoke(CardEffectKind.Attack)).Forget();
        }

        [FoldoutGroup("Debug")]
        [Button("播放受击", ButtonSizes.Medium)]
        private void DebugPlayHit()
        {
            PlayAsync(BuildDebugInvoke(CardEffectKind.Hit)).Forget();
        }

        [FoldoutGroup("Debug")]
        [Button("播放死亡", ButtonSizes.Medium)]
        private void DebugPlayDeath()
        {
            PlayDeathAsync(debugSelfSlot, debugSelfDirection).Forget();
        }

        [FoldoutGroup("Debug")]
        [Button("播放使用", ButtonSizes.Medium)]
        private void DebugPlayUse()
        {
            PlayUseAsync(debugSelfDirection).Forget();
        }

        [FoldoutGroup("Debug")]
        [Button("播放闪白", ButtonSizes.Medium)]
        private void DebugPlayHitFlash()
        {
            PlayHitFlashAsync(debugSelfDirection, debugSelfSlot).Forget();
        }

        [FoldoutGroup("Debug")]
        [Button("停止当前", ButtonSizes.Small)]
        private void DebugStopCurrent()
        {
            StopCurrent();
        }

        private CardEffectInvokeContext BuildDebugInvoke(CardEffectKind kind)
        {
            var otherSlot = debugOtherSlot > 0 ? debugOtherSlot : (int?)null;
            return kind switch
            {
                CardEffectKind.Attack => CardEffectInvokeContext.ForAttack(
                    debugSelfDirection,
                    debugSelfSlot,
                    otherSlot,
                    isOrchestrated: false),
                CardEffectKind.Hit => CardEffectInvokeContext.ForHit(
                    debugSelfDirection,
                    debugSelfSlot,
                    otherSlot,
                    isOrchestrated: false),
                CardEffectKind.HitFlash => CardEffectInvokeContext.ForHitFlash(
                    debugSelfDirection,
                    debugSelfSlot,
                    otherSlot,
                    isOrchestrated: false),
                _ => new CardEffectInvokeContext(kind, debugSelfDirection, debugSelfSlot, isOrchestrated: false),
            };
        }
#endif
    }
}
