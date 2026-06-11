using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Runefall.Combat;
using Runefall.Data;
using Runefall.Enemies;

namespace Runefall.Presentation.Combat
{
    public class BossTransitionPresenter
    {
        private readonly MonoBehaviour _coroutineRunner;
        private readonly IReadOnlyDictionary<ICombatActor, Transform> _actorPawns;
        private readonly IReadOnlyDictionary<ICombatActor, HPBarPresenter> _actorHPBars;
        private readonly IHudVisibilityPresenter _presenter;
        private readonly RuntimeAnimatorController _combatBaseController;
        private readonly ICameraDirector _cameraDirector;
        private readonly TimelineChoreographer _timelineChoreographer;
        private readonly Action<ICombatActor, Transform> _updatePawn;

        public BossTransitionPresenter(
            MonoBehaviour coroutineRunner,
            IReadOnlyDictionary<ICombatActor, Transform> actorPawns,
            IReadOnlyDictionary<ICombatActor, HPBarPresenter> actorHPBars,
            IHudVisibilityPresenter presenter,
            RuntimeAnimatorController baseController,
            ICameraDirector cameraDirector,
            TimelineChoreographer timelineChoreographer,
            Action<ICombatActor, Transform> updatePawn)
        {
            _coroutineRunner = coroutineRunner;
            _actorPawns = actorPawns;
            _actorHPBars = actorHPBars;
            _presenter = presenter;
            _combatBaseController = baseController;
            _cameraDirector = cameraDirector;
            _timelineChoreographer = timelineChoreographer;
            _updatePawn = updatePawn;
        }

        public IEnumerator RunBossPhaseTransition(IMultiPhaseActor boss)
        {
            if (boss == null) yield break;

            // 1. Hide HUD
            if (_presenter != null)
            {
                _presenter.HideAllUI();
            }

            // 2. Play transition cinematic
            int nextPhase = boss.CurrentPhase + 1;
            
            if (_actorPawns.TryGetValue(boss, out var bossPawn))
            {
                PlayableAsset timeline = boss.GetTransitionTimeline(nextPhase);

                if (timeline != null)
                {
                    yield return _coroutineRunner.StartCoroutine(PlayTransitionTimeline(timeline, bossPawn));
                }
                else
                {
                    yield return _coroutineRunner.StartCoroutine(PlayProgrammaticTransitionCinematic(boss, bossPawn, nextPhase));
                }
            }

            // 3. Complete the transition in domain layer
            boss.CompleteTransitionCinematic();

            // 4. Update the visual of the boss
            UpdateBossVisualsForPhase(boss, nextPhase);

            // 5. Restore HUD visibility
            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator PlayTransitionTimeline(PlayableAsset timeline, Transform bossPawn)
        {
            if (_timelineChoreographer == null) yield break;
            yield return _coroutineRunner.StartCoroutine(_timelineChoreographer.PlayTransitionTimeline(timeline, bossPawn));
        }

        private IEnumerator PlayProgrammaticTransitionCinematic(IMultiPhaseActor boss, Transform bossPawn, int nextPhase)
        {
            var camera = Camera.main;
            var brain = camera != null ? camera.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
            if (brain != null) brain.enabled = false;

            Vector3 startCamPos = camera != null ? camera.transform.position : Vector3.zero;
            Quaternion startCamRot = camera != null ? camera.transform.rotation : Quaternion.identity;

            Vector3 bossChest = bossPawn.position + Vector3.up * 1.5f;
            Vector3 targetCamPos = bossPawn.position + bossPawn.forward * 4.0f + Vector3.up * 2.2f;
            Quaternion targetCamRot = Quaternion.LookRotation(bossChest - targetCamPos);

            // 1. Zoom in on boss
            float elapsed = 0f;
            float duration = 0.8f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = EaseOutExpo(t);
                if (camera != null)
                {
                    camera.transform.position = Vector3.Lerp(startCamPos, targetCamPos, ease);
                    camera.transform.rotation = Quaternion.Slerp(startCamRot, targetCamRot, ease);
                }
                yield return null;
            }

            // 2. Play spell/roar animation
            var animator = bossPawn.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.CrossFadeInFixedTime("Spell", 0.15f);
            }

            // 3. Camera shake & Screen Flash
            if (_cameraDirector != null)
            {
                yield return _coroutineRunner.StartCoroutine(_cameraDirector.PerformCameraShake(1.5f, 0.35f));
            }

            var flashOverlay = CreateFlashOverlay();
            if (flashOverlay != null)
            {
                flashOverlay.color = Color.white;
                float flashElapsed = 0f;
                float flashDuration = 0.6f;
                while (flashElapsed < flashDuration)
                {
                    flashElapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(flashElapsed / flashDuration);
                    flashOverlay.color = new Color(1f, 1f, 1f, 1f - t);
                    yield return null;
                }
                UnityEngine.Object.Destroy(flashOverlay.canvas.gameObject);
            }

            // 4. Return camera to original combat view
            elapsed = 0f;
            duration = 0.6f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = EaseOutExpo(t);
                if (camera != null)
                {
                    camera.transform.position = Vector3.Lerp(targetCamPos, startCamPos, ease);
                    camera.transform.rotation = Quaternion.Slerp(targetCamRot, startCamRot, ease);
                }
                yield return null;
            }

            if (brain != null) brain.enabled = true;
        }

        private UnityEngine.UI.Image CreateFlashOverlay()
        {
            var canvasGO = new GameObject("BossTransitionFlashCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            
            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            var flashGO = new GameObject("Flash");
            flashGO.transform.SetParent(canvasGO.transform, false);
            var rt = flashGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = flashGO.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            return img;
        }

        private void UpdateBossVisualsForPhase(IMultiPhaseActor boss, int nextPhase)
        {
            Transform oldPawn = null;
            if (_actorPawns != null)
            {
                _actorPawns.TryGetValue(boss, out oldPawn);
            }
            if (oldPawn == null) return;

            Transform slotParent = oldPawn.parent;
            Vector3 oldPos = oldPawn.position;
            Quaternion oldRot = oldPawn.rotation;

            // 1. Destroy old pawn
            UnityEngine.Object.Destroy(oldPawn.gameObject);

            // 2. Spawn new phase's prefab
            GameObject prefab = boss.GetPhasePrefab(nextPhase);
            GameObject newPawnGo = prefab != null ? UnityEngine.Object.Instantiate(prefab, slotParent) : null;
            if (newPawnGo == null) return;

            newPawnGo.transform.position = oldPos;
            newPawnGo.transform.rotation = oldRot;

            Transform newPawn = newPawnGo.transform;

            // 3. Update dictionary reference via callback
            _updatePawn?.Invoke(boss, newPawn);

            // 4. Update HPBar target
            if (_actorHPBars != null && _actorHPBars.TryGetValue(boss, out var hpBar))
            {
                float yOffset = 2.0f;
                var slot = newPawn.GetComponent<EnemySlot>();
                if (slot != null)
                {
                    yOffset = slot.hpBarOffset;
                }
                hpBar.SetFollow(newPawn, Vector3.up * yOffset);
                hpBar.ForceRefresh();
            }

            // 5. Initialize CombatPawnAnimator
            var anim = newPawn.GetComponentInChildren<CombatPawnAnimator>();
            if (anim != null && boss.CurrentPhaseData != null)
            {
                anim.InitFromEnemy(boss.CurrentPhaseData, _combatBaseController);
            }
        }

        private static float EaseOutExpo(float t)
        {
            return t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
        }
    }
}
