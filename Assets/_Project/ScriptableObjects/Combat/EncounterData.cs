using UnityEngine;
using Runefall.Data;

namespace Runefall.Combat
{
    [System.Serializable]
    public class EncounterData
    {
        public EnemyData enemyData;
        public int enemyLevel;
        public float combatClass;      // cacheado desde enemyData.combatClass al inicio del encuentro
        public Transform enemyTransform; // cámara de combate lo apunta
    }
}
