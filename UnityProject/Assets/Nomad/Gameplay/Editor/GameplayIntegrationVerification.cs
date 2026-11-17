using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nomad.Combat;
using Nomad.Interaction;
using Nomad.Inventory;
using Nomad.Loot;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using Nomad.Player;
using Nomad.Terrain;
using Nomad.Terrain.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Nomad.Gameplay.Editor
{
    public static class GameplayIntegrationVerification
    {
        private const string Key = "Nomad.Gameplay.Verify";
        private static Stack<IEnumerator> routines;
        private static double deadline;
        private static int lastFrame = -1;
        private static Keyboard keyboard;
        private static GameplayIntegration gameplay;
        private static TerrainIntegrationControls controls;

        public static void Run()
        {
            EditorSceneManager.OpenScene(TerrainIntegrationAuthoring.ScenePath);
            SessionState.SetBool(Key, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            EditorApplication.update -= Tick;
            if (!SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 240f;
            routines = null;
            lastFrame = -1;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Gameplay verification deadline");
                if (!EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                if (routines == null)
                {
                    Application.runInBackground = true;
                    Time.captureFramerate = 60;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    routines = new Stack<IEnumerator>();
                    routines.Push(Verify());
                }
                while (routines.Count > 0)
                {
                    IEnumerator current = routines.Peek();
                    if (!current.MoveNext()) { routines.Pop(); continue; }
                    if (current.Current is IEnumerator nested) { routines.Push(nested); continue; }
                    return;
                }
                Directory.CreateDirectory("Artifacts");
                string report = "GAMEPLAY_INTEGRATION_PASS: saved scene, seeds0/7/51 and replay, valid 6+1+1 actors, generation cleanup/projectiles, real tracker across height, real PlayerCombat/projectiles, UI blocking, E-key chest inventory, real melee death, input lock and scene-reload retry";
                File.WriteAllText("Artifacts/gameplay-integration-results.txt", report);
                Debug.Log(report);
                Finish(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(1);
            }
        }

        private static IEnumerator Verify()
        {
            yield return Until(() => Object.FindFirstObjectByType<GameplayIntegration>()?.Ready == true, "initial field Ready", 30f);
            Bind();
            Require(gameplay.Enemies.Count == 8, "initial normal6 + elite1 + boss1");
            Require(gameplay.PlayerHealth.CurrentHealth == 100, "player HP bound");
            Require(controls.Player.GetComponent<PlayerCombat>() != null && controls.Player.GetComponent<PlayerInteraction>() != null,
                "shooting and interaction share player");
            var inventory = controls.Player.GetComponent<PlayerInventory>();
            inventory.Add("scrap", 7);
            string previousId = gameplay.GenerationId;
            foreach (int seed in new[] { 0, 7, 51 })
            {
                var data = new SerializedObject(controls);
                data.FindProperty("seed").intValue = seed;
                data.ApplyModifiedPropertiesWithoutUndo();
                controls.Regenerate(false);
                yield return null;
                Require(gameplay.Ready && gameplay.Enemies.Count == 8, "seed placement " + seed);
                Require(gameplay.Enemies.Count(enemy => enemy.GetComponent<MonsterIdentity>().Rank == MonsterRank.Normal) == 6 &&
                    gameplay.Enemies.Count(enemy => enemy.GetComponent<MonsterIdentity>().Rank == MonsterRank.Elite) == 1 &&
                    gameplay.Enemies.Count(enemy => enemy.GetComponent<MonsterIdentity>().Rank == MonsterRank.Boss) == 1,
                    "all three ranks placed");
                foreach (Health actor in gameplay.Enemies)
                    Require(actor.GetComponentInChildren<SkinnedMeshRenderer>() != null, "real model for every rank");
                Require(gameplay.GenerationId != previousId, "new generation identity");
                previousId = gameplay.GenerationId;
                string signature = Signature();
                Health[] old = gameplay.Enemies.ToArray();
                foreach (Health actor in old) actor.TryApplyDamage(10000);
                PlaceholderEquipmentDrop[] oldDrops = old.Select(actor => actor.GetComponent<MonsterDeathDrop>().SpawnedDrop).ToArray();
                Require(oldDrops.All(drop => drop != null), "every rank drops equipment marker");
                Require(Scrap(inventory) == 7, "death drops do not enter inventory");
                controls.Regenerate(false);
                Require(old.All(enemy => enemy == null || !enemy.gameObject.activeInHierarchy), "old actors disabled immediately");
                Require(oldDrops.All(drop => drop == null || !drop.gameObject.activeInHierarchy), "old drops disabled immediately");
                yield return null;
                Require(oldDrops.All(drop => drop == null), "regeneration destroys prior drops");
                Require(Signature() == signature, "same seed actor placement replay " + seed);
                Require(Scrap(inventory) == 7, "development regeneration preserves inventory");
                foreach (Health enemy in gameplay.Enemies)
                {
                    Require(enemy.GetComponent<NavMeshAgent>().isOnNavMesh, "enemy NavMesh");
                    Require(Vector3.Distance(enemy.transform.position, controls.Navigation.LastResult.PlayerPosition) >= 11.9f,
                        "safe initial distance");
                }
            }
            inventory.Remove("scrap", int.MaxValue);

            // 실제 Tracker가 서로 다른 높이의 가까운 두 유효 지점 사이를 이동합니다.
            Health tracked = gameplay.Enemies.First();
            foreach (Health enemy in gameplay.Enemies) if (enemy != tracked) enemy.gameObject.SetActive(false);
            tracked.GetComponent<MonsterMeleeAttack>().enabled = false;
            controls.Player.GetComponent<PlayerMovement>().enabled = false;
            var points = controls.Navigation.LastResult.EnemyPoints;
            var path = new NavMeshPath();
            FieldSpawnPoint start = null;
            FieldSpawnPoint goal = null;
            foreach (FieldSpawnPoint a in points)
            {
                foreach (FieldSpawnPoint b in points)
                {
                    if (Mathf.Abs(a.Position.y - b.Position.y) < 0.8f || Vector3.Distance(a.Position, b.Position) > 12f) continue;
                    if (!NavMesh.CalculatePath(a.Position, b.Position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    float length = 0f;
                    for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                    if (length > 15f) continue;
                    start = a; goal = b; break;
                }
                if (start != null) break;
            }
            Require(start != null, "height crossing pair in saved generated field");
            NavMeshAgent agent = tracked.GetComponent<NavMeshAgent>();
            agent.Warp(start.Position);
            agent.speed = 12f;
            agent.acceleration = 80f;
            agent.stoppingDistance = 0.5f;
            MovePlayer(goal.Position + Vector3.up * 0.05f);
            var tracker = tracked.GetComponent<MonsterTracker>();
            tracker.Configure(80f, 100f, 0f);
            tracker.ReactToDamage();
            yield return Until(() => Vector3.Distance(tracked.transform.position, goal.Position) < 0.8f,
                "actual tracker crosses generated stairs", 12f);
            Require(Mathf.Abs(tracked.transform.position.y - start.Position.y) > 0.6f, "actual height changed");
            controls.Regenerate(false);
            yield return null;

            // 실제 공개 사격 API → 실제 발사체 → 몬스터 피해/사망.
            Health target = gameplay.Enemies.First();
            foreach (Health enemy in gameplay.Enemies) if (enemy != target) enemy.gameObject.SetActive(false);
            target.GetComponent<MonsterMeleeAttack>().enabled = false;
            target.GetComponent<NavMeshAgent>().speed = 0f;
            controls.Player.GetComponent<PlayerMovement>().enabled = false;
            Require(PlaceNear(target.transform.position, 3f), "supported shooting position");
            controls.SetView(0);
            yield return null;
            var shooting = controls.Player.GetComponent<PlayerCombat>();
            Require(!shooting.TryShoot(new Vector2(25f, Screen.height - 50f)), "HUD blocks actual shooting API");
            for (int shot = 1; shot <= 4; shot++)
            {
                Vector2 pointer = Camera.main.WorldToScreenPoint(target.transform.position + Vector3.up);
                bool fired = shooting.TryShoot(pointer);
                Require(fired, $"actual player shot {shot}; enabled={shooting.isActiveAndEnabled}, hp={gameplay.PlayerHealth.CurrentHealth}, pointer={pointer}, screen={Screen.width}x{Screen.height}, aim={shooting.AimPoint}, player={controls.Player.transform.position}, target={target.transform.position}");
                int expected = 100 - shot * 25;
                yield return Until(() => target.CurrentHealth == expected, "projectile damage " + shot, 2f);
            }
            Require(target.IsDead, "monster death integration");
            Require(target.GetComponent<MonsterDeathDrop>().SpawnedDrop != null, "real projectile kill drops marker");
            yield return Until(() => target.GetComponent<MonsterDeathDrop>().SpawnedDrop.Landed, "equipment marker landed", 2f);
            ScreenCapture.CaptureScreenshot("Artifacts/monster-drop-playtest.png");
            yield return null;

            // 같은 플레이어의 실제 E키 상호작용으로 상자를 인벤토리에 옮깁니다.
            var chests = Object.FindObjectsByType<LootChest>(FindObjectsSortMode.None);
            Require(chests.Length > 0, "existing generated loot chests retained");
            bool adjacent = false;
            foreach (LootChest chest in chests)
                if (PlaceNear(chest.InteractionPoint, 1.25f)) { adjacent = true; break; }
            Require(adjacent, "chest approach position");
            controls.Player.GetComponent<PlayerMovement>().enabled = true;
            yield return null;
            yield return Press(UnityEngine.InputSystem.Key.E);
            yield return Until(() => Scrap(inventory) == 10, "E key transferred real chest contents", 2f);

            // 생성이 시작되면 날아가던 발사체와 이전 세대를 즉시 비활성화합니다.
            Projectile prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/Combat/PlayerProjectile.prefab").GetComponent<Projectile>();
            Vector3 muzzle = controls.Player.transform.position + Vector3.up * 1.2f;
            Require(Projectile.TryLaunch(prefab, controls.Player.transform, muzzle, muzzle + Vector3.up * 20f,
                Physics.DefaultRaycastLayers, 0.1f, 100f, 25), "in-flight projectile fixture");
            Require(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length > 0, "active projectile exists");
            controls.Regenerate(false);
            yield return null;
            Require(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length == 0, "regeneration cleared projectiles");
            Require(gameplay.Enemies.Count == 8 && Scrap(inventory) == 10, "new generation and carried loot");

            foreach (Health enemy in gameplay.Enemies) enemy.gameObject.SetActive(false);
            Health attacker = gameplay.Enemies.First();
            Require(PlaceNear(attacker.transform.position, 1.4f), "real melee fixture position");
            attacker.gameObject.SetActive(true);
            attacker.GetComponent<NavMeshAgent>().speed = 0f;
            attacker.GetComponent<MonsterMeleeAttack>().AttackSpeed = 4f;
            controls.Player.GetComponent<PlayerMovement>().enabled = true;
            yield return Until(() => gameplay.IsDefeated, "actual melee defeats player", 6f);
            Require(!controls.enabled && !shooting.enabled && !controls.Player.GetComponent<PlayerInteraction>().enabled,
                "death locks regeneration, shooting and interaction");
            yield return Press(UnityEngine.InputSystem.Key.E);
            Require(Scrap(inventory) == 10, "dead player cannot loot");
            GameplayIntegration oldRun = gameplay;
            gameplay.RestartRun();
            yield return Until(() => oldRun == null && Object.FindFirstObjectByType<GameplayIntegration>()?.Ready == true,
                "retry replaced old run with saved integration scene", 30f);
            Bind();
            Require(!gameplay.IsDefeated && gameplay.PlayerHealth.CurrentHealth == 100,
                "retry resets health and defeat state");
            Require(controls.Player.GetComponent<PlayerInventory>().UsedSlots == 0 && gameplay.Enemies.Count == 8,
                "retry clears carried loot and restores encounters");
        }

        private static void Bind()
        {
            gameplay = Object.FindFirstObjectByType<GameplayIntegration>();
            controls = gameplay.GetComponent<TerrainIntegrationControls>();
        }

        private static string Signature() => string.Join(";", gameplay.Enemies.Select(enemy =>
            enemy.name + ":" + enemy.transform.position.ToString("F3")).OrderBy(value => value));

        private static int Scrap(PlayerInventory inventory)
        {
            int result = 0;
            for (int i = 0; i < inventory.UsedSlots; i++)
                if (inventory.GetStack(i).Item.ItemId == "scrap") result += inventory.GetStack(i).Count;
            return result;
        }

        private static bool PlaceNear(Vector3 target, float distance)
        {
            foreach (Vector3 direction in new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right })
            {
                Vector3 origin = target + direction * distance;
                if (!Physics.Raycast(origin + Vector3.up * 4f, Vector3.down, out RaycastHit floor, 8f) ||
                    floor.collider.GetComponentInParent<TerrainPiece>() == null) continue;
                Vector3 feet = floor.point + Vector3.up * 0.06f;
                if (Physics.CheckCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.45f, 0.35f)) continue;
                Vector3 eye = feet + Vector3.up;
                if (Projectile.TryGetFirstHit(new Ray(eye, (target + Vector3.up - eye).normalized),
                    Vector3.Distance(eye, target + Vector3.up), Physics.DefaultRaycastLayers, controls.Player.transform,
                    out RaycastHit obstacle) && obstacle.collider.GetComponentInParent<TerrainPiece>() != null) continue;
                MovePlayer(feet);
                Vector3 facing = Vector3.ProjectOnPlane(target - feet, Vector3.up);
                if (facing.sqrMagnitude > 0.01f) controls.Player.transform.rotation = Quaternion.LookRotation(facing);
                Physics.SyncTransforms();
                return true;
            }
            return false;
        }

        private static void MovePlayer(Vector3 position)
        {
            CharacterController controller = controls.Player.GetComponent<CharacterController>();
            controller.enabled = false;
            controls.Player.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static IEnumerator Press(UnityEngine.InputSystem.Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static IEnumerator Until(Func<bool> condition, string label, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Require(condition(), label);
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("GAMEPLAY_INTEGRATION_FAIL: " + message);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            EditorApplication.Exit(code);
        }
    }
}
