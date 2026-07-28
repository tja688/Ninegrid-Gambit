using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #54：表演事件映射表锚点归属穷尽性——漏登记或 None 无理由即红。
    /// </summary>
    public sealed class PresentationEventMapBeatExhaustivenessTests
    {
        [Test]
        public void Every_CoreEventType_Has_Presentation_Map_Entry()
        {
            var missing = PresentationEventMap.FindMissingCoreEvents();
            Assert.IsEmpty(missing, FormatMissing("unmapped CoreEventType", missing));
        }

        [Test]
        public void Every_None_Beat_Has_NonEmpty_Reason()
        {
            var missing = PresentationEventMap.FindNoneBeatsMissingReason();
            Assert.IsEmpty(missing, FormatMissing("PresentationBeat.None without reason", missing));
        }

        [Test]
        public void CardFace_Stat_Events_Have_Expected_Beats()
        {
            Assert.AreEqual(PresentationBeat.Impact, PresentationEventMap.Get(CoreEventType.HpChanged).Beat);
            Assert.AreEqual(PresentationBeat.Impact, PresentationEventMap.Get(CoreEventType.ArmorChanged).Beat);
            Assert.AreEqual(PresentationBeat.Impact, PresentationEventMap.Get(CoreEventType.Healed).Beat);
            Assert.AreEqual(PresentationBeat.Settled, PresentationEventMap.Get(CoreEventType.BaseStatModified).Beat);
            Assert.AreEqual(PresentationBeat.Settled, PresentationEventMap.Get(CoreEventType.CardSpawned).Beat);
            Assert.AreEqual(PresentationBeat.Settled, PresentationEventMap.Get(CoreEventType.CardDealt).Beat);
            Assert.AreEqual(PresentationBeat.Settled, PresentationEventMap.Get(CoreEventType.AvatarAppeared).Beat);
            Assert.AreEqual(PresentationBeat.Settled, PresentationEventMap.Get(CoreEventType.CardKilled).Beat);
        }

        [Test]
        public void Gold_Decorative_Event_Is_Settled()
        {
            Assert.AreEqual(
                PresentationBeat.Settled,
                PresentationEventMap.Get(CoreEventType.GoldModified).Beat,
                "金币飞字/HUD 须在 Settled 消费结算指令");
        }

        [Test]
        public void RewardOffered_Is_Settled_For_Bounce_Face_Commit()
        {
            Assert.AreEqual(
                PresentationBeat.Settled,
                PresentationEventMap.Get(CoreEventType.RewardOffered).Beat,
                "奖励 Bounce 候选项须在 Settled 经排期器提交卡面投影");
        }

        [Test]
        public void Damage_And_Effect_Decorative_Events_Are_Impact()
        {
            Assert.AreEqual(
                PresentationBeat.Impact,
                PresentationEventMap.Get(CoreEventType.DamageDealt).Beat,
                "伤害飘字须在 Impact 消费结算指令");
            Assert.AreEqual(
                PresentationBeat.Impact,
                PresentationEventMap.Get(CoreEventType.EffectTriggered).Beat,
                "效果 FX 脉冲须在 Impact 消费结算指令");
        }

        [Test]
        public void Observer_Style_BaseStat_Is_Not_Impact()
        {
            Assert.AreNotEqual(
                PresentationBeat.Impact,
                PresentationEventMap.Get(CoreEventType.BaseStatModified).Beat,
                "观察型加攻不得落在命中锚点（抬手就涨回流）");
        }

        private static string FormatMissing(string label, List<CoreEventType> missing)
        {
            if (missing == null || missing.Count == 0)
            {
                return label + ": (none)";
            }

            var sb = new StringBuilder();
            sb.Append(label).Append(':');
            for (var i = 0; i < missing.Count; i++)
            {
                sb.Append(' ').Append(missing[i]);
            }

            return sb.ToString();
        }
    }
}
