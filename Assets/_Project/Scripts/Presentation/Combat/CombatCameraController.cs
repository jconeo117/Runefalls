using UnityEngine;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Orbits the camera between two positions around the field center.
    /// Player turn  → behind the player team  (Edit-Mode camera position).
    /// Enemy turn   → behind the enemy team   (mirror of player position through field center).
    /// Both positions always look at the field center.
    ///
    /// Call InitFromTeams() once. Wire OnPlayerTurnStarted / OnEnemyTurnStarted
    /// to the corresponding TurnManager events via CombatBootstrapper.
    /// </summary>
    public class CombatCameraController : MonoBehaviour
    {
        [Header("Blend")]
        [Range(0.5f, 10f)] public float lerpSpeed = 4f;

        private Transform   _playerAnchor;
        private Transform   _enemyAnchor;
        private Transform   _target;

        private void LateUpdate()
        {
            if (_target == null) return;
            transform.position = Vector3.Lerp(
                transform.position, _target.position, lerpSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, _target.rotation, lerpSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Computes the two camera anchors from the Edit-Mode camera transform and team positions.
        /// playerAnchor = current transform (Edit-Mode position + rotation, preserved exactly).
        /// enemyAnchor  = mirror of playerAnchor through fieldCenter, looking back at fieldCenter.
        /// </summary>
        public void InitFromTeams(Transform playerRoot, Transform enemyRoot)
        {
            if (playerRoot == null || enemyRoot == null) return;

            Vector3 fieldCenter  = (playerRoot.position + enemyRoot.position) * 0.5f;
            Vector3 playerCamPos = transform.position;

            // Mirror XZ through field center — preserve Y so camera stays above ground.
            Vector3 enemyCamPos = new Vector3(
                2f * fieldCenter.x - playerCamPos.x,
                playerCamPos.y,
                2f * fieldCenter.z - playerCamPos.z);

            if (_playerAnchor != null) Destroy(_playerAnchor.gameObject);
            if (_enemyAnchor  != null) Destroy(_enemyAnchor.gameObject);

            _playerAnchor = CreateAnchor("PlayerSide", playerCamPos, fieldCenter, transform.rotation);
            _enemyAnchor  = CreateAnchor("EnemySide",  enemyCamPos,  fieldCenter);

            _target = _playerAnchor;
        }

        public void OnPlayerTurnStarted(int round) => _target = _playerAnchor;

        public void OnEnemyTurnStarted() => _target = _enemyAnchor;

        /// <summary>Instantly snaps camera to the anchor and keeps it as lerp target.</summary>
        public void SnapToAnchor(bool enemySide)
        {
            _target = enemySide ? _enemyAnchor : _playerAnchor;
            if (_target == null) return;
            transform.position = _target.position;
            transform.rotation = _target.rotation;
        }

        /// <summary>Snaps to gameplay anchor + worldOffset; LateUpdate lerps down — cinematic settle.</summary>
        public void SnapToAnchorWithOffset(bool enemySide, Vector3 worldOffset)
        {
            _target = enemySide ? _enemyAnchor : _playerAnchor;
            if (_target == null) return;
            transform.position = _target.position + worldOffset;
            transform.rotation = _target.rotation;
        }

        /// <summary>Sets any transform as the lerp target (intro custom anchors).</summary>
        public void SetTarget(Transform t) => _target = t;

        /// <summary>Instantly snaps to any transform and sets it as lerp target.</summary>
        public void SnapTo(Transform t)
        {
            _target = t;
            if (t == null) return;
            transform.position = t.position;
            transform.rotation = t.rotation;
        }

        /// <summary>
        /// Snaps camera to anchor + worldOffset, sets anchor as lerp target.
        /// LateUpdate lerps from the offset position down to the anchor — cinematic settle.
        /// </summary>
        public void SnapToWithOffset(Transform anchor, Vector3 worldOffset)
        {
            _target = anchor;
            if (anchor == null) return;
            transform.position = anchor.position + worldOffset;
            transform.rotation = anchor.rotation;
        }

        private void OnDestroy()
        {
            if (_playerAnchor != null) Destroy(_playerAnchor.gameObject);
            if (_enemyAnchor  != null) Destroy(_enemyAnchor.gameObject);
        }

        private static Transform CreateAnchor(string anchorName, Vector3 position,
            Vector3 lookAtPoint, Quaternion? overrideRotation = null)
        {
            var go = new GameObject($"CamAnchor_{anchorName}");
            go.transform.position = position;
            go.transform.rotation = overrideRotation ?? Quaternion.LookRotation(
                (lookAtPoint - position).sqrMagnitude > 0.0001f
                    ? lookAtPoint - position
                    : Vector3.forward);
            return go.transform;
        }
    }
}
