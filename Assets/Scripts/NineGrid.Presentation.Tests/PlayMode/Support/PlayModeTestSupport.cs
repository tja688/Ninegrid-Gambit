using System;
using System.Collections;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Bridge;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Presentation.Tests.Support
{
    internal static class PlayModeTestSupport
    {
        public const string MainScenePath = "Assets/Scenes/MainScene.unity";

        public static IEnumerator LoadMainScene()
        {
#if UNITY_EDITOR
            var parameters = new LoadSceneParameters(LoadSceneMode.Single);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(MainScenePath, parameters);
#else
            yield return SceneManager.LoadSceneAsync("MainScene", LoadSceneMode.Single);
#endif
        }

        public static IEnumerator WaitUntil(Func<bool> predicate, float timeoutSeconds, string label)
        {
            var elapsed = 0f;
            while (!predicate())
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= timeoutSeconds)
                {
                    throw new TimeoutException("Timed out waiting for: " + label);
                }

                yield return null;
            }
        }

        public static IEnumerator WaitForBootstrapReady(float timeoutSeconds = 10f)
        {
            yield return WaitUntil(() => NineGridSceneBootstrap.Current != null, timeoutSeconds, "NineGridSceneBootstrap");
            yield return null;
        }

        public static IEnumerator WaitForInputUnlock(CommandGateway gateway, float timeoutSeconds = 30f)
        {
            if (gateway == null)
            {
                throw new InvalidOperationException("CommandGateway is null.");
            }

            yield return WaitUntil(() => !gateway.IsInputLocked, timeoutSeconds, "input unlock");
        }

        public static SlotId FindFirstMonsterSlot(IArchitecture architecture)
        {
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var uid = board.GetCardUid(slot);
                if (uid != 0 && registry.Get(uid).Kind == CardKind.Monster)
                {
                    return slot;
                }
            }

            throw new InvalidOperationException("Could not find monster slot.");
        }

        public static SlotId FindFirstAdjacentMonsterSlot(IArchitecture architecture)
        {
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var boardSystem = architecture.GetSystem<IBoardSystem>();
            var avatarSlot = board.AvatarSlot.Value;

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var uid = board.GetCardUid(slot);
                if (uid == 0 || !boardSystem.AreAdjacent(avatarSlot, slot))
                {
                    continue;
                }

                if (registry.Get(uid).Kind == CardKind.Monster)
                {
                    return slot;
                }
            }

            return SlotId.None;
        }

        public static bool EventLogContains(IArchitecture architecture, CoreEventType eventType)
        {
            var log = architecture.GetSystem<IActionPipelineSystem>().EventLog;
            for (var i = 0; i < log.Entries.Count; i++)
            {
                if (log.Entries[i].Type == eventType)
                {
                    return true;
                }
            }

            return false;
        }

        public static SlotId FindFirstAdjacentEmptySlot(IArchitecture architecture)
        {
            var board = architecture.GetModel<BoardModel>();
            var boardSystem = architecture.GetSystem<IBoardSystem>();
            var avatarSlot = board.AvatarSlot.Value;

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == avatarSlot || board.GetCardUid(slot) != 0)
                {
                    continue;
                }

                if (boardSystem.AreAdjacent(avatarSlot, slot))
                {
                    return slot;
                }
            }

            return SlotId.None;
        }

        public static void CleanupPlayMode()
        {
            if (NineGridSceneBootstrap.Current != null)
            {
                var bootstrapObject = NineGridSceneBootstrap.Current.gameObject;
                UnityEngine.Object.Destroy(bootstrapObject);
            }

            NineGridArchitecture.ResetForTests();
        }
    }
}
