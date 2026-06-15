using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Runefall.EditorTools
{
    /// <summary>
    /// One-shot: relocate the skill-card sprites dumped in Shared/UI/"6. Tarjetas de Habilidad"
    /// into each character's folder. Hab 1 / Hab 2 / Ultimate art → Characters/&lt;Char&gt;/Skills/card art.
    /// Passive art → Characters/&lt;Char&gt;/Passive. GUID-preserving (AssetDatabase.MoveAsset).
    /// Lyra == Ice Queen.
    /// </summary>
    public static class RunefallCardArtMove
    {
        private const string SrcFolder = "Assets/_Project/Shared/UI/6. Tarjetas de Habilidad";

        // sprite-name prefix -> character folder name
        private static readonly Dictionary<string, string> CharByPrefix = new()
        {
            { "Kael", "Kael" },
            { "Lyra", "Ice Queen" },
            { "Vorn", "Vorn" },
        };

        [MenuItem("Tools/Runefall/Move Skill Card Art Into Characters")]
        public static void Move()
        {
            if (!AssetDatabase.IsValidFolder(SrcFolder))
            {
                Debug.LogError($"[CardArtMove] Source folder not found: {SrcFolder}");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SrcFolder });
            int moved = 0, skipped = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileName(path);

                int space = file.IndexOf(' ');
                string prefix = space > 0 ? file.Substring(0, space) : file;
                if (!CharByPrefix.TryGetValue(prefix, out var charFolder))
                {
                    Debug.LogWarning($"[CardArtMove] Unknown prefix '{prefix}' — skipped: {file}");
                    skipped++;
                    continue;
                }

                bool isPassive = file.Contains("Pasiva");
                string charRoot = $"Assets/_Project/Characters/{charFolder}";
                string destFolder = isPassive ? $"{charRoot}/Passive" : $"{charRoot}/Skills/card art";

                if (!isPassive)
                {
                    string skills = $"{charRoot}/Skills";
                    if (!AssetDatabase.IsValidFolder(skills))
                    {
                        Debug.LogError($"[CardArtMove] Missing {skills} — skipped: {file}");
                        skipped++;
                        continue;
                    }
                    if (!AssetDatabase.IsValidFolder(destFolder))
                        AssetDatabase.CreateFolder(skills, "card art");
                }

                if (!AssetDatabase.IsValidFolder(destFolder))
                {
                    Debug.LogError($"[CardArtMove] Dest folder missing: {destFolder} — skipped: {file}");
                    skipped++;
                    continue;
                }

                string dest = $"{destFolder}/{file}";
                string err = AssetDatabase.MoveAsset(path, dest);
                if (string.IsNullOrEmpty(err)) { moved++; Debug.Log($"[CardArtMove] {file} -> {destFolder}"); }
                else { skipped++; Debug.LogError($"[CardArtMove] FAILED {file}: {err}"); }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CardArtMove] Done. moved={moved} skipped={skipped}");
        }
    }
}
