using NineGrid.Cards;
using NineGrid.Flow;
using NineGrid.Presentation.Setup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Presentation.Editor
{
    /// <summary>
    /// C2：把 PresentationSceneRoot 挂到打开中的战斗场景并绑定宿主引用。
    /// </summary>
    public static class PresentationSceneRootSetup
    {
        [MenuItem("NineGrid/Presentation/Wire PresentationSceneRoot (active scene)")]
        public static void WireActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[PresentationSceneRootSetup] 无有效活动场景。");
                return;
            }

            var directors = GameObject.Find("Directors");
            if (directors == null)
            {
                directors = new GameObject("Directors");
                Undo.RegisterCreatedObjectUndo(directors, "Create Directors");
            }

            var rootGo = GameObject.Find("PresentationRoot");
            if (rootGo == null)
            {
                rootGo = new GameObject("PresentationRoot");
                Undo.RegisterCreatedObjectUndo(rootGo, "Create PresentationRoot");
                rootGo.transform.SetParent(directors.transform, false);
            }

            var root = rootGo.GetComponent<PresentationSceneRoot>();
            if (root == null)
            {
                root = Undo.AddComponent<PresentationSceneRoot>(rootGo);
            }

            var so = new SerializedObject(root);
            Bind(so, "inBattle", Object.FindFirstObjectByType<InBattleManagerSingleton>(FindObjectsInactive.Include));
            Bind(so, "mainGameLoop", Object.FindFirstObjectByType<MainGameLoopManagerSingleton>(FindObjectsInactive.Include));
            Bind(so, "relicManager", Object.FindFirstObjectByType<RelicManagerSingleton>(FindObjectsInactive.Include));
            Bind(so, "selectorManager", Object.FindFirstObjectByType<SelectorManagerSingleton>(FindObjectsInactive.Include));
            Bind(so, "descriptionManager", Object.FindFirstObjectByType<DescriptionManagerSingleton>(FindObjectsInactive.Include));
            Bind(so, "damageNumberManager", Object.FindFirstObjectByType<DamageNumberManagerSingleton>(FindObjectsInactive.Include));
            Bind(so, "goldGainFxManager", Object.FindFirstObjectByType<GoldGainFxManagerSingleton>(FindObjectsInactive.Include));
            Bind(so, "cardManager", Object.FindFirstObjectByType<CardManagerSingleton>(FindObjectsInactive.Include));
            Bind(so, "cardHand", Object.FindFirstObjectByType<CardHandManagerSingleton>(FindObjectsInactive.Include));
            Bind(so, "cardDeck", Object.FindFirstObjectByType<CardDeckManagerSingleton>(FindObjectsInactive.Include));
            Bind(so, "groundField", Object.FindFirstObjectByType<GroundFieldManagerSingleton>(FindObjectsInactive.Include));
            Bind(so, "fieldBattle", Object.FindFirstObjectByType<FieldBattleManagerSingleton>(FindObjectsInactive.Include));
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(root);

            // 强制重写 m_EditorClassIdentifier，清掉 C1 残留的 NineGrid.Cards/Flow 程序集名。
            var presentationBehaviours = Object
                .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var reserialized = 0;
            for (var i = 0; i < presentationBehaviours.Length; i++)
            {
                var mb = presentationBehaviours[i];
                if (mb == null)
                {
                    continue;
                }

                var asmName = mb.GetType().Assembly.GetName().Name;
                if (asmName != "NineGrid.Presentation")
                {
                    continue;
                }

                EditorUtility.SetDirty(mb);
                reserialized++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                $"[PresentationSceneRootSetup] Wired and saved: {scene.path}; reserialized={reserialized}");
        }

        private static void Bind(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[PresentationSceneRootSetup] missing field {field}");
                return;
            }

            prop.objectReferenceValue = value;
        }
    }
}
