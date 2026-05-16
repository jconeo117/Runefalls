using System;
using System.Collections;
using UnityEngine;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Plays the combat intro before TurnManager.StartCombat is called.
    /// Sequence: snap to enemy side → hold → lerp to player side → hold → onComplete.
    /// Wire via CombatBootstrapper: assign in Inspector, bootstrapper defers StartCombat.
    /// </summary>
    public class CombatIntroSequencer : MonoBehaviour
    {
        [Header("Camera")]
        public CombatCameraController cameraController;

        [Header("Intro Anchors")]
        [Tooltip("Empty GO placed in scene facing the enemy team. If null, falls back to gameplay player anchor.")]
        public Transform introEnemyAnchor;
        [Tooltip("Empty GO placed in scene facing the player team. If null, falls back to gameplay enemy anchor.")]
        public Transform introPlayerAnchor;

        [Header("Camera Animation")]
        [Tooltip("Units above anchor the camera starts — LateUpdate lerps it down during reveal.")]
        public float dropHeight = 1.0f;
        [Tooltip("Lerp speed during intro settle. Lower = slower/weightier. Restored after intro.")]
        public float introLerpSpeed = 2.0f;

        [Header("Timing")]
        [Tooltip("Seconds the camera holds on the enemy team.")]
        public float enemyRevealDuration = 1.2f;
        [Tooltip("Seconds after camera snaps to player side before combat begins.")]
        public float playerRevealDuration = 1.0f;

        public void Run(Action onComplete)
        {
            StartCoroutine(PlaySequence(onComplete));
        }

        private IEnumerator PlaySequence(Action onComplete)
        {
            if (cameraController != null)
            {
                float originalLerpSpeed = cameraController.lerpSpeed;
                cameraController.lerpSpeed = introLerpSpeed;

                var drop = Vector3.up * dropHeight;

                var enemyAnchor = introEnemyAnchor;
                var playerAnchor = introPlayerAnchor;

                if (enemyAnchor != null)
                    cameraController.SnapToWithOffset(enemyAnchor, drop);
                else
                    cameraController.SnapToAnchor(enemySide: false);

                yield return new WaitForSeconds(enemyRevealDuration);

                if (playerAnchor != null)
                    cameraController.SnapToWithOffset(playerAnchor, drop);
                else
                    cameraController.SnapToAnchor(enemySide: true);

                yield return new WaitForSeconds(playerRevealDuration);

                cameraController.lerpSpeed = originalLerpSpeed;
                cameraController.SnapToAnchorWithOffset(enemySide: false, drop);
            }

            onComplete?.Invoke();
        }
    }
}
