using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Runefall.Editor
{
    public static class CombatAnimatorSetup
    {
        private const string BasePath    = "Assets/_Project/Animations/Combat/CombatBase.controller";
        private const string OverrideDir = "Assets/_Project/Animations/Combat/Overrides";

        [MenuItem("Runefall/Animations/Create Override Controllers")]
        private static void CreateOverrides()
        {
            var baseCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(BasePath);
            if (baseCtrl == null)
            {
                Debug.LogError($"[CombatAnimatorSetup] Base controller not found at: {BasePath}");
                return;
            }

            if (!AssetDatabase.IsValidFolder(OverrideDir))
                AssetDatabase.CreateFolder("Assets/_Project/Animations/Combat", "Overrides");

            int created = 0;

            // One override per CharacterData asset
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterData", new[] { "Assets/_Project" }))
            {
                var name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                created += CreateOverride(baseCtrl, $"{name}_Override");
            }

            // One override per EnemyData asset
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/_Project" }))
            {
                var name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                created += CreateOverride(baseCtrl, $"{name}_Override");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CombatAnimatorSetup] Done. Created {created} override controller(s) in {OverrideDir}");
        }

        private static int CreateOverride(AnimatorController baseCtrl, string overrideName)
        {
            var overridePath = $"{OverrideDir}/{overrideName}.overrideController";
            if (File.Exists(Path.GetFullPath(overridePath)))
            {
                Debug.Log($"[CombatAnimatorSetup] Skip (exists): {overrideName}");
                return 0;
            }

            var oc = new AnimatorOverrideController(baseCtrl) { name = overrideName };
            AssetDatabase.CreateAsset(oc, overridePath);
            return 1;
        }
    }
}
