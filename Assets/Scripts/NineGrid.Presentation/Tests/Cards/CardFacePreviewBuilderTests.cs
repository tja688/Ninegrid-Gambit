#if UNITY_EDITOR
using NUnit.Framework;
using NineGrid.Cards.Convergence;
using NineGrid.Presentation.Editor;
using NineGrid.Cards.Slots;
using NineGrid.Content;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 编辑器预览 Builder：假投影 → ApplyPresentation → 描述 [SlotCode] 上卡面。
    /// </summary>
    public sealed class CardFacePreviewBuilderTests
    {
        private static readonly System.Type TextMeshProType =
            System.Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");

        private Sprite _mainIcon;
        private Sprite _actionIcon;
        private GameObject _chassis;
        private GameObject _monsterFace;
        private GameObject _itemFace;

        [SetUp]
        public void SetUp()
        {
            Assert.IsNotNull(TextMeshProType, "需要 TMP");
            _mainIcon = CreateSprite("preview-main");
            _actionIcon = CreateSprite("preview-action");
            _chassis = CreateChassisPrefab("PreviewChassis");
            _monsterFace = CreateMonsterFacePrefab("PreviewMonsterFace");
            _itemFace = CreateItemFacePrefab("PreviewItemFace");

            if (CardFaceSlotNodeMap.TryFindRenderer(
                    _monsterFace.transform,
                    CardFaceSlotCodes.ActionIcon,
                    out var actionRenderer))
            {
                actionRenderer.sprite = _actionIcon;
            }
        }

        [TearDown]
        public void TearDown()
        {
            DestroySprite(ref _mainIcon);
            DestroySprite(ref _actionIcon);
            DestroyIfPresent(ref _chassis);
            DestroyIfPresent(ref _monsterFace);
            DestroyIfPresent(ref _itemFace);
        }

        [Test]
        public void TryBuild_ItemFace_AppliesBasicDescriptionWithMainIconSlot()
        {
            var request = new CardFacePreviewRequest
            {
                DefId = "help.preview_test",
                Kind = CardPresentationKind.HelpCard,
                DisplayName = "预览卡",
                BasicDescription = "造成[Main_Icon]点伤害",
                MainIcon = _mainIcon,
                FaceUp = true,
            };

            Assert.IsTrue(
                CardFacePreviewBuilder.TryBuild(
                    request,
                    out var build,
                    out var error,
                    _chassis,
                    _itemFace),
                error);

            try
            {
                Assert.IsNotNull(build.Binder);
                Assert.IsTrue(
                    CardFaceSlotNodeMap.TryReadText(
                        build.FaceRoot,
                        CardFaceSlotCodes.BasicDescription,
                        out var tmpText),
                    "道具卡应有基础描述槽");
                Assert.AreEqual(
                    "造成<sprite name=\"Main_Icon\">点伤害",
                    tmpText,
                    "Builder 路径应与运行时 Binder 一致解析 SlotCode");
            }
            finally
            {
                CardFacePreviewBuilder.DestroyBuild(build);
            }
        }

        [Test]
        public void TryBuild_Monster_AppliesActionIconInDescription()
        {
            var request = new CardFacePreviewRequest
            {
                DefId = "monster.preview_test",
                Kind = CardPresentationKind.Monster,
                DisplayName = "预览怪",
                BasicDescription = "在[Action_Icon]回合后攻击",
                MainIcon = _mainIcon,
                Attack = 2,
                Armor = 1,
                Hp = 8,
                FaceUp = true,
            };

            Assert.IsTrue(
                CardFacePreviewBuilder.TryBuild(
                    request,
                    out var build,
                    out var error,
                    _chassis,
                    _monsterFace),
                error);

            try
            {
                Assert.IsTrue(
                    CardFaceSlotNodeMap.TryReadText(
                        build.FaceRoot,
                        CardFaceSlotCodes.BasicDescription,
                        out var tmpText));
                Assert.AreEqual("在<sprite name=\"Action_Icon\">回合后攻击", tmpText);
            }
            finally
            {
                CardFacePreviewBuilder.DestroyBuild(build);
            }
        }

        [Test]
        public void ToPresentationKind_Skill_IsUnknown()
        {
            Assert.AreEqual(
                CardPresentationKind.Unknown,
                CardFacePreviewBuilder.ToPresentationKind(ContentVisualKind.Skill, "skill.x"));
        }

        [Test]
        public void ListInsertableSlots_ContainsMainIconAndActionIcon()
        {
            var slots = CardFacePreviewBuilder.ListInsertableSlots();
            Assert.IsTrue(slots.Exists(s => s.Code == CardFaceSlotCodes.MainIcon));
            Assert.IsTrue(slots.Exists(s => s.Code == CardFaceSlotCodes.ActionIcon));
        }

        [Test]
        public void CaptureTemplateDefaults_ResolvesActionNodeNamed行动()
        {
            var face = new GameObject("CaptureActionFace");
            try
            {
                var front = new GameObject("Front");
                front.transform.SetParent(face.transform, false);
                CreateSpriteChild(front.transform, "行动");
                if (CardFaceSlotNodeMap.TryFindRenderer(
                        face.transform,
                        CardFaceSlotCodes.ActionIcon,
                        out var renderer))
                {
                    renderer.sprite = _actionIcon;
                }

                var defaults = CardFaceSlotNodeMap.CaptureTemplateDefaults(face.transform);
                Assert.IsTrue(
                    defaults.TryGetValue(CardFaceSlotCodes.ActionIcon, out var captured),
                    "节点名「行动」应映射到 Action_Icon");
                Assert.AreSame(_actionIcon, captured);
            }
            finally
            {
                Object.DestroyImmediate(face);
            }
        }

        [Test]
        public void TryBuild_Monster_ActionNodeNamed行动_ResolvesInDescription()
        {
            var face = new GameObject("PreviewMonsterFace_行动");
            try
            {
                var front = new GameObject("Front");
                front.transform.SetParent(face.transform, false);
                new GameObject("back").transform.SetParent(face.transform, false);
                CreateSpriteChild(front.transform, "核心图标");
                CreateSpriteChild(front.transform, "行动");
                if (CardFaceSlotNodeMap.TryFindRenderer(
                        face.transform,
                        CardFaceSlotCodes.ActionIcon,
                        out var actionRenderer))
                {
                    actionRenderer.sprite = _actionIcon;
                }

                var nameRoot = new GameObject("名字");
                nameRoot.transform.SetParent(front.transform, false);
                CreateTmpChild(nameRoot.transform, "标准世界文字", "模板怪");
                CreateTmpChild(front.transform, "攻击数值", "0");
                CreateTmpChild(front.transform, "护甲数值", "0");
                CreateTmpChild(front.transform, "血量数值", "0");
                CreateTmpChild(front.transform, "行动计数", "0");
                CreateTmpChild(front.transform, "描述", string.Empty);

                var request = new CardFacePreviewRequest
                {
                    DefId = "monster.preview_action_node",
                    Kind = CardPresentationKind.Monster,
                    DisplayName = "行动节点怪",
                    BasicDescription = "在[Action_Icon]后攻击",
                    MainIcon = _mainIcon,
                    FaceUp = true,
                };

                Assert.IsTrue(
                    CardFacePreviewBuilder.TryBuild(
                        request,
                        out var build,
                        out var error,
                        _chassis,
                        face),
                    error);

                try
                {
                    Assert.IsTrue(
                        CardFaceSlotNodeMap.TryReadText(
                            build.FaceRoot,
                            CardFaceSlotCodes.BasicDescription,
                            out var tmpText));
                    Assert.AreEqual("在<sprite name=\"Action_Icon\">后攻击", tmpText);
                }
                finally
                {
                    CardFacePreviewBuilder.DestroyBuild(build);
                }
            }
            finally
            {
                Object.DestroyImmediate(face);
            }
        }

        private static GameObject CreateChassisPrefab(string name)
        {
            var go = new GameObject(name);
            go.AddComponent<SortingGroup>();
            go.AddComponent<StandardCardView>();
            go.AddComponent<CardTransformTower>().EnsureTower();
            return go;
        }

        private static GameObject CreateMonsterFacePrefab(string name)
        {
            var go = new GameObject(name);
            var front = new GameObject("Front");
            front.transform.SetParent(go.transform, false);
            new GameObject("back").transform.SetParent(go.transform, false);

            CreateSpriteChild(front.transform, "核心图标");
            CreateSpriteChild(front.transform, "行动图标");
            var nameRoot = new GameObject("名字");
            nameRoot.transform.SetParent(front.transform, false);
            CreateTmpChild(nameRoot.transform, "标准世界文字", "模板怪");
            CreateTmpChild(front.transform, "攻击数值", "0");
            CreateTmpChild(front.transform, "护甲数值", "0");
            CreateTmpChild(front.transform, "血量数值", "0");
            CreateTmpChild(front.transform, "行动计数", "9");
            CreateTmpChild(front.transform, "描述", string.Empty);
            return go;
        }

        private static GameObject CreateItemFacePrefab(string name)
        {
            var go = new GameObject(name);
            var front = new GameObject("Front");
            front.transform.SetParent(go.transform, false);
            new GameObject("back").transform.SetParent(go.transform, false);

            CreateSpriteChild(front.transform, "核心图标");
            var banner = new GameObject("名字横幅");
            banner.transform.SetParent(front.transform, false);
            CreateTmpChild(banner.transform, "标准世界文字", "模板道具");
            var intro = new GameObject("介绍区域");
            intro.transform.SetParent(front.transform, false);
            CreateTmpChild(intro.transform, "标准世界文字", string.Empty);
            return go;
        }

        private static void CreateSpriteChild(Transform parent, string childName)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            go.AddComponent<SpriteRenderer>();
        }

        private static void CreateTmpChild(Transform parent, string childName, string text)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent(TextMeshProType);
            TextMeshProType.GetProperty("text")?.SetValue(tmp, text ?? string.Empty);
        }

        private static Sprite CreateSprite(string name)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.name = name + "_tex";
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }

        private static void DestroySprite(ref Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            var texture = sprite.texture;
            Object.DestroyImmediate(sprite);
            sprite = null;
            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static void DestroyIfPresent(ref GameObject go)
        {
            if (go == null)
            {
                return;
            }

            Object.DestroyImmediate(go);
            go = null;
        }
    }
}
#endif
