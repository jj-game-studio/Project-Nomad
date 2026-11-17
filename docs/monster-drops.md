# Three monster ranks and placeholder equipment drops

Refs #64; public PR #59 / private Assets PR #4. Human worker/reviewer: Lemonade8080.

## Play

Open `Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity` (menu `Nomad > Gameplay > Open Integrated Gameplay`). The generated field places normal6 + elite1 + boss1, all with real models. Spawn points remain seed-driven and at least12m from the player. R generates a new field, T replays the seed, F1 toggles diagnostics. Existing MonsterCombat / MonsterElite test scenes support Normal, Elite and Boss in their spawn panel, including per-rank properties.

- Normal: existing KayKit Skeleton variants, HP100.
- Elite: Standout7 Iron Golem, HP250. No authored animation is included; model stays in its supplied rig pose while existing movement/melee runs.
- Boss: Cethiel/Drummyfish Dragon, HP1000. Generic idle/walk/attack/death animation; existing elite melee cycle reused with temporary settings. No flight, breath, dedicated encounter phases or final balance.

All sizes and values are test settings. Navigation and damage use the existing humanoid capsule, not the entire visual outline. Boss HP appears at the screen top; other monster HP remains overhead. Human art/gameplay acceptance is pending. This test-role request supersedes #60's missing Skeleton Golem/Necromancer placement; it does not mean those EXTRA models were acquired.

## Death -> marker -> landing

`MonsterActorFactory.CreateRanked` adds `MonsterIdentity` and `MonsterDeathDrop`. `Health.Died` -> one `PlaceholderEquipmentDrop` ->0.65s arc -> static crate marker on ground. Repeated damage cannot duplicate it. Normal/Elite/Boss use cyan/purple/gold marker colors only; these are source-rank diagnostics, not equipment rarity.

No item definition, random roll, equipment stats, collider, interaction target, pickup, inventory write or equip logic is attached. E continues to operate real existing chests and cannot pick up a marker. The marker persists until field regeneration, Remove all in the spawn panel, or scene restart. Deleting a live monster does not count as a kill.

## Handoff to equipment owner

Do not change shared Health or Inventory APIs for this prototype. Current replacement point is `MonsterDeathDrop.OnDied`, immediately after its duplicate guard. `MonsterIdentity.Rank` supplies source rank. Future work must define item identity/count/rarity/probability, then replace the unconditional one-marker creation with drop-table results. `PlaceholderEquipmentDrop` is a visual placeholder, not an item container or promised API for pickup.

The parent supplied to `MonsterDeathDrop.Configure` owns lifetime (integrated generation root or test controls). Future dropped item objects should retain that generation lifetime rule. Decide whether equipment stays on scene changes only when the equipment/run rules are implemented.

Explicit follow-up: **drop tables are not configured**. Actual equipment and pickup belong to the other worker. Assets/Nomad/Inventory, Loot and shared Health remain unchanged here.

## Asset provenance

Raw ZIPs remain in private SourceArchives~. Extracted models/textures and generated prefab/material/controller remain in the private ThirdParty submodule. Dragon publisher declares CC0: https://opengameart.org/content/cethiels-dragon-3d (Drummyfish, original Cethiel). Iron Golem publisher allows personal/commercial game use: https://standout7.itch.io/lowpo-golem ; CC0 metadata conflicts with restrictive body terms, so do not label it unconditionally CC0 or publish standalone source assets. This change uses it for private game testing only. Wyvern is not imported. No bundled license text was fabricated.

Rebuild imports with `Nomad > Gameplay > Apply Three Monster Ranks`. Run this after `SkeletonAppearanceSetup.Apply`, which builds the basic catalog. Verification evidence and final commit are recorded in Issue #64 / PR #59.

## Later visual/combat update (#65)

Golem now uses procedural bone motion at3.5m reference height; Dragon5m with a distinct final-guardian damage-trail HUD. Ranged attacks, weapons and remaining art requests are documented in [monster-ranged.md](monster-ranged.md). Earlier rig-pose-only notes above describe the previous implementation. Visual-only death drops and future equipment ownership remain unchanged.
