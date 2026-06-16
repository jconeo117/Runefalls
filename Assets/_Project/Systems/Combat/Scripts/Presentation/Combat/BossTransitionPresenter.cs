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

            // Advance the DOMAIN phase NOW: this increments CurrentPhase, swaps CurrentPhaseData
            // (stats + skills) to the next phase, and ARMS the completion callback that
            // CompleteTransitionCinematic() runs below (ResetStats + ClearAll). Nothing else calls this,
            // so without it the boss keeps the previous phase's stats/skills/AOC even though the
            // cinematic plays — exactly the "still phase 1 after dying" bug.
            boss.CheckPhaseTransition(null);

            if (_actorPawns.TryGetValue(boss, out var bossPawn))
            {
                PlayableAsset timeline = boss.GetTransitionTimeline(nextPhase);

                if (timeline != null)
                {
                    // The authored timeline drives anim + magic-circle VFX. We layer a cinematic
                    // CAMERA ORBIT + flash bursts on top (Camera.main, Cinemachine brain off), running
                    // for the timeline's duration so both finish together.
                    float dur = (timeline as TimelineAsset)?.duration is double d && d > 0.05 ? (float)d : 4f;
                    var orbit = _coroutineRunner.StartCoroutine(CinematicOrbit(bossPawn, dur));
                    yield return _coroutineRunner.StartCoroutine(PlayTransitionTimeline(timeline, bossPawn));
                    if (orbit != null) _coroutineRunner.StopCoroutine(orbit);
                    RestoreBrain();
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

        // Cinematic camera orbit around the boss with periodic flash bursts, layered over an authored
        // transition timeline. Disables the Cinemachine brain so it can pose Camera.main directly; the
        // brain is restored by RestoreBrain() once the timeline finishes.
        private IEnumerator CinematicOrbit(Transform bossPawn, float duration)
        {
            var cam = Camera.main;
            if (cam == null || bossPawn == null) yield break;

            var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false;

            // Initial angle/radius from where the combat camera already is, so the orbit eases out of it.
            Vector3 focus = bossPawn.position + Vector3.up * 1.4f;
            Vector3 off   = cam.transform.position - focus; off.y = 0f;
            float startAngle = Mathf.Atan2(off.z, off.x);
            float startRadius = Mathf.Clamp(off.magnitude, 3f, 7f);

            const float endRadius = 4.5f;
            const float baseHeight = 2.1f;
            const float sweepDeg = 300f;                // total orbit arc
            float sweepRad = sweepDeg * Mathf.Deg2Rad;

            float t = 0f;
            float nextFlash = 0.35f;
            // A brighter flash at the climax (~60% through, when the magic circle peaks).
            float climax = duration * 0.6f;
            bool climaxFlashed = false;

            while (t < duration)
            {
                t += Time.deltaTime;
                float k    = Mathf.Clamp01(t / duration);
                float ease = Mathf.SmoothStep(0f, 1f, k);

                focus = bossPawn.position + Vector3.up * 1.4f;   // re-read (boss may shift during getup)

                float angle  = startAngle + sweepRad * ease;
                // Dolly in toward the climax, then back out — adds drama.
                float radius = Mathf.Lerp(startRadius, endRadius, ease) * (1f - 0.20f * Mathf.Sin(k * Mathf.PI));
                float height = baseHeight + 0.7f * Mathf.Sin(k * Mathf.PI);

                Vector3 pos = focus + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius
                              + Vector3.up * height;
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.LookRotation(focus - pos);

                if (!climaxFlashed && t >= climax) { climaxFlashed = true; SpawnFlash(0.95f, 0.45f); }
                else if (t >= nextFlash)
                {
                    SpawnFlash(UnityEngine.Random.Range(0.4f, 0.7f), 0.22f);
                    nextFlash = t + UnityEngine.Random.Range(0.45f, 0.8f);
                }

                yield return null;
            }

            if (brain != null) brain.enabled = true;
        }

        private void RestoreBrain()
        {
            var cam = Camera.main;
            var brain = cam != null ? cam.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
            if (brain != null) brain.enabled = true;
        }

        private void SpawnFlash(float intensity, float dur)
        {
            var img = CreateFlashOverlay();
            if (img != null) _coroutineRunner.StartCoroutine(FadeFlash(img, intensity, dur));
        }

        private IEnumerator FadeFlash(UnityEngine.UI.Image img, float intensity, float dur)
        {
            float e = 0f;
            while (e < dur && img != null)
            {
                e += Time.deltaTime;
                img.color = new Color(1f, 1f, 1f, Mathf.Lerp(intensity, 0f, e / dur));
                yield return null;
            }
            if (img != null && img.canvas != null) UnityEngine.Object.Destroy(img.canvas.gameObject);
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
            Vector3 oldScale = oldPawn.localScale;   // keep the combat-normalized size across phases

            // 1. Destroy old pawn
            UnityEngine.Object.Destroy(oldPawn.gameObject);

            // 2. Spawn new phase's prefab
            GameObject prefab = boss.GetPhasePrefab(nextPhase);
            GameObject newPawnGo = prefab != null ? UnityEngine.Object.Instantiate(prefab, slotParent) : null;
            if (newPawnGo == null) return;

            newPawnGo.transform.position = oldPos;
            newPawnGo.transform.rotation = oldRot;
            newPawnGo.transform.localScale = oldScale;   // phases share the boss prefab → same scale

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
                // Apply the phase's combat AOC FIRST so InitFromEnemy unwraps it as the override source
                // (the arena assembler does the same at initial spawn). Without this, a per-phase
                // animatorController — e.g. Tharok's wizard AOC for phases 2/3 — would be ignored.
                if (boss.CurrentPhaseData.animatorController != null)
                {
                    var animator = newPawn.GetComponentInChildren<Animator>();
                    if (animator != null)
                        animator.runtimeAnimatorController = boss.CurrentPhaseData.animatorController;
                }
                anim.InitFromEnemy(boss.CurrentPhaseData, _combatBaseController);
            }
        }

        private static float EaseOutExpo(float t)
        {
            return t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
        }
    }
}
