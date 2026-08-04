using System.Text;
using MoreMountains.Tools;
using NineGrid.Flow.Transitions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NineGrid.Presentation.Editor
{
    /// <summary>一次性 / 可重入：在 MainScene 装配 Feel Round+Directional 过场 Canvas。</summary>
    public static class FeelTransitionSceneInstall
    {
        [MenuItem("NineGrid/Setup/Install Run Scene Transitions (MainScene)")]
        public static void InstallFromMenu()
        {
            Debug.Log("[FeelTransition] " + Install());
        }

        public static string Install()
        {
            var log = new StringBuilder();

            const string assetPath = "Assets/Resources/Transitions/RunSceneTransition.asset";
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources/Transitions"))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Transitions");
            }

            var so = AssetDatabase.LoadAssetAtPath<RunSceneTransitionSettingsSO>(assetPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<RunSceneTransitionSettingsSO>();
                AssetDatabase.CreateAsset(so, assetPath);
                log.Append("created-so;");
            }
            else
            {
                log.Append("so-exists;");
            }

            var maskSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Plugins/Feel/MMTools/Accessories/MMGUI/Sprites/MMFaderRoundMask.png");
            if (maskSprite == null)
            {
                maskSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Plugins/Feel/MMFeedbacks/Demos/MMFeedbacksDemo/Textures/FeedbacksDemoFaderMask.png");
            }

            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Plugins/Feel/MMFeedbacks/Demos/MMFeedbacksDemo/Textures/FeedbacksDemo1px.png");
            var matMask = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Plugins/Feel/MMTools/Accessories/MMGUI/Materials/MMFaderRoundMaterialMask.mat");
            var matMasked = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Plugins/Feel/MMTools/Accessories/MMGUI/Materials/MMFaderRoundMaterialMasked.mat");

            if (maskSprite != null)
            {
                so.SetRoundMaskSprite(maskSprite);
                EditorUtility.SetDirty(so);
            }

            var existing = GameObject.Find("TransitionsCanvas");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
                log.Append("removed-old;");
            }

            var canvasGo = new GameObject(
                "TransitionsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var service = canvasGo.AddComponent<RunSceneTransitionService>();

            var roundGo = new GameObject(
                "MMFaderRound", typeof(RectTransform), typeof(CanvasGroup), typeof(MMFaderRound));
            roundGo.transform.SetParent(canvasGo.transform, false);
            StretchFull(roundGo.GetComponent<RectTransform>());
            var roundCg = roundGo.GetComponent<CanvasGroup>();
            roundCg.alpha = 0f;
            roundCg.blocksRaycasts = false;

            var roundBgGo = new GameObject(
                "FaderRoundBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            roundBgGo.transform.SetParent(roundGo.transform, false);
            StretchFull(roundBgGo.GetComponent<RectTransform>());
            var roundBgImg = roundBgGo.GetComponent<Image>();
            roundBgImg.color = Color.black;
            if (bgSprite != null)
            {
                roundBgImg.sprite = bgSprite;
            }

            if (matMasked != null)
            {
                roundBgImg.material = matMasked;
            }

            roundBgImg.raycastTarget = true;

            var roundMaskGo = new GameObject(
                "MMFaderRoundMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            roundMaskGo.transform.SetParent(roundGo.transform, false);
            var maskRt = roundMaskGo.GetComponent<RectTransform>();
            maskRt.anchorMin = new Vector2(0.5f, 0.5f);
            maskRt.anchorMax = new Vector2(0.5f, 0.5f);
            maskRt.pivot = new Vector2(0.5f, 0.5f);
            maskRt.sizeDelta = new Vector2(100f, 100f);
            maskRt.anchoredPosition = Vector2.zero;
            var roundMaskImg = roundMaskGo.GetComponent<Image>();
            if (maskSprite != null)
            {
                roundMaskImg.sprite = maskSprite;
            }

            if (matMask != null)
            {
                roundMaskImg.material = matMask;
            }

            roundMaskImg.color = Color.white;
            roundMaskImg.raycastTarget = false;

            var round = roundGo.GetComponent<MMFaderRound>();
            round.ID = RunSceneTransitionSettingsSO.RoundFaderId;
            round.FaderBackground = roundBgGo.GetComponent<RectTransform>();
            round.FaderMask = maskRt;
            round.MaskScale = so.RoundMaskScale;
            round.DefaultDuration = so.RoundFadeInDuration;
            round.IgnoreTimescale = so.IgnoreTimeScale;
            round.ShouldBlockRaycasts = so.BlockRaycastsDuringFade;
            round.CameraMode = MMFaderRound.CameraModes.Main;

            var dirGo = new GameObject(
                "MMFaderDirectional",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(MMFaderDirectional));
            dirGo.transform.SetParent(canvasGo.transform, false);
            StretchFull(dirGo.GetComponent<RectTransform>());
            var dirCg = dirGo.GetComponent<CanvasGroup>();
            dirCg.alpha = 0f;
            dirCg.blocksRaycasts = false;
            var dirImg = dirGo.GetComponent<Image>();
            dirImg.color = Color.black;
            if (bgSprite != null)
            {
                dirImg.sprite = bgSprite;
            }

            dirImg.raycastTarget = true;

            var dir = dirGo.GetComponent<MMFaderDirectional>();
            dir.ID = RunSceneTransitionSettingsSO.DirectionalFaderId;
            dir.DefaultDuration = so.DirectionalFadeInDuration;
            dir.IgnoreTimescale = so.IgnoreTimeScale;
            dir.ShouldBlockRaycasts = so.BlockRaycastsDuringFade;
            dir.DisableOnInit = true;

            var ser = new SerializedObject(service);
            ser.FindProperty("settings").objectReferenceValue = so;
            ser.FindProperty("roundFader").objectReferenceValue = round;
            ser.FindProperty("directionalFader").objectReferenceValue = dir;
            ser.FindProperty("roundMaskImage").objectReferenceValue = roundMaskImg;
            ser.FindProperty("roundBackgroundImage").objectReferenceValue = roundBgImg;
            ser.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            log.Append("ok mask=").Append(maskSprite != null)
                .Append(" bg=").Append(bgSprite != null)
                .Append(" matMask=").Append(matMask != null)
                .Append(" matMasked=").Append(matMasked != null);
            return log.ToString();
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
