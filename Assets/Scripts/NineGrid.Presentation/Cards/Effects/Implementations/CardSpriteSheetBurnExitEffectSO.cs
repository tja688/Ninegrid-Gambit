using Cysharp.Threading.Tasks;
using NineGrid.Cards.Anim;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards
{
    /// <summary>
    /// 卡牌退场：脱卡生成 Burning 精灵表 FX，立刻藏卡，PlayAsync 即返回（发射后不管）。
    /// 帧默认取 PixelartCardTCG CardEffects_Burning_0~6。
    /// Sorting 必须落在卡牌同层（Main），Default 会沉在 BG 之下不可见。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CardSpriteSheetBurnExitEffect",
        menuName = "NineGrid/Cards/Effects/Sprite Sheet Burn Exit")]
    public sealed class CardSpriteSheetBurnExitEffectSO : CardEffectSO
    {
        public const string DefaultSortingLayerName = "Main";

        [Tooltip("Burning 帧序列（建议 CardEffects_Burning_0~6）。")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("精灵表播放帧率（相对源 12fps；快两倍用 24）。")]
        [SerializeField] private float fps = 24f;

        [Tooltip("相对卡面包围盒的拟合倍率（1=长边贴齐）。")]
        [SerializeField] private float fitToCardScale = 1f;

        [Tooltip("无法测到卡面时的兜底统一缩放。")]
        [SerializeField] private float fallbackUniformScale = 0.94f;

        [Tooltip("Sorting Layer；留空则优先继承卡牌 SortingGroup，再回退 Main。")]
        [SerializeField] private string sortingLayerName = DefaultSortingLayerName;

        [Tooltip("FX SortingOrder（须高于场上卡牌序）。")]
        [SerializeField] private int sortingOrder = 5000;

        [Tooltip("无帧或帧极少时的兜底销毁延迟（秒）。")]
        [SerializeField] private float fallbackLifetimeSeconds = 0.29f;

        public override UniTask PlayAsync(CardEffectPlayContext context)
        {
            var root = context.Root;
            var worldPos = ResolveWorldPosition(context);
            var cardWorldSize = MeasureCardWorldSize(root);

            if (root != null)
            {
                root.localScale = Vector3.zero;
            }

            if (frames == null || frames.Length == 0)
            {
                return UniTask.CompletedTask;
            }

            var fx = new GameObject("CardBurnExitFx");
            fx.transform.SetPositionAndRotation(worldPos, Quaternion.identity);
            fx.transform.localScale = Vector3.one;

            var renderer = fx.AddComponent<SpriteRenderer>();
            ApplySorting(renderer, root);

            var player = fx.AddComponent<SpriteSheetLoopPlayer>();
            player.BindTarget(renderer);
            player.Fps = Mathf.Max(0.01f, fps);
            player.Looping = false;
            player.UniformScale = ResolveUniformScale(frames[0], cardWorldSize);
            player.SetFrames(frames, play: true);

            var lifetime = EstimateLifetimeSeconds();
            // 不挂 context.CancellationToken：卡 Release / 效果取消后 FX 仍播完自毁。
            DestroyAfterAsync(fx, lifetime).Forget();
            return UniTask.CompletedTask;
        }

        private float ResolveUniformScale(Sprite firstFrame, Vector2 cardWorldSize)
        {
            if (firstFrame == null)
            {
                return Mathf.Max(0.01f, fallbackUniformScale);
            }

            var burnSize = firstFrame.bounds.size;
            if (burnSize.x < 0.001f || burnSize.y < 0.001f)
            {
                return Mathf.Max(0.01f, fallbackUniformScale);
            }

            if (cardWorldSize.x < 0.001f || cardWorldSize.y < 0.001f)
            {
                return Mathf.Max(0.01f, fallbackUniformScale);
            }

            // 用较短边比例贴齐卡面，避免碎亡圈明显大于卡框。
            var scale = Mathf.Min(cardWorldSize.x / burnSize.x, cardWorldSize.y / burnSize.y);
            return Mathf.Max(0.01f, scale * Mathf.Max(0.01f, fitToCardScale));
        }

        private static Vector2 MeasureCardWorldSize(Transform root)
        {
            if (root == null)
            {
                return Vector2.zero;
            }

            // 藏卡前用 Renderer.bounds；SortingGroup 下仍可读子 Sprite 世界包围盒。
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
            var hasBounds = false;
            var min = Vector3.zero;
            var max = Vector3.zero;
            for (var i = 0; i < renderers.Length; i++)
            {
                var sr = renderers[i];
                if (sr == null || !sr.enabled || sr.sprite == null)
                {
                    continue;
                }

                var b = sr.bounds;
                if (!hasBounds)
                {
                    min = b.min;
                    max = b.max;
                    hasBounds = true;
                }
                else
                {
                    min = Vector3.Min(min, b.min);
                    max = Vector3.Max(max, b.max);
                }
            }

            if (!hasBounds)
            {
                // 模板边框约 3.31×4.69（怪物卡标准模板）。
                return new Vector2(3.31f, 4.69f);
            }

            return new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
        }

        private void ApplySorting(SpriteRenderer renderer, Transform root)
        {
            if (renderer == null)
            {
                return;
            }

            var layerName = sortingLayerName;
            if (string.IsNullOrWhiteSpace(layerName) && root != null)
            {
                var group = root.GetComponent<SortingGroup>();
                if (group != null && !string.IsNullOrEmpty(group.sortingLayerName))
                {
                    layerName = group.sortingLayerName;
                }
            }

            if (string.IsNullOrWhiteSpace(layerName))
            {
                layerName = DefaultSortingLayerName;
            }

            renderer.sortingLayerName = layerName;
            renderer.sortingOrder = sortingOrder;
        }

        private static Vector3 ResolveWorldPosition(CardEffectPlayContext context)
        {
            // SelfWorldPosition 由 EffectManager 按格锚点填写；击杀前尸体会被 Stage 挪走，不能用 Root.position。
            var worldPos = context.SelfWorldPosition;
            if (worldPos == Vector3.zero && context.Root != null)
            {
                worldPos = context.Root.position;
            }

            return worldPos;
        }

        private float EstimateLifetimeSeconds()
        {
            if (frames == null || frames.Length == 0)
            {
                return Mathf.Max(0.05f, fallbackLifetimeSeconds);
            }

            var rate = Mathf.Max(0.01f, fps);
            return Mathf.Max(0.05f, frames.Length / rate);
        }

        private static async UniTaskVoid DestroyAfterAsync(GameObject fx, float seconds)
        {
            if (fx == null)
            {
                return;
            }

            try
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(Mathf.Max(0.01f, seconds)),
                    cancellationToken: fx.GetCancellationTokenOnDestroy());
            }
            catch (System.OperationCanceledException)
            {
            }
            finally
            {
                if (fx != null)
                {
                    Object.Destroy(fx);
                }
            }
        }
    }
}
