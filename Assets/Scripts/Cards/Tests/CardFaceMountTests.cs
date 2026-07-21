using NUnit.Framework;
using NineGrid.Cards.Convergence;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards.Tests
{
    /// <summary>
    /// Seam：给定 Kind Spawn 后，挂载的是对应卡面模板（外部行为）。
    /// 对齐 #13 / ADR-0002：Instantiate(底盘) → L4/FacePivot 挂面；RegisterPrefab 仅特例。
    /// </summary>
    public sealed class CardFaceMountTests
    {
        private GameObject _managerGo;
        private CardManagerSingleton _cardManager;
        private GameObject _chassis;
        private GameObject _avatarFace;
        private GameObject _monsterFace;
        private GameObject _itemFace;
        private GameObject _relicFace;
        private GameObject _overridePrefab;
        private int _nextUid = 13001;

        [SetUp]
        public void SetUp()
        {
            DestroyAllCardManagers();
            ResetCardManagerSingleton();

            _managerGo = new GameObject("CardManager_FaceMountTest");
            _cardManager = _managerGo.AddComponent<CardManagerSingleton>();

            _chassis = CreateChassisPrefab("ChassisPrefab");
            _avatarFace = CreateFacePrefab("AvatarFacePrefab");
            _monsterFace = CreateFacePrefab("MonsterFacePrefab");
            _itemFace = CreateFacePrefab("ItemFacePrefab");
            _relicFace = CreateFacePrefab("RelicFacePrefab");
            _overridePrefab = CreateChassisPrefab("OverrideFullPrefab");

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
            DestroyIfPresent(ref _overridePrefab);

            DestroyAllCardManagers();
            ResetCardManagerSingleton();
        }

        [TestCase(CardPresentationKind.Avatar, "AvatarFacePrefab")]
        [TestCase(CardPresentationKind.Monster, "MonsterFacePrefab")]
        [TestCase(CardPresentationKind.HelpCard, "ItemFacePrefab")]
        [TestCase(CardPresentationKind.Item, "ItemFacePrefab")]
        [TestCase(CardPresentationKind.PlayerCard, "ItemFacePrefab")]
        [TestCase(CardPresentationKind.Relic, "RelicFacePrefab")]
        public void SpawnView_ByKind_MountsMappedFaceUnderFacePivot(
            CardPresentationKind kind,
            string expectedFaceName)
        {
            var card = _cardManager.SpawnView(
                _nextUid++,
                defId: $"test.{kind}",
                kind: kind);

            Assert.IsNotNull(card);
            Assert.IsNotNull(card.MountedFaceRoot, "主路径应挂上卡面");
            Assert.AreEqual(expectedFaceName, card.MountedFaceRoot.name);
            Assert.AreEqual(CardTransformTower.FacePivotName, card.MountedFaceRoot.parent.name);
            Assert.AreEqual(kind, card.CoreKind);

            var tower = card.GameObject.GetComponent<CardTransformTower>();
            Assert.IsNotNull(tower);
            Assert.AreSame(tower.FacePivot, card.MountedFaceRoot.parent);
            Assert.AreSame(tower.CardVisual, tower.FacePivot.parent);

            Assert.IsNull(
                card.MountedFaceRoot.GetComponent<SortingGroup>(),
                "卡面不得自挂第二套卡级 SortingGroup");
            Assert.IsNotNull(
                card.GameObject.GetComponent<SortingGroup>(),
                "底盘须保留卡级 SortingGroup");
        }

        [Test]
        public void RegisterPrefab_Override_SkipsKindFaceMount()
        {
            const string overrideDefId = "test.override.full";
            _cardManager.RegisterPrefab(overrideDefId, _overridePrefab);

            var card = _cardManager.SpawnView(
                _nextUid++,
                overrideDefId,
                kind: CardPresentationKind.Monster);

            Assert.IsNotNull(card);
            Assert.IsNull(card.MountedFaceRoot, "RegisterPrefab 特例路径不挂 Kind 卡面");
            Assert.IsTrue(card.GameObject.name.Contains("OverrideFullPrefab"));
        }

        [Test]
        public void SpawnView_UnknownKind_DoesNotMountFace()
        {
            var card = _cardManager.SpawnView(
                _nextUid++,
                defId: "skill.legacy_player",
                kind: CardPresentationKind.Unknown);

            Assert.IsNotNull(card, "Unknown 仍可实例化底盘（不为 PlayerSkill 开卡面）");
            Assert.IsNull(card.MountedFaceRoot);
            Assert.AreEqual(0, CountFacePivotChildren(card));
        }

        private static int CountFacePivotChildren(ManagedCard card)
        {
            var tower = card.GameObject.GetComponent<CardTransformTower>();
            return tower != null && tower.FacePivot != null ? tower.FacePivot.childCount : -1;
        }

        private static GameObject CreateChassisPrefab(string name)
        {
            var go = new GameObject(name);
            go.AddComponent<SortingGroup>();
            go.AddComponent<StandardCardView>();
            go.AddComponent<CardTransformTower>().EnsureTower();
            return go;
        }

        private static GameObject CreateFacePrefab(string name)
        {
            var go = new GameObject(name);
            var front = new GameObject("front");
            front.transform.SetParent(go.transform, false);
            return go;
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
