using NUnit.Framework;
using NineGrid.Cards.Convergence;
using NineGrid.Cards.Presentation;
using NineGrid.Cards.Slots;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NineGrid.Cards.Tests
{
    /// <summary>
    /// 主 Seam：构建胖投影 → Commit 到底盘宿主 → Binder 呈现图标/名字/数值。
    /// 对齐 #15 / ADR-0002：正式路径走 ApplyPresentation，不依赖旁路 Set*；FaceUp 预留不抛错。
    /// </summary>
    public sealed class CardPresentationCommitTests
    {
        private static readonly System.Type TextMeshProType =
            System.Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");

        private GameObject _managerGo;
        private CardManagerSingleton _cardManager;
        private GameObject _chassis;
        private GameObject _avatarFace;
        private GameObject _monsterFace;
        private GameObject _itemFace;
        private GameObject _relicFace;
        private Sprite _mainIcon;
        private Sprite _templateIcon;
        private Sprite _actionIcon;
        private int _nextUid = 15001;

        [SetUp]
        public void SetUp()
        {
            Assert.IsNotNull(TextMeshProType, "EditMode 支架需要 TMP（Unity.TextMeshPro）");

            DestroyAllCardManagers();
            ResetCardManagerSingleton();

            _managerGo = new GameObject("CardManager_PresentationCommitTest");
            _cardManager = _managerGo.AddComponent<CardManagerSingleton>();

            _chassis = CreateChassisPrefab("ChassisPrefab");
            _avatarFace = CreateAvatarFacePrefab("AvatarFacePrefab");
            _monsterFace = CreateMonsterFacePrefab("MonsterFacePrefab");
            _itemFace = CreateItemFacePrefab("ItemFacePrefab");
            _relicFace = CreateItemFacePrefab("RelicFacePrefab");
            _mainIcon = CreateSprite("commit-main-icon");
            _templateIcon = CreateSprite("template-main-icon");
            _actionIcon = CreateSprite("template-action-icon");

            // 模板默认主图标：Commit 未覆盖时回退此图。
            if (CardFaceSlotNodeMap.TryFindRenderer(
                    _avatarFace.transform,
                    CardFaceSlotCodes.MainIcon,
                    out var templateRenderer))
            {
                templateRenderer.sprite = _templateIcon;
            }

            if (CardFaceSlotNodeMap.TryFindRenderer(
                    _monsterFace.transform,
                    CardFaceSlotCodes.ActionIcon,
                    out var actionRenderer))
            {
                actionRenderer.sprite = _actionIcon;
            }

            _cardManager.ConfigureChassisAndFaces(
                _chassis,
                _avatarFace,
                _monsterFace,
                _itemFace,
                _relicFace);
        }

        [TearDown]
        public void TearDown()
        {
            if (_managerGo != null)
            {
                Object.DestroyImmediate(_managerGo);
                _managerGo = null;
            }

            DestroyIfPresent(ref _chassis);
            DestroyIfPresent(ref _avatarFace);
            DestroyIfPresent(ref _monsterFace);
            DestroyIfPresent(ref _itemFace);
            DestroyIfPresent(ref _relicFace);
            DestroySprite(ref _mainIcon);
            DestroySprite(ref _templateIcon);
            DestroySprite(ref _actionIcon);

            DestroyAllCardManagers();
            ResetCardManagerSingleton();
        }

        [Test]
        public void Commit_AvatarSnapshot_ShowsIconNameAttackArmor_WithoutSetBypass()
        {
            var card = _cardManager.SpawnView(
                _nextUid++,
                defId: "avatar.test_hero",
                kind: CardPresentationKind.Avatar);

            var snapshot = new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Avatar,
                DefId = "avatar.test_hero",
                DisplayName = "测试英雄",
                MainIcon = _mainIcon,
                Attack = 7,
                Armor = 3,
                Hp = 99,
                ActionCount = 0,
                FaceUp = true,
            };

            card.CommitPresentation(snapshot);

            Assert.AreSame(snapshot, card.CommittedPresentation);
            Assert.AreSame(
                _mainIcon,
                ReadMainIcon(card),
                "主图标应经 Commit 落到卡面，而非底盘 SetMainIcon 旁路");
            Assert.AreEqual("测试英雄", ReadName(card));
            Assert.AreEqual("7", ReadNumericText(card, CardFaceSlotCodes.Attack));
            Assert.AreEqual("3", ReadNumericText(card, CardFaceSlotCodes.Armor));
            Assert.IsNull(
                FindDeep(card.MountedFaceRoot, "血量数值"),
                "玩家卡面不绑血量节点");
        }

        [Test]
        public void Commit_MonsterSnapshot_FillsAttackArmorHp_UnwiredActionCountDefaultsZero()
        {
            var card = _cardManager.SpawnView(
                _nextUid++,
                defId: "monster.test_slime",
                kind: CardPresentationKind.Monster);

            var snapshot = new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Monster,
                DefId = "monster.test_slime",
                DisplayName = "史莱姆",
                MainIcon = _mainIcon,
                Attack = 4,
                Armor = 1,
                Hp = 12,
                FaceUp = true,
            };

            card.CommitPresentation(snapshot);

            Assert.AreEqual("史莱姆", ReadName(card));
            Assert.AreEqual("4", ReadNumericText(card, CardFaceSlotCodes.Attack));
            Assert.AreEqual("1", ReadNumericText(card, CardFaceSlotCodes.Armor));
            Assert.AreEqual("12", ReadNumericText(card, CardFaceSlotCodes.Hp));
            Assert.AreEqual(
                "0",
                ReadNumericText(card, CardFaceSlotCodes.ActionCount),
                "未接线行动计数走数值缺省 0");
        }

        [Test]
        public void Commit_NullMainIcon_FallsBackToTemplateIcon()
        {
            var card = _cardManager.SpawnView(
                _nextUid++,
                defId: "avatar.fallback",
                kind: CardPresentationKind.Avatar);

            card.CommitPresentation(new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Avatar,
                DefId = "avatar.fallback",
                DisplayName = "回退",
                MainIcon = null,
                Attack = 1,
                Armor = 0,
                FaceUp = true,
            });

            Assert.AreSame(_templateIcon, ReadMainIcon(card));
        }

        [Test]
        public void Commit_FaceUpReserved_DoesNotThrow_AndKeepsFrontVisible()
        {
            var card = _cardManager.SpawnView(
                _nextUid++,
                defId: "avatar.faceup",
                kind: CardPresentationKind.Avatar);

            Assert.DoesNotThrow(() =>
            {
                card.CommitPresentation(new CardPresentationSnapshot
                {
                    Kind = CardPresentationKind.Avatar,
                    DefId = "avatar.faceup",
                    DisplayName = "朝向预留",
                    MainIcon = _mainIcon,
                    Attack = 2,
                    Armor = 2,
                    FaceUp = false,
                });
            });

            var front = FindDeep(card.MountedFaceRoot, "front");
            Assert.IsNotNull(front);
            Assert.IsTrue(front.gameObject.activeSelf, "本波恒正面；FaceUp=false 亦不抛错、不隐式走 DisplayMode");
        }

        [TestCase(CardPresentationKind.HelpCard)]
        [TestCase(CardPresentationKind.Relic)]
        public void Commit_ItemOrRelic_ShowsNameAndIcon(CardPresentationKind kind)
        {
            var card = _cardManager.SpawnView(
                _nextUid++,
                defId: $"test.{kind}",
                kind: kind);

            card.CommitPresentation(new CardPresentationSnapshot
            {
                Kind = kind,
                DefId = $"test.{kind}",
                DisplayName = "辨识名",
                MainIcon = _mainIcon,
                Attack = 9,
                Armor = 9,
                Hp = 9,
                FaceUp = true,
            });

            Assert.AreEqual("辨识名", ReadName(card));
            Assert.AreSame(_mainIcon, ReadMainIcon(card));
            Assert.IsFalse(
                CardFaceSlotNodeMap.TryReadText(card.MountedFaceRoot, CardFaceSlotCodes.Attack, out _),
                "道具/遗物以名字为主，不要求绑攻击数值");
        }

        [Test]
        public void ReapplyCommitted_DoesNotRequireLatestCoreValues()
        {
            var card = _cardManager.SpawnView(
                _nextUid++,
                defId: "avatar.reapply",
                kind: CardPresentationKind.Avatar);

            card.CommitPresentation(new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Avatar,
                DefId = "avatar.reapply",
                DisplayName = "已提交",
                MainIcon = _mainIcon,
                Attack = 5,
                Armor = 2,
                FaceUp = true,
            });

            card.ReapplyCommittedPresentation();
            Assert.AreEqual("5", ReadNumericText(card, CardFaceSlotCodes.Attack));
            Assert.AreEqual("已提交", ReadName(card));
        }

        [Test]
        public void Commit_MonsterBasicDescription_ShowsOnFace_WithSlotCodeIconTag()
        {
            var card = _cardManager.SpawnView(
                _nextUid++,
                defId: "monster.desc",
                kind: CardPresentationKind.Monster);

            card.CommitPresentation(new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Monster,
                DefId = "monster.desc",
                DisplayName = "史莱姆",
                MainIcon = _mainIcon,
                Attack = 2,
                Armor = 0,
                Hp = 5,
                BasicDescription = "在[Action_Icon]回合后攻击玩家",
                FaceUp = true,
            });

            Assert.AreEqual(
                "在<sprite name=\"Action_Icon\">回合后攻击玩家",
                ReadBasicDescription(card),
                "主 seam：基础描述应上卡面，且 SlotCode 解析为真实图标标签");
            Assert.GreaterOrEqual(
                ReadDescriptionSpriteIndex(card, CardFaceSlotCodes.ActionIcon),
                0,
                "装配槽 Action_Icon 应进入描述 SpriteAsset（真实游戏图标，非表情）");
        }

        [Test]
        public void Commit_BasicDescription_DoesNotChangeWhenOnlyStatsChange()
        {
            var card = _cardManager.SpawnView(
                _nextUid++,
                defId: "monster.static_desc",
                kind: CardPresentationKind.Monster);

            card.CommitPresentation(new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Monster,
                DefId = "monster.static_desc",
                DisplayName = "静态怪",
                MainIcon = _mainIcon,
                Attack = 1,
                Armor = 0,
                Hp = 3,
                BasicDescription = "在[Action_Icon]后攻击",
                FaceUp = true,
            });

            var before = ReadBasicDescription(card);

            card.CommitPresentation(new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Monster,
                DefId = "monster.static_desc",
                DisplayName = "静态怪",
                MainIcon = _mainIcon,
                Attack = 9,
                Armor = 4,
                Hp = 20,
                BasicDescription = "在[Action_Icon]后攻击",
                FaceUp = true,
            });

            Assert.AreEqual("9", ReadNumericText(card, CardFaceSlotCodes.Attack));
            Assert.AreEqual(
                before,
                ReadBasicDescription(card),
                "基础描述不随战斗数值 Commit 跳动");
        }

        [Test]
        public void Commit_ItemBasicDescription_ShowsOnFace()
        {
            var card = _cardManager.SpawnView(
                _nextUid++,
                defId: "item.potion",
                kind: CardPresentationKind.HelpCard);

            card.CommitPresentation(new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.HelpCard,
                DefId = "item.potion",
                DisplayName = "药水",
                MainIcon = _mainIcon,
                BasicDescription = "[使用时]恢复生命",
                FaceUp = true,
            });

            Assert.AreEqual("[使用时]恢复生命", ReadBasicDescription(card));
        }

        [Test]
        public void Commit_RelicBasicDescription_ShowsOnFace()
        {
            // 遗物模板与道具共用介绍/描述槽候选（描述面板）。
            var relicFace = CreateRelicFacePrefab("RelicFaceWithDesc");
            _cardManager.ConfigureChassisAndFaces(
                _chassis,
                _avatarFace,
                _monsterFace,
                _itemFace,
                relicFace);

            var card = _cardManager.SpawnView(
                _nextUid++,
                defId: "relic.coin_armor",
                kind: CardPresentationKind.Relic);

            card.CommitPresentation(new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Relic,
                DefId = "relic.coin_armor",
                DisplayName = "金币护甲",
                MainIcon = _mainIcon,
                BasicDescription = "获得护甲",
                FaceUp = true,
            });

            Assert.AreEqual("获得护甲", ReadBasicDescription(card));
            Object.DestroyImmediate(relicFace);
        }

        private static Sprite ReadMainIcon(ManagedCard card)
        {
            Assert.IsTrue(
                CardFaceSlotNodeMap.TryFindRenderer(
                    card.MountedFaceRoot,
                    CardFaceSlotCodes.MainIcon,
                    out var renderer));
            return renderer.sprite;
        }

        private static string ReadName(ManagedCard card)
        {
            Assert.IsTrue(
                CardFaceSlotNodeMap.TryReadText(
                    card.MountedFaceRoot,
                    CardFaceSlotCodes.Name,
                    out var text));
            return text;
        }

        private static string ReadNumericText(ManagedCard card, string slotCode)
        {
            Assert.IsTrue(
                CardFaceSlotNodeMap.TryReadText(card.MountedFaceRoot, slotCode, out var text),
                $"缺少数值槽节点：{slotCode}");
            return text;
        }

        private static string ReadBasicDescription(ManagedCard card)
        {
            Assert.IsTrue(
                CardFaceSlotNodeMap.TryReadText(
                    card.MountedFaceRoot,
                    CardFaceSlotCodes.BasicDescription,
                    out var text),
                "缺少基础描述槽节点");
            return text;
        }

        /// <summary>
        /// 经反射读描述 TMP 的 spriteAsset 索引（Tests 程序集不直接引用 TMP 类型）。
        /// </summary>
        private static int ReadDescriptionSpriteIndex(ManagedCard card, string slotCode)
        {
            Assert.IsNotNull(TextMeshProType);
            var descNode = FindDeep(card.MountedFaceRoot, "描述");
            Assert.IsNotNull(descNode, "怪物描述节点");
            var tmp = descNode.GetComponent(TextMeshProType);
            Assert.IsNotNull(tmp);
            var spriteAsset = TextMeshProType.GetProperty("spriteAsset")?.GetValue(tmp);
            Assert.IsNotNull(spriteAsset, "应挂运行时 TMP_SpriteAsset");
            var getIndex = spriteAsset.GetType().GetMethod("GetSpriteIndexFromName");
            Assert.IsNotNull(getIndex);
            return (int)getIndex.Invoke(spriteAsset, new object[] { slotCode });
        }

        private static GameObject CreateChassisPrefab(string name)
        {
            var go = new GameObject(name);
            go.AddComponent<SortingGroup>();
            go.AddComponent<StandardCardView>();
            go.AddComponent<CardTransformTower>().EnsureTower();
            return go;
        }

        private static GameObject CreateAvatarFacePrefab(string name)
        {
            var go = new GameObject(name);
            var front = new GameObject("front");
            front.transform.SetParent(go.transform, false);
            new GameObject("back").transform.SetParent(go.transform, false);

            CreateSpriteChild(front.transform, "主图标");
            CreateTmpChild(front.transform, "名字", "模板名");
            CreateTmpChild(front.transform, "攻击数值", "0");
            CreateTmpChild(front.transform, "护甲数值", "0");
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

        private static GameObject CreateRelicFacePrefab(string name)
        {
            var go = new GameObject(name);
            var front = new GameObject("GameObject");
            front.transform.SetParent(go.transform, false);
            new GameObject("back").transform.SetParent(go.transform, false);

            CreateSpriteChild(front.transform, "遗物主图标");
            var nameRoot = new GameObject("遗物名字");
            nameRoot.transform.SetParent(front.transform, false);
            CreateTmpChild(nameRoot.transform, "标准世界文字", "模板遗物");
            var panel = new GameObject("描述面板");
            panel.transform.SetParent(front.transform, false);
            CreateTmpChild(panel.transform, "标准世界文字", string.Empty);
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
            var texture = new Texture2D(2, 2);
            texture.name = name;
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
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
            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }

            sprite = null;
        }

        private static Transform FindDeep(Transform root, string name)
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
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
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

        private static void DestroyAllCardManagers()
        {
            foreach (var manager in Object.FindObjectsByType<CardManagerSingleton>(FindObjectsSortMode.None))
            {
                if (manager != null)
                {
                    Object.DestroyImmediate(manager.gameObject);
                }
            }
        }

        private static void ResetCardManagerSingleton()
        {
            var field = typeof(CardManagerSingleton).GetField(
                "_instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }
    }
}
