using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Runefall.Data;
using Runefall.Presentation.Enemies;

namespace Runefall.EditorTools
{
    /// <summary>
    /// One-shot: populate the boss (Tharok) AnimatorController with a single Idle state (humanoid idle
    /// clip), assign it to the boss model in the scene, and strip its patrol waypoints — boss stands
    /// still, intimidating, waiting at its door. Run with DungeonExploration open.
    /// </summary>
    public static class RunefallBossIdleSetup
    {
        private const string IdleFbx  = "Assets/_Project/Shared/Animations/Idle_Animations/Anim_Fighter_Idle.FBX";
        private const string CtrlPath = "Assets/_Project/Enemies/Tharok/Tharok.controller";

        [MenuItem("Tools/Runefall/Setup Boss Idle (Tharok)")]
        public static void Setup()
        {
            // 1) Idle clip from the FBX.
            AnimationClip idle = null;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(IdleFbx))
                if (a is AnimationClip c && !c.name.StartsWith("__preview")) { idle = c; break; }
            if (idle == null) { Debug.LogError($"[BossIdle] No AnimationClip in {IdleFbx}"); return; }

            // 2) Load (or create) the controller and ensure a Base Layer + single Idle default state.
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
            if (ac == null) { ac = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath); }
            if (ac.layers == null || ac.layers.Length == 0)
                ac.AddLayer("Base Layer");

            var sm = ac.layers[0].stateMachine;
            AnimatorState idleState = null;
            foreach (var cs in sm.states) if (cs.state.name == "Idle") { idleState = cs.state; break; }
            if (idleState == null) idleState = sm.AddState("Idle");
            idleState.motion = idle;
            sm.defaultState = idleState;
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BossIdle] Controller populated: {CtrlPath} state=Idle clip={idle.name}");

            // 3) Assign to the boss model in the scene + clear waypoints.
            int bossCount = 0;
            foreach (var ec in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so   = new SerializedObject(ec);
                if (so.FindProperty("_enemyData").objectReferenceValue is not BossEnemyData) continue;

                var anim = ec.GetComponentInChildren<Animator>(true);
                if (anim != null) { anim.runtimeAnimatorController = ac; EditorUtility.SetDirty(anim); }

                so.FindProperty("_waypoints").ClearArray();
                so.ApplyModifiedProperties();
                bossCount++;
                Debug.Log($"[BossIdle] {ec.name}: controller assigned + waypoints cleared");
            }

            if (bossCount > 0)
            {
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.Refresh();
            Debug.Log($"[BossIdle] Done. bossesUpdated={bossCount}");
        }
    }
}
