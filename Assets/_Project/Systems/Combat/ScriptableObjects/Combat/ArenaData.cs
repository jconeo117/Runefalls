using UnityEngine;

namespace Runefall.Data
{
    [CreateAssetMenu(menuName = "Runefall/Combat/ArenaData")]
    public class ArenaData : ScriptableObject
    {
        [Header("Environment")]
        public GameObject environmentPrefab;

        [Header("Layout Prefab")]
        [Tooltip("Prefab with empty GOs relative to arena center. " +
                 "Expected children: PlayerLine, EnemyLine, CameraGameplay, CamIntro_Enemy, CamIntro_Player.")]
        public GameObject layoutPrefab;

        [Header("Slot Config")]
        [Min(0.5f)] public float slotSpacing = 2f;
        [Min(1)] public int maxPlayerSlots = 4;
        [Min(1)] public int maxEnemySlots  = 4;
    }
}
