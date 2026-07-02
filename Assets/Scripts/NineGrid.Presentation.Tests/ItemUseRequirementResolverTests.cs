using NineGrid.Presentation.Interaction;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class ItemUseRequirementResolverTests
    {
        [Test]
        public void Resolve_FoodCard_IsDirect()
        {
            ItemUseRequirement requirement = ItemUseRequirementResolver.Resolve("help.food_card");
            Assert.AreEqual(ItemUseRequirementKind.None, requirement.Kind);
        }

        [Test]
        public void Resolve_StatBoost_IsOptionWithThreeChoices()
        {
            ItemUseRequirement requirement = ItemUseRequirementResolver.Resolve("help.stat_boost_card");
            Assert.AreEqual(ItemUseRequirementKind.Option, requirement.Kind);
            Assert.AreEqual(3, requirement.OptionIds.Count);
            Assert.AreEqual("Attack", requirement.ResolveOptionId(0));
            Assert.AreEqual("Armor", requirement.ResolveOptionId(1));
            Assert.AreEqual("Hp", requirement.ResolveOptionId(2));
        }

        [Test]
        public void Resolve_SwapCard_RequiresTwoBoardTargets()
        {
            ItemUseRequirement requirement = ItemUseRequirementResolver.Resolve("help.swap_card");
            Assert.AreEqual(ItemUseRequirementKind.BoardTarget, requirement.Kind);
            Assert.AreEqual(2, requirement.TargetCount);
        }

        [Test]
        public void Resolve_TeleportCard_RequiresOneBoardTarget()
        {
            ItemUseRequirement requirement = ItemUseRequirementResolver.Resolve("help.teleport_card");
            Assert.AreEqual(ItemUseRequirementKind.BoardTarget, requirement.Kind);
            Assert.AreEqual(1, requirement.TargetCount);
        }

        [Test]
        public void Resolve_Kidnapping_RequiresNonEliteBossMonster()
        {
            ItemUseRequirement requirement = ItemUseRequirementResolver.Resolve("help.kidnapping");
            Assert.AreEqual(ItemUseRequirementKind.BoardTarget, requirement.Kind);
            Assert.AreEqual(1, requirement.TargetCount);
            Assert.AreEqual(NineGrid.Core.CardKind.Monster, requirement.TargetKind);
            Assert.IsTrue(requirement.ExcludeElite);
            Assert.IsTrue(requirement.ExcludeBoss);
        }

        [Test]
        public void Resolve_ThrowingKnife_RequiresMonsterTarget()
        {
            ItemUseRequirement requirement = ItemUseRequirementResolver.Resolve("help.throwing_knife");
            Assert.AreEqual(ItemUseRequirementKind.BoardTarget, requirement.Kind);
            Assert.AreEqual(1, requirement.TargetCount);
            Assert.AreEqual(NineGrid.Core.CardKind.Monster, requirement.TargetKind);
        }
    }
}
