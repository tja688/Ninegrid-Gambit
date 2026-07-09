using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineGrid.Cards.Tests
{
    public sealed class BattlePresentationRouterTests
    {
        [Test]
        public void ClampKnockback_MonsterIntent_LocksBetween1And3()
        {
            Assert.AreEqual(1f, BattleEncounterProfileSO.ClampKnockback(BattleIntent.Attack, 0.2f));
            Assert.AreEqual(3f, BattleEncounterProfileSO.ClampKnockback(BattleIntent.AttackLethal, 9f));
            Assert.AreEqual(2f, BattleEncounterProfileSO.ClampKnockback(BattleIntent.Attack, 2f));
        }

        [Test]
        public void ClampKnockback_CounterIntent_LocksBetween03And1()
        {
            Assert.AreEqual(0.3f, BattleEncounterProfileSO.ClampKnockback(BattleIntent.CounterAttack, 0.01f));
            Assert.AreEqual(1f, BattleEncounterProfileSO.ClampKnockback(BattleIntent.CounterAttackLethal, 2f));
            Assert.AreEqual(0.5f, BattleEncounterProfileSO.ClampKnockback(BattleIntent.CounterAttack, 0.5f));
        }

        [Test]
        public void Profile_OnValidate_ClampsAndFillsIdentity()
        {
            var profile = ScriptableObject.CreateInstance<BattleEncounterProfileSO>();
            profile.name = "Battle_Clamp_Test";
            profile.ApplyIntentDefaults(BattleIntent.Attack);
            profile.EditorSetKnockback(99f);
            profile.EditorSetIdentity(string.Empty, string.Empty, "desc");

            var so = new SerializedObjectProxy(profile);
            so.InvokeOnValidate();

            Assert.AreEqual(3f, profile.VictimKnockbackCoefficient);
            Assert.AreEqual("Battle_Clamp_Test", profile.ProfileId);
            Assert.IsTrue(profile.HasDescription);

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void Router_Priority_ExactOverWildcards()
        {
            var exact = CreateProfile(BattleIntent.Attack, "player_a", "bear", "exact");
            var playerWild = CreateProfile(BattleIntent.Attack, "player_a", "*", "playerWild");
            var monsterWild = CreateProfile(BattleIntent.Attack, "*", "bear", "monsterWild");
            var bothWild = CreateProfile(BattleIntent.Attack, "*", "*", "bothWild");

            var catalog = ScriptableObject.CreateInstance<BattleEncounterCatalogSO>();
            catalog.EditorSetEntries(new System.Collections.Generic.List<BattleEncounterCatalogSO.Entry>
            {
                Entry(BattleIntent.Attack, "*", "*", bothWild),
                Entry(BattleIntent.Attack, "*", "bear", monsterWild),
                Entry(BattleIntent.Attack, "player_a", "*", playerWild),
                Entry(BattleIntent.Attack, "player_a", "bear", exact),
            });

            Assert.IsTrue(BattlePresentationRouter.TryResolve(
                catalog, BattleIntent.Attack, "player_a", "bear", out var hit, out var kind));
            Assert.AreEqual(BattlePresentationRouter.MatchKind.Exact, kind);
            Assert.AreEqual("exact", hit.ProfileId);

            Assert.IsTrue(BattlePresentationRouter.TryResolve(
                catalog, BattleIntent.Attack, "player_a", "slime", out hit, out kind));
            Assert.AreEqual(BattlePresentationRouter.MatchKind.PlayerWildcard, kind);
            Assert.AreEqual("playerWild", hit.ProfileId);

            Assert.IsTrue(BattlePresentationRouter.TryResolve(
                catalog, BattleIntent.Attack, "other", "bear", out hit, out kind));
            Assert.AreEqual(BattlePresentationRouter.MatchKind.MonsterWildcard, kind);
            Assert.AreEqual("monsterWild", hit.ProfileId);

            Assert.IsTrue(BattlePresentationRouter.TryResolve(
                catalog, BattleIntent.Attack, "other", "slime", out hit, out kind));
            Assert.AreEqual(BattlePresentationRouter.MatchKind.BothWildcard, kind);
            Assert.AreEqual("bothWild", hit.ProfileId);

            DestroyAll(exact, playerWild, monsterWild, bothWild, catalog);
        }

        [Test]
        public void Router_MissingCatalog_ReturnsSafeFallbackBindParams()
        {
            LogAssert.Expect(
                LogType.Error,
                "[BattlePresentationRouter] 未命中 Catalog：intent=AttackLethal, player=p, monster=m。使用 SafeFallback 绑参。");

            var bind = BattlePresentationRouter.ResolveBindParams(
                null,
                BattleIntent.AttackLethal,
                "p",
                "m",
                out var profile,
                out var kind);

            Assert.IsNull(profile);
            Assert.AreEqual(BattlePresentationRouter.MatchKind.SafeFallback, kind);
            Assert.AreEqual("SafeFallback", bind.ProfileId);
            Assert.IsTrue(bind.BindDeathCallback);
            Assert.IsFalse(bind.RestoreVictimToSlot);
            Assert.IsTrue(bind.RequireFinalStateGuard);
            Assert.AreEqual(1f, bind.VictimKnockbackCoefficient);
        }

        [Test]
        public void IntentDefaults_LethalAndCounterFlags()
        {
            var attack = ScriptableObject.CreateInstance<BattleEncounterProfileSO>();
            attack.ApplyIntentDefaults(BattleIntent.Attack);
            Assert.IsFalse(attack.BindDeathCallback);
            Assert.IsTrue(attack.RestoreVictimToSlot);
            Assert.IsFalse(attack.UseRelativeAttackerMotion);

            var lethal = ScriptableObject.CreateInstance<BattleEncounterProfileSO>();
            lethal.ApplyIntentDefaults(BattleIntent.AttackLethal);
            Assert.IsTrue(lethal.BindDeathCallback);
            Assert.IsFalse(lethal.RestoreVictimToSlot);

            var counter = ScriptableObject.CreateInstance<BattleEncounterProfileSO>();
            counter.ApplyIntentDefaults(BattleIntent.CounterAttack);
            Assert.IsTrue(counter.UseRelativeAttackerMotion);
            Assert.IsTrue(counter.UseRelativeVictimKnockback);
            Assert.AreEqual(0.5f, counter.VictimKnockbackCoefficient);

            Object.DestroyImmediate(attack);
            Object.DestroyImmediate(lethal);
            Object.DestroyImmediate(counter);
        }

        [Test]
        public void FinalStateGuard_SnapImmediate_AlignsTransform()
        {
            var go = new GameObject("guard_test");
            go.transform.position = new Vector3(10f, 5f, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);

            var target = new Vector3(1f, 2f, 0f);
            BattleFinalStateGuard.SnapImmediate(go.transform, target);

            Assert.AreEqual(target.x, go.transform.position.x, 0.0001f);
            Assert.AreEqual(target.y, go.transform.position.y, 0.0001f);
            Assert.AreEqual(Quaternion.identity, go.transform.rotation);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Catalog_RequiresDescription_ForAiContract()
        {
            var catalog = ScriptableObject.CreateInstance<BattleEncounterCatalogSO>();
            catalog.EditorSetDescription("routing contract");
            Assert.IsTrue(catalog.HasDescription);
            Object.DestroyImmediate(catalog);
        }

        private static BattleEncounterProfileSO CreateProfile(
            BattleIntent intent,
            string playerId,
            string monsterId,
            string profileId)
        {
            var profile = ScriptableObject.CreateInstance<BattleEncounterProfileSO>();
            profile.ApplyIntentDefaults(intent);
            profile.EditorSetIdentity(profileId, profileId, $"test profile {profileId}");
            profile.EditorSetParticipants(playerId, monsterId);
            return profile;
        }

        private static BattleEncounterCatalogSO.Entry Entry(
            BattleIntent intent,
            string playerId,
            string monsterId,
            BattleEncounterProfileSO profile)
        {
            return new BattleEncounterCatalogSO.Entry
            {
                intent = intent,
                playerContentId = playerId,
                monsterContentId = monsterId,
                profile = profile,
            };
        }

        private static void DestroyAll(params Object[] objects)
        {
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    Object.DestroyImmediate(objects[i]);
                }
            }
        }

        /// <summary>
        /// EditMode 下触发 OnValidate：通过 SerializedObject Apply 间接触发不够稳，直接反射调用。
        /// </summary>
        private sealed class SerializedObjectProxy
        {
            private readonly BattleEncounterProfileSO _profile;

            public SerializedObjectProxy(BattleEncounterProfileSO profile)
            {
                _profile = profile;
            }

            public void InvokeOnValidate()
            {
                var method = typeof(BattleEncounterProfileSO).GetMethod(
                    "OnValidate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                method?.Invoke(_profile, null);
                _profile.ClampLockedFields();
            }
        }
    }
}
