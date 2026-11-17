# Monster debug arena and weapon actions (#68)

Open menu **Nomad > Monsters > Open Monster Debug** or scene `Assets/Nomad/Monsters/MonsterDebug.unity`. This is a separate flat arena, not a generated terrain replacement. It opens in Play Mode with three normal types, Golem and Dragon; the player is combat-invincible by default. F2 switches overview/follow camera. WASD / left click / Shift are unchanged. Right panel edits properties, selects weapon style and spawns/removes actors; left panel shows logs. Bottom-left resets the five-monster lineup. Turn off invincibility to test damage. Detection is a faint circle/disc by default: cyan normal, purple elite, gold boss. Existing detection/travel toggles remain.

## Three normal types

| Type | Model / weapon | Behavior |
| --- | --- | --- |
| Swordsman | Skeleton_Warrior / Skeleton_Blade | Melee only. Raises the blade, swings through during final windup, then recovers. |
| Crossbowman | Skeleton_Rogue / Skeleton_Crossbow + Arrow | Raises both arms to aim, fires from the weapon muzzle at the locked position, then recoils. Close-range fallback uses existing melee. |
| Mage | Skeleton_Mage / Skeleton_Staff | Raises staff/casting arm during windup, releases magic bolt from weapon muzzle and recovers. |

The main integrated population remains six normal + one elite + one boss. Normals cycle through the three styles so all appear (two each). Minion source/prefab remains archived but is not in the active three-style catalog. The test panel has Mix/Sword/Crossbow/Staff. Sword ignores the ranged toggle by design. Damage timing, projectile collision/evasion, HP and visual-only equipment death drops remain; no equipment pickup/drop table work is added.

Weapon actions are procedural bone/weapon overlays synchronized to the real attack phase, rather than fabricated imported combat clips. Original KayKit FREE pack only supplies General/MovementBasic animations. Full authored grip/reload/cast/slash clips can replace these later.

## Motion correction

Golem: gait follows movement speed, motion blends exponentially, lower legs flex during lift, neutral arms blend down, attack pose blends and corpse bob settles.

Dragon: derived Generic clips enforce quaternion continuity. DragonLegMotion overrides only leg joints with small-range diagonal quadruped gait, blends at velocity changes and returns legs to rest on death. This deliberately bypasses the authored leg rotation tracks responsible for flips; head, wings, spine and tail retain original animation. It is not full ground-foot IK or a new authored animation pack. Automated tests check limited leg angle and frame-to-frame rotation in walking/death.

## Derived low-poly dragon

Original DAE/PNG remain unchanged in the private repository. Editor vertex clustering preserves dominant-bone groups, skin weights, bindposes and UV channels in a derived mesh. UVs within a cluster are averaged (minor texture/seam changes are expected). Degenerate triangles are removed, and split face vertices produce flat normals. Original1262 -> derived804 triangles (36.3% fewer). Flat shading can have more vertices than a smooth mesh, so triangle count is the reduction metric. The derived128px point-filtered palette texture reduces color detail without changing the source image. Private output: Candidates/DragonLowPoly.asset and DragonPalette.png. Original rig remains32 bindposes.

Rebuild using `Nomad > Gameplay > Apply Three Monster Ranks`; this also creates/saves MonsterDebug. Code is public, imported/derived third-party content remains private. #66 terrain/Kingdom/Resources and Inventory/Loot/shared Health are unchanged.

## Review

Automated verification covers three styles, real weapon arm motion during actual attacks, weapon-muzzle projectile release, debug lineup/coverage, no-collider faint ranges, reduced skinned mesh/flat normals and bounded Dragon legs. Integration regression covers combat/farming/retry. Human review is still needed for limb/weapon grip aesthetics, low-poly silhouette/colors and gameplay balance. Dedicated large-boss arena navigation, full foot IK, authored reload/cast/throw motions and final audio/VFX remain later art work, not blockers to the current debug prototype.

Final pose QA: actual rendered crossbow still pointed down after the first angle overlay, so arms now use shoulder/elbow/hand goal solving to put the crossbow at chest level and orient it toward the player. Sword hand follows a raised-to-front slash arc; staff has separate cast-hand targets. New IK pose was rechecked with relevant6/6 tests after the prior complete59/59 run, plus actual overview and three windup renders. Foot control remains bounded procedural gait, not terrain IK.
