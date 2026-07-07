#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace NineGrid.Cards.Editor
{
    public static class CardPresentationMigrationUtility
    {
        public const string PresentationChildName = "Presentation";

        private static readonly HashSet<string> PresentationTypeFullNames = new(StringComparer.Ordinal)
        {
            "Dott.DOTweenTimeline",
            "DG.Tweening.DOTweenAnimation",
            "Dott.DOTweenLink",
            "Dott.DOTweenCallback",
        };

        [MenuItem("NineGrid/Cards/Migrate Presentation Timeline To Child")]
        public static void MigrateSelectedCards()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                Debug.LogWarning("[CardPresentationMigration] 请在 Hierarchy 中选中卡牌根节点。");
                return;
            }

            for (var i = 0; i < selected.Length; i++)
            {
                MigrateCardRoot(selected[i]);
            }
        }

        [MenuItem("NineGrid/Cards/Migrate Scene Test Cards Presentation")]
        public static void MigrateSceneTestCards()
        {
            MigrateByName("Standard Card");
            MigrateByName("Standard Card (1)");
            RepairSceneTestCardLink();
        }

        [MenuItem("NineGrid/Cards/Repair Scene Test Cards Presentation Links")]
        public static void RepairSceneTestCardLink()
        {
            var attacker = GameObject.Find("Standard Card");
            var victim = GameObject.Find("Standard Card (1)");
            if (attacker == null || victim == null)
            {
                return;
            }

            var attackerPresentation = attacker.transform.Find(PresentationChildName);
            var victimPresentation = victim.transform.Find(PresentationChildName);
            if (attackerPresentation == null || victimPresentation == null)
            {
                return;
            }

            var linkType = ResolveTypeByName("Dott.DOTweenLink");
            var timelineType = ResolveTypeByName("Dott.DOTweenTimeline");
            if (linkType == null || timelineType == null)
            {
                return;
            }

            var link = attackerPresentation.GetComponent(linkType);
            var victimTimeline = victimPresentation.GetComponent(timelineType);
            if (link == null || victimTimeline == null)
            {
                return;
            }

            var serialized = new SerializedObject(link);
            var timelineProperty = serialized.FindProperty("timeline");
            if (timelineProperty == null)
            {
                return;
            }

            timelineProperty.objectReferenceValue = victimTimeline;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(link);
            Debug.Log("[CardPresentationMigration] 已修复攻击卡 DOTweenLink。");
        }

        public static GameObject MigrateCardRoot(GameObject cardRoot)
        {
            if (cardRoot == null)
            {
                return null;
            }

            Undo.SetCurrentGroupName("Migrate Card Presentation");
            var undoGroup = Undo.GetCurrentGroup();

            var presentation = GetOrCreatePresentationChild(cardRoot.transform);
            var movedCount = 0;
            var components = CollectPresentationComponents(cardRoot);

            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                ComponentUtility.CopyComponent(component);
                if (!ComponentUtility.PasteComponentAsNew(presentation))
                {
                    Debug.LogWarning(
                        $"[CardPresentationMigration] 无法迁移 {component.GetType().Name} → {presentation.name}",
                        component);
                    continue;
                }

                Undo.DestroyObjectImmediate(component);
                movedCount++;
            }

            RetargetDotweenAnimations(presentation, cardRoot.transform);
            EditorUtility.SetDirty(cardRoot);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                $"[CardPresentationMigration] {cardRoot.name}: 已迁移 {movedCount} 个表现组件到 {PresentationChildName}。",
                cardRoot);
            return presentation;
        }

        private static List<Component> CollectPresentationComponents(GameObject cardRoot)
        {
            var results = new List<Component>();
            var components = cardRoot.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                var fullName = component.GetType().FullName;
                if (fullName != null && PresentationTypeFullNames.Contains(fullName))
                {
                    results.Add(component);
                }
            }

            return results;
        }

        private static void MigrateByName(string objectName)
        {
            var card = GameObject.Find(objectName);
            if (card == null)
            {
                Debug.LogWarning($"[CardPresentationMigration] 场景中未找到 {objectName}。");
                return;
            }

            MigrateCardRoot(card);
        }

        private static GameObject GetOrCreatePresentationChild(Transform cardRoot)
        {
            var existing = cardRoot.Find(PresentationChildName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var child = new GameObject(PresentationChildName);
            Undo.RegisterCreatedObjectUndo(child, "Create Card Presentation");
            child.transform.SetParent(cardRoot, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        private static void RetargetDotweenAnimations(GameObject presentation, Transform cardRoot)
        {
            var components = presentation.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null || component.GetType().FullName != "DG.Tweening.DOTweenAnimation")
                {
                    continue;
                }

                var serialized = new SerializedObject(component);
                var targetIsSelf = serialized.FindProperty("targetIsSelf");
                var targetGo = serialized.FindProperty("targetGO");
                if (targetIsSelf == null || targetGo == null)
                {
                    continue;
                }

                targetIsSelf.boolValue = false;
                targetGo.objectReferenceValue = cardRoot.gameObject;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Type ResolveTypeByName(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
#endif
