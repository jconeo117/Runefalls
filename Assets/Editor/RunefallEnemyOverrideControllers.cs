using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Runefall.Data;

namespace Runefall.EditorTools
{
    /// <summary>
    /// One-shot: create an AnimatorOverrideController (based on CombatBase) for each production normal
    /// enemy (Vigilante / Carcelero / Verdugo), save it in the enemy's folder, and assign it to the
    /// EnemyData.animatorController so combat pawns get a real controller (override clips filled later).
    /// </summary>
    public static class RunefallEnemyOverrideControllers
    {
        private const string BasePath = "Assets/_Project/Systems/Combat/Animations/CombatBase.controller";

        // EnemyData asset path  ->  output override controller path
        private static readonly (string data, string outCtrl)[] Targets =
        {
            ("Assets/_Project/Enemies/Vigilante Novato/VigilanteNovato_EnemyData.asset",
             "Assets/_Project/Enemies/Vigilante Novato/VigilanteNovato_Override.overrideController"),
            ("Assets/_Project/Enemies/Carcelero Runico/CarceleroRunico_EnemyData.asset",
             "Assets/_Project/Enemies/Carcelero Runico/CarceleroRunico_Override.overrideController"),
            ("Assets/_Project/Enemies/Gran Verdugo/GranVerdugo_EnemyData.asset",
             "Assets/_Project/Enemies/Gran Verdugo/GranVerdugo_Override.overrideController"),
        };

        [MenuItem("Tools/Runefall/Create Enemy Override Controllers")]
        public static void Create()
        {
            var baseCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(BasePath);
            if (baseCtrl == null)
            {
                Debug.LogError($"[EnemyOverrides] Base controller not found: {BasePath}");
                return;
            }

            int done = 0;
            foreach (var (dataPath, outPath) in Targets)
            {
                var ed = AssetDatabase.LoadAssetAtPath<EnemyData>(dataPath);
                if (ed == null) { Debug.LogWarning($"[EnemyOverrides] EnemyData missing: {dataPath}"); continue; }

                var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(outPath);
                if (oc == null)
                {
                    oc = new AnimatorOverrideController(baseCtrl) { name = System.IO.Path.GetFileNameWithoutExtension(outPath) };
                    AssetDatabase.CreateAsset(oc, outPath);
                }
                else
                {
                    oc.runtimeAnimatorController = baseCtrl;   // ensure base wired
                }

                ed.animatorController = oc;
                EditorUtility.SetDirty(ed);
                done++;
                Debug.Log($"[EnemyOverrides] {ed.name} -> {outPath} (assigned to EnemyData.animatorController)");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[EnemyOverrides] Done. created/assigned {done}");
        }
    }
}
