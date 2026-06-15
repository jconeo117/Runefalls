using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Runefall.EditorTools
{
    /// <summary>
    /// One-shot NavMesh baker for the exploration scene. The project uses the AI Navigation
    /// package (NavMeshSurface) but no surface/bake existed, so enemy agents had no NavMesh to
    /// stand on. Run Tools ▸ Runefall ▸ Bake Exploration NavMesh with the scene open.
    /// </summary>
    public static class RunefallNavBaker
    {
        [MenuItem("Tools/Runefall/Bake Exploration NavMesh")]
        public static void BakeActiveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            GameObject world = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "_World") { world = root; break; }

            if (world == null)
            {
                Debug.LogError("[RunefallNavBaker] No '_World' root in the active scene.");
                return;
            }

            var surface = world.GetComponent<NavMeshSurface>();
            if (surface == null) surface = world.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry    = NavMeshCollectGeometry.RenderMeshes;
            surface.defaultArea    = 0; // Walkable

            surface.BuildNavMesh();

            if (surface.navMeshData == null)
            {
                Debug.LogError("[RunefallNavBaker] BuildNavMesh produced no data — check that _World children have walkable geometry.");
                return;
            }

            AssetDatabase.Refresh();
            string path = "Assets/DungeonExploration_NavMesh.asset";
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(path) == null)
                AssetDatabase.CreateAsset(surface.navMeshData, path);

            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            var tri = NavMesh.CalculateTriangulation();
            Debug.Log($"[RunefallNavBaker] NavMesh baked → {path} (verts={tri.vertices.Length}, tris={tri.indices.Length / 3}). Scene saved.");
        }
    }
}
