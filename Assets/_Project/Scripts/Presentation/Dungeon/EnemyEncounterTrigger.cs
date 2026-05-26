using UnityEngine;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Dungeon
{
    [RequireComponent(typeof(SphereCollider))]
    public class EnemyEncounterTrigger : MonoBehaviour
    {
        [SerializeField] private EnemyData           _enemyData;
        [SerializeField] private EncounterReadyEvent _encounterReadyEvent;
        [SerializeField] private int                 _enemyLevel = 1;

        private SphereCollider _sphere;
        private bool           _triggered;

        private void Awake()
        {
            _sphere           = GetComponent<SphereCollider>();
            _sphere.isTrigger = true;
            if (_enemyData != null)
                _sphere.radius = _enemyData.encounterRange;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered || !other.CompareTag("Player")) return;
            if (_enemyData == null || _encounterReadyEvent == null) return;

            _triggered = true;
            _encounterReadyEvent.Raise(new EncounterData
            {
                enemyData      = _enemyData,
                enemyLevel     = _enemyLevel,
                combatClass    = _enemyData.combatClass,
                enemyTransform = transform
            });
        }

        // Called by EnemyData.respawnsOnRoomExit flow or after combat ends
        public void ResetTrigger() => _triggered = false;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_enemyData == null) return;
            // Detection range — orange, large
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.15f);
            Gizmos.DrawSphere(transform.position, _enemyData.detectionRange);
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _enemyData.detectionRange);
            // Encounter range — yellow, contact zone
            Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.25f);
            Gizmos.DrawSphere(transform.position, _enemyData.encounterRange);
            Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, _enemyData.encounterRange);
        }
#endif
    }
}
