using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 运行时鼠标悬停场景元素，驱动 <see cref="SelectableSceneElement"/> 高亮。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneElementPointerSelector : MonoBehaviour
    {
        static readonly List<SelectableSceneElement> Registered = new List<SelectableSceneElement>(8);
        static readonly List<SelectableSceneElement> HitBuffer = new List<SelectableSceneElement>(8);

        [SerializeField] Camera targetCamera;
        [SerializeField] bool ignoreWhenPointerOverUi = true;

        SelectableSceneElement hovered;
        Object interactionScopeOwner;
        readonly HashSet<SelectableSceneElement> interactionWhitelist = new HashSet<SelectableSceneElement>();

        public SelectableSceneElement Hovered => hovered;

        /// <summary>
        /// 为当前世界点击选择器设置一层“只允许这些场景元素被命中”的作用域。
        /// 传入空白名单即可在作用域期间阻断所有世界物体命中。
        /// </summary>
        public void ApplyInteractionScope(Object owner, params SelectableSceneElement[] allowedElements)
        {
            if (owner == null)
            {
                Debug.LogWarning("[SceneElementPointerSelector] ApplyInteractionScope 需要非空 owner。");
                return;
            }

            interactionScopeOwner = owner;
            interactionWhitelist.Clear();

            if (allowedElements != null)
            {
                for (var i = 0; i < allowedElements.Length; i++)
                {
                    var element = allowedElements[i];
                    if (element != null)
                    {
                        interactionWhitelist.Add(element);
                    }
                }
            }

            if (hovered != null && !IsAllowedByScope(hovered))
            {
                SetHovered(null);
            }
        }

        public void ClearInteractionScope(Object owner)
        {
            if (owner == null || interactionScopeOwner != owner)
            {
                return;
            }

            interactionScopeOwner = null;
            interactionWhitelist.Clear();
            SetHovered(null);
        }

        public static void Register(SelectableSceneElement element)
        {
            if (element == null || Registered.Contains(element))
            {
                return;
            }

            Registered.Add(element);
        }

        public static void Unregister(SelectableSceneElement element)
        {
            if (element == null)
            {
                return;
            }

            Registered.Remove(element);
        }

        void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        void Update()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    return;
                }
            }

            if (ignoreWhenPointerOverUi && IsPointerOverUi())
            {
                SetHovered(null);
                return;
            }

            if (!TryGetPointerWorldPosition(out Vector2 worldPoint))
            {
                SetHovered(null);
                return;
            }

            SetHovered(FindTopmostAt(worldPoint));
        }

        void OnDisable()
        {
            SetHovered(null);
        }

        void SetHovered(SelectableSceneElement element)
        {
            if (hovered == element)
            {
                return;
            }

            if (hovered != null)
            {
                hovered.SetHovered(false);
            }

            hovered = element;

            if (hovered != null)
            {
                hovered.SetHovered(true);
            }
        }

        SelectableSceneElement FindTopmostAt(Vector2 worldPoint)
        {
            HitBuffer.Clear();
            for (int i = 0; i < Registered.Count; i++)
            {
                SelectableSceneElement element = Registered[i];
                if (element == null || !element.isActiveAndEnabled)
                {
                    continue;
                }

                if (!IsAllowedByScope(element))
                {
                    continue;
                }

                if (element.ContainsWorldPoint(worldPoint))
                {
                    HitBuffer.Add(element);
                }
            }

            if (HitBuffer.Count == 0)
            {
                return null;
            }

            SelectableSceneElement top = HitBuffer[0];
            for (int i = 1; i < HitBuffer.Count; i++)
            {
                SelectableSceneElement candidate = HitBuffer[i];
                if (candidate.SortingOrder > top.SortingOrder)
                {
                    top = candidate;
                    continue;
                }

                if (candidate.SortingOrder == top.SortingOrder
                    && candidate.transform.GetSiblingIndex() > top.transform.GetSiblingIndex())
                {
                    top = candidate;
                }
            }

            return top;
        }

        bool IsAllowedByScope(SelectableSceneElement element)
        {
            return interactionScopeOwner == null || interactionWhitelist.Contains(element);
        }

        bool TryGetPointerWorldPosition(out Vector2 worldPoint)
        {
            Vector3 screenPoint = Input.mousePosition;
            if (float.IsNaN(screenPoint.x) || float.IsNaN(screenPoint.y))
            {
                worldPoint = default;
                return false;
            }

            float depth = Mathf.Abs(targetCamera.transform.position.z);
            Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, depth));
            worldPoint = world;
            return true;
        }

        static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
