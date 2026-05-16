using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class AnimationTypeBatcher
{
    // ── Menu items ────────────────────────────────────────────────────────────

    [MenuItem("Assets/Animation Type/Set Humanoid", priority = 1)]
    static void SetHumanoid() => Apply(ModelImporterAnimationType.Human);

    [MenuItem("Assets/Animation Type/Set Generic", priority = 2)]
    static void SetGeneric() => Apply(ModelImporterAnimationType.Generic);

    [MenuItem("Assets/Animation Type/Set Legacy", priority = 3)]
    static void SetLegacy() => Apply(ModelImporterAnimationType.Legacy);

    [MenuItem("Assets/Animation Type/Set None", priority = 4)]
    static void SetNone() => Apply(ModelImporterAnimationType.None);

    // Also available under Tools menu for convenience
    [MenuItem("Tools/Runefall/Animation Type/Set Humanoid")]
    static void SetHumanoidT() => Apply(ModelImporterAnimationType.Human);

    [MenuItem("Tools/Runefall/Animation Type/Set Generic")]
    static void SetGenericT() => Apply(ModelImporterAnimationType.Generic);

    // ── Validators — only enable when selection contains FBX/model assets ─────

    [MenuItem("Assets/Animation Type/Set Humanoid", validate = true)]
    [MenuItem("Assets/Animation Type/Set Generic",  validate = true)]
    [MenuItem("Assets/Animation Type/Set Legacy",   validate = true)]
    [MenuItem("Assets/Animation Type/Set None",     validate = true)]
    static bool Validate() => CollectPaths().Count > 0;

    // ── Core ─────────────────────────────────────────────────────────────────

    private static void Apply(ModelImporterAnimationType type)
    {
        var paths = CollectPaths();
        if (paths.Count == 0)
        {
            Debug.LogWarning("[AnimationTypeBatcher] No FBX/model assets in selection.");
            return;
        }

        int success = 0;
        int skipped = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                EditorUtility.DisplayProgressBar(
                    "Setting Animation Type",
                    System.IO.Path.GetFileName(path),
                    (float)i / paths.Count);

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) { skipped++; continue; }

                if (importer.animationType == type) { skipped++; continue; }

                importer.animationType = type;
                importer.SaveAndReimport();
                success++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[AnimationTypeBatcher] {type}: {success} changed, {skipped} skipped — {paths.Count} total.");
    }

    // Gathers asset paths from current selection.
    // Accepts files AND folders (recursive). Filters to model importers only.
    private static List<string> CollectPaths()
    {
        var result = new HashSet<string>();

        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;

            if (AssetDatabase.IsValidFolder(path))
            {
                // Recurse into folder
                string[] guids = AssetDatabase.FindAssets("t:Model", new[] { path });
                foreach (var guid in guids)
                    result.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            else
            {
                result.Add(path);
            }
        }

        // Keep only paths that have a ModelImporter
        var filtered = new List<string>();
        foreach (var p in result)
            if (AssetImporter.GetAtPath(p) is ModelImporter)
                filtered.Add(p);

        return filtered;
    }
}
