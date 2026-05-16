using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    [RequireComponent(typeof(Animator))]
    public class CombatPawnAnimator : MonoBehaviour
    {
        [Header("Blend")]
        [Tooltip("Cross-fade duration between consecutive attack clips (seconds).")]
        [Range(0f, 0.4f)]
        [SerializeField] private float _blendDuration = 0.12f;

        public float BlendDuration => _blendDuration;

        [Header("VFX")]
        [Tooltip("Bone where weapon / slash VFX spawns. Assign the hand or weapon socket bone in the Inspector.")]
        public Transform weaponBone;
        [Tooltip("Empty GO on the weapon aligned with the blade edge. If assigned, VFX uses this transform instead of weaponBone — no rotation offset needed.")]
        public Transform bladeRoot;

        private Animator _animator;

        private static readonly int IdleHash     = Animator.StringToHash("Idle");
        private static readonly int ApproachHash = Animator.StringToHash("Approach");
        private static readonly int HitHash      = Animator.StringToHash("Hit");
        private static readonly int DeathHash    = Animator.StringToHash("Death");

        private const string KeyApproach = "Placeholder_Approach";
        private const string KeyHit      = "Placeholder_Hit";
        private const string KeyDeath    = "Placeholder_Death";

        private PlayableGraph _skillGraph;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _animator.applyRootMotion = false;
        }

        private void OnDestroy()
        {
            if (_skillGraph.IsValid()) _skillGraph.Destroy();
        }

        public void InitFromCharacter(CharacterData cd, RuntimeAnimatorController baseController)
        {
            ApplyOverrides(baseController, new Dictionary<string, AnimationClip>
            {
                { KeyApproach, cd.animApproach },
                { KeyHit,      cd.animGetHit   },
                { KeyDeath,    cd.animDeath    },
            });
        }

        public void InitFromEnemy(EnemyData ed, RuntimeAnimatorController baseController)
        {
            ApplyOverrides(baseController, new Dictionary<string, AnimationClip>
            {
                { KeyApproach, ed.animApproach },
                { KeyHit,      ed.animGetHit   },
                { KeyDeath,    ed.animDeath    },
            });
        }

        private void ApplyOverrides(RuntimeAnimatorController baseController, Dictionary<string, AnimationClip> clips)
        {
            if (baseController == null) return;

            var oc    = new AnimatorOverrideController(baseController);
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(oc.overridesCount);
            oc.GetOverrides(pairs);

            for (int i = 0; i < pairs.Count; i++)
            {
                var key = pairs[i].Key;
                if (key == null) continue;
                if (clips.TryGetValue(key.name, out var replacement) && replacement != null)
                    pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(key, replacement);
            }

            oc.ApplyOverrides(pairs);
            _animator.runtimeAnimatorController = oc;
        }

        // ── Animation Event hooks ─────────────────────────────────────────────────

        /// <summary>Raised by "SlashVFX" Animation Event — frame weapon arc begins.</summary>
        public event System.Action OnSlashVFX;

        /// <summary>Raised by "ImpactFrame" Animation Event — melee contact frame.</summary>
        public event System.Action OnImpactFrame;

        /// <summary>Raised by "shoot" Animation Event — ranged projectile release frame.</summary>
        public event System.Action OnShoot;

        /// <summary>Called from Animation Event named "SlashVFX" on attack clips — weapon arc start.</summary>
        public void SlashVFX()
        {
            Debug.Log($"[CombatPawnAnimator] 'SlashVFX' on '{gameObject.name}'");
            OnSlashVFX?.Invoke();
        }

        /// <summary>Called from Animation Event named "ImpactFrame" on melee/generic attack clips.</summary>
        public void ImpactFrame()
        {
            Debug.Log($"[CombatPawnAnimator] 'ImpactFrame' on '{gameObject.name}' — subscribers: {(OnImpactFrame != null ? OnImpactFrame.GetInvocationList().Length : 0)}");
            OnImpactFrame?.Invoke();
        }

        /// <summary>Called from Animation Event named "shoot" on ranged/archer attack clips.</summary>
        public void Shoot()
        {
            Debug.Log($"[CombatPawnAnimator] 'shoot' on '{gameObject.name}' — subscribers: {(OnShoot != null ? OnShoot.GetInvocationList().Length : 0)}");
            OnShoot?.Invoke();
        }

        // ── Single-state animations (Animator state machine) ──────────────────

        public void PlayApproach() => CrossFade(ApproachHash);
        public void PlayReturn()   => CrossFade(IdleHash);
        public void PlayHit()      => CrossFade(HitHash);
        public void PlayDeath()    => CrossFade(DeathHash);

        private void CrossFade(int stateHash)
        {
            if (_skillGraph.IsValid()) _skillGraph.Stop();
            if (_animator.HasState(0, stateHash))
                _animator.CrossFade(stateHash, 0.15f, 0, 0f);
        }

        // ── Skill sequence (Playables — arbitrary clip count) ─────────────────

        /// <summary>
        /// Plays <paramref name="clips"/> sequentially via PlayableGraph with cross-fade blending.
        /// <paramref name="onImpact"/> fires after clip at <paramref name="impactAfterClipIndex"/> completes.
        /// Pass -1 to fire before all clips, or >= clips.Length to fire after the last.
        /// Safe with null / empty arrays — onImpact still fires.
        /// Blend duration is controlled by the _blendDuration field (Inspector).
        /// </summary>
        public IEnumerator PlaySkillSequence(AnimationClip[] clips,
                                             System.Action onImpact = null,
                                             int impactAfterClipIndex = -1)
        {
            if (clips == null || clips.Length == 0) { onImpact?.Invoke(); yield break; }
            if (_skillGraph.IsValid()) _skillGraph.Destroy();

            _skillGraph = PlayableGraph.Create("SkillSeq");
            _skillGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            var mixer  = AnimationMixerPlayable.Create(_skillGraph, 2);
            var output = AnimationPlayableOutput.Create(_skillGraph, "out", _animator);
            output.SetSourcePlayable(mixer);
            _skillGraph.Play();

            if (impactAfterClipIndex < 0) onImpact?.Invoke();

            int   activeSlot  = 0;
            bool  hasActive   = false;
            float prevClipLen = 0f;

            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null)
                {
                    if (i == impactAfterClipIndex) onImpact?.Invoke();
                    continue;
                }

                int incomingSlot = hasActive ? 1 - activeSlot : 0;

                // Connect new clip to the incoming slot
                var prev = mixer.GetInput(incomingSlot);
                if (prev.IsValid()) { prev.Destroy(); mixer.DisconnectInput(incomingSlot); }
                var playable = AnimationClipPlayable.Create(_skillGraph, clip);
                mixer.ConnectInput(incomingSlot, playable, 0, hasActive ? 0f : 1f);
                if (!hasActive) mixer.SetInputWeight(1 - incomingSlot, 0f);

                // Blend active → incoming (skipped for first clip)
                float bdSpent = 0f;
                if (hasActive)
                {
                    float bd = Mathf.Min(_blendDuration, clip.length * 0.45f, prevClipLen * 0.45f);
                    for (float t = 0f; t < bd; t += Time.deltaTime)
                    {
                        float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / bd));
                        mixer.SetInputWeight(activeSlot,   1f - s);
                        mixer.SetInputWeight(incomingSlot, s);
                        yield return null;
                    }
                    mixer.SetInputWeight(activeSlot,   0f);
                    mixer.SetInputWeight(incomingSlot, 1f);

                    var old = mixer.GetInput(activeSlot);
                    if (old.IsValid()) { old.Destroy(); mixer.DisconnectInput(activeSlot); }
                    bdSpent = bd;
                }

                activeSlot  = incomingSlot;
                hasActive   = true;
                prevClipLen = clip.length;

                // Reserve time for blend-out into next non-null clip
                float waitBlend = 0f;
                for (int j = i + 1; j < clips.Length; j++)
                {
                    if (clips[j] == null) continue;
                    waitBlend = Mathf.Min(_blendDuration, clip.length * 0.45f, clips[j].length * 0.45f);
                    break;
                }

                float wait = Mathf.Max(0f, clip.length - bdSpent - waitBlend);
                if (wait > 0f) yield return new WaitForSeconds(wait);

                if (i == impactAfterClipIndex) onImpact?.Invoke();
            }

            if (impactAfterClipIndex >= clips.Length) onImpact?.Invoke();
            if (_skillGraph.IsValid()) _skillGraph.Destroy();
        }
    }
}
