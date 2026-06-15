using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Runefall.Data;
using Runefall.Presentation.Enemies;

namespace Runefall.EditorTools
{
    /// <summary>
    /// One-shot: create an exploration AnimatorOverrideController (based on ExplorationEnemyNew) per
    /// production normal enemy, and assign each to the matching scene enemy's model Animator so every
    /// enemy type can carry its own locomotion clips later. Boss excluded. Run with DungeonExploration open.
    /// </summary>
    public static class RunefallEnemyExplorationOverrides
    {
        private const string BasePath = "Assets/_Project/Systems/Characters/Animations/ExplorationEnemyNew.controller";

        // EnemyData asset path -> output exploration override controller path
        private static readonly Dictionary<string, string> Map = new()
        {
            { "Assets/_Project/Enemies/Vigilante Novato/VigilanteNovato_EnemyData.asset",
              "Assets/_Project/Enemies/Vigilante Novato/VigilanteNovato_ExplorationOverride.overrideController" },
            { "Assets/_Project/Enemies/Carcelero Runico/CarceleroRunico_EnemyData.asset",
              "Assets/_Project/Enemies/Carcelero Runico/CarceleroRunico_ExplorationOverride.overrideController" },
            { "Assets/_Project/Enemies/Gran Verdugo/GranVerdugo_EnemyData.asset",
              "Assets/_Project/Enemies/Gran Verdugo/GranVerdugo_ExplorationOverride.overrideController" },
        };

        [MenuItem("Tools/Runefall/Create Enemy Exploration Override Controllers")]
        public static void Create()
        {
            var baseCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(BasePath);
            if (baseCtrl == null) { Debug.LogError($"[ExplOverrides] Base not found: {BasePath}"); return; }

            // 1) Create (or rewire) the override assets.
            var ocByData = new Dictionary<string, AnimatorOverrideController>();
            foreach (var kv in Map)
            {
                var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(kv.Value);
                if (oc == null)
                {
                    oc = new AnimatorOverrideController(baseCtrl) { name = System.IO.Path.GetFileNameWithoutExtension(kv.Value) };
                    AssetDatabase.CreateAsset(oc, kv.Value);
                }
                else oc.runtimeAnimatorController = baseCtrl;
                ocByData[kv.Key] = oc;
                Debug.Log($"[ExplOverrides] asset ready: {kv.Value}");
            }
            AssetDatabase.SaveAssets();

            // 2) Assign to each matching scene enemy's model Animator.
            int assigned = 0;
            var controllers = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ec in controllers)
            {
                var data = new SerializedObject(ec).FindProperty("_enemyData").objectReferenceValue;
                if (data == null) continue;
                string dataPath = AssetDatabase.GetAssetPath(data);
                if (!ocByData.TryGetValue(dataPath, out var oc)) continue;   // boss / unknown -> skip

                var anim = ec.GetComponentInChildren<Animator>(true);
                if (anim == null) continue;
                anim.runtimeAnimatorController = oc;
                EditorUtility.SetDirty(anim);
                assigned++;
                Debug.Log($"[ExplOverrides] {ec.name} model -> {oc.name}");
            }

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();
            Debug.Log($"[ExplOverrides] Done. overrides={Map.Count} sceneAssigned={assigned}");
        }
    }
}
