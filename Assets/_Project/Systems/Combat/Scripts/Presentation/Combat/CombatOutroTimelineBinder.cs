using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Rendering;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Handles the dynamic track binding and scheduling of Signal emitters on the victory timeline.
    /// </summary>
    public class CombatOutroTimelineBinder
    {
        private readonly MonoBehaviour _runner;

        public CombatOutroTimelineBinder(MonoBehaviour runner)
        {
            _runner = runner;
        }

        public void BindTracks(
            PlayableDirector director,
            Animator playerAnimator,
            Unity.Cinemachine.CinemachineBrain brain,
            GameObject winScreenInstance,
            GameObject flashOverlayInstance,
            List<Unity.Cinemachine.CinemachineCamera> dynamicVcams,
            CombatPresenterBase hudPresenter)
        {
            if (director == null || director.playableAsset == null) return;

            var timeline = director.playableAsset as TimelineAsset;
            if (timeline == null) return;

            var hudGO = hudPresenter?.gameObject;

            Unity.Cinemachine.CinemachineCamera enemyVcam = dynamicVcams.Count > 0 ? dynamicVcams[0] : null;
            Unity.Cinemachine.CinemachineCamera startVcam = dynamicVcams.Count > 1 ? dynamicVcams[1] : null;
            Unity.Cinemachine.CinemachineCamera endVcam = dynamicVcams.Count > 2 ? dynamicVcams[2] : null;

            GameObject flashOverlayGO = flashOverlayInstance != null ? flashOverlayInstance.transform.Find("FlashOverlay")?.gameObject : null;

            foreach (var output in timeline.outputs)
            {
                // A. Cinemachine Track
                if (output.outputTargetType == typeof(Unity.Cinemachine.CinemachineBrain))
                {
                    if (brain != null)
                    {
                        director.SetGenericBinding(output.sourceObject, brain);
                        Debug.Log($"[CombatOutroTimelineBinder] Bound CinemachineBrain to track: {output.streamName}");
                    }

                    if (output.sourceObject is Unity.Cinemachine.CinemachineTrack cinemachineTrack)
                    {
                        var clips = cinemachineTrack.GetClips();
                        int clipIdx = 0;
                        foreach (var clip in clips)
                        {
                            var shot = clip.asset as Unity.Cinemachine.CinemachineShot;
                            if (shot != null)
                            {
                                string clipName = clip.displayName.ToLower();
                                if (clipName.Contains("enemy"))
                                {
                                    if (enemyVcam != null)
                                        director.SetReferenceValue(shot.VirtualCamera.exposedName, enemyVcam);
                                }
                                else if (clipName.Contains("start") || clipName.Contains("1"))
                                {
                                    if (startVcam != null)
                                        director.SetReferenceValue(shot.VirtualCamera.exposedName, startVcam);
                                }
                                else if (clipName.Contains("end") || clipName.Contains("2"))
                                {
                                    if (endVcam != null)
                                        director.SetReferenceValue(shot.VirtualCamera.exposedName, endVcam);
                                }
                                else
                                {
                                    if (clipIdx == 0 && enemyVcam != null)
                                        director.SetReferenceValue(shot.VirtualCamera.exposedName, enemyVcam);
                                    else if (clipIdx == 1 && startVcam != null)
                                        director.SetReferenceValue(shot.VirtualCamera.exposedName, startVcam);
                                    else if (clipIdx == 2 && endVcam != null)
                                        director.SetReferenceValue(shot.VirtualCamera.exposedName, endVcam);
                                }
                            }
                            clipIdx++;
                        }
                    }
                }
                // B. Animator Track (Players)
                else if (output.outputTargetType == typeof(Animator))
                {
                    string name = output.streamName.ToLower();
                    if (name.Contains("player") || name.Contains("hero") || name.Contains("character"))
                    {
                        if (playerAnimator != null)
                        {
                            director.SetGenericBinding(output.sourceObject, playerAnimator);
                            Debug.Log($"[CombatOutroTimelineBinder] Bound Player Animator to track: {output.streamName}");
                        }
                    }
                }
                // C. Activation Track
                else if (output.outputTargetType == typeof(GameObject))
                {
                    string name = output.streamName.ToLower();
                    if (name.Contains("hud") || name.Contains("ui"))
                    {
                        if (hudGO != null)
                        {
                            director.SetGenericBinding(output.sourceObject, hudGO);
                            Debug.Log($"[CombatOutroTimelineBinder] Bound HUD GameObject to activation track: {output.streamName}");
                        }
                    }
                    else if (name.Contains("victory") || name.Contains("winscreen") || name.Contains("win"))
                    {
                        if (winScreenInstance != null)
                        {
                            director.SetGenericBinding(output.sourceObject, winScreenInstance);
                            Debug.Log($"[CombatOutroTimelineBinder] Bound WinScreen GameObject to activation track: {output.streamName}");
                        }
                    }
                    else if (name.Contains("flash") || name.Contains("white") || name.Contains("screenflash"))
                    {
                        if (flashOverlayGO != null)
                        {
                            director.SetGenericBinding(output.sourceObject, flashOverlayGO);
                            Debug.Log($"[CombatOutroTimelineBinder] Bound FlashOverlay GameObject to activation track: {output.streamName}");
                        }
                    }
                }
                // D. Volume Track (Post Processing)
                else if (output.outputTargetType == typeof(Volume))
                {
                    var volume = UnityEngine.Object.FindFirstObjectByType<Volume>();
                    if (volume == null)
                    {
                        #pragma warning disable CS0618
                        volume = UnityEngine.Object.FindObjectOfType<Volume>();
                        #pragma warning restore CS0618
                    }

                    if (volume != null)
                    {
                        director.SetGenericBinding(output.sourceObject, volume);
                        Debug.Log($"[CombatOutroTimelineBinder] Bound Post-Processing Volume to track: {output.streamName}");

                        var volGO = volume.gameObject;
                        if (volGO.GetComponent<Animator>() == null)
                        {
                            volGO.AddComponent<Animator>();
                        }
                    }
                }
            }
        }

        public void SetupRuntimeSignals(
            PlayableDirector director,
            Func<string, UnityEngine.Events.UnityAction> actionResolver,
            Func<string, float> beatTimeResolver)
        {
            if (director == null || director.playableAsset == null) return;

            var timeline = director.playableAsset as TimelineAsset;
            if (timeline == null) return;

            SignalTrack signalTrack = null;
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is SignalTrack sigTrack)
                {
                    signalTrack = sigTrack;
                    break;
                }
            }

            if (signalTrack == null)
            {
                Debug.LogWarning("[CombatOutroTimelineBinder] No SignalTrack found on the outro timeline — outro emitters will not fire.");
                return;
            }

            int scheduled = 0;
            foreach (var marker in signalTrack.GetMarkers())
            {
                if (!(marker is SignalEmitter emitter) || emitter.asset == null) continue;

                UnityEngine.Events.UnityAction action = actionResolver(emitter.asset.name);
                if (action == null)
                {
                    Debug.LogWarning($"[CombatOutroTimelineBinder] Unmapped outro signal '{emitter.asset.name}' — skipped.");
                    continue;
                }

                float delay = beatTimeResolver(emitter.asset.name);
                _runner.StartCoroutine(FireOutroBeat(delay, emitter.asset.name, action));
                scheduled++;
            }

            Debug.Log($"[CombatOutroTimelineBinder] Scheduled {scheduled} authored outro beat(s) from the Signal Track.");
        }

        private IEnumerator FireOutroBeat(float delay, string signalName, UnityEngine.Events.UnityAction action)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            Debug.Log($"[CombatOutroTimelineBinder] Outro beat: {signalName} @ {delay:0.###}s");
            action.Invoke();
        }
    }
}
