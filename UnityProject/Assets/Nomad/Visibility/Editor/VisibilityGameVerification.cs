using System;
using System.Linq;
using Nomad.Combat;
using Nomad.Game;
using Nomad.Monsters;
using Nomad.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.Visibility.Editor
{
    // 실제 Game 흐름의 영속 모듈과 지역 오브젝트 수명을 검사합니다.
    public static class VisibilityGameVerification
    {
        private const string SessionKey = "Nomad.Visibility.GameVerify";
        private const string PreviewKey = "Nomad.Visibility.GameVerifyPreview";
        private static GameManager manager;
        private static PlayerVisibility sight;
        private static TerrainIntegrationControls controls;
        private static double deadline;
        private static float waitUntil;
        private static int stage;
        private static int checks;
        private static Transform observer;
        private static string generation;
        private static VisibilityFoliage tree;
        private static Texture sourceTexture;

        [MenuItem("Nomad/Visibility/Verify Main Game")]
        public static void Run() => Begin(false);

        // 공용 설정의 선점이 해소되기 전에는 런타임에만 같은 프리팹을 붙여 검증합니다.
        public static void RunPreview() => Begin(true);

        private static void Begin(bool preview)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before verification.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            stage = 0;
            checks = 0;
            waitUntil = 0f;
            manager = null;
            sight = null;
            observer = null;
            tree = null;

            EditorSceneManager.OpenScene("Assets/Nomad/Game/Game.unity");
            SessionState.SetBool(SessionKey, true);
            SessionState.SetBool(PreviewKey, preview);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException("Visibility Game timeout at stage " + stage);
                if (!EditorApplication.isPlaying || Time.unscaledTime < waitUntil) return;

                Application.runInBackground = true;
                GameManager current = GameManager.Instance;
                if (current == null || current.IsLoading || !current.CurrentRegion.IsValid()) return;
                if (current.Player == null || !current.Player.activeInHierarchy) return;
                current.Player.GetComponent<Health>().IsCombatImmune = true;

                if (SessionState.GetBool(PreviewKey, false) && current.GetComponentInChildren<PlayerVisibility>() == null)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/Nomad/Visibility/PlayerVisibilityModule.prefab");
                    Object.Instantiate(prefab, current.transform);
                    waitUntil = Time.unscaledTime + 0.3f;
                    return;
                }

                PlayerVisibility active = current.GetComponentInChildren<PlayerVisibility>();
                Check(active != null, "GameManager owns visibility module");
                if (active.Observer != current.Player.transform) return;

                // 마을에서는 시야가 쉬고, 필드에서는 켜진 뒤에 검사합니다.
                bool village = current.CurrentRegion.name == "Village";
                if (village != active.Suppressed) return;
                if (village) CheckVillage(active);
                else if (!active.Available) return;

                if (stage == 0)
                {
                    manager = current;
                    sight = active;
                    observer = sight.Observer;
                    Check(current.CurrentRegion.name == "Village", "starts in Village");
                    CheckModule(current);
                    Check(current.LoadRegion("Field"), "enter Field");
                    stage = 1;
                }
                else if (stage == 1 || stage == 2 || stage == 4)
                {
                    controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
                    if (controls == null || controls.Navigation.State != FieldGenerationState.Ready) return;
                    if (stage == 2 && controls.Navigation.LastResult.GenerationId == generation) return;
                    waitUntil = Time.unscaledTime + 0.3f;
                    stage += 10;
                }
                else if (stage == 11 || stage == 12 || stage == 14)
                {
                    CheckModule(current);
                    Check(current == manager && active == sight, "same persistent visibility module");
                    Check(active.Observer == observer, "same persistent observer");
                    MonsterIdentity[] monsters = Object.FindObjectsByType<MonsterIdentity>();
                    Check(monsters.Length > 0, "Field contains monsters");
                    Check(monsters.All(m => m.GetComponent<Health>().IsDead || m.GetComponent<VisibilityTarget>()?.Controlled == true), "all monsters controlled");
                    Check(VisibilityTarget.ActiveCount == monsters.Length, "no stale targets");
                    Check(monsters.Any(m => !m.GetComponent<VisibilityTarget>().IsVisible), "hidden enemies exist");
                    Check(monsters.All(m => m.GetComponent<Health>().enabled && m.GetComponent<Collider>().enabled), "combat and collision retained");

                    if (stage == 11 && tree == null)
                    {
                        PrepareTree(current.Player);
                        waitUntil = Time.unscaledTime + 0.6f;
                    }
                    else if (stage == 11)
                    {
                        VerifyTree(current.Player);
                        generation = controls.Navigation.LastResult.GenerationId;
                        controls.Regenerate(false);
                        stage = 2;
                    }
                    else if (stage == 12)
                    {
                        Check(current.LoadRegion("Village"), "return Village");
                        stage = 3;
                    }
                    else
                    {
                        current.Restart();
                        stage = 5;
                    }
                }
                else if (stage == 3)
                {
                    Check(current.CurrentRegion.name == "Village", "returned Village");
                    Check(active == sight && active.Observer == observer, "binding survives return");
                    Check(VisibilityTarget.ActiveCount == 0, "unloaded Field targets removed");
                    Check(current.LoadRegion("Field"), "reenter Field");
                    stage = 4;
                }
                else if (stage == 5)
                {
                    Check(current != manager, "restart replaces GameManager");
                    Check(!ReferenceEquals(active, sight), "restart replaces visibility module");
                    Check(!ReferenceEquals(active.Observer, observer), "restart binds new Player");
                    Check(current.CurrentRegion.name == "Village", "restart returns Village");
                    CheckModule(current);
                    Check(VisibilityTarget.ActiveCount == 0, "restart removes old targets");
                    string mode = SessionState.GetBool(PreviewKey, false) ? "PREVIEW" : "REGISTERED";
                    Debug.Log("VISIBILITY_GAME_PASS mode=" + mode + " checks=" + checks);
                    Finish(0);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        // 실제 구매 식생: 나무는 시선을 막지 않고 원본 텍스처로 반투명해지며, 소품은 차단을 유지합니다.
        private static void PrepareTree(GameObject player)
        {
            VisibilityFoliage[] connected = Object.FindObjectsByType<VisibilityFoliage>();
            VisibilityFoliage[] trees = connected.Where(f => Label(f).Contains("tree") && !Label(f).Contains("stump")).ToArray();
            Check(trees.Length > 0, "field trees connected");
            Check(trees.All(f => !f.BlocksSight), "field trees do not block sight");
            Check(connected.Where(f => Label(f).Contains("prop_")).All(f => f.BlocksSight), "field props keep blocking");

            tree = trees.First(f => !f.Faded);
            Material source = tree.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            sourceTexture = new[] { "_BaseMap", "_MainTex", "_Albedo_Map" }
                .Where(source.HasProperty).Select(source.GetTexture).FirstOrDefault(texture => texture != null);
            Check(sourceTexture != null, "field tree source texture found");

            // 이동 컴포넌트가 위치를 되돌리지 않도록 잠시 끄고 나무 옆으로 옮깁니다.
            player.GetComponent<Nomad.Player.PlayerMovement>().enabled = false;
            player.transform.position = tree.transform.position + Vector3.right * 1.5f;
            Physics.SyncTransforms();
        }

        private static void VerifyTree(GameObject player)
        {
            Check(tree.InSight && tree.Faded && tree.Alpha < 1f, "field tree near player fades");
            Material faded = tree.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Check(faded.shader.name == "Nomad/Visibility/Foliage Fade", "field tree uses fade shader");
            Check(faded.GetTexture("_BaseMap") == sourceTexture, "field tree keeps source texture");

            Vector3 toTree = tree.transform.position - sight.Eye;
            toTree.y = 0f;
            Check(sight.ClearDistance(sight.Eye, toTree.normalized, toTree.magnitude + 1f) >= toTree.magnitude + 0.5f, "field trunk does not block sight");
            Check(Object.FindFirstObjectByType<VisibilityField>().ShadeRenderer.enabled, "field shade active");
            player.GetComponent<Nomad.Player.PlayerMovement>().enabled = true;
            Debug.Log("VISIBILITY_GAME_TREE " + tree.name);
        }

        private static string Label(Component item) => item.name.ToLowerInvariant();

        private static void CheckVillage(PlayerVisibility active)
        {
            Check(!active.Available, "visibility off in Village");
            Check(Shader.GetGlobalFloat("_NomadVisibilityActive") == 0f, "no visibility mask in Village");
            VisibilityField field = active.GetComponent<VisibilityField>();
            Check(field.ShadeRenderer == null || !field.ShadeRenderer.enabled, "no shade in Village");
            Check(Object.FindObjectsByType<VisibilityTarget>().All(t => !t.Controlled), "nothing hidden in Village");
        }

        private static void CheckModule(GameManager current)
        {
            Check(Object.FindObjectsByType<PlayerVisibility>().Length == 1, "one visibility module");
            Check(current.GetComponentInChildren<VisibilityBinding>() != null, "automatic binding present");
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Visibility Game: " + label);
            checks++;
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Tick;
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else EditorApplication.ExitPlaymode();
        }
    }
}
