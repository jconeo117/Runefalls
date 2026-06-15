using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using Runefall.Data;
using Runefall.Presentation.Enemies;
using Runefall.Presentation.Combat;

namespace Runefall.EditorTools
{
    /// <summary>
    /// One-shot: replace each exploration enemy's visual child (the SK_Mannequin "RPG-Character")
    /// with the model referenced by its EnemyData.prefab, preserving the exploration animation setup
    /// (ExplorationEnemyNew controller via humanoid retargeting + EnemyAnimationDriver + CombatPawnAnimator)
    /// and matching the old child's world height. Run with DungeonExploration open.
    /// </summary>
    public static class RunefallEnemyModelSwap
    {
        [MenuItem("Tools/Runefall/Swap Enemy Models To EnemyData")]
        public static void Swap()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var controllers = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int done = 0, skipped = 0;

            foreach (var ec in controllers)
            {
                var so   = new SerializedObject(ec);
                var data = so.FindProperty("_enemyData").objectReferenceValue as EnemyData;
                if (data == null)        { Debug.LogWarning($"[Swap] {ec.name}: no _enemyData — skipped"); skipped++; continue; }
                if (data.prefab == null) { Debug.LogWarning($"[Swap] {ec.name}: EnemyData '{data.name}' has no prefab — skipped"); skipped++; continue; }

                var oldAnim = ec.GetComponentInChildren<Animator>(true);
                if (oldAnim == null)     { Debug.LogWarning($"[Swap] {ec.name}: no Animator child — skipped"); skipped++; continue; }

                Transform oldChild   = oldAnim.transform;
                var       ctrl       = oldAnim.runtimeAnimatorController;
                bool      applyRoot  = oldAnim.applyRootMotion;
                var       cullMode   = oldAnim.cullingMode;
                float     oldHeight  = oldAnim.humanScale * oldChild.lossyScale.y;   // world height to match
                Vector3   localPos   = oldChild.localPosition;
                Quaternion localRot  = oldChild.localRotation;

                var oldDriver = oldChild.GetComponent<EnemyAnimationDriver>();
                var oldCPA    = oldChild.GetComponent<CombatPawnAnimator>();

                // Instantiate the matching model and detach it from the prefab so it behaves like the old child.
                var newGo = (GameObject)PrefabUtility.InstantiatePrefab(data.prefab, scene);
                PrefabUtility.UnpackPrefabInstance(newGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                newGo.name = data.prefab.name;
                newGo.transform.SetParent(ec.transform, false);
                newGo.transform.localPosition = localPos;
                newGo.transform.localRotation = localRot;

                var newAnim = newGo.GetComponent<Animator>() ?? newGo.AddComponent<Animator>();
                newAnim.runtimeAnimatorController = ctrl;          // humanoid clips retarget onto the model's own avatar
                newAnim.applyRootMotion = applyRoot;
                newAnim.cullingMode = cullMode;

                // Scale so the new model stands the same height as the mannequin it replaces.
                float ns = (newAnim.humanScale > 0.0001f && oldHeight > 0.0001f)
                    ? oldHeight / newAnim.humanScale
                    : newGo.transform.localScale.y;
                newGo.transform.localScale = new Vector3(ns, ns, ns);

                // Re-create the exploration driver (no scene refs) + combat animator (weaponBone refs go null — combat spawns its own pawn).
                if (oldDriver != null) { ComponentUtility.CopyComponent(oldDriver); ComponentUtility.PasteComponentAsNew(newGo); }
                else                   { newGo.AddComponent<EnemyAnimationDriver>(); }
                if (oldCPA != null)    { ComponentUtility.CopyComponent(oldCPA);    ComponentUtility.PasteComponentAsNew(newGo); }

                Object.DestroyImmediate(oldChild.gameObject);
                done++;
                Debug.Log($"[Swap] {ec.name}: {data.name} -> {newGo.name} (scale {ns:F2}, ctrl {(ctrl != null ? ctrl.name : "NULL")}, humanScale {newAnim.humanScale:F2})");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Swap] Done. swapped={done} skipped={skipped}");
        }
    }
}
