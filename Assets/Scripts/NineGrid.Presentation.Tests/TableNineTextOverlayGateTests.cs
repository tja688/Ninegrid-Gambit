using NUnit.Framework;
using NineGrid.Presentation.Shell;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class TableNineTextOverlayGateTests
    {
        [Test]
        public void ApplyForScreen_OnlyOneWindowActive_PerScreen()
        {
            var root = new GameObject("overlay-root");
            var inGame = new GameObject("in-game");
            var room = new GameObject("room");
            var notice = new GameObject("notice");
            inGame.transform.SetParent(root.transform);
            room.transform.SetParent(root.transform);
            notice.transform.SetParent(root.transform);

            var gate = root.AddComponent<TableNineTextOverlayGate>();
            SetPrivateField(gate, "inGameInfoRoot", inGame);
            SetPrivateField(gate, "roomInfoRoot", room);
            SetPrivateField(gate, "noticeRoot", notice);

            gate.ApplyForScreen(MainFlowScreen.NodePlaying);
            Assert.IsTrue(inGame.activeSelf);
            Assert.IsFalse(room.activeSelf);
            Assert.IsFalse(notice.activeSelf);

            gate.ShowRoomInfo(true);
            Assert.IsTrue(room.activeSelf);

            gate.ApplyForScreen(MainFlowScreen.Victory);
            Assert.IsFalse(inGame.activeSelf);
            Assert.IsFalse(room.activeSelf);
            Assert.IsTrue(notice.activeSelf);

            Object.DestroyImmediate(root);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }
    }
}
