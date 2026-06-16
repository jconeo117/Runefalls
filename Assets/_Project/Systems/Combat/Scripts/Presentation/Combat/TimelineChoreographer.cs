using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;

namespace Runefall.Presentation.Combat
{
    public class TimelineChoreographer
    {
        private readonly Transform _parent;
        private readonly List<CinemachineCamera> _skillVcams = new();
        private PlayableDirector _skillTimelineDirector;

        public TimelineChoreographer(Transform parent, PlayableDirector director = null)
        {
            _parent = parent;
            _skillTimelineDirector = director;
        }

        public PlayableDirector ResolveSkillTimelineDirector()
        {
            if (_skillTimelineDirector != null) return _skillTimelineDirector;
            
            var go = new GameObject("SkillTimelineDirector");
            go.transform.SetParent(_parent);
            _skillTimelineDirector = go.AddComponent<PlayableDirector>();
            _skillTimelineDirector.playOnAwake = false;
            return _skillTimelineDirector;
        }

        public IEnumerator PlayTransitionTimeline(PlayableAsset timeline, Transform bossPawn)
        {
            var director = ResolveSkillTimelineDirector();
            director.playableAsset = timeline;

            var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            var timelineAsset = timeline as TimelineAsset;
            if (timelineAsset != null)
            {
                foreach (var output in timelineAsset.outputs)
                {
                    if (output.streamName == "Cinemachine Track" && brain != null)
                    {
                        director.SetGenericBinding(output.sourceObject, brain);
                    }
                    // Bind the boss Animator to the transition's animation track. Accept the default
                    // "Animation Track" name OR any track whose name contains "caster" (same convention
                    // as the skill timelines), so the death/getup choreography plays on the boss.
                    else if (output.streamName == "Animation Track"
                             || (output.streamName != null && output.streamName.ToLower().Contains("caster")))
                    {
                        var bossAnimator = bossPawn.GetComponentInChildren<Animator>();
                        if (bossAnimator != null)
                            director.SetGenericBinding(output.sourceObject, bossAnimator);
                    }
                    // Audio Track → needs an AudioSource bound so authored SFX clips play. Control Tracks
                    // (VFX prefabs) are self-contained and need no binding. Get/add an AudioSource on the
                    // boss pawn so the transition's choreographed sounds come from the boss.
                    else if (output.outputTargetType == typeof(AudioSource) && bossPawn != null)
                    {
                        var src = bossPawn.GetComponentInChildren<AudioSource>();
                        if (src == null) src = bossPawn.gameObject.AddComponent<AudioSource>();
                        director.SetGenericBinding(output.sourceObject, src);
                    }
                }
            }

            director.time = 0;
            director.Evaluate();
            director.Play();

            bool stopped = false;
            Action<PlayableDirector> onStopped = null;
            onStopped = d =>
            {
                if (d == director)
                {
                    stopped = true;
                    director.stopped -= onStopped;
                }
            };
            director.stopped += onStopped;

            while (!stopped && director.state == PlayState.Playing)
            {
                yield return null;
            }
        }

        public void BindSkillTimelineTracks(PlayableDirector director, int rank, Animator casterAnimator, CinemachineBrain brain)
        {
            if (!(director.playableAsset is TimelineAsset timeline)) return;

            string activeRankKey = rank <= 1 ? "bronze" : (rank == 2 ? "silver" : "gold");

            foreach (var track in timeline.GetOutputTracks())
            {
                // Per-rank camera: Cinemachine tracks named by rank; only the active rank stays unmuted.
                if (track is CinemachineTrack cmTrack)
                {
                    string n = cmTrack.name.ToLower();
                    bool isRankTrack = n.Contains("bronze") || n.Contains("silver") || n.Contains("gold");
                    cmTrack.muted = isRankTrack && !n.Contains(activeRankKey);

                    if (!cmTrack.muted)
                    {
                        if (brain != null) director.SetGenericBinding(cmTrack, brain);
                        BindCinemachineShots(director, cmTrack);
                    }
                    else
                    {
                        director.SetGenericBinding(cmTrack, null);
                    }
                    continue;
                }

                // Caster animation track.
                if (track is AnimationTrack animTrack && casterAnimator != null
                    && animTrack.name.ToLower().Contains("caster"))
                {
                    director.SetGenericBinding(animTrack, casterAnimator);
                }
            }
        }

        private void BindCinemachineShots(PlayableDirector director, CinemachineTrack cmTrack)
        {
            foreach (var clip in cmTrack.GetClips())
            {
                if (!(clip.asset is CinemachineShot shot)) continue;
                var vcam = FindSkillVcam(shot.VirtualCamera.exposedName.ToString());
                if (vcam != null)
                    director.SetReferenceValue(shot.VirtualCamera.exposedName, vcam);
            }
        }

        public void CreateSkillVcams(Transform caster, Vector3 targetPos)
        {
            if (caster == null) return;
            CleanupSkillVcams();

            Vector3 casterPos = caster.position;
            Vector3 dir = targetPos - casterPos; dir.y = 0f;
            if (targetPos == Vector3.zero || dir.sqrMagnitude < 1.0f) 
                dir = caster.forward;
            dir.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Vector3 mid = (casterPos + targetPos) * 0.5f;

            _skillVcams.Add(MakeSkillVcam("cam_caster", casterPos - dir * 2.0f + Vector3.up * 1.8f, targetPos + Vector3.up * 1.2f));
            _skillVcams.Add(MakeSkillVcam("cam_target", targetPos + dir * 2.2f + Vector3.up * 1.6f, targetPos + Vector3.up * 1.2f));
            _skillVcams.Add(MakeSkillVcam("cam_side", mid + right * 4.0f + Vector3.up * 1.8f, mid + Vector3.up * 1.2f));
        }

        private CinemachineCamera MakeSkillVcam(string name, Vector3 pos, Vector3 lookAt)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_parent);
            go.transform.position = pos;
            go.transform.LookAt(lookAt);
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Priority = 5;
            return vcam;
        }

        private CinemachineCamera FindSkillVcam(string exposedName)
        {
            foreach (var v in _skillVcams)
                if (v != null && v.gameObject.name == exposedName) return v;
            return null;
        }

        public void CleanupSkillVcams()
        {
            foreach (var v in _skillVcams)
                if (v != null) UnityEngine.Object.Destroy(v.gameObject);
            _skillVcams.Clear();
        }
    }
}
