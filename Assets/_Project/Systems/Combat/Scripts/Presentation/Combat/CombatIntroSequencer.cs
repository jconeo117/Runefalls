using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Plays the combat intro before TurnManager.StartCombat is called.
    /// Hides the gameplay HUD, plays either a Timeline/Cinemachine sequence or a logical camera lerp,
    /// and triggers onComplete to start active gameplay.
    /// </summary>
    public class CombatIntroSequencer : MonoBehaviour
    {
        [Header("Timeline (Dynamic/Cinematic)")]
        [Tooltip("Optional. PlayableDirector that plays the cinematic intro timeline.")]
        public PlayableDirector playableDirector;

        [Header("Camera (Logical Fallback)")]
        public CombatCameraController cameraController;

        [Header("Intro Anchors (Logical Fallback)")]
        [Tooltip("Empty GO placed in scene facing the enemy team. If null, falls back to gameplay player anchor.")]
        public Transform introEnemyAnchor;
        [Tooltip("Empty GO placed in scene facing the player team. If null, falls back to gameplay enemy anchor.")]
        public Transform introPlayerAnchor;

        [Header("Camera Animation (Logical Fallback)")]
        [Tooltip("Units above anchor the camera starts — LateUpdate lerps it down during reveal.")]
        public float dropHeight = 1.0f;
        [Tooltip("Lerp speed during intro settle. Lower = slower/weightier. Restored after intro.")]
        public float introLerpSpeed = 2.0f;

        [Header("Timing (Logical Fallback)")]
        [Tooltip("Seconds the camera holds on the enemy team.")]
        public float enemyRevealDuration = 1.2f;
        [Tooltip("Seconds after camera snaps to player side before combat begins.")]
        public float playerRevealDuration = 1.0f;

        private Action _onCompleteCallback;
        private CombatPresenterBase _hudPresenter;
        private bool _isCompleted;

        public void Run(Action onComplete)
        {
            _onCompleteCallback = onComplete;
            _isCompleted = false;

            // 1. Locate and hide the active gameplay HUD presenter so it doesn't clutter the cinematic intro
            _hudPresenter = FindFirstObjectByType<CombatPresenterBase>();
            if (_hudPresenter == null)
            {
                #pragma warning disable CS0618
                _hudPresenter = FindObjectOfType<CombatPresenterBase>();
                #pragma warning restore CS0618
            }

            if (_hudPresenter != null)
            {
                _hudPresenter.HideAllUI();
            }

            // 2. Play using the dynamic timeline if available, otherwise run the code-driven fallback camera sweep
            if (playableDirector != null && playableDirector.playableAsset != null)
            {
                StartCoroutine(PlayTimelineSequence());
            }
            else
            {
                StartCoroutine(PlayFallbackSequence());
            }
        }

        /// <summary>
        /// Public entry point for Timeline Signals or script markers to finish the intro sequence exactly when desired.
        /// </summary>
        public void CompleteIntro()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            // Stop the director if it is still playing
            if (playableDirector != null && playableDirector.state == PlayState.Playing)
            {
                playableDirector.Stop();
            }

            // Restore the gameplay HUD
            if (_hudPresenter != null)
            {
                _hudPresenter.ShowAllUI();
            }

            _onCompleteCallback?.Invoke();
        }

        private IEnumerator PlayTimelineSequence()
        {
            // Reset state
            playableDirector.time = 0;
            playableDirector.Evaluate();

            // Set up stopped callback as a fallback
            playableDirector.stopped += OnDirectorStopped;

            playableDirector.Play();

            // Safety net fallback: if for some reason the timeline stopped event or the CompleteIntro signal
            // isn't fired, we complete after the timeline duration has passed plus a small safety margin.
            float duration = (float)playableDirector.duration;
            float elapsed = 0f;
            float safetyMargin = 0.5f;

            while (elapsed < duration + safetyMargin && !_isCompleted)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!_isCompleted)
            {
                CompleteIntro();
            }
        }

        private void OnDirectorStopped(PlayableDirector director)
        {
            if (playableDirector != null)
            {
                playableDirector.stopped -= OnDirectorStopped;
            }
            CompleteIntro();
        }

        private IEnumerator PlayFallbackSequence()
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

            CompleteIntro();
        }

        private void OnDestroy()
        {
            if (playableDirector != null)
            {
                playableDirector.stopped -= OnDirectorStopped;
            }
        }
    }
}
