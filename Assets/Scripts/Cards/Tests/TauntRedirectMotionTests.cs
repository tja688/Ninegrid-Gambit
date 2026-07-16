using System.Reflection;
using NUnit.Framework;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    /// <summary>
    /// 嘲讽重定向相对位移：蓄力远离点选目标、冲刺朝向嘲讽目标。
    /// </summary>
    public sealed class TauntRedirectMotionTests
    {
        [Test]
        public void BindParticipants_AttackIntent_KnockbackUsesRelativeOffsetOnEffectFrame()
        {
            var rigObject = new GameObject("rig_attack_knockback_test");
            var rig = rigObject.AddComponent<CardAttackBasicDirectionRig>();

            var attacker = new GameObject("attacker").transform;
            attacker.position = Vector3.zero;
            var victim = new GameObject("victim").transform;
            victim.position = new Vector3(-1f, 0f, 0f);
            victim.gameObject.AddComponent<CardTransformTower>().EnsureTower();

            try
            {
                SeedAttackerAnimations(rigObject, attacker.gameObject);

                var bind = BattleBindParams.CreateSafeFallback(BattleIntent.Attack);
                Assert.IsFalse(bind.UseRelativeVictimKnockback, "Attack 预设仍标记为绝对击退，但 L3 必须几何重绑");

                rig.BindParticipants(
                    attacker,
                    victim,
                    victimEffects: null,
                    bindDeathCallback: false,
                    relativeAttackerMotion: bind.UseRelativeAttackerMotion,
                    relativeVictimKnockback: bind.UseRelativeVictimKnockback,
                    victimKnockbackCoefficient: bind.VictimKnockbackCoefficient);

                var tower = victim.GetComponent<CardTransformTower>();
                Assert.IsNotNull(tower?.EffectFrame);
                var motionTarget = tower.EffectFrame;
                var knockbackLocalEnd = ReadFirstLocalEnd(rigObject, delayMin: 0.35f, delayMax: 0.55f);
                var knockbackEnd = motionTarget.parent != null
                    ? motionTarget.parent.TransformPoint(knockbackLocalEnd)
                    : knockbackLocalEnd;
                var homeWorld = motionTarget.position;
                var knockbackDistance = Vector3.Distance(knockbackEnd, homeWorld);
                Assert.Less(knockbackDistance, 2.5f, "击退距离应接近烘焙幅度，而非模板绝对 local 坐标");
                Assert.Greater(knockbackDistance, 0.05f);

                var knockbackDir = (knockbackEnd - homeWorld).normalized;
                var expectedKnockback = (victim.position - attacker.position).normalized;
                Assert.Greater(Vector3.Dot(knockbackDir, expectedKnockback), 0.9f);
            }
            finally
            {
                Object.DestroyImmediate(attacker.gameObject);
                Object.DestroyImmediate(victim.gameObject);
                var victimTemplate = GameObject.Find("victim_template");
                if (victimTemplate != null)
                {
                    Object.DestroyImmediate(victimTemplate);
                }

                Object.DestroyImmediate(rigObject);
            }
        }

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

                var knockbackEnd = ReadFirstWorldEnd(rigObject, lungeVictim, delayMin: 0.35f, delayMax: 0.55f);
                var knockbackDir = (knockbackEnd - lungeVictim.position).normalized;
                var expectedKnockback = (lungeVictim.position - attacker.position).normalized;
                Assert.Greater(Vector3.Dot(knockbackDir, expectedKnockback), 0.9f, "击退应沿嘲讽怪远离攻击者方向");
            }
            finally
            {
                Object.DestroyImmediate(attacker.gameObject);
                Object.DestroyImmediate(windupVictim.gameObject);
                Object.DestroyImmediate(lungeVictim.gameObject);
                var victimTemplate = GameObject.Find("victim_template");
                if (victimTemplate != null)
                {
                    Object.DestroyImmediate(victimTemplate);
                }

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

            var victimObject = new GameObject("victim_template").transform;
            victimObject.position = new Vector3(-1f, 0f, 0f);
            CreateMoveTween(rigObject, victimObject.gameObject, dotweenAnimationType, delay: 0.4f, endValue: new Vector3(2f, 0f, 0f));
            CreateMoveTween(rigObject, victimObject.gameObject, dotweenAnimationType, delay: 0.7f, endValue: Vector3.zero);
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

        private static Vector3 ReadFirstLocalEnd(
            GameObject rigObject,
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

                return (Vector3)(dotweenAnimationType.GetField(
                    "endValueV3",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(animation) ?? Vector3.zero);
            }

            Assert.Fail("No matching animation for delay window.");
            return Vector3.zero;
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
