using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Runefall.Combat;
using Runefall.Data;
using Runefall.Enemies;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Handles all combat animation: pawn lunges, impact VFX, HP visual feedback, damage numbers,
    /// and enemy phase sequencing. Implements IEnemyPhaseAnimator so TurnManager can trigger
    /// the animated enemy phase without importing any Presentation type.
    ///
    /// Call Init() from CombatBootstrapper after the combat context and HP bars are built.
    /// Assign serialized fields (combatBaseController, impactEvent, timing) in the Inspector.
    /// </summary>
    public class CombatAnimationDriver : MonoBehaviour, IEnemyPhaseAnimator
    {
        [Header("Animation")]
        [Tooltip("Base controller applied to each pawn via AnimatorOverrideController.")]
        public RuntimeAnimatorController combatBaseController;

        [Header("Timing")]
        [Tooltip("Seconds camera has to reach enemy side before the first enemy acts.")]
        public float enemyPhaseDelay = 0.45f;
        [Tooltip("How far a pawn stops in front of its target (world units).")]
        public float lungeStopDistance = 1.5f;
        [Tooltip("Seconds the pawn rotates toward origin before the return lunge starts.")]
        public float returnRotateDuration = 0.3f;

        [Header("Events")]
        [Tooltip("Raised once per hit at the moment of physical impact. VFX/audio subscribe here.")]
        [SerializeField] private ImpactEvent impactEvent;

        [Header("VFX")]
        [Tooltip("Handles spawning of skill VFX. Optional — assign the CombatVFXPlayer in the scene.")]
        [SerializeField] private CombatVFXPlayer vfxPlayer;

        [Header("Damage Numbers")]
        [Tooltip("FloatingDamage prefab with DamageNumber component. Spawned per hit.")]
        [SerializeField] private GameObject _damageNumberPrefab;

        [Header("Turn Transition")]
        [Tooltip("Seconds between last player animation finishing and enemy turn starting.")]
        [SerializeField] private float _postPlayerTurnDelay = 0.8f;

        [Header("Last Hit Drama")]
        [Tooltip("Time.timeScale during the killing blow slow-mo.")]
        [SerializeField] private float _lastHitSlowMoScale    = 0.05f;
        [Tooltip("Real seconds the slow-mo lasts.")]
        [SerializeField] private float _lastHitSlowMoDuration = 0.55f;
        [Tooltip("Real seconds of normal speed before the result screen appears.")]
        [SerializeField] private float _lastHitPauseDuration  = 0.30f;

        // Injected by CombatBootstrapper via Init()
        private CombatContext                                     _ctx;
        private TurnManager                                       _tm;
        private IReadOnlyDictionary<ICombatActor, Transform>      _actorPawns;
        private IReadOnlyDictionary<ICombatActor, CharacterData>  _actorCharData;
        private IReadOnlyDictionary<ICombatActor, EnemyData>      _actorEnemyData;
        private IReadOnlyDictionary<ICombatActor, HPBarPresenter> _actorHPBars;
        private ICombatPresenter                                   _presenter;

        private readonly Queue<PendingAction> _animQueue = new();
        private Camera _camera;

        // ── init ─────────────────────────────────────────────────────────────────

        public void Init(
            CombatContext                                     ctx,
            TurnManager                                       tm,
            IReadOnlyDictionary<ICombatActor, Transform>      actorPawns,
            IReadOnlyDictionary<ICombatActor, CharacterData>  actorCharData,
            IReadOnlyDictionary<ICombatActor, EnemyData>      actorEnemyData,
            IReadOnlyDictionary<ICombatActor, HPBarPresenter> actorHPBars,
            ICombatPresenter                                   presenter)
        {
            _ctx            = ctx;
            _tm             = tm;
            _actorPawns     = actorPawns;
            _actorCharData  = actorCharData;
            _actorEnemyData = actorEnemyData;
            _actorHPBars    = actorHPBars;
            _presenter      = presenter;
            _camera         = Camera.main;

            impactEvent?.Subscribe(OnImpactReceived);
            InitPawnAnimators();
        }

        private void OnDestroy()
        {
            impactEvent?.Unsubscribe(OnImpactReceived);
        }

        // ── IEnemyPhaseAnimator ───────────────────────────────────────────────────

        public void RunEnemyPhase(
            IReadOnlyList<ICombatActor> enemies,
            Action<ICombatActor>        executeTurn,
            Action                      onComplete)
        {
            StartCoroutine(EnemyPhaseAnimated(enemies, executeTurn, onComplete));
        }

        private IEnumerator EnemyPhaseAnimated(
            IReadOnlyList<ICombatActor> enemies,
            Action<ICombatActor>        executeTurn,
            Action                      onComplete)
        {
            yield return new WaitForSeconds(enemyPhaseDelay);

            for (int i = 0; i < enemies.Count; i++)
            {
                if (_ctx != null && _ctx.IsOver) break;
                if (!enemies[i].IsAlive) continue;
                executeTurn(enemies[i]);                     // fires TM.OnActionPending → Bootstrapper.Enqueue()
                yield return StartCoroutine(DrainQueue(null));
            }

            onComplete();
        }

        // ── public API called by CombatBootstrapper ───────────────────────────────

        /// <summary>Queues a pending action for deferred resolution at animation impact frame.</summary>
        public void Enqueue(PendingAction pending) => _animQueue.Enqueue(pending);

        /// <summary>
        /// Drains the animation queue with full visual feedback.
        /// Called by Bootstrapper when the player exhausts all actions (pass _tm.EndPlayerTurn
        /// as onComplete) or after enemy phase actions.
        /// fadeSlots: when true, notifies presenter after each animation so it can fade action slots.
        /// </summary>
        public void PlayQueuedAnimations(Action onComplete, bool fadeSlots = false)
        {
            StartCoroutine(DrainQueue(onComplete, fadeSlots));
        }

        // ── queue drain ───────────────────────────────────────────────────────────

        private IEnumerator DrainQueue(Action onComplete, bool fadeSlots = false)
        {
            int slotIndex = 0;
            while (_animQueue.Count > 0)
            {
                // Combat may have ended during a previous group — skip remaining actions.
                if (_ctx != null && _ctx.IsOver) break;
                var pending = _animQueue.Dequeue();
                yield return StartCoroutine(PlayActionGroup(pending));
                if (fadeSlots)
                    _presenter?.NotifyActionAnimationComplete(slotIndex++);
                if (_ctx != null && _ctx.IsOver) break;
            }
            if (_ctx != null && _ctx.IsOver)
                yield return StartCoroutine(LastHitDrama());
            if (onComplete != null && fadeSlots && _postPlayerTurnDelay > 0f)
                yield return new WaitForSeconds(_postPlayerTurnDelay);
            onComplete?.Invoke();
        }

        // ── pawn animators ────────────────────────────────────────────────────────

        private void InitPawnAnimators()
        {
            foreach (var (actor, pawn) in _actorPawns)
            {
                var anim = pawn.GetComponentInChildren<CombatPawnAnimator>();
                if (anim == null) continue;

                if (_actorCharData.TryGetValue(actor, out var cd))
                    anim.InitFromCharacter(cd, combatBaseController);
                else if (_actorEnemyData.TryGetValue(actor, out var ed))
                    anim.InitFromEnemy(ed, combatBaseController);
            }
        }

        // ── action group ──────────────────────────────────────────────────────────

        private IEnumerator PlayActionGroup(PendingAction pending)
        {
            if (pending.Caster == null || !_actorPawns.TryGetValue(pending.Caster, out var casterPawn))
                yield break;
            if (!casterPawn.gameObject.activeSelf) yield break;

            bool isRanged   = pending.Skill != null && pending.Skill.isRanged;
            var  casterAnim = casterPawn.GetComponentInChildren<CombatPawnAnimator>();
            var  clips      = GetSkillClips(pending);
            int  impactIdx  = GetImpactClipIndex(pending);
            var  trigger    = ResolveTrigger(pending, casterPawn, GetTargetTransform(pending), clips);
            float blend     = casterAnim != null ? casterAnim.BlendDuration : 0f;

            Vector3    targetPos        = GetTargetPosition(pending);
            Quaternion originalRotation = casterPawn.rotation;

            // SlashVFX AE drives onStartVFX timing.
            // Debounce: AE baked on multiple frames fires many times per clip — only allow
            // one spawn per 80 ms window. Clips >80 ms apart (always true) each get their VFX.
            var   vfxConfig      = ResolveVFXConfig(pending);
            bool  holdLoop       = vfxConfig != null && vfxConfig.holdAnimLoopUntilVFXDone;
            int   loopClipIdx    = holdLoop ? vfxConfig.animLoopClipIndex : -1;
            float lastSlashTime  = float.MinValue;
            const float kSlashDebounce = 0.08f;
            GameObject slashGO   = null;

            // Capture after slashGO is declared so the closure sees the variable, not the value.
            System.Func<bool> exitLoopWhen = holdLoop ? () => slashGO == null : (System.Func<bool>)null;

            // Immediate spawn: no SlashVFX AE needed (e.g. mage casting circle at enemy center).
            if (vfxConfig?.spawnOnStartImmediately == true)
                slashGO = vfxPlayer?.PlayOnStartVFX(vfxConfig, casterPawn, targetPos);

            void HandleSlashVFX()
            {
                // Skip if already spawned via spawnOnStartImmediately.
                if (vfxConfig?.spawnOnStartImmediately == true) return;
                if (Time.time - lastSlashTime < kSlashDebounce) return;
                lastSlashTime = Time.time;
                // Stop previous slash VFX emission before spawning the next one.
                if (slashGO != null)
                    foreach (var ps in slashGO.GetComponentsInChildren<ParticleSystem>())
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                slashGO = vfxPlayer?.PlayOnStartVFX(vfxConfig, casterPawn, targetPos);
            }
            if (casterAnim != null) casterAnim.OnSlashVFX += HandleSlashVFX;
            try
            {

            if (isRanged)
            {
                // Ranged: snap rotate toward target, stay in place
                RotateCasterToward(casterPawn, targetPos);

                if (trigger != null)
                {
                    trigger.Arm(() => RaiseImpactGroup(pending));
                    bool animDone = casterAnim == null;
                    if (casterAnim != null)
                        StartCoroutine(RunThenSignal(
                            casterAnim.PlaySkillSequence(clips,
                                holdClipIndex: loopClipIdx,
                                shouldAdvanceFromHold: exitLoopWhen),
                            () => animDone = true));
                    yield return new WaitUntil(() => animDone && trigger.HasFired);
                }
                else
                {
                    RaiseImpactGroup(pending);
                    if (casterAnim != null)
                        yield return StartCoroutine(casterAnim.PlaySkillSequence(clips,
                            holdClipIndex: loopClipIdx,
                            shouldAdvanceFromHold: exitLoopWhen));
                }
            }
            else
            {
                int returnIdx = ResolveReturnClipIndex(pending, clips);

                Vector3 origin      = casterPawn.position;
                Vector3 lungeTarget = ComputeLungeTarget(casterPawn, targetPos);

                // Approach: rotate from origin toward target spanning all pre-impact clips
                // so the model is fully facing the target before the last (attack) clip plays.
                float approachDelay  = SumClipDurations(clips, 0, impactIdx - 1);
                float approachDur    = clips != null && impactIdx >= 0 && impactIdx < clips.Length
                                       ? clips[impactIdx]?.length ?? 0f : 0f;
                float approachRotDur = approachDelay > blend ? approachDelay : blend;
                StartCoroutine(SmoothRotateTo(casterPawn, targetPos, 0f, approachRotDur));
                if (approachDur > 0f)
                    StartCoroutine(DelayedLungeTo(casterPawn, lungeTarget, approachDelay, approachDur));

                // Return: when returnIdx < clips.Length, overlap return with that clip (explicit).
                // When returnIdx >= clips.Length (default), do a clean sequential return after
                // the sequence so attacks play fully before the model moves home.
                bool      afterAll    = returnIdx >= (clips?.Length ?? 0);
                Coroutine returnLunge = null;
                if (!afterAll && returnIdx >= 0)
                {
                    float rawReturn  = SumClipDurations(clips, 0, returnIdx - 1);
                    float rotStart   = Mathf.Max(0f, rawReturn - blend);
                    float returnDur  = clips != null && returnIdx < clips.Length
                                       ? clips[returnIdx]?.length ?? 0.5f : 0.5f;
                    float rotBegin   = Mathf.Max(0f, rotStart - returnRotateDuration);
                    float lungeStart = rotBegin + returnRotateDuration;
                    StartCoroutine(SmoothRotateTo(casterPawn, origin, rotBegin, returnRotateDuration));
                    returnLunge = StartCoroutine(DelayedLungeTo(casterPawn, origin, lungeStart, returnDur, snapOnEnd: true));
                }

                if (trigger != null)
                {
                    trigger.Arm(() => RaiseImpactGroup(pending));
                    bool animDone = casterAnim == null;
                    if (casterAnim != null)
                        StartCoroutine(RunThenSignal(
                            casterAnim.PlaySkillSequence(clips,
                                holdClipIndex: loopClipIdx,
                                shouldAdvanceFromHold: exitLoopWhen),
                            () => animDone = true));
                    yield return new WaitUntil(() => animDone && trigger.HasFired);
                }
                else
                {
                    if (casterAnim != null)
                        yield return StartCoroutine(casterAnim.PlaySkillSequence(
                            clips,
                            () => RaiseImpactGroup(pending),
                            impactIdx,
                            holdClipIndex: loopClipIdx,
                            shouldAdvanceFromHold: exitLoopWhen));
                    else
                        RaiseImpactGroup(pending);
                }

                if (afterAll)
                {
                    // Sequential return: full sequence done → rotate → approach back → idle.
                    // Uses actual approach-clip length so animation and movement are locked.
                    float approachLen = GetApproachClipDuration(pending.Caster);
                    yield return StartCoroutine(SmoothRotateTo(casterPawn, origin, 0f, returnRotateDuration));
                    casterAnim?.PlayApproach();
                    yield return StartCoroutine(LungeTo(casterPawn, origin, approachLen));
                    casterAnim?.PlayReturn();
                }
                else
                {
                    if (returnLunge != null) casterAnim?.PlayApproach();
                    if (returnLunge != null) yield return returnLunge;
                    casterAnim?.PlayReturn();
                }
                casterPawn.rotation = originalRotation;
            }

            } // try
            finally
            {
                if (casterAnim != null) casterAnim.OnSlashVFX -= HandleSlashVFX;

                // Stop slash VFX emission when action ends so it doesn't overstay.
                // Existing particles finish their natural lifetime (no hard pop).
                if (slashGO != null)
                {
                    foreach (var ps in slashGO.GetComponentsInChildren<ParticleSystem>())
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        // ── trigger helpers ───────────────────────────────────────────────────────

        private SkillVFXConfig ResolveVFXConfig(PendingAction pending) => pending.Skill?.vfxConfig;

        private IImpactTrigger ResolveTrigger(PendingAction pending, Transform casterPawn, Transform targetPawn, AnimationClip[] allClips = null)
        {
            return pending.Skill?.impactTrigger?.Create(casterPawn, targetPawn, this, allClips);
        }

        private Transform GetTargetTransform(PendingAction pending)
        {
            if (pending.Target != null && _actorPawns.TryGetValue(pending.Target, out var tp))
                return tp;
            bool isEnemy = pending.Caster is EnemyAgent;
            var side = isEnemy
                ? (IReadOnlyList<ICombatActor>)_ctx.Players
                : _ctx.Enemies;
            foreach (var a in side)
                if (a.IsAlive && _actorPawns.TryGetValue(a, out var p)) return p;
            return null;
        }

        private IEnumerator RunThenSignal(IEnumerator inner, Action onDone)
        {
            yield return inner;
            onDone?.Invoke();
        }

        private IEnumerator DelayedCall(float delay, Action action)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        // ── impact ────────────────────────────────────────────────────────────────

        private void RaiseImpactGroup(PendingAction pending)
        {
            if (_tm == null) return;

            // Damage applied here — this is the real-time resolution moment.
            var results = _tm.ResolveAction(pending);

            // Fire ImpactEvent individually per target (damage numbers + HP bar).
            for (int i = 0; i < results.Length; i++)
            {
                var r      = results[i];
                Vector3 hitPos = r.Target != null && _actorPawns.TryGetValue(r.Target, out var tp)
                    ? tp.position + Vector3.up * 1.2f
                    : Vector3.zero;

                var ctx = ImpactContext.FromResult(r, hitPos);
                if (impactEvent != null)
                    impactEvent.Raise(ctx);
                else
                    OnImpactReceived(ctx);
            }

            // Death check per target — accurate because damage was just applied above.
            for (int i = 0; i < results.Length; i++)
            {
                var r = results[i];
                if (r.Target != null && !r.Target.IsAlive
                    && _actorPawns.TryGetValue(r.Target, out var deadPawn))
                {
                    deadPawn.GetComponentInChildren<CombatPawnAnimator>()?.PlayDeath();
                    StartCoroutine(HidePawnDelayed(deadPawn.gameObject, 1.2f));
                }
            }
        }

        /// <summary>
        /// Subscriber to ImpactEvent SO. Applies visual hit reaction, damage numbers, and HP bar updates.
        /// Future VFX/audio systems subscribe to the same ImpactEvent SO without touching this class.
        /// </summary>
        private void OnImpactReceived(ImpactContext ctx)
        {
            Transform targetPawn = null;
            bool hasPawn = ctx.Target != null && _actorPawns.TryGetValue(ctx.Target, out targetPawn);
            if (ctx.DamageDealt > 0f && ctx.Target != null && hasPawn)
            {
                targetPawn.GetComponentInChildren<CombatPawnAnimator>()?.PlayHit();
                SpawnDamageNumber(targetPawn, ctx.DamageDealt, ctx.IsCrit);
            }

            if (ctx.Target != null && _actorHPBars.TryGetValue(ctx.Target, out var targetBar))
            {
                if (ctx.DamageDealt    > 0f) targetBar.ApplyVisualDamage(ctx.DamageDealt);
                if (ctx.HealApplied    > 0f) targetBar.ApplyVisualHeal(ctx.HealApplied);
            }

            if (ctx.LifeStealApplied > 0f && ctx.Attacker != null
                && _actorHPBars.TryGetValue(ctx.Attacker, out var attackerBar))
                attackerBar.ApplyVisualHeal(ctx.LifeStealApplied);
        }

        // ── lunge helpers ─────────────────────────────────────────────────────────

        private IEnumerator LastHitDrama()
        {
            Time.timeScale = _lastHitSlowMoScale;
            yield return new WaitForSecondsRealtime(_lastHitSlowMoDuration);
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(_lastHitPauseDuration);
        }

        private IEnumerator HidePawnDelayed(GameObject pawn, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (pawn != null) pawn.SetActive(false);
        }

        private Vector3 GetTargetPosition(PendingAction pending)
        {
            bool isAoe = pending.TargetType == TargetType.AllEnemies
                      || pending.TargetType == TargetType.AllAllies;

            if (isAoe)
            {
                // AllEnemies → _ctx.Enemies; AllAllies → _ctx.Players.
                var targets = pending.TargetType == TargetType.AllEnemies
                    ? (IReadOnlyList<ICombatActor>)_ctx.Enemies
                    : _ctx.Players;
                Vector3 centroid = Vector3.zero;
                int     count    = 0;
                foreach (var a in targets)
                    if (a.IsAlive && _actorPawns.TryGetValue(a, out var p))
                        { centroid += p.position; count++; }
                return count > 0 ? centroid / count : Vector3.zero;
            }

            if (pending.Target != null && _actorPawns.TryGetValue(pending.Target, out var tp))
                return tp.position;

            return Vector3.zero;
        }

        private Vector3 ComputeLungeTarget(Transform casterPawn, Vector3 targetPos)
        {
            if (targetPos == Vector3.zero) return casterPawn.position;
            Vector3 dir = (casterPawn.position - targetPos).normalized;
            return targetPos + dir * lungeStopDistance;
        }

        private static void RotateCasterToward(Transform caster, Vector3 target)
        {
            if (target == Vector3.zero) return;
            Vector3 dir = target - caster.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                caster.rotation = Quaternion.LookRotation(dir);
        }

        private IEnumerator SmoothRotateTo(Transform pawn, Vector3 lookTarget, float delay, float duration)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (pawn == null) yield break;

            Vector3 dir = lookTarget - pawn.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) yield break;

            Quaternion from = pawn.rotation;
            Quaternion to   = Quaternion.LookRotation(dir);

            if (duration <= 0f) { pawn.rotation = to; yield break; }

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (pawn == null) yield break;
                pawn.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            if (pawn != null) pawn.rotation = to;
        }

        private IEnumerator DelayedLungeTo(
            Transform pawn, Vector3 target, float delay, float duration, bool snapOnEnd = false)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            yield return StartCoroutine(LungeTo(pawn, target, duration));
            if (snapOnEnd) pawn.position = target;
        }

        private static IEnumerator LungeTo(Transform pawn, Vector3 target, float duration)
        {
            if (duration <= 0f) { pawn.position = target; yield break; }
            Vector3 origin = pawn.position;
            for (float t = 0f; t < 1f;)
            {
                t             = Mathf.Min(1f, t + Time.deltaTime / duration);
                pawn.position = Vector3.Lerp(origin, target, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }

        // ── clip helpers ──────────────────────────────────────────────────────────

        private AnimationClip[] GetSkillClips(PendingAction pending)
        {
            if (pending.Skill != null)
                return pending.Skill.animSequence;

            // Ultimate: Skill is null — look up via CharacterData or EnemyData
            if (pending.IsUltimate)
            {
                if (_actorCharData.TryGetValue(pending.Caster, out var cd) && cd.ultimate != null)
                    return cd.ultimate.animSequence;
                if (_actorEnemyData.TryGetValue(pending.Caster, out var ed) && ed.ultimate != null)
                    return ed.ultimate.animSequence;
            }

            return null;
        }

        private int GetImpactClipIndex(PendingAction pending) => pending.Skill?.impactAfterClipIndex ?? 0;

        private int ResolveReturnClipIndex(PendingAction pending, AnimationClip[] clips)
        {
            int raw = pending.Skill?.returnAtClipIndex ?? -1;
            // -1 → "after all clips": use clips.Length as sentinel so rawReturn sums ALL clips
            if (raw < 0 && clips != null && clips.Length > 0)
                return clips.Length;
            return raw;
        }

        private float GetApproachClipDuration(ICombatActor actor)
        {
            if (_actorCharData.TryGetValue(actor, out var cd) && cd.animApproach != null)
                return cd.animApproach.length;
            if (_actorEnemyData.TryGetValue(actor, out var ed) && ed.animApproach != null)
                return ed.animApproach.length;
            return 0.5f;
        }

        private static float SumClipDurations(AnimationClip[] clips, int from, int to)
        {
            if (clips == null) return 0f;
            float sum = 0f;
            for (int i = Mathf.Max(0, from); i <= Mathf.Min(to, clips.Length - 1); i++)
                sum += clips[i]?.length ?? 0f;
            return sum;
        }

        // ── damage number ─────────────────────────────────────────────────────────

        private void SpawnDamageNumber(Transform targetPawn, float damage, bool isCrit)
        {
            if (_damageNumberPrefab == null) return;

            Vector3 offset = new Vector3(
                UnityEngine.Random.Range(-0.3f, 0.3f),
                1.8f + UnityEngine.Random.Range(0f, 0.35f),
                UnityEngine.Random.Range(-0.15f, 0.15f));

            var go  = Instantiate(_damageNumberPrefab, targetPawn.position + offset, Quaternion.identity);
            var dmg = go.GetComponent<DamageNumber>();

            string text     = $"{damage:F0}";
            float  fontSize = isCrit ? 16f : 12f;
            dmg?.Show(text, fontSize, isCrit);
        }
    }
}
