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

Original DAE/PNG remain unchanged in the private repository. After #69 visual feedback, the damaging vertex-cluster reduction and UV averaging were removed. The current derived mesh preserves every original triangle position, UV, bindpose and per-vertex skin weight exactly; only vertices are split per face for flat normals. Both original and current derivative have1262triangles. The original texture is used again; the old804-triangle asset contents and palette are no longer active. Correct topology/skin/UV continuity takes priority over reduction. Source rig remains32bindposes.

Rebuild using `Nomad > Gameplay > Apply Three Monster Ranks`; this also creates/saves MonsterDebug. Code is public, imported/derived third-party content remains private. #66 terrain/Kingdom/Resources and Inventory/Loot/shared Health are unchanged.

## Review

Automated verification covers three styles, real weapon arm motion during actual attacks, weapon-muzzle projectile release, debug lineup/coverage, no-collider faint ranges, reduced skinned mesh/flat normals and bounded Dragon legs. Integration regression covers combat/farming/retry. Human review is still needed for limb/weapon grip aesthetics, low-poly silhouette/colors and gameplay balance. Dedicated large-boss arena navigation, full foot IK, authored reload/cast/throw motions and final audio/VFX remain later art work, not blockers to the current debug prototype.

Final pose QA: actual rendered crossbow still pointed down after the first angle overlay, so arms now use shoulder/elbow/hand goal solving to put the crossbow at chest level and orient it toward the player. Sword hand follows a raised-to-front slash arc; staff has separate cast-hand targets. New IK pose was rechecked with relevant6/6 tests after the prior complete59/59 run, plus actual overview and three windup renders. Foot control remains bounded procedural gait, not terrain IK.

## Focused corrections (#69)

Golem hands now use world-space front targets and a shared two-bone solver instead of wrong local-axis shoulder offsets; idle, walk and windup must keep both hands in front of the shoulders. Debug normal types are arranged on the same front row and labelled SWORD/CROSSBOW/MAGE above HP and in the selection list. Integrated HUD shows each type count and regression verifies two of each. Normal melee, arrow and magic attacks have no ground warning; heavy attacks and elite/boss ranged skills keep warnings. Faint detection circles are independent and remain enabled/toggleable. Restart Play to rebuild actors after updates.

Reset lineup also restores the player start position before spawning; otherwise standing on a normal spawn point could reject that type in the capsule-clearance check. Regression explicitly occupies the crossbow slot before reset and requires all five actors to exist afterward.
