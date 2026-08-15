# Icon token diff
Changed: 10

## relic.armor_strip_knife (Relic)
- units: 19 -> 17
- before: `攻击时若目标当前[armor]大于{relic.armor_strip_knife.battle.value}，攻击伤害+{relic.armor_strip_knife.armor_hit.value}`
- after: `[attack]时若目标当前[armor]大于{relic.armor_strip_knife.battle.value}，[attack]伤害+{relic.armor_strip_knife.armor_hit.value}`

## relic.berserker_axe (Relic)
- units: 18 -> 15
- before: `攻击+{relic.berserker_axe.base.value}，血量低于一半时玩家攻击翻倍`
- after: `[attack]+{relic.berserker_axe.base.value}，[HP]低于一半时玩家[attack]翻倍`

## relic.beyond_dimension (Relic)
- units: 12 -> 11
- before: `攻击+{relic.beyond_dimension.base.value}，攻击后旋转{relic.beyond_dimension.battle.count}次`
- after: `[attack]+{relic.beyond_dimension.base.value}，攻击后旋转{relic.beyond_dimension.battle.count}次`

## relic.blood_burst (Relic)
- units: 18 -> 15
- before: `血量上限+{relic.blood_burst.max_hp.value}，受伤时对随机怪物：反伤`
- after: `[MHP]+{relic.blood_burst.max_hp.value}，受伤时对随机怪物：反伤`

## relic.blood_cycle (Relic)
- units: 18 -> 14
- before: `血量上限+{relic.blood_cycle.max_hp.value}，受到伤害时恢复{relic.blood_cycle.taken.amount}点血量`
- after: `[MHP]+{relic.blood_cycle.max_hp.value}，受到伤害时恢复{relic.blood_cycle.taken.amount}点[HP]`

## relic.rotten_cleave_axe (Relic)
- units: 22 -> 21
- before: `[attack]+{relic.rotten_cleave_axe.base.value}，普通攻击改为对目标[adjacent]范围造成{relic.rotten_cleave_axe.cleave.amount}点伤害`
- after: `[attack]+{relic.rotten_cleave_axe.base.value}，[[普通攻击]]改为对目标[adjacent]范围造成{relic.rotten_cleave_axe.cleave.amount}点伤害`

## trap.healing_spring (Trap)
- units: 18 -> 17
- before: `每移动{trap.healing_spring.heal_on_move.every}次，若玩家在[adjacent]则恢复{trap.healing_spring.heal_on_move.amount}点血量`
- after: `每移动{trap.healing_spring.heal_on_move.every}次，若玩家在[adjacent]则恢复{trap.healing_spring.heal_on_move.amount}点[HP]`

## trap.recovery_totem (Trap)
- units: 20 -> 19
- before: `每移动{trap.recovery_totem.heal_on_move.every}次，[adjacent]的怪物和玩家各恢复{trap.recovery_totem.heal_on_move.amount}点血量`
- after: `每移动{trap.recovery_totem.heal_on_move.every}次，[adjacent]的怪物和玩家各恢复{trap.recovery_totem.heal_on_move.amount}点[HP]`

## trap.tutorial.countdown (Trap)
- units: 9 -> 6
- before: `怪物卡角有行动计数`
- after: `怪物卡角有[action]`

## trap.tutorial.retreat (Trap)
- units: 9 -> 8
- before: `血量吃紧就早点破门`
- after: `[HP]吃紧就早点破门`

