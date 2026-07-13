using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    /// <summary>
    /// 嘲讽重定向相对位移：蓄力远离点选目标、冲刺朝向嘲讽目标。
    /// </summary>
    public sealed class TauntRedirectMotionTests
    {
        [Test]
        public void RebindAttackerMotionTauntRedirect_WindupAwayFromClicked_LungeTowardTaunt()
        {
            var rigObject = new GameObject("rig_taunt_motion_test");
            var rig = rigObject.AddComponent<CardAttackBasicDirectionRig>();

            var attacker = new GameObject("attacker").transform;
            attacker.position = Vector3.zero;
            var windupVictim = new GameObject("windup").transform;
            windupVictim.position = new Vector3(0f, 1f, 0f);
            var lungeVictim = new GameObject("lunge").transform;
            lungeVictim.position = new Vector3(-1f, 0f, 0f);

            try
            {
                SeedAttackerAnimations(rigObject, attacker.gameObject);

                var bind = BattleBindParams.CreateSafeFallback(BattleIntent.Attack);
                rig.BindParticipantsTauntRedirect(
                    attacker,
                    windupVictim,
                    lungeVictim,
                    lungeTargetEffects: null,
                    in bind,
                    onLungeBegin: null,
                    onCombatHit: null);

                var windupEnd = ReadFirstWorldEnd(rigObject, attacker, delayMax: 0.05f);
                var lungeEnd = ReadFirstWorldEnd(rigObject, attacker, delayMin: 0.05f, delayMax: 0.35f);

                var awayFromWindup = (windupEnd - attacker.position).normalized;
                var expectedAway = (-(windupVictim.position - attacker.position)).normalized;
                Assert.Greater(Vector3.Dot(awayFromWindup, expectedAway), 0.9f, "蓄力应远离点选目标");

                var towardTaunt = (lungeEnd - attacker.position).normalized;
                var expectedToward = ((lungeVictim.position - attacker.position)).normalized;
                Assert.Greater(Vector3.Dot(towardTaunt, expectedToward), 0.9f, "冲刺应朝向嘲讽目标");
            }
            finally
            {
                Object.DestroyImmediate(attacker.gameObject);
                Object.DestroyImmediate(windupVictim.gameObject);
                Object.DestroyImmediate(lungeVictim.gameObject);
                Object.DestroyImmediate(rigObject);
            }
        }

        private static void SeedAttackerAnimations(GameObject rigObject, GameObject attackerObject)
        {
            var dotweenAnimationType = ResolveType("DG.Tweening.DOTweenAnimation");
            if (dotweenAnimationType == null)
            {
                Assert.Inconclusive("DOTweenAnimation unavailable in EditMode.");
                return;
            }

            CreateMoveTween(rigObject, attackerObject, dotweenAnimationType, delay: 0f, endValue: new Vector3(0f, -0.1f, 0f));
            CreateMoveTween(rigObject, attackerObject, dotweenAnimationType, delay: 0.1f, endValue: new Vector3(1f, 0f, 0f));
            CreateMoveTween(rigObject, attackerObject, dotweenAnimationType, delay: 0.7f, endValue: Vector3.zero);
        }

        private static void CreateMoveTween(
            GameObject rigObject,
            GameObject target,
            System.Type dotweenAnimationType,
            float delay,
            Vector3 endValue)
        {
            var animation = rigObject.AddComponent(dotweenAnimationType);
            var endValueField = dotweenAnimationType.GetField(
                "endValueV3",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var delayField = dotweenAnimationType.GetField(
                "delay",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var targetGoField = dotweenAnimationType.GetField(
                "targetGO",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            endValueField?.SetValue(animation, endValue);
            delayField?.SetValue(animation, delay);
            targetGoField?.SetValue(animation, target);
        }

        private static Vector3 ReadFirstWorldEnd(
            GameObject rigObject,
            Transform attacker,
            float delayMax = float.MaxValue,
            float delayMin = float.MinValue)
        {
            var dotweenAnimationType = ResolveType("DG.Tweening.DOTweenAnimation");
            if (dotweenAnimationType == null)
            {
                return Vector3.zero;
            }

            var animations = rigObject.GetComponents(dotweenAnimationType);
            for (var i = 0; i < animations.Length; i++)
            {
                var animation = animations[i];
                var delay = (float)(dotweenAnimationType.GetField(
                    "delay",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(animation) ?? 0f);
                if (delay < delayMin || delay > delayMax)
                {
                    continue;
                }

                var localEnd = (Vector3)(dotweenAnimationType.GetField(
                    "endValueV3",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(animation) ?? Vector3.zero);
                return attacker.parent != null
                    ? attacker.parent.TransformPoint(localEnd)
                    : localEnd;
            }

            Assert.Fail("No matching animation for delay window.");
            return Vector3.zero;
        }

        private static System.Type ResolveType(string typeName)
        {
            var type = System.Type.GetType(typeName + ", DOTween");
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
