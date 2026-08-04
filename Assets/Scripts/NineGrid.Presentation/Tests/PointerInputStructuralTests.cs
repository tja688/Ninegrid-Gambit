using System.IO;
using System.Text.RegularExpressions;
using NineGrid.Presentation.Platform;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// ADR-0006：命中代理禁用 OnMouse*；指针读口与 Win Player mitigation 标记存在。
    /// </summary>
    public sealed class PointerInputStructuralTests
    {
        private static readonly string[] HitProxyFiles =
        {
            "Cards/GroundCardHitProxy.cs",
            "Cards/GroundFieldHitSurface.cs",
            "Cards/GroundSlotHitProxy.cs",
            "Cards/HandCardHitProxy.cs",
            "Cards/BoardSelectParkedCardHitProxy.cs",
        };

        [Test]
        public void Adr0006_StatusIsAccepted()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "docs", "adr", "0006-windows-high-polling-mouse-mitigation.md"));
            Assert.IsTrue(File.Exists(path), "missing ADR-0006");
            var text = File.ReadAllText(path);
            Assert.IsTrue(
                Regex.IsMatch(text, @"^---\s*\r?\nstatus:\s*accepted\s*\r?\n---", RegexOptions.Multiline),
                "ADR-0006 status 应为 accepted");
        }

        [Test]
        public void HitProxies_DoNotDeclareOnMouseCallbacks()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
            var ban = new Regex(@"\bvoid\s+OnMouse(Enter|Exit|Down|Up|Over|Drag)\s*\(", RegexOptions.CultureInvariant);
            foreach (var relative in HitProxyFiles)
            {
                var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsTrue(File.Exists(path), "missing " + relative);
                Assert.IsFalse(
                    ban.IsMatch(File.ReadAllText(path)),
                    relative + " 禁止 OnMouse*（须走 PointerHitRouter）");
            }
        }

        [Test]
        public void EnsureHitProxies_Source_DoesNotWriteSlotColliderSizeOrOffset()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
            var view = File.ReadAllText(Path.Combine(root, "Cards", "GroundFieldView.cs"));
            var ensure = Regex.Match(
                view,
                @"public void EnsureHitProxies\(\)\s*\{[\s\S]*?\n        public void RefreshSlotHit");
            Assert.IsTrue(ensure.Success, "找不到 EnsureHitProxies");
            Assert.IsFalse(
                Regex.IsMatch(ensure.Value, @"\.size\s*="),
                "EnsureHitProxies 不得写格位命中框 size");
            Assert.IsFalse(
                Regex.IsMatch(ensure.Value, @"\.offset\s*="),
                "EnsureHitProxies 不得写格位命中框 offset");
            Assert.IsFalse(
                ensure.Value.Contains("slotHitBoxSize"),
                "EnsureHitProxies 不得再用 slotHitBoxSize 覆盖场景权威");

            var surface = File.ReadAllText(Path.Combine(root, "Cards", "GroundFieldHitSurface.cs"));
            Assert.IsFalse(
                Regex.IsMatch(surface, @"\.size\s*="),
                "GroundFieldHitSurface 不得写 collider size");
            Assert.IsFalse(
                Regex.IsMatch(surface, @"\.offset\s*="),
                "GroundFieldHitSurface 不得写 collider offset");
        }

        [Test]
        public void PointerSeam_AndMitigationMarker_Exist()
        {
            Assert.IsNotNull(typeof(NineGrid.Flow.WorldPointerUtility));
            Assert.IsNotNull(typeof(NineGrid.Flow.PointerHitRouter));
            Assert.IsNotNull(typeof(NineGrid.Flow.IPointerHitTarget));
            Assert.IsNotNull(typeof(NineGrid.Cards.GroundFieldHitSurface));
            Assert.AreEqual("0006", WindowsHighPollingMouseMitigationInfo.AdrId);
        }

        [Test]
        public void RightClickInspect_ResolvesFieldCardViaClaimNotColliderDriver()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
            var router = File.ReadAllText(Path.Combine(root, "Flow", "PointerHitRouter.cs"));
            var surface = File.ReadAllText(Path.Combine(root, "Cards", "GroundFieldHitSurface.cs"));

            Assert.IsTrue(
                router.Contains("GroundFieldHitSurface")
                && router.Contains("TryResolveInspectCard"),
                "ADR-0023 后场卡无自带 collider：右键详述须经 GroundFieldHitSurface.TryResolveInspectCard");
            Assert.IsTrue(
                surface.Contains("TryResolveInspectCard")
                && surface.Contains("GroundCardHitProxy"),
                "场地面详述只认 GroundCardHitProxy 认领者，不误开房间图标");
        }

        [Test]
        public void ShuffleIntoNewCard_UsesAddAnchorsPath_NotOriginShortcut()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "BattleSession",
                "BoardPresentationPlayer.cs"));
            Assert.IsTrue(File.Exists(path), "missing BoardPresentationPlayer.cs");
            var text = File.ReadAllText(path);

            // PresentOneShuffleIntoDeckAsync 的 NewCard 段须显式 originAnchor: null，
            // 才能走 CardDeckAddAnchors；禁止把死亡格/卡组左槽当 origin 直插。
            var method = Regex.Match(
                text,
                @"private async UniTask PresentOneShuffleIntoDeckAsync\([\s\S]*?\n        private bool TryResolveShuffleIntoOrigin",
                RegexOptions.CultureInvariant);
            Assert.IsTrue(method.Success, "找不到 PresentOneShuffleIntoDeckAsync");
            Assert.IsTrue(
                method.Value.Contains("originAnchor: null")
                || method.Value.Contains("originAnchor : null"),
                "NewCard 洗入须经 CardDeckAddAnchors（originAnchor: null）");
            Assert.IsFalse(
                Regex.IsMatch(
                    method.Value,
                    @"AddCardAtFromOriginAsync\s*\(\s*[^)]*TryResolveShuffleIntoOrigin"),
                "NewCard 洗入不得把 ShuffleIntoOrigin 传给 AddCardAtFromOrigin");
            Assert.IsTrue(
                method.Value.Contains("WaitReturnSettledAsync"),
                "ExistingCard 回库须 await WaitReturnSettledAsync，禁止 fire-and-forget（传送后同 uid Deal 会卡死主线）");
        }
    }
}
