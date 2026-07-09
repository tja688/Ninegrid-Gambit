namespace NineGrid.Cards
{
    /// <summary>
    /// 战斗 Rig 绑参 DTO。由 Profile / SafeFallback 产出，供 Adapter 执行。
    /// </summary>
    public readonly struct BattleBindParams
    {
        public BattleBindParams(
            BattleIntent intent,
            string profileId,
            string rigFamily,
            bool useRelativeAttackerMotion,
            bool useRelativeVictimKnockback,
            float victimKnockbackCoefficient,
            bool bindDeathCallback,
            BattleHitFlashTimingPolicy hitFlashTimingPolicy,
            float hitFlashCallbackDelay,
            float deathCallbackDelay,
            bool restoreAttackerToSlot,
            bool restoreVictimToSlot,
            bool requireFinalStateGuard)
        {
            Intent = intent;
            ProfileId = profileId ?? string.Empty;
            RigFamily = string.IsNullOrWhiteSpace(rigFamily) ? BattleEncounterProfileSO.DefaultRigFamily : rigFamily;
            UseRelativeAttackerMotion = useRelativeAttackerMotion;
            UseRelativeVictimKnockback = useRelativeVictimKnockback;
            VictimKnockbackCoefficient = victimKnockbackCoefficient;
            BindDeathCallback = bindDeathCallback;
            HitFlashTimingPolicy = hitFlashTimingPolicy;
            HitFlashCallbackDelay = hitFlashCallbackDelay;
            DeathCallbackDelay = deathCallbackDelay;
            RestoreAttackerToSlot = restoreAttackerToSlot;
            RestoreVictimToSlot = restoreVictimToSlot;
            RequireFinalStateGuard = requireFinalStateGuard;
        }

        public BattleIntent Intent { get; }

        public string ProfileId { get; }

        public string RigFamily { get; }

        public bool UseRelativeAttackerMotion { get; }

        public bool UseRelativeVictimKnockback { get; }

        public float VictimKnockbackCoefficient { get; }

        public bool BindDeathCallback { get; }

        public BattleHitFlashTimingPolicy HitFlashTimingPolicy { get; }

        public float HitFlashCallbackDelay { get; }

        public float DeathCallbackDelay { get; }

        public bool RestoreAttackerToSlot { get; }

        public bool RestoreVictimToSlot { get; }

        public bool RequireFinalStateGuard { get; }

        public bool IsLethal => BattleIntentUtility.IsLethal(Intent);

        public bool IsCounter => BattleIntentUtility.IsCounter(Intent);

        public static BattleBindParams CreateSafeFallback(BattleIntent intent)
        {
            var counter = BattleIntentUtility.IsCounter(intent);
            var lethal = BattleIntentUtility.IsLethal(intent);
            var knockback = counter
                ? BattleEncounterProfileSO.DefaultPlayerVictimKnockback
                : BattleEncounterProfileSO.DefaultMonsterVictimKnockback;

            return new BattleBindParams(
                intent,
                profileId: "SafeFallback",
                rigFamily: BattleEncounterProfileSO.DefaultRigFamily,
                useRelativeAttackerMotion: counter,
                useRelativeVictimKnockback: counter,
                victimKnockbackCoefficient: knockback,
                bindDeathCallback: lethal,
                hitFlashTimingPolicy: BattleHitFlashTimingPolicy.Heuristic,
                hitFlashCallbackDelay: 0f,
                deathCallbackDelay: 0f,
                restoreAttackerToSlot: true,
                restoreVictimToSlot: !lethal,
                requireFinalStateGuard: true);
        }
    }
}
