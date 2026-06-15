using UnityEditor;
using UnityEngine;
using Runefall.Data;
using Runefall.Presentation.Enemies;
using Runefall.Presentation.Player;

namespace Runefall.EditorTools
{
    /// <summary>
    /// One-shot: copy the weapon the user attached (in the scene) to Ice Queen and Chief onto their
    /// spawn prefabs, under mixamorig:RightHand with the same local transform, so combat instances
    /// spawn already holding the weapon. Run with DungeonExploration open.
    /// </summary>
    public static class RunefallWeaponToPrefab
    {
        private const string HandBone        = "mixamorig:RightHand";
        private const string IceQueenPrefab  = "Assets/_Project/Characters/Ice Queen/Prefab/IceQueen.prefab";
        private const string ChiefPrefab     = "Assets/_Project/Enemies/Gran Verdugo/Prefab/Chief.prefab";

        [MenuItem("Tools/Runefall/Copy Scene Weapons To Prefabs")]
        public static void Run()
        {
            // Ice Queen: source = player model in scene.
            var ep = Object.FindFirstObjectByType<ExplorationPlayer>();
            var iqWeapon = ep != null ? FindHandWeapon(ep.transform) : null;
            CopyToPrefab(iqWeapon, IceQueenPrefab, "Ice Queen");

            // Chief (Gran Verdugo): source = the enemy whose model carries a hand weapon.
            Transform chiefWeapon = null;
            foreach (var ec in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (new SerializedObject(ec).FindProperty("_enemyData").objectReferenceValue is not EnemyData d) continue;
                if (!d.name.Contains("GranVerdugo")) continue;
                var w = FindHandWeapon(ec.transform);
                if (w != null) { chiefWeapon = w; break; }
            }
            CopyToPrefab(chiefWeapon, ChiefPrefab, "Chief");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WeaponToPrefab] Done.");
        }

        private static void CopyToPrefab(Transform sceneWeapon, string prefabPath, string label)
        {
            if (sceneWeapon == null) { Debug.LogWarning($"[WeaponToPrefab] {label}: no scene weapon found — skipped"); return; }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var hand = FindDeep(root.transform, HandBone);
                if (hand == null) { Debug.LogError($"[WeaponToPrefab] {label}: '{HandBone}' not found in {prefabPath}"); return; }

                // Idempotent: drop a previous copy of the same name.
                var existing = hand.Find(sceneWeapon.name);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);

                var copy = Object.Instantiate(sceneWeapon.gameObject);
                copy.name = sceneWeapon.name;
                copy.transform.SetParent(hand, false);
                copy.transform.localPosition = sceneWeapon.localPosition;
                copy.transform.localRotation = sceneWeapon.localRotation;
                copy.transform.localScale    = sceneWeapon.localScale;

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[WeaponToPrefab] {label}: added '{copy.name}' under {HandBone} in {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // First descendant with a MeshFilter that sits under a mixamorig:RightHand bone.
        private static Transform FindHandWeapon(Transform root)
        {
            var hand = FindDeep(root, HandBone);
            if (hand == null) return null;
            foreach (Transform child in hand)
                if (child.GetComponent<MeshFilter>() != null) return child;
            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
