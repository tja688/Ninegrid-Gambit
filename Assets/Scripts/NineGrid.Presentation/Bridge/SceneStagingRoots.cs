using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Presentation.Bridge
{
    /// <summary>
    /// MainScene 舞台根节点引用（棋盘锚点 / 演员层 / HUD 等）。
    /// </summary>
    public sealed class SceneStagingRoots
    {
        public Transform AnchorsRoot { get; private set; }
        public Transform PanelsRoot { get; private set; }
        public Transform NineGridAnchors { get; private set; }
        public Transform CardDeckAnchors { get; private set; }
        public Transform HandCardAnchors { get; private set; }
        public Transform PanelsAnchor { get; private set; }
        public Transform ActorsRoot { get; private set; }
        public Transform HandActorsRoot { get; private set; }

        public bool IsValid => AnchorsRoot != null && ActorsRoot != null;

        public static bool TryResolve(out SceneStagingRoots roots)
        {
            roots = new SceneStagingRoots();
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return false;
            }

            GameObject[] sceneRoots = scene.GetRootGameObjects();
            for (var i = 0; i < sceneRoots.Length; i++)
            {
                Transform root = sceneRoots[i].transform;
                if (roots.AnchorsRoot == null)
                {
                    roots.AnchorsRoot = FindChildRecursive(root, "Anchors");
                }

                if (roots.PanelsRoot == null)
                {
                    roots.PanelsRoot = FindChildRecursive(root, "Panels");
                }

                if (roots.ActorsRoot == null)
                {
                    roots.ActorsRoot = FindChildRecursive(root, "Actors");
                }

                if (roots.HandActorsRoot == null)
                {
                    roots.HandActorsRoot = FindChildRecursive(root, "HandCardActors");
                }
            }

            if (roots.AnchorsRoot != null)
            {
                roots.NineGridAnchors = roots.AnchorsRoot.Find("NineGridAnchors");
                roots.CardDeckAnchors = roots.AnchorsRoot.Find("CardDeckAnchors");
                roots.HandCardAnchors = ResolveHandAnchors(roots.AnchorsRoot);
                roots.PanelsAnchor = roots.AnchorsRoot.Find("PanelsAnchor");
            }

            return roots.IsValid;
        }

        private static Transform ResolveHandAnchors(Transform anchorsRoot)
        {
            Transform hand = anchorsRoot.Find("HandCardAnchors");
            if (hand != null)
            {
                return hand;
            }

            return anchorsRoot.Find("IteamCardHandAnchors");
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
