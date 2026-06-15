using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Runefall.Data;

namespace Runefall.EditorTools
{
    /// <summary>
    /// One-shot: create an exploration AnimatorOverrideController (based on ExplorationPlayer) per playable
    /// character (Kael, Vorn, Ice Queen) and assign it to CharacterData.explorationAnimatorController so
    /// ExplorationPlayer can wire the model Animator at runtime from the selected character.
    /// </summary>
    public static class RunefallCharacterExplorationOverrides
    {
        private const string BasePath = "Assets/_Project/Systems/Characters/Animations/ExplorationPlayer.controller";

        // CharacterData asset path -> output override controller path
        private static readonly (string data, string outCtrl)[] Targets =
        {
            ("Assets/_Project/Characters/Kael/Kael_CharacterData.asset",
             "Assets/_Project/Characters/Kael/Kael_ExplorationOverride.overrideController"),
            ("Assets/_Project/Characters/Vorn/Vorn_CharacterData.asset",
             "Assets/_Project/Characters/Vorn/Vorn_ExplorationOverride.overrideController"),
            ("Assets/_Project/Characters/Ice Queen/IceQueen_CharacterData.asset",
             "Assets/_Project/Characters/Ice Queen/IceQueen_ExplorationOverride.overrideController"),
        };

        [MenuItem("Tools/Runefall/Create Character Exploration Override Controllers")]
        public static void Create()
        {
            var baseCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(BasePath);
            if (baseCtrl == null) { Debug.LogError($"[CharExplOverrides] Base not found: {BasePath}"); return; }

            int done = 0;
            foreach (var (dataPath, outPath) in Targets)
            {
                var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(outPath);
                if (oc == null)
                {
                    oc = new AnimatorOverrideController(baseCtrl) { name = System.IO.Path.GetFileNameWithoutExtension(outPath) };
                    AssetDatabase.CreateAsset(oc, outPath);
                }
                else oc.runtimeAnimatorController = baseCtrl;

                var cd = AssetDatabase.LoadAssetAtPath<CharacterData>(dataPath);
                if (cd != null)
                {
                    cd.explorationAnimatorController = oc;
                    EditorUtility.SetDirty(cd);
                    Debug.Log($"[CharExplOverrides] {cd.name} -> {outPath} (assigned to explorationAnimatorController)");
                }
                else Debug.LogWarning($"[CharExplOverrides] CharacterData missing: {dataPath}");
                done++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CharExplOverrides] Done. {done}");
        }
    }
}
