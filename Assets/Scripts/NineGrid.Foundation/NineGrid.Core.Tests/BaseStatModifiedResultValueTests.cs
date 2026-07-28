using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #54：BaseStatModified 携带结算后绝对值，且不破坏 Amount=StatId / Delta 增量约定。
    /// </summary>
    public sealed class BaseStatModifiedResultValueTests
    {
        private IArchitecture mArch;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 54UL });
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void ModifyBaseStat_Emits_ResultValue_Equal_To_PostSettlement_Absolute()
        {
            var avatar = Avatar();
            var previous = (int)avatar.Stats.GetBase(StatId.Attack);
            var startIndex = mPipeline.EventLog.Entries.Count;

            mPipeline.Enqueue(new ModifyBaseStatAction(avatar.Uid, StatId.Attack, 1, "test:+1"));
            mPipeline.RunToCompletion();

            var evt = FindLastBaseStatModified(startIndex);
            Assert.IsNotNull(evt, "应产出 BaseStatModified");
            Assert.AreEqual((int)StatId.Attack, evt.Amount, "Amount 仍为 StatId，供 DSL EventFilter 使用");
            Assert.AreEqual(1, evt.Delta, "Delta 仍为增量，供事件日志与触发原子读取");
            Assert.AreEqual(previous + 1, evt.ResultValue, "ResultValue 为结算后绝对值");
            Assert.AreEqual(previous + 1, (int)Avatar().Stats.GetBase(StatId.Attack));
        }

        [Test]
        public void ModifyBaseStat_Clamped_To_Zero_Still_Writes_ResultValue()
        {
            var avatar = Avatar();
            avatar.Stats.SetBase(StatId.Attack, 2);
            var startIndex = mPipeline.EventLog.Entries.Count;

            mPipeline.Enqueue(new ModifyBaseStatAction(avatar.Uid, StatId.Attack, -5, "test:clamp"));
            mPipeline.RunToCompletion();

            var evt = FindLastBaseStatModified(startIndex);
            Assert.IsNotNull(evt);
            Assert.AreEqual(-5, evt.Delta);
            Assert.AreEqual(0, evt.ResultValue);
            Assert.AreEqual(0, (int)Avatar().Stats.GetBase(StatId.Attack));
        }

        private CardInstance Avatar()
        {
            return mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
        }

        private CoreGameEvent FindLastBaseStatModified(int startIndex)
        {
            var entries = mPipeline.EventLog.Entries;
            CoreGameEvent found = null;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.BaseStatModified)
                {
                    found = entries[i];
                }
            }

            return found;
        }
    }
}
