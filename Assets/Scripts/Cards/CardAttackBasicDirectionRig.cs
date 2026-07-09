using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace NineGrid.Cards
{
    /// <summary>
    /// 单方向 CardAttackBasic 表演 rig：运行时重绑攻击者/受击者 Transform 与 CardEffectManager 回调。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardAttackBasicDirectionRig : MonoBehaviour
    {
        private const float HitFlashCallbackDelayMax = 0.45f;
        private const float DeathCallbackDelayMin = 0.5f;
        private const string DotweenAnimationTypeName = "DG.Tweening.DOTweenAnimation";
        private const string DotweenCallbackTypeName = "Dott.DOTweenCallback";
        private const string DotweenTimelineTypeName = "Dott.DOTweenTimeline";

        [Header("Templates (Editor 调手感占位，用于划分 tween 归属)")]
        [Tooltip("场景中攻击者占位 Standard Card；运行时会被真实对象替换。留空时 Awake 按 tween 数量自动推断。")]
        [SerializeField] private GameObject attackerTemplate;

        [Tooltip("场景中受击者占位 Standard Card；运行时会被真实对象替换。留空时 Awake 按 tween 数量自动推断。")]
        [SerializeField] private GameObject victimTemplate;

        [Header("Timeline")]
        [Tooltip("同物体上的 CardPerformanceTimelinePlayer；留空时 Awake 自动查找。")]
        [SerializeField] private CardPerformanceTimelinePlayer timelinePlayer;

        [Tooltip("同物体上的 DOTweenTimeline；留空时从 timelinePlayer 推断。")]
        [SerializeField] private Component dotweenTimeline;

        [Header("Runtime Maps")]
        [SerializeField] private List<Component> attackerAnimations = new();
        [SerializeField] private List<Component> victimAnimations = new();
        [SerializeField] private Component hitFlashCallback;
        [SerializeField] private Component deathCallback;

        private static Type _dotweenAnimationType;
        private static FieldInfo _targetIsSelfField;
        private static FieldInfo _targetGoField;
        private static FieldInfo _targetField;
        private static FieldInfo _endValueV3Field;
        private static FieldInfo _delayField;

        private const float DefaultVictimKnockbackDistance = 3f;
        private const float WindupDelayMax = 0.05f;
        private const float LungeDelayMax = 0.35f;
        private const float KnockbackDelayMin = 0.35f;
        private const float KnockbackDelayMax = 0.55f;
        private const float ReturnDelayMin = 0.65f;

        private bool _roleMapBuilt;
        private bool _bakedClipValuesCached;
        private readonly Dictionary<int, Vector3> _bakedEndValuesByAnimId = new();

        public CardBoardDirection Direction { get; private set; }

        private void Awake()
        {
            EnsureDotweenReflection();
            Direction = ResolveDirectionFromName();
            timelinePlayer ??= GetComponent<CardPerformanceTimelinePlayer>();
            dotweenTimeline ??= FindTimelineOn(gameObject);
            BuildRoleMapIfNeeded();
            CacheCallbacksIfNeeded();
            CacheBakedClipValuesIfNeeded();
        }

        public void BindParticipants(
            Transform attacker,
            Transform victim,
            CardEffectManager victimEffects,
            bool bindDeathCallback,
            bool relativeAttackerMotion = false,
            bool relativeVictimKnockback = false,
            float victimKnockbackCoefficient = 1f)
        {
            BindParticipants(
                attacker,
                victim,
                victimEffects,
                bindDeathCallback,
                relativeAttackerMotion,
                relativeVictimKnockback,
                victimKnockbackCoefficient,
                BattleHitFlashTimingPolicy.Heuristic,
                hitFlashCallbackDelay: 0f,
                deathCallbackDelay: 0f);
        }

        /// <summary>
        /// 按战斗编排绑参重绑参战双方与 Timeline 回调。
        /// </summary>
        public void BindParticipants(Transform attacker, Transform victim, CardEffectManager victimEffects, in BattleBindParams bind)
        {
            BindParticipants(
                attacker,
                victim,
                victimEffects,
                bind.BindDeathCallback,
                bind.UseRelativeAttackerMotion,
                bind.UseRelativeVictimKnockback,
                bind.VictimKnockbackCoefficient,
                bind.HitFlashTimingPolicy,
                bind.HitFlashCallbackDelay,
                bind.DeathCallbackDelay);
        }

        public void BindParticipants(
            Transform attacker,
            Transform victim,
            CardEffectManager victimEffects,
            bool bindDeathCallback,
            bool relativeAttackerMotion,
            bool relativeVictimKnockback,
            float victimKnockbackCoefficient,
            BattleHitFlashTimingPolicy hitFlashTimingPolicy,
            float hitFlashCallbackDelay,
            float deathCallbackDelay)
        {
            BuildRoleMapIfNeeded();
            CacheBakedClipValuesIfNeeded();

            if (attacker == null || victim == null)
            {
                Debug.LogWarning($"[{nameof(CardAttackBasicDirectionRig)}] {name} 绑定失败：攻击者或受击者为空。", this);
                return;
            }

            // 相对位移会原地改写 DOTweenAnimation.endValueV3；每次绑定必须先回到场景烘焙值，
            // 否则 Absolute 路径会继承上一场 Relative（如反击）留下的世界坐标局部化结果 → 隔空击打。
            RestoreBakedEndValues(attackerAnimations);
            RestoreBakedEndValues(victimAnimations);

            if (relativeAttackerMotion)
            {
                RebindAttackerMotionFromVictim(attacker, victim);
            }
            else
            {
                RebindAnimations(attackerAnimations, attacker);
            }

            if (relativeVictimKnockback)
            {
                RebindVictimKnockbackFromAttacker(victim, attacker, victimKnockbackCoefficient);
            }
            else
            {
                RebindAnimations(victimAnimations, victim);
                ScaleVictimKnockbackEndValues(victimKnockbackCoefficient);
            }

            if (hitFlashTimingPolicy == BattleHitFlashTimingPolicy.Explicit)
            {
                ApplyExplicitCallbackDelays(hitFlashCallbackDelay, bindDeathCallback ? deathCallbackDelay : 0f);
            }

            ConfigureHitFlashCallback(victimEffects);
            ConfigureDeathCallback(victimEffects, bindDeathCallback);
        }

        public async UniTask PlayAsync(CancellationToken cancellationToken = default)
        {
            if (timelinePlayer == null)
            {
                Debug.LogWarning($"[{nameof(CardAttackBasicDirectionRig)}] {name} 未配置 Timeline 播放器。", this);
                return;
            }

            var timeline = dotweenTimeline != null ? dotweenTimeline : FindTimelineOn(gameObject);
            if (timeline == null)
            {
                Debug.LogWarning($"[{nameof(CardAttackBasicDirectionRig)}] {name} 未找到 DOTweenTimeline。", this);
                return;
            }

            timelinePlayer.RestartTimeline();

            var sequence = ResolveSequence(timeline);
            if (sequence == null || !sequence.IsActive())
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.9f), cancellationToken: cancellationToken);
                return;
            }

            await WaitForSequenceAsync(sequence, cancellationToken);
        }

        private static async UniTask WaitForSequenceAsync(Sequence sequence, CancellationToken cancellationToken)
        {
            if (sequence == null)
            {
                return;
            }

            while (sequence.IsActive() && !sequence.IsComplete())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        public void ResetParticipantMotion(Transform attacker, Transform victim)
        {
            if (attacker != null)
            {
                CardDeckTween.KillMotion(attacker);
            }

            if (victim != null)
            {
                CardDeckTween.KillMotion(victim);
            }
        }

        private void BuildRoleMapIfNeeded()
        {
            EnsureDotweenReflection();
            if (attackerAnimations.Count > 0 && victimAnimations.Count > 0)
            {
                _roleMapBuilt = true;
                return;
            }

            if (_roleMapBuilt)
            {
                return;
            }

            attackerAnimations.Clear();
            victimAnimations.Clear();

            var animations = GetDotweenAnimations();
            if (animations.Length == 0)
            {
                return;
            }

            if (attackerTemplate != null || victimTemplate != null)
            {
                for (var i = 0; i < animations.Length; i++)
                {
                    var animation = animations[i];
                    if (animation == null)
                    {
                        continue;
                    }

                    var targetGo = ReadTargetGameObject(animation);
                    if (attackerTemplate != null && targetGo == attackerTemplate)
                    {
                        attackerAnimations.Add(animation);
                    }
                    else if (victimTemplate != null && targetGo == victimTemplate)
                    {
                        victimAnimations.Add(animation);
                    }
                }
            }
            else
            {
                InferTemplatesFromAnimations(animations);
                BuildRoleMapIfNeeded();
                return;
            }

            _roleMapBuilt = attackerAnimations.Count > 0 && victimAnimations.Count > 0;
        }

        private void InferTemplatesFromAnimations(Component[] animations)
        {
            var counts = new Dictionary<int, int>();
            for (var i = 0; i < animations.Length; i++)
            {
                var targetGo = ReadTargetGameObject(animations[i]);
                if (targetGo == null)
                {
                    continue;
                }

                var id = targetGo.GetInstanceID();
                counts.TryGetValue(id, out var count);
                counts[id] = count + 1;
            }

            if (counts.Count == 0)
            {
                return;
            }

            var bestAttackerId = 0;
            var bestAttackerCount = -1;
            var bestVictimId = 0;
            var bestVictimCount = -1;
            foreach (var pair in counts)
            {
                if (pair.Value > bestAttackerCount)
                {
                    bestAttackerCount = pair.Value;
                    bestAttackerId = pair.Key;
                }
            }

            foreach (var pair in counts)
            {
                if (pair.Key == bestAttackerId)
                {
                    continue;
                }

                if (pair.Value > bestVictimCount)
                {
                    bestVictimCount = pair.Value;
                    bestVictimId = pair.Key;
                }
            }

            for (var i = 0; i < animations.Length; i++)
            {
                var target = ReadTargetGameObject(animations[i]);
                if (target == null)
                {
                    continue;
                }

                var id = target.GetInstanceID();
                if (id == bestAttackerId)
                {
                    attackerTemplate ??= target;
                }
                else if (id == bestVictimId)
                {
                    victimTemplate ??= target;
                }
            }
        }

        private void CacheCallbacksIfNeeded()
        {
            if (hitFlashCallback != null && deathCallback != null)
            {
                return;
            }

            var callbackType = ResolveType(DotweenCallbackTypeName);
            if (callbackType == null)
            {
                return;
            }

            var callbacks = GetComponents(callbackType);
            for (var i = 0; i < callbacks.Length; i++)
            {
                var callback = callbacks[i];
                if (callback == null)
                {
                    continue;
                }

                var delay = ReadCallbackDelay(callback);
                if (hitFlashCallback == null && delay <= HitFlashCallbackDelayMax)
                {
                    hitFlashCallback = callback;
                }
                else if (deathCallback == null && delay >= DeathCallbackDelayMin)
                {
                    deathCallback = callback;
                }
            }
        }

        private Component[] GetDotweenAnimations()
        {
            EnsureDotweenReflection();
            if (_dotweenAnimationType == null)
            {
                return Array.Empty<Component>();
            }

            return GetComponents(_dotweenAnimationType);
        }

        private void RebindAttackerMotionFromVictim(Transform attacker, Transform victim)
        {
            if (attackerAnimations == null || attackerAnimations.Count == 0)
            {
                return;
            }

            var homeWorld = attacker.position;
            var towardVictim = victim.position - homeWorld;
            var flatToward = new Vector3(towardVictim.x, towardVictim.y, 0f);
            if (flatToward.sqrMagnitude < 0.0001f)
            {
                flatToward = Vector3.right;
            }
            else
            {
                flatToward.Normalize();
            }

            var awayFromVictim = -flatToward;
            var homeBaked = Vector3.zero;

            for (var i = 0; i < attackerAnimations.Count; i++)
            {
                var animation = attackerAnimations[i];
                if (animation == null)
                {
                    continue;
                }

                WriteAnimationTarget(animation, attacker.gameObject, attacker);

                var bakedEnd = GetBakedEndValue(animation);
                var delay = ReadDelay(animation);
                var axisMagnitude = ExtractAxisMagnitude(bakedEnd - homeBaked);
                Vector3 worldEnd;
                if (delay <= WindupDelayMax)
                {
                    worldEnd = homeWorld + awayFromVictim * axisMagnitude;
                }
                else if (delay <= LungeDelayMax)
                {
                    worldEnd = homeWorld + flatToward * axisMagnitude;
                }
                else
                {
                    worldEnd = homeWorld;
                }

                WriteWorldEndAsLocal(animation, attacker, worldEnd);
            }
        }

        private void RebindVictimKnockbackFromAttacker(
            Transform victim,
            Transform attacker,
            float knockbackCoefficient)
        {
            if (victimAnimations == null || victimAnimations.Count == 0)
            {
                return;
            }

            var homeWorld = victim.position;
            var away = victim.position - attacker.position;
            var flatAway = new Vector3(away.x, away.y, 0f);
            if (flatAway.sqrMagnitude < 0.0001f)
            {
                flatAway = Vector3.right;
            }
            else
            {
                flatAway.Normalize();
            }

            var coefficient = Mathf.Max(0f, knockbackCoefficient);

            for (var i = 0; i < victimAnimations.Count; i++)
            {
                var animation = victimAnimations[i];
                if (animation == null)
                {
                    continue;
                }

                WriteAnimationTarget(animation, victim.gameObject, victim);

                var delay = ReadDelay(animation);
                var bakedEnd = GetBakedEndValue(animation);
                var axisMagnitude = ExtractAxisMagnitude(bakedEnd);
                var scale = coefficient;
                if (delay >= KnockbackDelayMin && delay <= KnockbackDelayMax)
                {
                    axisMagnitude = ReadBakedKnockbackMagnitude(animation) * scale;
                }
                else if (delay >= ReturnDelayMin)
                {
                    axisMagnitude *= scale;
                }

                var knockbackWorld = delay >= ReturnDelayMin && axisMagnitude <= 0.001f
                    ? homeWorld
                    : homeWorld + flatAway * axisMagnitude;
                WriteWorldEndAsLocal(animation, victim, knockbackWorld);
            }
        }

        private static void WriteWorldEndAsLocal(Component animation, Transform target, Vector3 worldEnd)
        {
            var localEnd = target.parent != null
                ? target.parent.InverseTransformPoint(worldEnd)
                : worldEnd;
            WriteEndValueV3(
                animation,
                new Vector3(localEnd.x, localEnd.y, target.localPosition.z));
        }

        private void CacheBakedClipValuesIfNeeded()
        {
            if (_bakedClipValuesCached)
            {
                return;
            }

            EnsureDotweenReflection();
            var animations = GetDotweenAnimations();
            for (var i = 0; i < animations.Length; i++)
            {
                var animation = animations[i];
                if (animation == null)
                {
                    continue;
                }

                _bakedEndValuesByAnimId[animation.GetInstanceID()] = ReadEndValueV3(animation);
            }

            _bakedClipValuesCached = true;
        }

        private void InvalidateBakedClipValueCache()
        {
            _bakedClipValuesCached = false;
            _bakedEndValuesByAnimId.Clear();
        }

        private Vector3 GetBakedEndValue(Component animation)
        {
            CacheBakedClipValuesIfNeeded();
            if (animation != null
                && _bakedEndValuesByAnimId.TryGetValue(animation.GetInstanceID(), out var baked))
            {
                return baked;
            }

            return ReadEndValueV3(animation);
        }

        private static float ExtractAxisMagnitude(Vector3 delta)
        {
            return Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z));
        }

        private float ReadBakedKnockbackMagnitude(Component animation)
        {
            var endValue = GetBakedEndValue(animation);
            var magnitude = ExtractAxisMagnitude(endValue);
            return magnitude > 0.01f ? magnitude : DefaultVictimKnockbackDistance;
        }

        private static float ReadDelay(Component animation)
        {
            EnsureDotweenReflection();
            if (animation == null || _delayField == null)
            {
                return 0f;
            }

            return Convert.ToSingle(_delayField.GetValue(animation));
        }

        private static Vector3 ReadEndValueV3(Component animation)
        {
            EnsureDotweenReflection();
            if (animation == null || _endValueV3Field == null)
            {
                return Vector3.zero;
            }

            return _endValueV3Field.GetValue(animation) is Vector3 value ? value : Vector3.zero;
        }

        private static void WriteEndValueV3(Component animation, Vector3 endValue)
        {
            EnsureDotweenReflection();
            _endValueV3Field?.SetValue(animation, endValue);
        }

        private static void RebindAnimations(IReadOnlyList<Component> animations, Transform target)
        {
            if (animations == null || target == null)
            {
                return;
            }

            var targetObject = target.gameObject;
            for (var i = 0; i < animations.Count; i++)
            {
                WriteAnimationTarget(animations[i], targetObject, target);
            }
        }

        /// <summary>
        /// 将动画 endValue 恢复为 Awake 时缓存的场景烘焙值。
        /// BindParticipants 的 Absolute / Relative 分支都假定从此干净起点出发。
        /// </summary>
        private void RestoreBakedEndValues(IReadOnlyList<Component> animations)
        {
            CacheBakedClipValuesIfNeeded();
            if (animations == null)
            {
                return;
            }

            for (var i = 0; i < animations.Count; i++)
            {
                var animation = animations[i];
                if (animation == null)
                {
                    continue;
                }

                WriteEndValueV3(animation, GetBakedEndValue(animation));
            }
        }

        /// <summary>
        /// 绝对位移路径下按系数缩放受击动画 endValue（相对路径由 RebindVictimKnockbackFromAttacker 处理）。
        /// 系数 1 保持场景烘焙手感；越界值应由 Profile clamp 后再传入。
        /// 调用前 BindParticipants 已 RestoreBakedEndValues，此处仍从烘焙字典重算以免系数残留。
        /// </summary>
        private void ScaleVictimKnockbackEndValues(float knockbackCoefficient)
        {
            CacheBakedClipValuesIfNeeded();
            if (victimAnimations == null)
            {
                return;
            }

            var scale = Mathf.Max(0f, knockbackCoefficient);
            for (var i = 0; i < victimAnimations.Count; i++)
            {
                var animation = victimAnimations[i];
                if (animation == null)
                {
                    continue;
                }

                var baked = GetBakedEndValue(animation);
                WriteEndValueV3(animation, baked * scale);
            }
        }

        private static void WriteAnimationTarget(Component animation, GameObject targetObject, Transform targetTransform)
        {
            if (animation == null || targetObject == null || targetTransform == null)
            {
                return;
            }

            EnsureDotweenReflection();
            _targetIsSelfField?.SetValue(animation, false);
            _targetGoField?.SetValue(animation, targetObject);
            _targetField?.SetValue(animation, targetTransform);
        }

        private static GameObject ReadTargetGameObject(Component animation)
        {
            EnsureDotweenReflection();
            return _targetGoField?.GetValue(animation) as GameObject;
        }

        private void ConfigureHitFlashCallback(CardEffectManager victimEffects)
        {
            if (hitFlashCallback == null || victimEffects == null)
            {
                return;
            }

            ReplaceCallbackListeners(hitFlashCallback, victimEffects.CallbackPlayHitFlash);
        }

        private void ConfigureDeathCallback(CardEffectManager victimEffects, bool bindDeathCallback)
        {
            if (deathCallback == null)
            {
                return;
            }

            ClearCallbackListeners(deathCallback);
            if (!bindDeathCallback || victimEffects == null)
            {
                return;
            }

            ReplaceCallbackListeners(
                deathCallback,
                () => victimEffects.Callback((int)CardEffectCallbackAction.PlayDeath));
        }

        private static void ReplaceCallbackListeners(Component callbackComponent, UnityAction action)
        {
            var unityEvent = ReadCallbackEvent(callbackComponent);
            if (unityEvent == null)
            {
                return;
            }

            unityEvent.RemoveAllListeners();
            if (action != null)
            {
                unityEvent.AddListener(action);
            }
        }

        private static void ClearCallbackListeners(Component callbackComponent)
        {
            ReadCallbackEvent(callbackComponent)?.RemoveAllListeners();
        }

        private static UnityEvent ReadCallbackEvent(Component callbackComponent)
        {
            if (callbackComponent == null)
            {
                return null;
            }

            var field = callbackComponent.GetType().GetField(
                "onCallback",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(callbackComponent) as UnityEvent;
        }

        private void ApplyExplicitCallbackDelays(float hitFlashDelay, float deathDelay)
        {
            CacheCallbacksIfNeeded();
            if (hitFlashCallback != null && hitFlashDelay > 0f)
            {
                WriteCallbackDelay(hitFlashCallback, hitFlashDelay);
            }

            if (deathCallback != null && deathDelay > 0f)
            {
                WriteCallbackDelay(deathCallback, deathDelay);
            }
        }

        private static float ReadCallbackDelay(Component callbackComponent)
        {
            if (callbackComponent == null)
            {
                return 0f;
            }

            var field = callbackComponent.GetType().GetField(
                "delay",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? Convert.ToSingle(field.GetValue(callbackComponent)) : 0f;
        }

        private static void WriteCallbackDelay(Component callbackComponent, float delay)
        {
            if (callbackComponent == null)
            {
                return;
            }

            var field = callbackComponent.GetType().GetField(
                "delay",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            field.SetValue(callbackComponent, Mathf.Max(0f, delay));
        }

        private static Sequence ResolveSequence(Component timelineComponent)
        {
            if (timelineComponent == null)
            {
                return null;
            }

            if (timelineComponent.GetType().FullName == DotweenTimelineTypeName)
            {
                var property = timelineComponent.GetType().GetProperty(
                    "Sequence",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return property?.GetValue(timelineComponent) as Sequence;
            }

            return null;
        }

        private static Component FindTimelineOn(GameObject host)
        {
            var components = host.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null && component.GetType().FullName == DotweenTimelineTypeName)
                {
                    return component;
                }
            }

            return null;
        }

        private static void EnsureDotweenReflection()
        {
            if (_dotweenAnimationType != null)
            {
                return;
            }

            _dotweenAnimationType = ResolveType(DotweenAnimationTypeName)
                ?? ResolveType($"{DotweenAnimationTypeName}, DOTweenPro");
            if (_dotweenAnimationType == null)
            {
                return;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _targetIsSelfField = _dotweenAnimationType.GetField("targetIsSelf", flags);
            _targetGoField = _dotweenAnimationType.GetField("targetGO", flags);
            _targetField = _dotweenAnimationType.GetField("target", flags);
            _endValueV3Field = _dotweenAnimationType.GetField("endValueV3", flags);
            _delayField = _dotweenAnimationType.GetField("delay", flags);
        }

        private static Type ResolveType(string fullName)
        {
            var type = Type.GetType(fullName);
            if (type != null)
            {
                return type;
            }

            type = Type.GetType($"{fullName}, Assembly-CSharp-firstpass");
            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        public static CardBoardDirection ResolveDirectionFromName(string rigName)
        {
            if (string.IsNullOrEmpty(rigName))
            {
                return CardBoardDirection.None;
            }

            if (rigName.Contains("Up"))
            {
                return CardBoardDirection.Up;
            }

            if (rigName.Contains("Down"))
            {
                return CardBoardDirection.Down;
            }

            if (rigName.Contains("Left"))
            {
                return CardBoardDirection.Left;
            }

            if (rigName.Contains("Right"))
            {
                return CardBoardDirection.Right;
            }

            return CardBoardDirection.None;
        }

        private CardBoardDirection ResolveDirectionFromName()
        {
            return ResolveDirectionFromName(name);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _roleMapBuilt = false;
            InvalidateBakedClipValueCache();
            if (!Application.isPlaying)
            {
                BuildRoleMapIfNeeded();
                CacheCallbacksIfNeeded();
                CacheBakedClipValuesIfNeeded();
            }
        }
#endif
    }
}
