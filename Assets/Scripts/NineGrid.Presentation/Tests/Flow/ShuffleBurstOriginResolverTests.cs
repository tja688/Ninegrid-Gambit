using NUnit.Framework;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class ShuffleBurstOriginResolverTests
    {
        [Test]
        public void TryResolveWorld_PrefersBoardSlot_WhenTriggerStagedOffscreen()
        {
            // 复现 perflog：StageFieldDead 后 trigger 在 y≈-80；炸开须用死亡格锚点。
            var entry = new ShuffleIntoDeckPresentationEntry(
                uid: 24,
                defId: "monster.skull_head",
                cause: "fall_apart",
                actionId: 104,
                eventSequence: 1,
                kind: ShuffleIntoDeckEventKind.NewCard,
                triggerCardUid: 18,
                fromBoardSlot: 6);

            var slotAnchor = new Vector3(5f, 0.04f, 0f);
            var stagedCorpse = new Vector3(5f, -80f, 0f);

            var ok = ShuffleBurstOriginResolver.TryResolveWorld(
                entry,
                slot => slot == 6 ? slotAnchor : (Vector3?)null,
                _ => null,
                _ => stagedCorpse,
                out var world);

            Assert.IsTrue(ok);
            Assert.AreEqual(slotAnchor.x, world.x, 1e-4f);
            Assert.AreEqual(slotAnchor.y, world.y, 1e-4f);
            Assert.Greater(world.y, ShuffleBurstOriginResolver.UnusableOriginYThreshold);
        }

        [Test]
        public void TryResolveWorld_RejectsStagedCorpseFallback_WhenNoSlot()
        {
            var entry = new ShuffleIntoDeckPresentationEntry(
                uid: 24,
                defId: "monster.skull_head",
                cause: "fall_apart",
                actionId: 104,
                eventSequence: 1,
                kind: ShuffleIntoDeckEventKind.NewCard,
                triggerCardUid: 18,
                fromBoardSlot: 0);

            var ok = ShuffleBurstOriginResolver.TryResolveWorld(
                entry,
                _ => null,
                _ => null,
                _ => new Vector3(5f, -ShuffleBurstOriginResolver.StagedCorpseYOffset, 0f),
                out _);

            Assert.IsFalse(ok, "y=-80 staged corpse must not become burst origin");
        }

        [Test]
        public void TryResolveWorld_UsesLiveTrigger_WhenOnBoardAndNoSlot()
        {
            var entry = new ShuffleIntoDeckPresentationEntry(
                uid: 24,
                defId: "monster.skull_head",
                cause: "test",
                actionId: 1,
                eventSequence: 1,
                kind: ShuffleIntoDeckEventKind.NewCard,
                triggerCardUid: 18,
                fromBoardSlot: 0);

            var livePos = new Vector3(2f, 1f, 0f);
            var go = new GameObject("LiveTrigger");
            try
            {
                go.transform.position = livePos;
                // ManagedCard Transform 来自 View；此处用回调直接返回可用卡的等价：走 fallback 坐标。
                var ok = ShuffleBurstOriginResolver.TryResolveWorld(
                    entry,
                    _ => null,
                    _ => null,
                    _ => livePos,
                    out var world);

                Assert.IsTrue(ok);
                Assert.AreEqual(livePos.y, world.y, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
