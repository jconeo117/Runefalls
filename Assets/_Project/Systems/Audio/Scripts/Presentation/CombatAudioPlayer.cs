using System.Collections.Generic;
using UnityEngine;
using Runefall.Audio;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Reproduce voces de combate a partir del ImpactEvent.
    ///
    /// En cada hit resuelto:
    ///   • atacante  → grito de ataque (VoiceSetData.attackClips)
    ///   • objetivo  → si murió con este golpe → death; si sobrevive → hit
    ///
    /// El banco de voces se resuelve por actor desde CharacterData.voiceSet / EnemyData.voiceSet,
    /// con un fallback opcional cableado en el Inspector. La reproducción va vía IAudioService.
    ///
    /// Init() lo llama CombatBootstrapper tras montar el contexto. La suscripción al ImpactEvent
    /// se hace en Awake con la referencia serializada (igual que CombatVFXPlayer).
    /// </summary>
    public class CombatAudioPlayer : MonoBehaviour
    {
        [SerializeField] private ImpactEvent _impactEvent;

        [Header("Fallbacks (opcional)")]
        [Tooltip("Voz usada por jugadores sin voiceSet propio.")]
        [SerializeField] private VoiceSetData _defaultPlayerVoice;
        [Tooltip("Voz usada por enemigos sin voiceSet propio.")]
        [SerializeField] private VoiceSetData _defaultEnemyVoice;

        [Header("Anti-spam (segundos)")]
        [Tooltip("Mínimo entre gritos de ataque del mismo actor (skills multi-hit).")]
        [SerializeField] private float _attackCooldown = 0.30f;
        [Tooltip("Mínimo entre reacciones de golpe del mismo objetivo.")]
        [SerializeField] private float _hitCooldown = 0.12f;

        [SerializeField] private float _hitHeight = 1.4f;

        private IReadOnlyDictionary<ICombatActor, Transform>     _actorPawns;
        private IReadOnlyDictionary<ICombatActor, CharacterData> _actorCharData;
        private IReadOnlyDictionary<ICombatActor, EnemyData>     _actorEnemyData;

        private readonly Dictionary<ICombatActor, float> _lastAttack = new();
        private readonly Dictionary<ICombatActor, float> _lastHit    = new();
        private readonly HashSet<ICombatActor>           _deathPlayed = new();

        private IAudioService _audio;

        public void Init(
            IReadOnlyDictionary<ICombatActor, Transform>     actorPawns,
            IReadOnlyDictionary<ICombatActor, CharacterData> actorCharData,
            IReadOnlyDictionary<ICombatActor, EnemyData>     actorEnemyData)
        {
            _actorPawns     = actorPawns;
            _actorCharData  = actorCharData;
            _actorEnemyData = actorEnemyData;

            // Nuevo encuentro: limpiar throttles (los actores son instancias nuevas igualmente).
            _lastAttack.Clear();
            _lastHit.Clear();
            _deathPlayed.Clear();
        }

        private void Awake()
        {
            _impactEvent?.Subscribe(OnImpact);
        }

        private void OnDestroy()
        {
            _impactEvent?.Unsubscribe(OnImpact);
        }

        // ── ImpactEvent subscriber ──────────────────────────────────────────────────

        private void OnImpact(ImpactContext ctx)
        {
            float now = Time.time;

            // Atacante: grito de ataque (con cooldown para skills multi-hit).
            if (ctx.Attacker != null && Ready(_lastAttack, ctx.Attacker, now, _attackCooldown))
            {
                var voice = ResolveVoice(ctx.Attacker);
                Play(voice != null ? voice.RandomAttack() : null, PosOf(ctx.Attacker, ctx.HitPosition), voice);
            }

            // Objetivo: muerte o golpe.
            if (ctx.Target != null)
            {
                var voice = ResolveVoice(ctx.Target);
                Vector3 pos = PosOf(ctx.Target, ctx.HitPosition);

                if (!ctx.Target.IsAlive)
                {
                    if (_deathPlayed.Add(ctx.Target))
                        Play(voice != null ? voice.RandomDeath() : null, pos, voice);
                }
                else if (Ready(_lastHit, ctx.Target, now, _hitCooldown))
                {
                    Play(voice != null ? voice.RandomHit() : null, pos, voice);
                }
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────

        private void Play(AudioClip clip, Vector3 pos, VoiceSetData mix)
        {
            if (clip == null) return;
            _audio ??= AudioManager.GetOrCreate();
            float vol   = mix != null ? mix.volume : 1f;
            float pitch = mix != null ? mix.RandomPitch() : 1f;
            _audio.PlayAt(clip, pos, vol, pitch);
        }

        private VoiceSetData ResolveVoice(ICombatActor actor)
        {
            if (actor == null) return null;

            if (_actorCharData != null && _actorCharData.TryGetValue(actor, out var cd) && cd != null)
                return cd.voiceSet != null ? cd.voiceSet : _defaultPlayerVoice;

            if (_actorEnemyData != null && _actorEnemyData.TryGetValue(actor, out var ed) && ed != null)
                return ed.voiceSet != null ? ed.voiceSet : _defaultEnemyVoice;

            return _defaultPlayerVoice;
        }

        private Vector3 PosOf(ICombatActor actor, Vector3 fallback)
        {
            if (_actorPawns != null && actor != null
                && _actorPawns.TryGetValue(actor, out var t) && t != null)
                return t.position + Vector3.up * _hitHeight;
            return fallback;
        }

        private static bool Ready(Dictionary<ICombatActor, float> table, ICombatActor actor, float now, float cooldown)
        {
            if (table.TryGetValue(actor, out var last) && now - last < cooldown) return false;
            table[actor] = now;
            return true;
        }
    }
}
