# Generated-map monster sandbox and distance tactics (#71)

Open **Nomad > Monsters > Open Monster Debug** after stopping Play and allowing compilation/import. MonsterDebug now clones the existing TerrainIntegration map setup and uses the same perspective TopDownCamera (player offset0/12/-8). It starts with no monsters. The older flat five-monster scene is retained as MonsterAnimationLab for reproducible motion/collision tests, not the default debugging view.

## Debug panel

- Spawn: choose Sword/Bow/Mage/Golem/Dragon, Place on map then click a clear walkable floor (Esc cancels), or spawn5m ahead. Select an actor to inspect action/cooldown, remove it, or remove all. Max12. The panel blocks shooting, including the placement click.
- Tune: Move/HP, Basic and Skills pages. Large-monster Skills subdivides Charge/AoE and Projectile, so projectile skills are not mixed into the basic attack form. Edits apply to the next spawn or Apply+reset selected; reset restores HP and cooldowns. A normal reset keeps the chosen weapon style.
- Display: detection, target/home leash, skill telegraphs, monster HP, hit flash, numbers and shards. Player invincibility/HP restore/pause are always accessible. Action log is collapsed by default. Boss HUD avoids the side panel and expanded log.
- R new field / T same seed. Generation clears debug monsters, their skill pulses/death markers and flying projectiles before replacing NavMesh. No automatic enemy population is injected in this scene. Existing integrated gameplay still has its normal6+elite1+boss1 population.

## Temporary combat defaults

| Type | Move | Basic reach / cone | Basic windup / recovery | Gap closer | Area |
| --- | --- | --- | --- | --- | --- |
| Normal |3.8m/s|1.8m /360deg|0.35/0.35s|none|none|
| Golem |4.5m/s|2.4m /140deg|0.35/0.35s|4.5–12m,12m/s,5s cooldown|3.8m radius,5s cooldown,40damage|
| Dragon |3.8m/s|4.2m /120deg|0.35/0.35s|7–16m,16m/s,6s cooldown|5.5m radius,7s cooldown,45damage|

Windup/recovery are divided by AttackSpeed: normal/golem1.25, dragon1.15. Golem/dragon acceleration28m/s² and turning540deg/s improve response. Normals retain sword/crossbow/staff roles; normal projectile windup0.7/recovery0.65s. Golem rock0.8/0.65s, dragon fireball0.85/0.8s. Skill windup golem0.8/dragon0.9s; recovery0.45s. All are test tuning, not final balance.

## Selection and execution

Visible tracked player -> face target -> choose available action from distance/cooldowns:
- Far and charge ready: fixed-direction charge.
- Inside area radius and area ready: area when outside basic reach, or every third attack opportunity at close range.
- Otherwise ranged projectile when outside melee reach, or close basic / existing heavy cycle. If nothing is in reach, continue tracking.

Charge and area cooldowns start when the tell begins, so cancellation still consumes cooldown. Near basic attacks have no floor warning. Area/heavy have circle/sector warning; charge has a corridor matching its planned length and1.4m width. Warnings fill before release; aim/direction do not home after windup. Attack timing, movement pause and resumption remain owned by MonsterMeleeAttack, avoiding competing state machines.

Charge uses NavMesh raycast plus a capsule sweep, stops at walls/blocked terrain/nav edges or the first collider, and damages the player at most once. Allies block it without friendly damage. It does not teleport through terrain or force traversal of steps. Already-fired bolts retain range/lifetime rules. Area affects the bound player once only if inside radius and visible at release; walls, leaving radius, combat immunity and death apply. A short expanding ring is cosmetic and adds no extra damage. Death/disable stops pending/active skills. Movement and cast animations sample the actual skill phase; dragon attack playback accelerates to fit windup. Golem has alternating forward punches, two-arm slam and faster gait/forward charge lean. No new external animation assets are claimed.

## Verification and limits

PlayMode covers actual charge travel/hit-once/dodge/wall stop/death cancel/cooldown, area hit-once/dodge/cover, prior weapons/skin/HP/drop regressions. MonsterMapDebugVerification covers generated map, same play camera, five chosen types, display toggles, UI blocking and generation cleanup. GameplayIntegrationVerification retains real combat/loot/retry checks. Final paths/commits are on Issue71.

Existing Health/Projectile/Player/Inventory/Loot contracts, original TerrainIntegration scene and third-party assets are unchanged. Equipment pickup/drop tables remain another worker's scope. Hand/gait motion is procedural, not a final authored animation/foot IK system. Large visuals still use existing navigation geometry; dedicated boss arenas are separate work. Human review is required for readability, movement feel and difficulty; automated checks do not establish art/gameplay acceptance or Windows player-build performance.
