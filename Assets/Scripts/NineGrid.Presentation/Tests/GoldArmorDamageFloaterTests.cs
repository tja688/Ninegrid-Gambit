using System.Collections.Generic;
using System.Reflection;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 金币盔甲代偿的飘字口径回归（对照 Notes 卡面护甲只增不减复盘 2026-08-16）：
    /// 拆分模式下 ArmorDamage 是毛甲伤，飘字必须按「净甲伤 + 金币代偿」拆账——
    /// 全代偿命中不得再飘绿灰甲伤（否则误读成「卡面漏扣甲」），改飘金色「-N」；
    /// 部分代偿同时飘净甲伤与金币代偿两个数字。
    /// </summary>
    public sealed class GoldArmorDamageFloaterTests
    {
        private readonly List<(Vector3 Pos, float Amount, DamageNumberKind Kind)> mSpawns =
            new List<(Vector3, float, DamageNumberKind)>();

        private GameObject mCardManagerGo;
        private GameObject mViewGo;

        [SetUp]
        public void SetUp()
        {
            mSpawns.Clear();
            DamageNumberHook.Spawn = (pos, amount, kind) => mSpawns.Add((pos, amount, kind));

            mViewGo = new GameObject("TestCardView");
            mViewGo.transform.position = new Vector3(1f, 2f, 0f);
            var view = mViewGo.AddComponent<StandardCardView>();

            mCardManagerGo = new GameObject("TestCardManager");
            var manager = mCardManagerGo.AddComponent<CardManagerSingleton>();
            var dict = (Dictionary<int, ManagedCard>)typeof(CardManagerSingleton)
                .GetField("_cardsByUid", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(manager);
            dict[7] = new ManagedCard(7, "avatar.default", view);

            CardEntityLifecycleHook.ResolveCards = () => manager;
            CardEntityLifecycleHook.TryGet = manager.TryGet;
        }

        [TearDown]
        public void TearDown()
        {
            DamageNumberHook.Spawn = null;
            CardEntityLifecycleHook.Reset();
            Object.DestroyImmediate(mViewGo);
            Object.DestroyImmediate(mCardManagerGo);
            TriggerPulseHub.ResetToNull();
        }

        private static PresentationInstruction BuildDamageDealt(
            int amount,
            int armorDamage,
            int hpDamage,
            int goldAbsorbed,
            int remainingArmor)
        {
            PresentationEventMap.TryGet(CoreEventType.DamageDealt, out var map);
            var gameEvent = new CoreGameEvent(CoreEventType.DamageDealt, 1, "CombatHit")
                .WithTarget(7)
                .WithAmount(amount)
                .WithDamageSplit(armorDamage, hpDamage)
                .WithGoldAbsorbedArmor(goldAbsorbed)
                .WithRemaining(17, remainingArmor);
            return new PresentationInstruction(gameEvent, map);
        }

        [Test]
        public void FullGoldAbsorb_NoArmorFloater_OnlyGoldSpendFloater()
        {
            // 7 伤 / 甲 4 全代偿：飘红字 3 + 金色 -20，绝不飘绿灰甲伤 4。
            var instruction = BuildDamageDealt(
                amount: 7, armorDamage: 4, hpDamage: 3, goldAbsorbed: 4, remainingArmor: 4);

            Assert.IsTrue(new DamageFloaterBeatHandler().TryApply(instruction));

            Assert.IsFalse(
                HasKind(DamageNumberKind.ArmorDamage),
                "全代偿不得产绿灰甲伤飘字（卡面当前甲未下降）");
            Assert.IsTrue(
                HasKindWithValue(DamageNumberKind.HpDamage, 3f),
                "血伤红字 3 照常");
            Assert.IsTrue(
                HasKindWithValue(DamageNumberKind.GoldSpend, -20f),
                "金色飘字应显示 -20（4 甲 × 5 金），负号承载扣款语义");
        }

        [Test]
        public void PartialGoldAbsorb_NetArmorFloater_AndGoldSpendFloater()
        {
            // 7 伤 / 甲 4 / 代偿 2：飘绿灰净甲伤 2 + 金色 -10 + 红字 3。
            var instruction = BuildDamageDealt(
                amount: 7, armorDamage: 4, hpDamage: 3, goldAbsorbed: 2, remainingArmor: 2);

            Assert.IsTrue(new DamageFloaterBeatHandler().TryApply(instruction));

            Assert.IsTrue(
                HasKindWithValue(DamageNumberKind.ArmorDamage, 2f),
                "净甲伤 = 毛 4 - 代偿 2 = 2");
            Assert.IsFalse(
                HasKindWithValue(DamageNumberKind.ArmorDamage, 4f),
                "不得再按毛甲伤 4 飘绿灰字");
            Assert.IsTrue(
                HasKindWithValue(DamageNumberKind.GoldSpend, -10f),
                "金币代偿飘 -10（2 甲 × 5 金）");
            Assert.IsTrue(HasKindWithValue(DamageNumberKind.HpDamage, 3f));
        }

        [Test]
        public void NoGoldAbsorb_ArmorFloaterOnly_NoGoldSpendFloater()
        {
            // 无代偿：行为与旧版一致——只飘绿灰甲伤 4，不飘金币。
            var instruction = BuildDamageDealt(
                amount: 7, armorDamage: 4, hpDamage: 3, goldAbsorbed: 0, remainingArmor: 0);

            Assert.IsTrue(new DamageFloaterBeatHandler().TryApply(instruction));

            Assert.IsTrue(
                HasKindWithValue(DamageNumberKind.ArmorDamage, 4f),
                "无代偿时甲伤飘字按原毛口径");
            Assert.IsFalse(
                HasKind(DamageNumberKind.GoldSpend),
                "无代偿不得产金币飘字");
        }

        private bool HasKind(DamageNumberKind kind)
        {
            for (var i = 0; i < mSpawns.Count; i++)
            {
                if (mSpawns[i].Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasKindWithValue(DamageNumberKind kind, float amount)
        {
            for (var i = 0; i < mSpawns.Count; i++)
            {
                if (mSpawns[i].Kind == kind
                    && Mathf.Approximately(mSpawns[i].Amount, amount))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
