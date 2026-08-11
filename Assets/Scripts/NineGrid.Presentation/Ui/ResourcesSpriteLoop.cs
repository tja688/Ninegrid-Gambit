using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// Resources 序列帧循环装饰：按键 <c>LoadAll&lt;Sprite&gt;</c>（图集或帧文件夹）并按帧名尾号排序循环。
    /// 仅服务面板装饰（主菜单 / 人物选择 / 结算的角色 idle），不参与卡面表现管线。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ResourcesSpriteLoop : MonoBehaviour
    {
        [Tooltip("Resources 键：指向精灵图集或帧文件夹（如 ContentArt/...，不带扩展名）。")]
        [SerializeField] private string resourcesKey = string.Empty;

        [Tooltip("播放帧率。")]
        [SerializeField] private float fps = 8f;

        [Tooltip("水平镜像。")]
        [SerializeField] private bool mirrorX;

        private SpriteRenderer mRenderer;
        private Sprite[] mFrames = Array.Empty<Sprite>();
        private int mFrameIndex;
        private float mElapsed;
        private string mLoadedKey;

        public string ResourcesKey => resourcesKey;

        public void SetResourcesKey(string key)
        {
            resourcesKey = key ?? string.Empty;
            if (isActiveAndEnabled)
            {
                LoadFrames();
            }
        }

        private void OnEnable()
        {
            mRenderer = GetComponent<SpriteRenderer>();
            LoadFrames();
        }

        private void Update()
        {
            if (mFrames.Length <= 1 || mRenderer == null)
            {
                return;
            }

            var rate = Mathf.Max(0.01f, fps);
            mElapsed += Time.unscaledDeltaTime;
            var frameDuration = 1f / rate;
            while (mElapsed >= frameDuration)
            {
                mElapsed -= frameDuration;
                mFrameIndex = (mFrameIndex + 1) % mFrames.Length;
                mRenderer.sprite = mFrames[mFrameIndex];
            }
        }

        private void LoadFrames()
        {
            if (string.Equals(mLoadedKey, resourcesKey, StringComparison.Ordinal)
                && mFrames.Length > 0)
            {
                ApplyFirstFrame();
                return;
            }

            mLoadedKey = resourcesKey;
            mFrameIndex = 0;
            mElapsed = 0f;
            mFrames = LoadSortedFrames(resourcesKey);
            ApplyFirstFrame();
        }

        private void ApplyFirstFrame()
        {
            if (mRenderer == null)
            {
                return;
            }

            mRenderer.flipX = mirrorX;
            if (mFrames.Length > 0)
            {
                mFrameIndex = Mathf.Clamp(mFrameIndex, 0, mFrames.Length - 1);
                mRenderer.sprite = mFrames[mFrameIndex];
            }
        }

        private static Sprite[] LoadSortedFrames(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return Array.Empty<Sprite>();
            }

            var loaded = Resources.LoadAll<Sprite>(key.Trim());
            if (loaded == null || loaded.Length == 0)
            {
                Debug.LogWarning("[ResourcesSpriteLoop] 序列帧加载失败 key=" + key);
                return Array.Empty<Sprite>();
            }

            var frames = new List<Sprite>(loaded);
            frames.Sort(CompareByTrailingNumber);
            return frames.ToArray();
        }

        private static int CompareByTrailingNumber(Sprite a, Sprite b)
        {
            var na = ExtractTrailingNumber(a != null ? a.name : null);
            var nb = ExtractTrailingNumber(b != null ? b.name : null);
            if (na != nb)
            {
                return na.CompareTo(nb);
            }

            return string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty);
        }

        private static int ExtractTrailingNumber(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return int.MaxValue;
            }

            var end = name.Length;
            var start = end;
            while (start > 0 && char.IsDigit(name[start - 1]))
            {
                start--;
            }

            if (start >= end)
            {
                return int.MaxValue;
            }

            return int.TryParse(name.Substring(start, end - start), out var value) ? value : int.MaxValue;
        }
    }
}
