using UnityEngine;
using Runefall.Data;

namespace Runefall.Combat
{
    [System.Serializable]
    public class EncounterData
    {
        public EnemyData enemyData;
        public int enemyLevel;
        public int approximatePower;   // enemyData.stats * levelMultiplier
        public Transform enemyTransform; // cámara de combate lo apunta
    }
}
