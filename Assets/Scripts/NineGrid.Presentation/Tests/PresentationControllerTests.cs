using NineGrid.Core;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    public sealed class PresentationControllerTests
    {
        [Test]
        public void Controller_BindsToCoreArchitecture()
        {
            using (PresentationArchitectureFixture.CreateBare())
            using (var host = PresentationControllerHost.Create())
            {
                Assert.AreSame(
                    NineGridArchitecture.Interface,
                    ((IBelongToArchitecture)host.Controller).GetArchitecture());
            }
        }

        [Test]
        public void Controller_RunsLifecycleTemplate_AndUnregistersOnDestroy()
        {
            using (PresentationArchitectureFixture.CreateBare())
            using (var host = PresentationControllerHost.Create("PresentationControllerLifecycle"))
            {
                // EditMode 下 AddComponent 不触发 Unity 消息，显式驱动模板钩子。
                host.Controller.DriveAwake();
                Assert.AreEqual(1, host.Controller.BindCount);
                Assert.AreEqual(0, host.Controller.UnbindCount);
                Assert.IsFalse(host.Controller.EventUnregistered);

                host.Controller.DriveOnDestroy();
                Assert.AreEqual(1, host.Controller.UnbindCount);
                Assert.IsTrue(host.Controller.EventUnregistered);
            }
        }
    }
}
