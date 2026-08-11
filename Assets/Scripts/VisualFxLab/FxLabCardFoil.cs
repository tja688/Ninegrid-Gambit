using System.Collections.Generic;
using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.VisualFxLab
{
    /// <summary>
    /// 画面实验室模块：卡面流光（F8）。
    /// 为场上 / 手牌卡面的「卡框」「主图标」精灵各挂一层同精灵的加色覆盖层，
    /// 用 NineGrid/FxLab/CardFoil 着色器做斜向流光扫过 + 稀疏像素闪点；
    /// 覆盖层与底精灵走同一套几何 Pixel Snap，随卡运动不错位。
    /// 每张卡相位随机错开，扫光不整齐划一。关闭即拆除全部覆盖层。
    /// </summary>
    public sealed class FxLabCardFoil : MonoBehaviour
    {
        const float ScanInterval = 0.5f;

        static readonly string[] TargetNames = { "卡框", "主图标" };

        sealed class FoilEntry
        {
            public SpriteRenderer Source;
            public SpriteRenderer Overlay;
        }

        readonly Dictionary<int, List<FoilEntry>> _entriesByUid = new();
        readonly List<int> _removeBuffer = new();
        readonly List<SpriteRenderer> _rendererBuffer = new();

        CardManagerSingleton _cardManager;
        MaterialPropertyBlock _mpb;
        float _nextScanAt;

        static readonly int FoilPhaseId = Shader.PropertyToID("_FoilPhase");
        static readonly int FoilIntensityId = Shader.PropertyToID("_FoilIntensity");

        void OnEnable()
        {
            _mpb = new MaterialPropertyBlock();
            _nextScanAt = 0f;
        }

        void OnDisable()
        {
            foreach (var entries in _entriesByUid.Values)
            {
                foreach (var entry in entries)
                {
                    if (entry.Overlay != null)
                    {
                        Destroy(entry.Overlay.gameObject);
                    }
                }
            }

            _entriesByUid.Clear();
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextScanAt)
            {
                _nextScanAt = Time.unscaledTime + ScanInterval;
                Scan();
            }

            SyncOverlays();
        }

        void Scan()
        {
            if (_cardManager == null)
            {
                _cardManager = FindFirstObjectByType<CardManagerSingleton>(FindObjectsInactive.Include);
                if (_cardManager == null)
                {
                    return;
                }
            }

            var foilMaterial = FxLabRuntimeAssets.FoilMaterial;
            if (foilMaterial == null)
            {
                return;
            }

            foreach (var pair in _cardManager.CardsByUid)
            {
                var card = pair.Value;
                if (card == null || card.View == null || card.MountedFaceRoot == null)
                {
                    continue;
                }

                bool wantFoil = card.DisplayMode == CardDisplayMode.HandCardMode
                    || card.DisplayMode == CardDisplayMode.GroundCardMode
                    || card.DisplayMode == CardDisplayMode.DragCardMode;

                if (!wantFoil || _entriesByUid.ContainsKey(pair.Key))
                {
                    continue;
                }

                var entries = CreateEntries(card, foilMaterial);
                if (entries.Count > 0)
                {
                    _entriesByUid.Add(pair.Key, entries);
                }
            }

            _removeBuffer.Clear();
            foreach (var pair in _entriesByUid)
            {
                if (!_cardManager.CardsByUid.TryGetValue(pair.Key, out var card)
                    || card.View == null
                    || card.DisplayMode == CardDisplayMode.RemovedMode
                    || card.DisplayMode == CardDisplayMode.CardDeckMode)
                {
                    _removeBuffer.Add(pair.Key);
                }
            }

            foreach (int uid in _removeBuffer)
            {
                foreach (var entry in _entriesByUid[uid])
                {
                    if (entry.Overlay != null)
                    {
                        Destroy(entry.Overlay.gameObject);
                    }
                }

                _entriesByUid.Remove(uid);
            }
        }

        List<FoilEntry> CreateEntries(ManagedCard card, Material foilMaterial)
        {
            var entries = new List<FoilEntry>(2);
            _rendererBuffer.Clear();
            card.MountedFaceRoot.GetComponentsInChildren(true, _rendererBuffer);

            float phase = (card.Uid * 0.37731f) % 10f;

            foreach (var source in _rendererBuffer)
            {
                if (source == null)
                {
                    continue;
                }

                bool isTarget = false;
                for (int i = 0; i < TargetNames.Length; i++)
                {
                    if (source.gameObject.name == TargetNames[i])
                    {
                        isTarget = true;
                        break;
                    }
                }

                if (!isTarget || source.transform.Find("FxLab_Foil") != null)
                {
                    continue;
                }

                var overlayGo = new GameObject("FxLab_Foil");
                overlayGo.transform.SetParent(source.transform, false);
                var overlay = overlayGo.AddComponent<SpriteRenderer>();
                overlay.sharedMaterial = foilMaterial;
                overlay.sprite = source.sprite;
                overlay.sortingLayerID = source.sortingLayerID;
                overlay.sortingOrder = source.sortingOrder + 1;

                bool isFrame = source.gameObject.name == "卡框";
                _mpb.Clear();
                _mpb.SetFloat(FoilPhaseId, phase);
                _mpb.SetFloat(FoilIntensityId, isFrame ? 0.55f : 0.32f);
                overlay.SetPropertyBlock(_mpb);

                entries.Add(new FoilEntry { Source = source, Overlay = overlay });
            }

            return entries;
        }

        void SyncOverlays()
        {
            foreach (var entries in _entriesByUid.Values)
            {
                foreach (var entry in entries)
                {
                    var source = entry.Source;
                    var overlay = entry.Overlay;
                    if (source == null || overlay == null)
                    {
                        continue;
                    }

                    overlay.enabled = source.enabled;
                    if (!source.enabled)
                    {
                        continue;
                    }

                    if (overlay.sprite != source.sprite)
                    {
                        overlay.sprite = source.sprite;
                    }

                    overlay.flipX = source.flipX;
                    overlay.flipY = source.flipY;
                    if (overlay.sortingOrder != source.sortingOrder + 1)
                    {
                        overlay.sortingLayerID = source.sortingLayerID;
                        overlay.sortingOrder = source.sortingOrder + 1;
                    }
                }
            }
        }
    }
}
