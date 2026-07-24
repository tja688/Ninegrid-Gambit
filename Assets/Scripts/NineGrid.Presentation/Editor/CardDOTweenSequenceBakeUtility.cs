#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using NineGrid.Cards;

namespace NineGrid.Presentation.Editor
{
    public static class CardDOTweenSequenceBakeUtility
    {
        private const string DotweenAnimationTypeName = "DG.Tweening.DOTweenAnimation, DOTweenPro";

        public static bool BakeFromGameObject(GameObject source, CardDOTweenSequenceEffectSO asset)
        {
            if (source == null || asset == null)
            {
                return false;
            }

            var animationType = ResolveDotweenAnimationType();
            if (animationType == null)
            {
                Debug.LogError("[CardDOTweenSequenceBake] 未找到 DOTweenAnimation 类型。");
                return false;
            }

            var components = source.GetComponents(animationType);
            var clips = new List<CardTweenClip>(components.Length);
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                if (!ReadBool(component, "isActive") || !ReadBool(component, "isValid"))
                {
                    continue;
                }

                if (!TryMapClip(component, out var clip))
                {
                    Debug.LogWarning(
                        $"[CardDOTweenSequenceBake] 跳过不支持的 DOTweenAnimation（{source.name} 上第 {i} 个）。",
                        source);
                    continue;
                }

                clips.Add(clip);
            }

            if (clips.Count == 0)
            {
                Debug.LogWarning($"[CardDOTweenSequenceBake] {source.name} 上未找到可烘焙的 DOTweenAnimation。", source);
                return false;
            }

            asset.EditorSetClips(clips);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardDOTweenSequenceBake] 已烘焙 {clips.Count} 个片段到 {asset.name}。", asset);
            return true;
        }

        public static bool TryMapClip(Component animation, out CardTweenClip clip)
        {
            clip = default;
            if (animation == null)
            {
                return false;
            }

            var animationTypeValue = ReadEnum(animation, "animationType");
            if (!TryMapClipType(animationTypeValue, out var clipType))
            {
                return false;
            }

            clip = new CardTweenClip
            {
                delay = ReadFloat(animation, "delay"),
                duration = ReadFloat(animation, "duration"),
                ease = ReadEase(animation, "easeType"),
                type = clipType,
                endValue = ReadVector3(animation, "endValueV3"),
                isRelative = ReadBool(animation, "isRelative"),
            };
            return true;
        }

        private static System.Type ResolveDotweenAnimationType()
        {
            return System.Type.GetType(DotweenAnimationTypeName)
                ?? System.Type.GetType("DG.Tweening.DOTweenAnimation, Assembly-CSharp-firstpass")
                ?? System.Type.GetType("DG.Tweening.DOTweenAnimation");
        }

        private static bool TryMapClipType(object animationTypeValue, out CardTweenClipType clipType)
        {
            clipType = default;
            if (animationTypeValue == null)
            {
                return false;
            }

            var name = animationTypeValue.ToString();
            if (name != "LocalMove")
            {
                return false;
            }

            clipType = CardTweenClipType.LocalMove;
            return true;
        }

        private static float ReadFloat(Component component, string memberName)
        {
            var field = component.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null && field.GetValue(component) is float fieldValue)
            {
                return fieldValue;
            }

            var property = component.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            return property != null && property.GetValue(component) is float propertyValue ? propertyValue : 0f;
        }

        private static bool ReadBool(Component component, string memberName)
        {
            var field = component.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null && field.GetValue(component) is bool fieldValue)
            {
                return fieldValue;
            }

            var property = component.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            return property != null && property.GetValue(component) is bool propertyValue && propertyValue;
        }

        private static Vector3 ReadVector3(Component component, string memberName)
        {
            var field = component.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null && field.GetValue(component) is Vector3 fieldValue)
            {
                return fieldValue;
            }

            var property = component.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            return property != null && property.GetValue(component) is Vector3 propertyValue ? propertyValue : Vector3.zero;
        }

        private static object ReadEnum(Component component, string memberName)
        {
            var field = component.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                return field.GetValue(component);
            }

            var property = component.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            return property?.GetValue(component);
        }

        private static Ease ReadEase(Component component, string memberName)
        {
            var field = component.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null && field.GetValue(component) is Ease fieldEase)
            {
                return fieldEase;
            }

            var property = component.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            return property != null && property.GetValue(component) is Ease propertyEase ? propertyEase : Ease.Linear;
        }
    }
}
#endif
