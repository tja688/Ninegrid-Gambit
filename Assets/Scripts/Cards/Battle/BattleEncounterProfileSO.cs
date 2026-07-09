using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 战斗编排预设：配置场景 Timeline Rig 容器的语义变体参数。
    /// 不替代 CardEffectSO；单卡闪白/死亡曲线仍在卡上，本 SO 只描述玩家×怪物起舞 timing 与绑参。
    /// </summary>
    [CreateAssetMenu(
        fileName = "Battle_Encounter_Profile",
        menuName = "NineGrid/Cards/Battle/Encounter Profile")]
    public sealed class BattleEncounterProfileSO : ScriptableObject
    {
        public const string DefaultRigFamily = "CardAttackBasic";
        public const string WildcardContentId = BattleParticipantIds.Wildcard;

        public const float MonsterVictimKnockbackMin = 1f;
        public const float MonsterVictimKnockbackMax = 3f;
        public const float PlayerVictimKnockbackMin = 0.3f;
        public const float PlayerVictimKnockbackMax = 1f;
        public const float DefaultMonsterVictimKnockback = 1f;
        public const float DefaultPlayerVictimKnockback = 0.5f;

        [Header("Identity")]
        [Tooltip("预设唯一标识；留空时使用资产名。AI 路由与单测以此为准。")]
        [SerializeField] private string profileId;

        [Tooltip("Inspector / 编排工具显示名。")]
        [SerializeField] private string displayName;

        [TextArea(3, 8)]
        [Tooltip("精炼语义说明：适用场景、与默认手感差异、禁止事项。供 AI 与策划阅读。")]
        [SerializeField] private string description;

        [Tooltip("本预设适用的战斗意图。Catalog 路由时必须与请求 Intent 一致。")]
        [SerializeField] private BattleIntent intent = BattleIntent.Attack;

        [Tooltip("玩家内容 ID（ManagedCard.DefId）。通配填 *。")]
        [SerializeField] private string playerContentId = WildcardContentId;

        [Tooltip("怪物内容 ID（ManagedCard.DefId）。通配填 *。熊怪特殊手感在此填具体 DefId。")]
        [SerializeField] private string monsterContentId = WildcardContentId;

        [Header("Container")]
        [Tooltip("场景 Rig 族名。当前仅支持 CardAttackBasic（四向 Timeline 容器）。")]
        [SerializeField] private string rigFamily = DefaultRigFamily;

        [Tooltip("是否按受击者几何重算攻击者位移（反击默认开启）。")]
        [SerializeField] private bool useRelativeAttackerMotion;

        [Tooltip("是否按攻击者几何重算受击击退（反击默认开启）。")]
        [SerializeField] private bool useRelativeVictimKnockback;

        [Header("Locked Motion")]
        [Tooltip("受击击退系数。怪物受击锁 [1,3]；玩家受击（反击）锁 [0.3,1]。越界会在 OnValidate/ToBindParams 被打回。")]
        [SerializeField] private float victimKnockbackCoefficient = DefaultMonsterVictimKnockback;

        [Header("Timeline Callbacks")]
        [Tooltip("是否在 Timeline 绑死亡回调到受击卡 CardEffectManager。击杀变体应为 true。")]
        [SerializeField] private bool bindDeathCallback;

        [Tooltip("闪白时机：Heuristic 用场景烘焙 delay；Explicit 用下方显式 delay 覆盖回调组件。")]
        [SerializeField] private BattleHitFlashTimingPolicy hitFlashTimingPolicy = BattleHitFlashTimingPolicy.Heuristic;

        [Tooltip("显式闪白回调 delay（秒）。仅 HitFlashTimingPolicy=Explicit 时生效。")]
        [SerializeField] private float hitFlashCallbackDelay;

        [Tooltip("显式死亡回调 delay（秒）。仅 HitFlashTimingPolicy=Explicit 且 bindDeathCallback 时生效；0 表示不改写。")]
        [SerializeField] private float deathCallbackDelay;

        [Header("Teardown")]
        [Tooltip("播完后是否把攻击者拉回格位锚点。")]
        [SerializeField] private bool restoreAttackerToSlot = true;

        [Tooltip("播完后是否把受击者拉回格位锚点。击杀变体通常为 false（随后会 Vacate/Release）。")]
        [SerializeField] private bool restoreVictimToSlot = true;

        [Tooltip("是否强制终态 Guard（Kill tween + 硬对齐）。建议始终开启。")]
        [SerializeField] private bool requireFinalStateGuard = true;

        public string ProfileId => string.IsNullOrWhiteSpace(profileId) ? name : profileId;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        public string Description => description ?? string.Empty;

        public BattleIntent Intent => intent;

        public string PlayerContentId => BattleParticipantIds.Normalize(playerContentId);

        public string MonsterContentId => BattleParticipantIds.Normalize(monsterContentId);

        public string RigFamily => string.IsNullOrWhiteSpace(rigFamily) ? DefaultRigFamily : rigFamily.Trim();

        public bool UseRelativeAttackerMotion => useRelativeAttackerMotion;

        public bool UseRelativeVictimKnockback => useRelativeVictimKnockback;

        public float VictimKnockbackCoefficient => victimKnockbackCoefficient;

        public bool BindDeathCallback => bindDeathCallback;

        public BattleHitFlashTimingPolicy HitFlashTimingPolicy => hitFlashTimingPolicy;

        public float HitFlashCallbackDelay => Mathf.Max(0f, hitFlashCallbackDelay);

        public float DeathCallbackDelay => Mathf.Max(0f, deathCallbackDelay);

        public bool RestoreAttackerToSlot => restoreAttackerToSlot;

        public bool RestoreVictimToSlot => restoreVictimToSlot;

        public bool RequireFinalStateGuard => requireFinalStateGuard;

        public bool HasDescription => !string.IsNullOrWhiteSpace(description);

        public BattleBindParams ToBindParams()
        {
            ClampLockedFields();
            return new BattleBindParams(
                intent,
                ProfileId,
                RigFamily,
                useRelativeAttackerMotion,
                useRelativeVictimKnockback,
                victimKnockbackCoefficient,
                bindDeathCallback,
                hitFlashTimingPolicy,
                HitFlashCallbackDelay,
                DeathCallbackDelay,
                restoreAttackerToSlot,
                restoreVictimToSlot,
                requireFinalStateGuard);
        }

        /// <summary>
        /// 按 Intent 选择击退锁区间并 clamp。反击意图用玩家受击区间，进攻意图用怪物受击区间。
        /// </summary>
        public void ClampLockedFields()
        {
            GetKnockbackClampRange(intent, out var min, out var max);
            victimKnockbackCoefficient = Mathf.Clamp(victimKnockbackCoefficient, min, max);
            hitFlashCallbackDelay = Mathf.Max(0f, hitFlashCallbackDelay);
            deathCallbackDelay = Mathf.Max(0f, deathCallbackDelay);
            playerContentId = BattleParticipantIds.Normalize(playerContentId);
            monsterContentId = BattleParticipantIds.Normalize(monsterContentId);
            if (string.IsNullOrWhiteSpace(rigFamily))
            {
                rigFamily = DefaultRigFamily;
            }
        }

        public static void GetKnockbackClampRange(BattleIntent intent, out float min, out float max)
        {
            if (BattleIntentUtility.IsCounter(intent))
            {
                min = PlayerVictimKnockbackMin;
                max = PlayerVictimKnockbackMax;
                return;
            }

            min = MonsterVictimKnockbackMin;
            max = MonsterVictimKnockbackMax;
        }

        public static float ClampKnockback(BattleIntent intent, float value)
        {
            GetKnockbackClampRange(intent, out var min, out var max);
            return Mathf.Clamp(value, min, max);
        }

        /// <summary>
        /// 按意图填充合理默认绑参开关（创建默认资产 / 编辑器工具用）。
        /// </summary>
        public void ApplyIntentDefaults(BattleIntent targetIntent)
        {
            intent = targetIntent;
            var counter = BattleIntentUtility.IsCounter(targetIntent);
            var lethal = BattleIntentUtility.IsLethal(targetIntent);
            useRelativeAttackerMotion = counter;
            useRelativeVictimKnockback = counter;
            bindDeathCallback = lethal;
            restoreAttackerToSlot = true;
            restoreVictimToSlot = !lethal;
            requireFinalStateGuard = true;
            victimKnockbackCoefficient = counter
                ? DefaultPlayerVictimKnockback
                : DefaultMonsterVictimKnockback;
            hitFlashTimingPolicy = BattleHitFlashTimingPolicy.Heuristic;
            playerContentId = WildcardContentId;
            monsterContentId = WildcardContentId;
            rigFamily = DefaultRigFamily;
            ClampLockedFields();
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                profileId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }

            ClampLockedFields();
        }

#if UNITY_EDITOR
        public void EditorSetIdentity(string id, string nameForDisplay, string desc)
        {
            profileId = id;
            displayName = nameForDisplay;
            description = desc;
        }

        public void EditorSetParticipants(string playerId, string monsterId)
        {
            playerContentId = BattleParticipantIds.Normalize(playerId);
            monsterContentId = BattleParticipantIds.Normalize(monsterId);
        }

        public void EditorSetKnockback(float value)
        {
            victimKnockbackCoefficient = value;
            ClampLockedFields();
        }
#endif
    }
}
