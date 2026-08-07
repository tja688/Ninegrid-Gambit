using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Flow
{
    /// <summary>
    /// 护栏：商店/卡店特色选项（道具牌格升级 / 三项服务 / 刷新）Instantiate 选项面后
    /// 必须按 defId 应用 JSON 主图标，不得恒显房间选项标准模板的默认图标。
    /// </summary>
    public sealed class RoomOptionFaceVisualsStructuralTests
    {
        private static readonly string[] PresenterRelPaths =
        {
            Path.Combine("Flow", "ShopBoard", "ShopBoardPresenter.cs"),
            Path.Combine("Flow", "TavernBoard", "TavernBoardPresenter.cs"),
        };

        [Test]
        public void Presenters_ApplyMainIcon_AfterOptionFaceInstantiate()
        {
            var root = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation"));

            for (var i = 0; i < PresenterRelPaths.Length; i++)
            {
                var path = Path.Combine(root, PresenterRelPaths[i]);
                Assert.IsTrue(File.Exists(path), "missing " + path);
                var text = File.ReadAllText(path);

                var instantiateIdx = text.IndexOf("UnityEngine.Object.Instantiate(prefab)", System.StringComparison.Ordinal);
                Assert.GreaterOrEqual(
                    instantiateIdx,
                    0,
                    "缺少 Instantiate(prefab): " + PresenterRelPaths[i]);
                var applyIdx = text.IndexOf(
                    "RoomOptionFaceVisuals.ApplyMainIcon(contentId, go)",
                    System.StringComparison.Ordinal);
                Assert.GreaterOrEqual(
                    applyIdx,
                    0,
                    "选项面 Instantiate 后须应用 JSON 主图标: " + PresenterRelPaths[i]);
                Assert.Greater(
                    applyIdx,
                    instantiateIdx,
                    "ApplyMainIcon 须在 Instantiate 之后: " + PresenterRelPaths[i]);
            }
        }
    }
}
