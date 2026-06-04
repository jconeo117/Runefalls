using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Runefall.Combat;
using Runefall.Data;
using Runefall.Enemies;
using UnityEngine.Playables;
using UnityEngine.Timeline;

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
        [Tooltip("Full cinematic finisher on last-enemy kill. When null, falls back to simple slow-mo.")]
        [SerializeField] private FinisherManager _finisherManager;

        public FinisherManager Finisher => _finisherManager;
        [Tooltip("Time.timeScale during the fallback slow-mo (used when FinisherManager is not assigned).")]
        [SerializeField] private float _lastHitSlowMoScale    = 0.05f;
        [Tooltip("Real seconds the fallback slow-mo lasts.")]
        [SerializeField] private float _lastHitSlowMoDuration = 0.55f;
        [Tooltip("Real seconds of pause after fallback slow-mo before the result screen appears.")]
        [SerializeField] private float _lastHitPauseDuration  = 0.30f;

        // Injected by CombatBootstrapper via Init()
        protected CombatContext                                     _ctx;
        protected TurnManager                                       _tm;
        protected IReadOnlyDictionary<ICombatActor, Transform>      _actorPawns;
        protected IReadOnlyDictionary<ICombatActor, CharacterData>  _actorCharData;
        protected IReadOnlyDictionary<ICombatActor, EnemyData>      _actorEnemyData;
        protected IReadOnlyDictionary<ICombatActor, HPBarPresenter> _actorHPBars;
        protected ICombatPresenter                                   _presenter;

        protected readonly Queue<PendingAction> _animQueue = new();
        protected Camera _camera;

        // Injected by multiplayer bootstrapper: called on server before each enemy queue drain
        // so non-server clients receive the signal to drain their local queues in sync.
        public Action OnBeforeEnemyQueueDrain;

        // World-space positions of the attacker and target on the killing blow.
        // Captured in RaiseImpactHit when _ctx.IsOver && _ctx.PlayerWon, passed to FinisherManager.
        private Vector3 _killingBlowAttackerPos;
        private Vector3 _killingBlowTargetPos;

        // Deferred death: actors killed before the last hit of a multi-hit skill.
        // Death animation and hide are deferred until the final hit so remaining clips play first.
        private readonly HashSet<ICombatActor>                      _pendingDeaths       = new();
        private readonly Dictionary<ICombatActor, CombatActionResult> _pendingDeathResults = new();

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
                OnBeforeEnemyQueueDrain?.Invoke();           // server notifies clients to drain their queues
                yield return StartCoroutine(DrainQueue(null));
            }

            onComplete();
        }

        // ── public API called by CombatBootstrapper ───────────────────────────────

        /// <summary>Queues a pending action for deferred resolution at animation impact frame.</summary>
        public void Enqueue(PendingAction pending) => _animQueue.Enqueue(pending);

        /// <summary>Clears the visual queue of pending actions.</summary>
        public void ClearQueue() => _animQueue.Clear();

        /// <summary>
        /// Drains the animation queue with full visual feedback.
        /// Called by Bootstrapper when the player exhausts all actions (pass _tm.EndPlayerTurn
        /// as onComplete) or after enemy phase actions.
        /// fadeSlots: when true, notifies presenter after each animation so it can fade action slots.
        /// </summary>
        private bool _isDraining = false;

        public virtual void PlayQueuedAnimations(Action onComplete, bool fadeSlots = false)
        {
            if (_isDraining) return;
            StartCoroutine(DrainQueue(onComplete, fadeSlots));
        }

        // ── queue drain ───────────────────────────────────────────────────────────

        private IEnumerator DrainQueue(Action onComplete, bool fadeSlots = false)
        {
            _isDraining = true;
            try
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
                    yield return StartCoroutine(RunCombatEndDrama());
                if (onComplete != null && fadeSlots && _postPlayerTurnDelay > 0f)
                    yield return new WaitForSecondsRealtime(_postPlayerTurnDelay);
                onComplete?.Invoke();
            }
            finally
            {
                _isDraining = false;
            }
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
            if (pending.IsUltimate)
            {
                yield return StartCoroutine(PlayDefaultAnimationSequenceCoroutine(pending));
            }
            else if (pending.Skill != null)
            {
                bool finished = false;
                pending.Skill.PlayPresentation(this, pending.Caster, pending.Target, pending.Rank, () => finished = true);
                yield return new WaitUntil(() => finished);
            }
        }

        public void PlayDefaultAnimationSequence(
            DefaultSkillData skill, 
            ICombatActor caster, 
            ICombatActor target, 
            int rank, 
            System.Action onComplete)
        {
            var pending = new PendingAction(
                caster, 
                target, 
                skill, 
                null, 
                rank, 
                skill.targetType, 
                false
            );
            StartCoroutine(PlayDefaultAnimationSequenceCoroutine(pending, onComplete));
        }

        private IEnumerator PlayDefaultAnimationSequenceCoroutine(PendingAction pending, System.Action onComplete = null)
        {
            if (pending.Caster == null || !_actorPawns.TryGetValue(pending.Caster, out var casterPawn))
            {
                onComplete?.Invoke();
                yield break;
            }
            if (!casterPawn.gameObject.activeSelf)
            {
                onComplete?.Invoke();
                yield break;
            }

            // Reset per-action multi-hit state.
            _pendingDeaths.Clear();
            _pendingDeathResults.Clear();

            bool isRanged   = pending.Skill != null && pending.Skill.isRanged;
            var  casterAnim = casterPawn.GetComponentInChildren<CombatPawnAnimator>();
            var  clips      = GetSkillClips(pending);
            int  impactIdx  = GetImpactClipIndex(pending);
            var  trigger    = ResolveTrigger(pending, casterPawn, GetTargetTransform(pending), clips);
            float blend     = casterAnim != null ? casterAnim.BlendDuration : 0f;

            Vector3    targetPos        = GetTargetPosition(pending);
            Quaternion originalRotation = casterPawn.rotation;

            // SlashVFX AE drives onStartVFX timing.
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
                    trigger.Arm((hitIdx, total, specificActor) => RaiseImpactHit(pending, hitIdx, total, specificActor));
                    bool animDone = casterAnim == null;
                    if (casterAnim != null)
                        StartCoroutine(RunThenSignal(
                            casterAnim.PlaySkillSequence(clips,
                                holdClipIndex: loopClipIdx,
                                shouldAdvanceFromHold: exitLoopWhen),
                            () => animDone = true));
                    yield return new WaitUntil(() => animDone);
                    if (!trigger.HasFired)
                    {
                        Debug.LogWarning($"[CombatAnimationDriver] Trigger never fired! Forcing fallback impact.");
                        RaiseImpactHit(pending, 0, 1);
                    }
                }
                else
                {
                    RaiseImpactHit(pending, 0, 1);
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
                    trigger.Arm((hitIdx, total, specificActor) => RaiseImpactHit(pending, hitIdx, total, specificActor));
                    bool animDone = casterAnim == null;
                    if (casterAnim != null)
                        StartCoroutine(RunThenSignal(
                            casterAnim.PlaySkillSequence(clips,
                                holdClipIndex: loopClipIdx,
                                shouldAdvanceFromHold: exitLoopWhen),
                                () => animDone = true));
                    yield return new WaitUntil(() => animDone);
                    if (!trigger.HasFired)
                    {
                        Debug.LogWarning($"[CombatAnimationDriver] Trigger never fired! Forcing fallback impact.");
                        RaiseImpactHit(pending, 0, 1);
                    }
                }
                else
                {
                    if (casterAnim != null)
                        yield return StartCoroutine(casterAnim.PlaySkillSequence(
                            clips,
                            () => RaiseImpactHit(pending, 0, 1),
                            impactIdx,
                            holdClipIndex: loopClipIdx,
                            shouldAdvanceFromHold: exitLoopWhen));
                    else
                        RaiseImpactHit(pending, 0, 1);
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

            onComplete?.Invoke();
        }

        public void PlayTimelineCinematic(
            TimelineAsset timeline, 
            ICombatActor caster, 
            ICombatActor target, 
            System.Action onComplete)
        {
            StartCoroutine(PlayTimelineCinematicCoroutine(timeline, caster, target, onComplete));
        }

        private IEnumerator PlayTimelineCinematicCoroutine(
            TimelineAsset timeline, 
            ICombatActor caster, 
            ICombatActor target, 
            System.Action onComplete)
        {
            if (timeline == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            // 1. Get Caster and Target Animator components from the active pawns
            Animator casterAnim = null;
            Transform cp = null;
            if (caster != null && _actorPawns.TryGetValue(caster, out cp))
                casterAnim = cp.GetComponentInChildren<Animator>();

            Animator targetAnim = null;
            if (target != null && _actorPawns.TryGetValue(target, out var tp))
                targetAnim = tp.GetComponentInChildren<Animator>();

            var brain = Camera.main != null ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
            var cameraController = Camera.main != null ? Camera.main.GetComponent<CombatCameraController>() : null;

            // Enable CinemachineBrain so the skill timeline can drive the camera
            if (brain != null)
            {
                brain.enabled = true;
            }

            // Disable manual camera controller so it does not fight/lock the camera during the timeline
            if (cameraController != null)
            {
                cameraController.enabled = false;
            }

            // 2. Set up a PlayableDirector on the caster's pawn or a temporary object
            PlayableDirector director = null;
            if (cp != null)
            {
                director = cp.GetComponent<PlayableDirector>();
                if (director == null)
                    director = cp.gameObject.AddComponent<PlayableDirector>();
            }
            else
            {
                director = GetComponent<PlayableDirector>();
                if (director == null)
                    director = gameObject.AddComponent<PlayableDirector>();
            }

            director.playableAsset = timeline;

            // 3. Dynamic runtime bindings (no scene dependencies!)
            foreach (var output in timeline.outputs)
            {
                if (output.outputTargetType == typeof(Unity.Cinemachine.CinemachineBrain) && brain != null)
                {
                    director.SetGenericBinding(output.sourceObject, brain);
                }
                else if (output.outputTargetType == typeof(Animator))
                {
                    string name = output.streamName.ToLower();
                    if (name.Contains("caster") && casterAnim != null)
                    {
                        director.SetGenericBinding(output.sourceObject, casterAnim);
                    }
                    else if (name.Contains("target") && targetAnim != null)
                    {
                        director.SetGenericBinding(output.sourceObject, targetAnim);
                    }
                }
            }

            // 4. Play and wait for stopped callback
            bool timelineDone = false;
            System.Action<PlayableDirector> onStopped = null;
            onStopped = (dir) => {
                timelineDone = true;
                dir.stopped -= onStopped;
            };
            director.stopped += onStopped;

            director.time = 0;
            director.Evaluate();
            director.Play();

            yield return new WaitUntil(() => timelineDone);

            // 5. Restore components and hand control back to CombatCameraController
            if (brain != null)
            {
                brain.enabled = false;
            }

            if (cameraController != null)
            {
                cameraController.enabled = true;
                
                // Snap back instantly to the current turn's camera target (Player or Enemy side)
                if (_tm != null)
                {
                    bool isEnemyTurn = _tm.Phase == CombatPhase.EnemyTurn;
                    cameraController.SnapToAnchor(isEnemyTurn);
                }
            }

            onComplete?.Invoke();
        }

        // ── trigger helpers ───────────────────────────────────────────────────────

        private SkillVFXConfig ResolveVFXConfig(PendingAction pending)
        {
            if (pending.Skill is DefaultSkillData defaultSkill)
                return defaultSkill.vfxConfig;
            return null;
        }

        private IImpactTrigger ResolveTrigger(PendingAction pending, Transform casterPawn, Transform targetPawn, AnimationClip[] allClips = null)
        {
            int hitCount = 0;
            ImpactTriggerData triggerData = null;

            if (pending.Skill is DefaultSkillData defaultSkill)
            {
                hitCount = defaultSkill.hitCount > 1 ? defaultSkill.hitCount : 0;
                triggerData = defaultSkill.impactTrigger;
            }

            // Build per-enemy target list for AoE ranged skills so ProjectileTrigger
            // spawns one projectile per alive enemy per shoot AE.
            List<(ICombatActor actor, Transform pawn)> aoeTargets = null;
            bool isAoeRanged = (pending.TargetType == TargetType.AllEnemies
                                || pending.TargetType == TargetType.AllAllies)
                               && (pending.Skill?.isRanged ?? false);
            if (isAoeRanged && _ctx != null)
            {
                var side = pending.TargetType == TargetType.AllEnemies
                    ? (IReadOnlyList<ICombatActor>)_ctx.Enemies
                    : _ctx.Players;
                aoeTargets = new List<(ICombatActor, Transform)>();
                foreach (var actor in side)
                    if (actor.IsAlive && _actorPawns.TryGetValue(actor, out var p))
                        aoeTargets.Add((actor, p));
                if (aoeTargets.Count == 0) aoeTargets = null;
            }

            return triggerData?.Create(casterPawn, targetPawn, this, allClips, hitCount, aoeTargets);
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

        /// <summary>
        /// Resolves one hit of a skill.
        /// hitIdx / totalHits: position in the multi-hit sequence per target.
        /// specificTarget: non-null for AoE projectile hits — resolves for that actor only.
        ///   Null = default path: uses pending.TargetType (single enemy, AoE, self, etc.)
        ///
        /// Death deferral: targets killed before the last hit are deferred so remaining clips
        /// play through. Subsequent hits on deferred-dead targets show overkill floating numbers.
        /// AoE per-actor mode: each actor's death is flushed individually on its own last hit.
        /// </summary>
        protected void RaiseImpactHit(PendingAction pending, int hitIdx, int totalHits,
                                    ICombatActor specificTarget = null)
        {
            if (_tm == null) return;

            float hitFraction = totalHits > 1 ? 1f / totalHits : 1f;
            bool  isLastHit   = hitIdx >= totalHits - 1;

            // Resolve damage: specific-target path (AoE projectile) or standard path.
            CombatActionResult[] results = specificTarget != null
                ? _tm.ResolveForTarget(pending, hitFraction, specificTarget)
                : _tm.ResolveAction(pending, hitFraction);

            TryCaptureKillingBlowPositions(results);

            // Normal path: fire visual feedback for each result.
            for (int i = 0; i < results.Length; i++)
            {
                var r = results[i];
                Vector3 hitPos = r.Target != null && _actorPawns.TryGetValue(r.Target, out var tp)
                    ? tp.position + Vector3.up * 1.2f
                    : Vector3.zero;

                var ctx = ImpactContext.FromResult(r, hitPos);
                if (impactEvent != null)
                    impactEvent.Raise(ctx);
                else
                    OnImpactReceived(ctx);
            }

            // Visual-only overkill hits on dead-deferred targets.
            // For specificTarget mode: only show for that specific target.
            // For AoE/single standard mode: show for all pending deaths.
            if (_pendingDeaths.Count > 0)
            {
                if (specificTarget != null)
                {
                    if (_pendingDeaths.Contains(specificTarget)
                        && _pendingDeathResults.TryGetValue(specificTarget, out var deadRes)
                        && _actorPawns.TryGetValue(specificTarget, out var dp))
                    {
                        dp.GetComponentInChildren<CombatPawnAnimator>()?.PlayHit();
                        SpawnDamageNumber(dp, deadRes.DamageDealt, deadRes.IsCrit);
                    }
                }
                else
                {
                    foreach (var (deadActor, deadResult) in _pendingDeathResults)
                    {
                        if (!_actorPawns.TryGetValue(deadActor, out var dp)) continue;
                        dp.GetComponentInChildren<CombatPawnAnimator>()?.PlayHit();
                        SpawnDamageNumber(dp, deadResult.DamageDealt, deadResult.IsCrit);
                    }
                }
            }

            // Death handling: defer or play immediately.
            for (int i = 0; i < results.Length; i++)
            {
                var r = results[i];
                if (r.Target == null || r.Target.IsAlive) continue;
                if (!_actorPawns.TryGetValue(r.Target, out var deadPawn)) continue;

                if (isLastHit)
                {
                    deadPawn.GetComponentInChildren<CombatPawnAnimator>()?.PlayDeath();
                    StartCoroutine(HidePawnDelayed(deadPawn.gameObject, 1.2f));
                    _pendingDeaths.Remove(r.Target);
                    _pendingDeathResults.Remove(r.Target);
                }
                else
                {
                    _pendingDeaths.Add(r.Target);
                    _pendingDeathResults[r.Target] = r;
                }
            }

            // Flush deferred deaths on last hit.
            if (isLastHit && _pendingDeaths.Count > 0)
            {
                if (specificTarget != null)
                {
                    // AoE per-actor: flush only this actor's deferred death.
                    if (_pendingDeaths.Contains(specificTarget)
                        && _actorPawns.TryGetValue(specificTarget, out var dp))
                    {
                        dp.GetComponentInChildren<CombatPawnAnimator>()?.PlayDeath();
                        StartCoroutine(HidePawnDelayed(dp.gameObject, 1.2f));
                    }
                    _pendingDeaths.Remove(specificTarget);
                    _pendingDeathResults.Remove(specificTarget);
                }
                else
                {
                    // Single-target or non-specific AoE: flush all.
                    foreach (var deadActor in _pendingDeaths)
                    {
                        if (!_actorPawns.TryGetValue(deadActor, out var deadPawn)) continue;
                        deadPawn.GetComponentInChildren<CombatPawnAnimator>()?.PlayDeath();
                        StartCoroutine(HidePawnDelayed(deadPawn.gameObject, 1.2f));
                    }
                    _pendingDeaths.Clear();
                    _pendingDeathResults.Clear();
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

        protected IEnumerator RunCombatEndDrama()
        {
            if (_ctx != null && _ctx.PlayerWon && _finisherManager != null)
                yield return _finisherManager.Play(_killingBlowAttackerPos, _killingBlowTargetPos);
            else if (_ctx != null && _ctx.PlayerWon)
                yield return StartCoroutine(FallbackLastHitDrama());
            // Defeat path: no drama — fall through so VictorySequencer / defeat screen run immediately.
        }

        private IEnumerator FallbackLastHitDrama()
        {
            Time.timeScale = _lastHitSlowMoScale;
            yield return new WaitForSecondsRealtime(_lastHitSlowMoDuration);
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(_lastHitPauseDuration);
        }

        private void TryCaptureKillingBlowPositions(CombatActionResult[] results)
        {
            if (_ctx == null || !_ctx.IsOver || !_ctx.PlayerWon) return;
            for (int i = 0; i < results.Length; i++)
            {
                var r = results[i];
                if (r.Target == null || r.Target.IsAlive) continue;
                _killingBlowAttackerPos = _actorPawns.TryGetValue(r.Caster, out var ap) ? ap.position : Vector3.zero;
                _killingBlowTargetPos   = _actorPawns.TryGetValue(r.Target,  out var tp) ? tp.position : Vector3.zero;
                return;
            }
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
            if (pending.Skill is DefaultSkillData defaultSkill)
                return defaultSkill.animSequence;

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

        private int GetImpactClipIndex(PendingAction pending)
        {
            if (pending.Skill is DefaultSkillData defaultSkill)
                return defaultSkill.impactAfterClipIndex;
            return 0;
        }

        private int ResolveReturnClipIndex(PendingAction pending, AnimationClip[] clips)
        {
            int raw = -1;
            if (pending.Skill is DefaultSkillData defaultSkill)
                raw = defaultSkill.returnAtClipIndex;
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
