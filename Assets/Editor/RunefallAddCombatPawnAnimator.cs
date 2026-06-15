using UnityEditor;
using UnityEngine;
using Runefall.Presentation.Combat;

namespace Runefall.EditorTools
{
    /// <summary>
    /// One-shot: ensure every combat-spawn prefab (players + production enemies) has a CombatPawnAnimator
    /// on its Animator GameObject. Without it, PawnVisualManager skips the pawn and the model never plays
    /// its combat attack/hit/death animations (stands idle).
    /// </summary>
    public static class RunefallAddCombatPawnAnimator
    {
        private static readonly string[] Prefabs =
        {
            "Assets/_Project/Enemies/Vigilante Novato/Prefab/Trash.prefab",
            "Assets/_Project/Enemies/Carcelero Runico/Prefab/Jailor.prefab",
            "Assets/_Project/Enemies/Gran Verdugo/Prefab/Chief.prefab",
            "Assets/_Project/Enemies/Tharok/Prefab/boss.prefab",
            "Assets/_Project/Characters/Ice Queen/Prefab/IceQueen.prefab",
            "Assets/_Project/Characters/Kael/Prefab/Kael.prefab",
            "Assets/_Project/Characters/Vorn/Prefab/Vorn.prefab",
        };

        [MenuItem("Tools/Runefall/Ensure CombatPawnAnimator On Prefabs")]
        public static void Run()
        {
            int added = 0, had = 0, missing = 0;
            foreach (var path in Prefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) { Debug.LogWarning($"[PawnAnim] missing prefab: {path}"); missing++; continue; }
                try
                {
                    var anim = root.GetComponentInChildren<Animator>(true);
                    if (anim == null) { Debug.LogWarning($"[PawnAnim] no Animator in {path}"); missing++; continue; }

                    if (anim.GetComponent<CombatPawnAnimator>() != null)
                    {
                        had++;
                        Debug.Log($"[PawnAnim] already has it: {path}");
                    }
                    else
                    {
                        anim.gameObject.AddComponent<CombatPawnAnimator>();
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        added++;
                        Debug.Log($"[PawnAnim] ADDED CombatPawnAnimator -> {path}");
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PawnAnim] Done. added={added} alreadyHad={had} missing={missing}");
        }
    }
}
