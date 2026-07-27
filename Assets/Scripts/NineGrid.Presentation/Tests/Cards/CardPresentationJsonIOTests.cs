using System.IO;
using NUnit.Framework;
using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Card Presentation JSON DTO / 槽解析契约（不依赖场景与 AssetDatabase 帧加载）。
    /// </summary>
    public sealed class CardPresentationJsonIOTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "NineGridCardPresentationTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_tempDir) && Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, true);
                }
                catch
                {
                    // ignore temp cleanup failures
                }
            }

            CardPresentationConfigCatalog.Invalidate();
        }

        [Test]
        public void ToSafeFileName_ReplacesDots()
        {
            Assert.AreEqual("monster_stone_man", CardPresentationJsonIO.ToSafeFileName("monster.stone_man"));
            Assert.AreEqual("avatar_default", CardPresentationJsonIO.ToSafeFileName("avatar.default"));
            Assert.AreEqual(string.Empty, CardPresentationJsonIO.ToSafeFileName(null));
            Assert.AreEqual(string.Empty, CardPresentationJsonIO.ToSafeFileName(string.Empty));
        }

        [Test]
        public void Normalize_UnknownOrEmpty_FallsBackToIdle()
        {
            Assert.AreEqual(CardAnimSlotIds.Idle, CardAnimSlotIds.Normalize(null));
            Assert.AreEqual(CardAnimSlotIds.Idle, CardAnimSlotIds.Normalize(""));
            Assert.AreEqual(CardAnimSlotIds.Idle, CardAnimSlotIds.Normalize("  "));
            Assert.AreEqual(CardAnimSlotIds.Idle, CardAnimSlotIds.Normalize("unknown"));
            Assert.AreEqual(CardAnimSlotIds.Attack, CardAnimSlotIds.Normalize("Attack"));
            Assert.AreEqual(CardAnimSlotIds.Lunch, CardAnimSlotIds.Normalize("LUNCH"));
        }

        [Test]
        public void RoundTrip_SerializeDeserialize_PreservesFields()
        {
            var original = CardPresentationJsonIO.CreateDefault("monster.demo", "Monster");
            original.displayName = "Demo";
            original.description = "desc";
            original.gold = 12;
            original.deckId = "deck.a";
            original.stats.hp = 8;
            original.stats.armor = 2;
            original.stats.attack = 3;
            original.stats.action = 1;
            original.sprites.mainIcon = "Assets/Arts/icon.png";
            original.sprites.cardFrame = "Assets/Arts/frame.png";
            original.sprites.banner = "Assets/Arts/banner.png";
            original.mainVisual.offsetX = 1.5f;
            original.mainVisual.offsetY = -2f;
            original.mainVisual.uniformScale = 0.85f;
            original.animations.defaultFps = 10f;
            original.animations.slots[0].sourceType = "folder";
            original.animations.slots[0].path = "Assets/Arts/idle";
            original.animations.slots[0].offsetX = 0.1f;
            original.extraSlots = new[]
            {
                new CardPresentationExtraSlotDto { code = "Extra", path = "Assets/Arts/extra.png" },
            };

            var json = CardPresentationJsonIO.ToJson(original);
            Assert.IsTrue(json.Contains("\"contentId\"") || json.Contains("contentId"));
            Assert.IsTrue(json.Contains("monster.demo"));

            var path = Path.Combine(_tempDir, "monster_demo.json");
            File.WriteAllText(path, json);

            Assert.IsTrue(CardPresentationJsonIO.TryLoad(path, out var loaded, out var error), error);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.schemaVersion);
            Assert.AreEqual("monster.demo", loaded.contentId);
            Assert.AreEqual("Monster", loaded.kind);
            Assert.AreEqual("Demo", loaded.displayName);
            Assert.AreEqual("desc", loaded.description);
            Assert.AreEqual(12, loaded.gold);
            Assert.AreEqual("deck.a", loaded.deckId);
            Assert.AreEqual(8, loaded.stats.hp);
            Assert.AreEqual(2, loaded.stats.armor);
            Assert.AreEqual(3, loaded.stats.attack);
            Assert.AreEqual(1, loaded.stats.action);
            Assert.AreEqual("Assets/Arts/icon.png", loaded.sprites.mainIcon);
            Assert.AreEqual("Assets/Arts/frame.png", loaded.sprites.cardFrame);
            Assert.AreEqual("Assets/Arts/banner.png", loaded.sprites.banner);
            Assert.AreEqual(1.5f, loaded.mainVisual.offsetX, 0.0001f);
            Assert.AreEqual(-2f, loaded.mainVisual.offsetY, 0.0001f);
            Assert.AreEqual(0.85f, loaded.mainVisual.uniformScale, 0.0001f);
            Assert.AreEqual(10f, loaded.animations.defaultFps, 0.0001f);
            Assert.AreEqual(CardAnimSlotIds.Idle, loaded.animations.slots[0].id);
            Assert.AreEqual("folder", loaded.animations.slots[0].sourceType);
            Assert.AreEqual("Assets/Arts/idle", loaded.animations.slots[0].path);
            Assert.AreEqual(0.1f, loaded.animations.slots[0].offsetX, 0.0001f);
            Assert.AreEqual(1, loaded.extraSlots.Length);
            Assert.AreEqual("Extra", loaded.extraSlots[0].code);
        }

        [Test]
        public void Resolve_MissingLunch_FallsBackToIdleThenStatic()
        {
            var config = CardPresentationJsonIO.CreateDefault("monster.x", "Monster");
            // all slots sourceType=none by default
            var kind = CardPresentationAnimResolve.Resolve(
                config,
                CardAnimSlotIds.Lunch,
                out var slotId,
                out var slotDto);
            Assert.AreEqual(CardPresentationAnimResolve.Kind.StaticMainIcon, kind);
            Assert.AreEqual(CardAnimSlotIds.Idle, slotId);
            Assert.IsNull(slotDto);

            config.animations.slots[0].sourceType = "folder";
            config.animations.slots[0].path = "Assets/Arts/idle";
            kind = CardPresentationAnimResolve.Resolve(
                config,
                CardAnimSlotIds.Lunch,
                out slotId,
                out slotDto);
            Assert.AreEqual(CardPresentationAnimResolve.Kind.Frames, kind);
            Assert.AreEqual(CardAnimSlotIds.Idle, slotId);
            Assert.IsNotNull(slotDto);
            Assert.AreEqual("Assets/Arts/idle", slotDto.path);
        }

        [Test]
        public void Resolve_AttackWithFrames_UsesAttack()
        {
            var config = CardPresentationJsonIO.CreateDefault("monster.y", "Monster");
            // attack is index 1 in All
            config.animations.slots[1].sourceType = "atlas";
            config.animations.slots[1].path = "Assets/Arts/attack.png";

            var kind = CardPresentationAnimResolve.Resolve(
                config,
                "ATTACK",
                out var slotId,
                out var slotDto);
            Assert.AreEqual(CardPresentationAnimResolve.Kind.Frames, kind);
            Assert.AreEqual(CardAnimSlotIds.Attack, slotId);
            Assert.IsNotNull(slotDto);
            Assert.AreEqual("atlas", slotDto.sourceType);
        }

        [Test]
        public void GetAuthoringAndStreamingPaths_UseSafeId()
        {
            Assert.AreEqual(
                "Assets/Arts/ContentVisual/cards/monster_stone_man.json",
                CardPresentationJsonIO.GetAuthoringPath("monster.stone_man"));
            Assert.AreEqual(
                "Assets/StreamingAssets/ContentVisual/cards/monster_stone_man.json",
                CardPresentationJsonIO.GetStreamingPath("monster.stone_man"));
        }

        [Test]
        public void TryLoad_MissingFile_ReturnsError()
        {
            var missing = Path.Combine(_tempDir, "nope.json");
            Assert.IsFalse(CardPresentationJsonIO.TryLoad(missing, out var dto, out var error));
            Assert.IsNull(dto);
            Assert.IsFalse(string.IsNullOrEmpty(error));
        }

        [Test]
        public void SpritePath_SplitAndCompose_RoundTripSubSpriteKey()
        {
            CardPresentationSpritePath.SplitPath(
                "Assets/Arts/Images/Multiple/icons_full_32.png#icons_full_32_42",
                out var path,
                out var spriteName);
            Assert.AreEqual("Assets/Arts/Images/Multiple/icons_full_32.png", path);
            Assert.AreEqual("icons_full_32_42", spriteName);
            Assert.AreEqual(
                "Assets/Arts/Images/Multiple/icons_full_32.png#icons_full_32_42",
                CardPresentationSpritePath.ComposePath(path, spriteName));

            CardPresentationSpritePath.SplitPath(
                "Assets/Arts/Images/Png/Items/platform.png",
                out path,
                out spriteName);
            Assert.AreEqual("Assets/Arts/Images/Png/Items/platform.png", path);
            Assert.IsNull(spriteName);
        }

#if UNITY_EDITOR
        [Test]
        public void LoadSprite_MultipleAtlas_NamedSubSprite_NotFirstSlice()
        {
            const string sheet = "Assets/Arts/Images/Multiple/icons_full_32.png";
            const string namedKey = sheet + "#icons_full_32_42";

            var first = CardPresentationSpritePath.LoadSprite(sheet);
            var named = CardPresentationSpritePath.LoadSprite(namedKey);

            Assert.IsNotNull(first, "图集应至少能回退到首切片");
            Assert.IsNotNull(named, "带 #spriteName 应能加载指定切片");
            Assert.AreEqual("icons_full_32_0", first.name);
            Assert.AreEqual("icons_full_32_42", named.name);
            Assert.AreNotSame(first, named);

            var encoded = CardPresentationSpritePath.EncodeAssetReference(named);
            Assert.AreEqual(namedKey, encoded);
            Assert.AreSame(named, CardPresentationSpritePath.LoadSprite(encoded));
        }
#endif
    }
}
