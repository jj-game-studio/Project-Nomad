# Large monsters and ranged combat (#65)

Human worker/reviewer Lemonade8080. Public PR59 / private Assets PR4.

## Current behavior

- Skeleton normal: staff + magic bolt for Mage, crossbow + arrow for other models. Existing KayKit CC0 weapons from the archived FREE pack; no new external download. HP100, ranged damage15, projectile speed9m/s, range12m, windup1.1s, recovery1.8s.
- Iron Golem elite: visual reference height3.5m (was2.5), overhead HP raised, body collision height3.2m. Procedural upper-arm/forearm/thigh movement provides breathing, walking, windup lift and death fall. It is not an authored animation clip or a retargeted motion pack. HP250, rock projectile damage25, speed7m/s, range12m, windup1.4s.
- Dragon boss: visual reference height5m (was2.5), existing Generic animations retained, body collision height3.8m. Wide gold/red top HUD with final-guardian title, numeric HP, four health quarters and delayed damage trail. Quarters are visual health marks, not implemented phase mechanics. HP1000, fireball damage30, range16m, speed9m/s, windup1.3s/recovery2.2s.

Values are temporary tuning. Root navigation still uses the existing humanoid NavMesh radius/baked navigation; large wings/tails are not damage colliders and may visually overlap narrow scenery. Dedicated large-boss arenas and navigation bake are separate content work.

## Shared attack sequence

Tracker detects visible player -> same MonsterMeleeAttack state machine chooses melee when close, ranged when outside melee reach and inside ranged reach -> face target -> lock aim and pause movement -> windup indicator fills -> release one MonsterBolt -> recovery -> resume tracking. Attack speed scales windup/recovery. Fixed aim permits sidestep evasion; no homing or immediate ranged damage. Range cone is the aiming preview, not an area damage hitbox.

MonsterRangedAttack owns configuration/launch, MonsterBolt uses the existing Projectile.TryGetFirstHit sweep. Terrain blocks the bolt; allied monsters block it without friendly damage. Owner is ignored, player Health handles dodge invulnerability. A bolt deals damage at most once and expires at max range. Shooter death cancels pending windup; already launched bolts continue flying. Generation cleanup and test-panel Remove all destroy bolts. Shared Projectile/Health/Inventory APIs are unchanged.

Test panel adds ranged enable, range, projectile speed, damage, windup and recovery. Apply/reset uses the selected rank. Existing no-pickup equipment marker/death drop remains unchanged; no drop table implemented.

## Asset use / replacement list for the user

Already available: KayKit Skeleton_Staff/Crossbow/Arrow (same CC0 pack/atlas); existing Synty rock mesh (private licensed source); procedural colored magic/fire sphere and trail. Synty original files are referenced, not copied to public repository or redistributed standalone.

Optional final art assets to supply later (implementation is usable without them):
1. Golem rig-compatible idle/walk/run, melee slam, rock throw and death animations; source FBX + skeleton/Avatar information. Humanoid animations require compatibility/retarget validation for the supplied Standout7 rig.
2. Dragon fireball/breath and impact VFX suitable for Unity6 URP, plus optional attack/cast clips for this exact dragon rig. Existing attack clip is reused now; no continuous breath attack or flight.
3. Mage cast and crossbow fire/reload clips compatible with KayKit Medium rig.
4. Arrow/rock/fireball impact audio, golem footsteps/throw and dragon roar/death sounds.

Keep download page URL and license with each source. No purchase, paid download or new license acceptance is performed. Placeholder VFX and procedural motion are clearly replaceable and are not claimed as final art.

## Review / verification

Open the same saved TerrainIntegration scene; normal6 + elite1 + boss1 remain. Restart Play after script/assets refresh to recreate actors using updated settings. The user editor remains open; tests run in an isolated ranged-validation copy. Evidence and accepted commit are recorded on Issue65. Human size, weapon grip, pose and gameplay acceptance and standalone build are separate from automated checks.

## Later three-style and debug update (#68)

Current normal lineup is Warrior sword / Rogue crossbow / Mage staff (two each in the integrated field), with actual procedural swing/aim/cast actions. Golem gait and Dragon bounded legs were refined; Dragon uses a reduced flat-shaded mesh and palette. See [monster-debug.md](monster-debug.md) for the dedicated debug arena, controls, exact triangle counts and limitations. Earlier all-normal ranged assignment above is superseded by the sword melee specialization.
