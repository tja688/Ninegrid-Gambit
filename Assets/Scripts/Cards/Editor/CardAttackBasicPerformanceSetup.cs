#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace NineGrid.Cards.Editor
{
    public static class CardAttackBasicPerformanceSetup
    {
        private const string RootName = "CardAttackBasic";
        private const string RightName = "CardAttackBasicRight";
        private const string LeftName = "CardAttackBasicLeft";
        private const string AttackerCardName = "Standard Card";
        private const string VictimCardName = "Standard Card (1)";
        private const string PresentationChildName = "Presentation";
        private const float OrchestrationDelay = 0.4f;
        private const string HitFlashMaterialPath = "Assets/Arts/VisualProfiles/TableNineSpriteHitFlash.mat";

        private static readonly ClipSpec RightWindup = new(0f, 0.1f, 15, new Vector3(-0.1f, 0f, 0f));
        private static readonly ClipSpec RightLunge = new(0.1f, 0.3f, 23, new Vector3(1.0625f, 0f, 0f));
        private static readonly ClipSpec RightHit = new(OrchestrationDelay, 0.3f, 18, new Vector3(3f, 0f, 0f));

        [MenuItem("NineGrid/Cards/Setup CardAttackBasic Performance Rigs")]
        public static void SetupSceneRigs()
        {
            var attacker = GameObject.Find(AttackerCardName);
            var victim = GameObject.Find(VictimCardName);
            if (attacker == null || victim == null)
            {
                Debug.LogWarning(
                    "[CardAttackBasicPerformanceSetup] 场景中需要 Standard Card 与 Standard Card (1)。");
                return;
            }

            Undo.SetCurrentGroupName("Setup CardAttackBasic Performance");
            var undoGroup = Undo.GetCurrentGroup();

            RemovePresentationChild(attacker);
            RemovePresentationChild(victim);

            var root = GetOrCreateRoot();
            CleanupOrphanRig(RightName);
            CleanupOrphanRig(LeftName);
            var right = GetOrCreateChild(root, RightName);
            var left = GetOrCreateChild(root, LeftName);

            BuildRig(right, attacker.transform, victim.transform, CardBoardDirection.Right);
            BuildRig(left, attacker.transform, victim.transform, CardBoardDirection.Left);

            Selection.activeGameObject = right;
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[CardAttackBasicPerformanceSetup] CardAttackBasic Left/Right 表演 rig 已就绪。");
        }

        private static void BuildRig(
            GameObject rig,
            Transform attacker,
            Transform victim,
            CardBoardDirection direction)
        {
            ClearPresentationComponents(rig);

            var timeline = GetOrAddComponent(rig, "Dott.DOTweenTimeline");
            GetOrAddComponent<CardPerformanceTimelinePlayer>(rig);

            var flash = GetOrAddComponent<CardSpriteHitFlash>(rig);
            var flashSerialized = new SerializedObject(flash);
            flashSerialized.FindProperty("rendererSearchRoot").objectReferenceValue = victim;
            flashSerialized.FindProperty("hitFlashMaterialTemplate").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>(HitFlashMaterialPath);
            flashSerialized.ApplyModifiedPropertiesWithoutUndo();

            AddMoveTween(rig, attacker.gameObject, ApplyDirection(RightWindup, direction, victim, false));
            AddMoveTween(rig, attacker.gameObject, ApplyDirection(RightLunge, direction, victim, false));
            AddMoveTween(rig, victim.gameObject, ApplyDirection(RightHit, direction, victim, true));

            var callback = GetOrAddComponent(rig, "Dott.DOTweenCallback");
            WireFlashCallback(callback, flash, OrchestrationDelay);

            var player = rig.GetComponent<CardPerformanceTimelinePlayer>();
            if (player != null)
            {
                var playerSerialized = new SerializedObject(player);
                playerSerialized.FindProperty("timeline").objectReferenceValue = timeline;
                playerSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(rig);
        }

        private static ClipSpec ApplyDirection(
            ClipSpec source,
            CardBoardDirection direction,
            Transform victim,
            bool isHitClip)
        {
            var endValue = source.EndValue;
            if (isHitClip)
            {
                var victimHomeX = victim.localPosition.x;
                var delta = source.EndValue.x - victimHomeX;
                endValue.x = victimHomeX + CardDOTweenDirectionUtility.ResolveHorizontalSign(direction) * Mathf.Abs(delta);
            }
            else
            {
                endValue = CardDOTweenDirectionUtility.ApplyHorizontalSign(source.EndValue, direction);
            }

            return source.WithEndValue(endValue);
        }

        private static void AddMoveTween(GameObject rig, GameObject target, ClipSpec clip)
        {
            var animationType = ResolveType("DG.Tweening.DOTweenAnimation")
                ?? throw new InvalidOperationException("未找到 DG.Tweening.DOTweenAnimation。");
            var animation = Undo.AddComponent(rig, animationType);
            var serialized = new SerializedObject(animation);
            serialized.FindProperty("targetIsSelf").boolValue = false;
            serialized.FindProperty("targetGO").objectReferenceValue = target;
            serialized.FindProperty("target").objectReferenceValue = target.transform;
            serialized.FindProperty("delay").floatValue = clip.Delay;
            serialized.FindProperty("duration").floatValue = clip.Duration;
            serialized.FindProperty("easeType").enumValueIndex = clip.Ease;
            serialized.FindProperty("animationType").enumValueIndex = 2;
            serialized.FindProperty("targetType").enumValueIndex = 11;
            serialized.FindProperty("endValueV3").vector3Value = clip.EndValue;
            serialized.FindProperty("autoPlay").boolValue = false;
            serialized.FindProperty("autoGenerate").boolValue = false;
            serialized.FindProperty("isActive").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireFlashCallback(Component callback, CardSpriteHitFlash flash, float delay)
        {
            var serialized = new SerializedObject(callback);
            serialized.FindProperty("delay").floatValue = delay;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var callbackType = callback.GetType();
            var onCallbackField = callbackType.GetField("onCallback", BindingFlags.Instance | BindingFlags.Public);
            if (onCallbackField?.GetValue(callback) is not UnityEvent unityEvent)
            {
                return;
            }

            for (var i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                UnityEventTools.RemovePersistentListener(unityEvent, i);
            }

            UnityEventTools.AddPersistentListener(unityEvent, flash.PlayHitFlash);
            EditorUtility.SetDirty(callback);
        }

        private static void RemovePresentationChild(GameObject cardRoot)
        {
            var presentation = cardRoot.transform.Find(PresentationChildName);
            if (presentation != null)
            {
                Undo.DestroyObjectImmediate(presentation.gameObject);
            }
        }

        private static void ClearPresentationComponents(GameObject rig)
        {
            RemoveComponentsByTypeName(rig, "Dott.DOTweenTimeline");
            RemoveComponentsByTypeName(rig, "DG.Tweening.DOTweenAnimation");
            RemoveComponentsByTypeName(rig, "Dott.DOTweenLink");
            RemoveComponentsByTypeName(rig, "Dott.DOTweenCallback");
            RemoveComponents<CardSpriteHitFlash>(rig);
            RemoveComponents<CardPerformanceTimelinePlayer>(rig);
        }

        private static void RemoveComponentsByTypeName(GameObject gameObject, string fullName)
        {
            var type = ResolveType(fullName);
            if (type == null)
            {
                return;
            }

            var components = gameObject.GetComponents(type);
            for (var i = components.Length - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(components[i]);
            }
        }

        private static void RemoveComponents<T>(GameObject gameObject) where T : Component
        {
            var components = gameObject.GetComponents<T>();
            for (var i = components.Length - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(components[i]);
            }
        }

        private static GameObject GetOrCreateRoot()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create CardAttackBasic");
            return root;
        }

        private static GameObject GetOrCreateChild(GameObject parent, string childName)
        {
            var existing = parent.transform.Find(childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var child = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static void CleanupOrphanRig(string rigName)
        {
            var rigs = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (var i = 0; i < rigs.Length; i++)
            {
                var transform = rigs[i];
                if (transform == null || transform.name != rigName)
                {
                    continue;
                }

                if (transform.parent != null &&
                    transform.parent.name == RootName)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(transform.gameObject);
            }
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            var existing = gameObject.GetComponent<T>();
            if (existing != null)
            {
                return existing;
            }

            return Undo.AddComponent<T>(gameObject);
        }

        private static Component GetOrAddComponent(GameObject gameObject, string typeName)
        {
            var type = ResolveType(typeName);
            if (type == null)
            {
                throw new InvalidOperationException($"未找到类型：{typeName}");
            }

            var existing = gameObject.GetComponent(type);
            if (existing != null)
            {
                return existing;
            }

            return Undo.AddComponent(gameObject, type);
        }

        private static Type ResolveType(string fullName)
        {
            var type = Type.GetType($"{fullName}, Assembly-CSharp-firstpass")
                ?? Type.GetType($"{fullName}, DOTweenPro");
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

        private readonly struct ClipSpec
        {
            public ClipSpec(float delay, float duration, int ease, Vector3 endValue)
            {
                Delay = delay;
                Duration = duration;
                Ease = ease;
                EndValue = endValue;
            }

            public float Delay { get; }
            public float Duration { get; }
            public int Ease { get; }
            public Vector3 EndValue { get; }

            public ClipSpec WithEndValue(Vector3 endValue) =>
                new(Delay, Duration, Ease, endValue);
        }
    }
}
#endif
