using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Runefall.Audio;
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
    /// Refactored to act as a Mediator delegating responsibilities to dedicated micro-classes.
    /// </summary>
    public class CombatAnimationDriver : MonoBehaviour, IEnemyPhaseAnimator
    {
        [Header("Animation")]
        [Tooltip("Base controller applied to each pawn via AnimatorOverrideController.")]
        public RuntimeAnimatorController combatBaseController;

        [Header("Timing")]
        [Tooltip("Seconds the camera settles on the enemy side before the first enemy acts. Higher = the " +
                 "combat camera fully arrives before the per-skill camera takes over, avoiding a jarring double move.")]
        public float enemyPhaseDelay = 1.4f;
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

        [Header("Skill Timelines")]
        [Tooltip("Optional shared PlayableDirector that plays skill choreography Timelines. Created at runtime if not assigned.")]
        [SerializeField] private PlayableDirector skillTimelineDirector;

        [Header("Skill Camera (shared, per rank — caster-relative)")]
        [Tooltip("Master toggle for the per-rank skill camera. Off = leave the gameplay camera.")]
        [SerializeField] private bool _useSkillCamera = true;
        [Tooltip("Bronze/Silver rest pose: camera height above the caster's feet (over-shoulder).")]
        [SerializeField] private float _bronzeHeight = 2.3f;
        [Tooltip("Bronze rest pose: distance behind the caster's back.")]
        [SerializeField] private float _bronzeBack = 2.5f;
        [Tooltip("Bronze rest pose: offset to the caster's RIGHT shoulder.")]
        [SerializeField] private float _bronzeRight = 0.6f;
        [Tooltip("Face close-up (silver/gold start): distance in front of the caster's face.")]
        [SerializeField] private float _faceDist = 1.6f;
        [Tooltip("Face close-up: height of the caster's face.")]
        [SerializeField] private float _faceHeight = 1.65f;
        [Tooltip("Seconds to blend into the bronze pose at the start.")]
        [SerializeField] private float _camBlendIn = 0.25f;
        [Tooltip("Silver: seconds the face close-up holds before returning to the bronze pose.")]
        [SerializeField] private float _silverFaceHold = 0.5f;
        [Tooltip("Silver: seconds to travel from the face close-up back to the bronze pose.")]
        [SerializeField] private float _silverReturnDur = 0.5f;
        [Tooltip("Gold: seconds the face close-up holds before the orbit starts.")]
        [SerializeField] private float _goldFaceHold = 0.4f;
        [Tooltip("Gold: orbit speed (legacy orbit — unused by the whip+settle path).")]
        [SerializeField] private float _goldOrbitSpeed = 0.7f;
        [Header("Gold — cut sequence (cinematic)")]
        [Tooltip("Impact shot: distance pulled back from the action center (caster<->target). Higher = wider/panoramic.")]
        [SerializeField] private float _goldEndBack = 4.0f;
        [Tooltip("Impact shot: lateral offset.")]
        [SerializeField] private float _goldEndRight = 1.8f;
        [Tooltip("Impact shot: height above the action center.")]
        [SerializeField] private float _goldEndHeight = 2.2f;
        [Tooltip("(Legacy, unused by the cut sequence.)")]
        [SerializeField] private float _goldWhipMinArc = 210f;
        [Tooltip("Seconds the low-hero anticipation cut holds.")]
        [SerializeField] private float _goldCut1Hold = 0.40f;
        [Tooltip("Seconds the wind-up close-up cut holds.")]
        [SerializeField] private float _goldCut2Hold = 0.35f;
        [Tooltip("Camera shake magnitude on the impact cut.")]
        [SerializeField] private float _goldShakeMagnitude = 0.12f;
        [Tooltip("Pause (seconds) between consecutive abilities so the camera settles and doesn't disorient the player.")]
        [SerializeField] private float _betweenSkillsDelay = 0.6f;

        [Header("Damage Numbers")]
        [Tooltip("FloatingDamage prefab with DamageNumber component. Spawned per hit.")]
        [SerializeField] private GameObject _damageNumberPrefab;

        [Header("Turn Transition")]
        [Tooltip("Seconds between last player animation finishing and enemy turn starting.")]
        [SerializeField] private float _postPlayerTurnDelay = 0.8f;

        [Header("Last Hit Drama")]
        [Tooltip("Full cinematic finisher on last-enemy kill. When null, falls back to simple slow-mo.")]
        [SerializeField] private FinisherManager _finisherManager;
        [SerializeField] private VictorySequencer _victorySequencer;

        [Tooltip("Time.timeScale during the fallback slow-mo (used when FinisherManager is not assigned).")]
        [SerializeField] private float _lastHitSlowMoScale    = 0.05f;
        [Tooltip("Real seconds the fallback slow-mo lasts.")]
        [SerializeField] private float _lastHitSlowMoDuration = 0.55f;
        [Tooltip("Real seconds of pause after fallback slow-mo before the result screen appears.")]
        [SerializeField] private float _lastHitPauseDuration  = 0.30f;

        public FinisherManager Finisher => _finisherManager;

        // Injected dependencies
        private CombatContext                                     _ctx;
        private TurnManager                                       _tm;
        private IReadOnlyDictionary<ICombatActor, Transform>      _actorPawns;
        private IReadOnlyDictionary<ICombatActor, CharacterData>  _actorCharData;
        private IReadOnlyDictionary<ICombatActor, EnemyData>      _actorEnemyData;
        private IReadOnlyDictionary<ICombatActor, HPBarPresenter> _actorHPBars;
        private ICombatPresenter                                   _presenter;
        private Action<ICombatActor, Transform>                    _updatePawn;
        private Camera                                             _camera;

        // Micro-class delegators
        private PawnMovementChoreographer _movementChoreographer;
        private SkillCameraDirector       _cameraDirector;
        private TimelineChoreographer     _timelineChoreographer;
        private CombatFeedbackManager     _feedbackManager;
        private PawnVisualManager         _visualManager;
        private CombatClimaxDirector      _climaxDirector;
        private BossTransitionPresenter   _bossTransitionPresenter;
        private CombatQueueManager        _queueManager;

        // Public event
        public event Action OnVictoryOutroTriggered;

        // Deferred death lists
        private readonly HashSet<ICombatActor>                      _pendingDeaths       = new();
        private readonly Dictionary<ICombatActor, CombatActionResult> _pendingDeathResults = new();

        // Timeline-skill signal state: Skill_Damage emitters drive RaiseImpactHit, Skill_VFX_<n> drive cues.
        private bool _skillDamageFiredThisPlay;
        private int  _skillHitIndex;

        public void Init(
            CombatContext                                     ctx,
            TurnManager                                       tm,
            IReadOnlyDictionary<ICombatActor, Transform>      actorPawns,
            IReadOnlyDictionary<ICombatActor, CharacterData>  actorCharData,
            IReadOnlyDictionary<ICombatActor, EnemyData>      actorEnemyData,
            IReadOnlyDictionary<ICombatActor, HPBarPresenter> actorHPBars,
            ICombatPresenter                                   presenter,
            Action<ICombatActor, Transform>                    updatePawn)
        {
            _ctx            = ctx;
            _tm             = tm;
            _actorPawns     = actorPawns;
            _actorCharData  = actorCharData;
            _actorEnemyData = actorEnemyData;
            _actorHPBars    = actorHPBars;
            _presenter      = presenter;
            _updatePawn     = updatePawn;
            _camera         = Camera.main;

            // Initialize delegators
            _movementChoreographer = new PawnMovementChoreographer(lungeStopDistance);

            var cameraConfig = new CameraConfig
            {
                useSkillCamera = _useSkillCamera,
                bronzeHeight = _bronzeHeight,
                bronzeBack = _bronzeBack,
                bronzeRight = _bronzeRight,
                faceDist = _faceDist,
                faceHeight = _faceHeight,
                camBlendIn = _camBlendIn,
                silverFaceHold = _silverFaceHold,
                silverReturnDur = _silverReturnDur,
                goldFaceHold = _goldFaceHold,
                goldOrbitSpeed = _goldOrbitSpeed,
                goldEndBack = _goldEndBack,
                goldEndRight = _goldEndRight,
                goldEndHeight = _goldEndHeight,
                goldWhipMinArc = _goldWhipMinArc,
                goldCut1Hold = _goldCut1Hold,
                goldCut2Hold = _goldCut2Hold,
                goldShakeMagnitude = _goldShakeMagnitude
            };
            _cameraDirector = new SkillCameraDirector(cameraConfig, () => _climaxDirector != null && _climaxDirector.HasTriggeredOutroClimax);

            _timelineChoreographer = new TimelineChoreographer(transform, skillTimelineDirector);
            _feedbackManager       = new CombatFeedbackManager(_damageNumberPrefab, vfxPlayer);
            _visualManager         = new PawnVisualManager(combatBaseController);
            
            _climaxDirector        = new CombatClimaxDirector(
                this, 
                _finisherManager, 
                _victorySequencer, 
                _lastHitSlowMoScale, 
                _lastHitSlowMoDuration, 
                _lastHitPauseDuration);

            _bossTransitionPresenter = new BossTransitionPresenter(
                this,
                _actorPawns,
                _actorHPBars,
                _presenter,
                combatBaseController,
                _cameraDirector,
                _timelineChoreographer,
                _updatePawn);

            _queueManager = new CombatQueueManager(
                this,
                PlayActionGroup,
                () => _climaxDirector.RunCombatEndDrama(_ctx),
                _presenter,
                () => _ctx != null && _ctx.IsOver,
                _postPlayerTurnDelay,
                _betweenSkillsDelay,
                _bossTransitionPresenter,
                () => _ctx,
                () => { if (_tm != null) _tm.SkipNextEnemyPhase = true; });

            _climaxDirector.OnVictoryOutroTriggered += () => OnVictoryOutroTriggered?.Invoke();

            impactEvent?.Subscribe(OnImpactReceived);
            InitPawnAnimators();
        }

        private void OnDestroy()
        {
            impactEvent?.Unsubscribe(OnImpactReceived);
            if (_timelineChoreographer != null)
                _timelineChoreographer.CleanupSkillVcams();
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
                executeTurn(enemies[i]);
                yield return StartCoroutine(_queueManager.DrainQueue(null));
            }

            onComplete();
        }

        // ── public API called by CombatBootstrapper ───────────────────────────────

        public void Enqueue(PendingAction pending) => _queueManager.Enqueue(pending);
        public void ClearQueue() => _queueManager.ClearQueue();
        public void PlayQueuedAnimations(Action onComplete, bool fadeSlots = false) => _queueManager.PlayQueuedAnimations(onComplete, fadeSlots);

        // ── pawn animators ────────────────────────────────────────────────────────

        private void InitPawnAnimators()
        {
            _visualManager?.InitPawnAnimators(_actorPawns, _actorCharData, _actorEnemyData);
        }

        /// <summary>Toggles an actor's world-space HP bar. Hidden while the skill camera frames a close-up
        /// (the caster's own bar billboards into frame), restored when the gameplay camera resumes.</summary>
        private void SetActorHPBarVisible(ICombatActor actor, bool visible)
        {
            if (actor != null && _actorHPBars.TryGetValue(actor, out var bar) && bar != null)
            {
                if (visible) bar.Show();   // fade in so it doesn't pop back from nothing
                else         bar.Hide();
            }
        }

        // ── action group ──────────────────────────────────────────────────────────

        private IEnumerator PlayActionGroup(PendingAction pending)
        {
            if (pending.Skill != null)
            {
                bool finished = false;
                pending.Skill.PlayPresentation(this, pending.Caster, pending.Target, pending.Rank, () => finished = true);
                yield return new WaitUntil(() => finished);
            }
        }

        public void PlayDefaultAnimationSequence(
            SkillData skill, 
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
                skill != null ? skill.targetType : TargetType.SingleEnemy, 
                skill is UltimateData
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

            var   vfxConfig      = ResolveVFXConfig(pending);
            bool  holdLoop       = vfxConfig != null && vfxConfig.holdAnimLoopUntilVFXDone;
            int   loopClipIdx    = holdLoop ? vfxConfig.animLoopClipIndex : -1;
            float lastSlashTime  = float.MinValue;
            const float kSlashDebounce = 0.08f;
            GameObject slashGO   = null;

            System.Func<bool> exitLoopWhen = holdLoop ? () => slashGO == null : (System.Func<bool>)null;

            if (vfxConfig?.spawnOnStartImmediately == true)
                slashGO = _feedbackManager.PlayOnStartVFX(vfxConfig, casterPawn, targetPos);

            void HandleSlashVFX()
            {
                if (vfxConfig?.spawnOnStartImmediately == true) return;
                if (Time.time - lastSlashTime < kSlashDebounce) return;
                lastSlashTime = Time.time;
                if (slashGO != null)
                {
                    foreach (var ps in slashGO.GetComponentsInChildren<ParticleSystem>())
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                slashGO = _feedbackManager.PlayOnStartVFX(vfxConfig, casterPawn, targetPos);
            }
            if (casterAnim != null) casterAnim.OnSlashVFX += HandleSlashVFX;

            Coroutine camOrbit = null;
            if (_useSkillCamera)
            {
                var bossTargetTf = GetTargetTransform(pending);
                if (pending.Caster is IMultiPhaseActor && bossTargetTf != null)
                {
                    // Boss attack → over the PLAYER's shoulder looking at the boss (bronze-style). The boss
                    // is too big for its own shoulder cam; this frames the strike from the player POV.
                    SetActorHPBarVisible(pending.Target, false);
                    camOrbit = StartCoroutine(_cameraDirector.SkillCameraRoutine(1, bossTargetTf, casterPawn.position, SumClipDurations(clips, 0, clips.Length - 1)));
                }
                else
                {
                    SetActorHPBarVisible(pending.Caster, false);   // hide caster's bar during the skill camera
                    camOrbit = StartCoroutine(_cameraDirector.SkillCameraRoutine(pending.Rank, casterPawn, targetPos, SumClipDurations(clips, 0, clips.Length - 1)));
                }
            }

            try
            {
                if (isRanged)
                {
                    _movementChoreographer.RotateCasterToward(casterPawn, targetPos);

                    if (trigger != null)
                    {
                        trigger.Arm((hitIdx, total, specificActor) => RaiseImpactHit(pending, hitIdx, total, specificActor));
                        bool animDone = casterAnim == null;
                        if (casterAnim != null)
                            StartCoroutine(RunThenSignal(
                                casterAnim.PlaySkillSequence(clips, holdClipIndex: loopClipIdx, shouldAdvanceFromHold: exitLoopWhen),
                                () => animDone = true));
                        yield return new WaitUntil(() => animDone);
                        if (!trigger.HasFired)
                        {
                            RaiseImpactHit(pending, 0, 1);
                        }
                    }
                    else
                    {
                        RaiseImpactHit(pending, 0, 1);
                        if (casterAnim != null)
                            yield return StartCoroutine(casterAnim.PlaySkillSequence(clips, holdClipIndex: loopClipIdx, shouldAdvanceFromHold: exitLoopWhen));
                    }
                }
                else
                {
                    int returnIdx = ResolveReturnClipIndex(pending, clips);
                    Vector3 origin = casterPawn.position;
                    Vector3 lungeTarget = _movementChoreographer.ComputeLungeTarget(casterPawn, targetPos);

                    float approachDelay  = SumClipDurations(clips, 0, impactIdx - 1);
                    float approachDur    = clips != null && impactIdx >= 0 && impactIdx < clips.Length
                                           ? clips[impactIdx]?.length ?? 0f : 0f;
                    float approachRotDur = approachDelay > blend ? approachDelay : blend;
                    StartCoroutine(_movementChoreographer.SmoothRotateTo(casterPawn, targetPos, 0f, approachRotDur));
                    if (approachDur > 0f)
                        StartCoroutine(_movementChoreographer.DelayedLungeTo(casterPawn, lungeTarget, approachDelay, approachDur));

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
                        StartCoroutine(_movementChoreographer.SmoothRotateTo(casterPawn, origin, rotBegin, returnRotateDuration));
                        returnLunge = StartCoroutine(_movementChoreographer.DelayedLungeTo(casterPawn, origin, lungeStart, returnDur, snapOnEnd: true));
                    }

                    if (trigger != null)
                    {
                        trigger.Arm((hitIdx, total, specificActor) => RaiseImpactHit(pending, hitIdx, total, specificActor));
                        bool animDone = casterAnim == null;
                        if (casterAnim != null)
                            StartCoroutine(RunThenSignal(
                                casterAnim.PlaySkillSequence(clips, holdClipIndex: loopClipIdx, shouldAdvanceFromHold: exitLoopWhen),
                                () => animDone = true));
                        yield return new WaitUntil(() => animDone);
                        if (!trigger.HasFired)
                        {
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
                        float approachLen = GetApproachClipDuration(pending.Caster);
                        yield return StartCoroutine(_movementChoreographer.SmoothRotateTo(casterPawn, origin, 0f, returnRotateDuration));
                        casterAnim?.PlayApproach();
                        yield return StartCoroutine(_movementChoreographer.LungeTo(casterPawn, origin, approachLen, null));
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
            }
            finally
            {
                if (casterAnim != null) casterAnim.OnSlashVFX -= HandleSlashVFX;
                if (slashGO != null)
                {
                    foreach (var ps in slashGO.GetComponentsInChildren<ParticleSystem>())
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                if (camOrbit != null)
                {
                    StopCoroutine(camOrbit);
                }
                // Restore unless the killing blow just ended combat (the outro hides all UI + HP bars).
                if (_useSkillCamera && (_ctx == null || !_ctx.IsOver))
                    SetActorHPBarVisible(pending.Caster is IMultiPhaseActor ? pending.Target : pending.Caster, true);
            }

            onComplete?.Invoke();
        }

        public void PlayTimelineSkill(
            SkillData skill,
            ICombatActor caster,
            ICombatActor target,
            int rank,
            System.Action onComplete)
        {
            StartCoroutine(PlayTimelineSkillCoroutine(skill, caster, target, rank, onComplete));
        }

        private IEnumerator PlayTimelineSkillCoroutine(
            SkillData skill,
            ICombatActor caster,
            ICombatActor target,
            int rank,
            System.Action onComplete)
        {
            var timelineSkill = skill as ITimelineSkill;
            if (skill == null || timelineSkill?.SkillTimeline == null)
            {
                onComplete?.Invoke();
                yield break;
            }
            var timeline = timelineSkill.SkillTimeline as TimelineAsset;
            if (timeline == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            var pending = new PendingAction(caster, target, skill, null, rank, skill.targetType, skill is UltimateData);
            _pendingDeaths.Clear();
            _pendingDeathResults.Clear();

            Animator casterAnim = null;
            Transform cp = null;
            if (caster != null && _actorPawns.TryGetValue(caster, out cp))
                casterAnim = cp.GetComponentInChildren<Animator>();

            Animator targetAnim = null;
            Transform tp = null;
            if (target != null && _actorPawns.TryGetValue(target, out tp))
                targetAnim = tp.GetComponentInChildren<Animator>();

            // Skill camera is code-driven per rank (SkillCameraDirector poses Camera.main directly with
            // the Cinemachine brain OFF). The timeline drives ONLY animation, VFX and damage signals —
            // it has no Cinemachine tracks, so we never bind the brain to it.
            var brain = Camera.main != null ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
            if (brain != null) brain.enabled = false;

            Vector3 targetPos = Vector3.zero;
            if (tp != null)
            {
                targetPos = tp.position;
            }
            else
            {
                bool targetEnemies = skill.targetType == TargetType.AllEnemies || skill.targetType == TargetType.SingleEnemy || skill.targetType == TargetType.RandomEnemy;
                var targets = targetEnemies ? (System.Collections.Generic.IReadOnlyList<ICombatActor>)_ctx.Enemies : _ctx.Players;
                Vector3 centroid = Vector3.zero;
                int count = 0;
                foreach (var a in targets)
                {
                    if (a.IsAlive && _actorPawns.TryGetValue(a, out var p))
                    {
                        centroid += p.position;
                        count++;
                    }
                }
                if (count > 0)
                {
                    targetPos = centroid / count;
                }
            }

            if (cp != null && targetPos == Vector3.zero)
                targetPos = cp.position + cp.forward * 5f;

            var director = _timelineChoreographer.ResolveSkillTimelineDirector();
            director.playableAsset  = timeline;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;

            // Bind the caster/target animator outputs (camera is handled in code, not the timeline)
            foreach (var output in timeline.outputs)
            {
                if (output.outputTargetType == typeof(Unity.Cinemachine.CinemachineBrain) && brain != null)
                {
                    // Cinemachine tracks are bound selectively by rank in BindSkillTimelineTracks.
                    // Doing SetGenericBinding here for all of them would override muting.
                    continue;
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

            float duration = (float)timeline.duration;
            if (duration <= 0.05f) duration = 2f;

            // Shared per-rank skill camera (bronze: over-shoulder; silver/gold: face close-up first,
            // then move). It poses Camera.main directly and self-restores the gameplay camera when done.
            if (_useSkillCamera && cp != null)
            {
                if (caster is IMultiPhaseActor && tp != null)
                {
                    // Boss attack → over the PLAYER's shoulder looking at the boss (bronze-style). The boss
                    // is too big for its own shoulder cam; this keeps the strike framed from the player POV.
                    SetActorHPBarVisible(target, false);
                    StartCoroutine(_cameraDirector.SkillCameraRoutine(1, tp, cp.position, duration));
                }
                else
                {
                    SetActorHPBarVisible(caster, false);   // hide caster's bar during the skill camera
                    StartCoroutine(_cameraDirector.SkillCameraRoutine(rank, cp, targetPos, duration));
                }
            }

            // Silver/gold open on a face close-up: hold the timeline content (anim + VFX + damage)
            // until the close-up ends so the choreography plays WHILE the camera travels (bronze = 0).
            float introDelay = GetSkillIntroDelay(rank);
            if (introDelay > 0f) yield return new WaitForSeconds(introDelay);

            // Damage + VFX fire from named Signal emitters, scheduled by authored time (a runtime
            // SignalReceiver doesn't reliably receive notifications — same approach as the outro).
            ScheduleSkillSignals(director, skill, pending, cp, targetPos);

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

            float elapsed = 0f;
            while (!timelineDone && elapsed < duration + 0.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            director.stopped -= onStopped;

            // Safety net: if no Skill_Damage emitter fired, resolve damage once so combat still progresses.
            if (!_skillDamageFiredThisPlay)
                RaiseImpactHit(pending, 0, Mathf.Max(1, skill.HitCount));

            // Restore the caster's bar as gameplay resumes — but NOT if this was the killing blow:
            // combat is over and the victory/defeat outro already hid all combat UI + HP bars.
            if (_useSkillCamera && cp != null && (_ctx == null || !_ctx.IsOver))
                SetActorHPBarVisible(caster is IMultiPhaseActor ? target : caster, true);

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

            Animator casterAnim = null;
            Transform cp = null;
            if (caster != null && _actorPawns.TryGetValue(caster, out cp))
                casterAnim = cp.GetComponentInChildren<Animator>();

            Animator targetAnim = null;
            Transform tp = null;
            if (target != null && _actorPawns.TryGetValue(target, out tp))
                targetAnim = tp.GetComponentInChildren<Animator>();

            var brain = Camera.main != null ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
            var cameraController = Camera.main != null ? Camera.main.GetComponent<CombatCameraController>() : null;

            if (brain != null) brain.enabled = true;
            if (cameraController != null) cameraController.enabled = false;

            _timelineChoreographer.CleanupSkillVcams();
            if (cp != null && tp != null)
            {
                _timelineChoreographer.CreateSkillVcams(cp, tp.position, tp);
            }

            var director = _timelineChoreographer.ResolveSkillTimelineDirector();
            director.playableAsset = timeline;

            _timelineChoreographer.BindSkillTimelineTracks(director, 1, casterAnim, brain);

            // Bind outputs dynamically
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

            if (brain != null) brain.enabled = false;
            if (cameraController != null)
            {
                cameraController.enabled = true;
                if (_tm != null)
                {
                    cameraController.SnapToAnchor(_tm.Phase == CombatPhase.EnemyTurn);
                }
            }

            onComplete?.Invoke();
        }

        // ── trigger helpers ───────────────────────────────────────────────────────

        private SkillVFXConfig ResolveVFXConfig(PendingAction pending)
        {
            return pending.Skill != null ? pending.Skill.VfxConfig : null;
        }

        private IImpactTrigger ResolveTrigger(PendingAction pending, Transform casterPawn, Transform targetPawn, AnimationClip[] allClips = null)
        {
            int hitCount = 0;
            ImpactTriggerData triggerData = null;

            if (pending.Skill != null)
            {
                hitCount = pending.Skill.HitCount > 1 ? pending.Skill.HitCount : 0;
                triggerData = pending.Skill.ImpactTrigger;
            }

            List<(ICombatActor actor, Transform pawn)> aoeTargets = null;
            bool isAoeRanged = (pending.TargetType == TargetType.AllEnemies || pending.TargetType == TargetType.AllAllies) && (pending.Skill?.isRanged ?? false);
            if (isAoeRanged && _ctx != null)
            {
                var side = pending.TargetType == TargetType.AllEnemies ? (IReadOnlyList<ICombatActor>)_ctx.Enemies : _ctx.Players;
                aoeTargets = new List<(ICombatActor, Transform)>();
                foreach (var actor in side)
                {
                    if (actor.IsAlive && _actorPawns.TryGetValue(actor, out var p))
                        aoeTargets.Add((actor, p));
                }
                if (aoeTargets.Count == 0) aoeTargets = null;
            }

            return triggerData?.Create(casterPawn, targetPawn, this, allClips, hitCount, aoeTargets);
        }

        private Transform GetTargetTransform(PendingAction pending)
        {
            if (pending.Target != null && _actorPawns.TryGetValue(pending.Target, out var tp))
                return tp;
            bool isEnemy = pending.Caster is EnemyAgent;
            var side = isEnemy ? (IReadOnlyList<ICombatActor>)_ctx.Players : _ctx.Enemies;
            foreach (var a in side)
            {
                if (a.IsAlive && _actorPawns.TryGetValue(a, out var p)) return p;
            }
            return null;
        }

        private IEnumerator RunThenSignal(IEnumerator inner, Action onDone)
        {
            yield return inner;
            onDone?.Invoke();
        }

        // ── impact ────────────────────────────────────────────────────────────────

        protected void RaiseImpactHit(PendingAction pending, int hitIdx, int totalHits, ICombatActor specificTarget = null)
        {
            if (_tm == null) return;

            float hitFraction = totalHits > 1 ? 1f / totalHits : 1f;
            bool  isLastHit   = hitIdx >= totalHits - 1;

            CombatActionResult[] results = specificTarget != null
                ? _tm.ResolveForTarget(pending, hitFraction, specificTarget)
                : _tm.ResolveAction(pending, hitFraction);

            _climaxDirector.TryCaptureKillingBlowPositions(_ctx, results, _actorPawns);
            _climaxDirector.TryTriggerOutroClimax(_ctx, () => OnVictoryOutroTriggered?.Invoke());

            // Visual impact feedback
            for (int i = 0; i < results.Length; i++)
            {
                var r = results[i];
                Vector3 hitPos = r.Target != null && _actorPawns.TryGetValue(r.Target, out var tp)
                    ? tp.position + Vector3.up * 1.2f
                    : Vector3.zero;

                var ctx = ImpactContext.FromResult(r, hitPos);
                if (impactEvent != null)
                    _feedbackManager.PlayOnImpactVFX(impactEvent, ctx);
                else
                    OnImpactReceived(ctx);
            }

            // Visual-only overkill hits on dead-deferred targets
            if (_pendingDeaths.Count > 0)
            {
                if (specificTarget != null)
                {
                    if (_pendingDeaths.Contains(specificTarget) && _pendingDeathResults.TryGetValue(specificTarget, out var deadRes) && _actorPawns.TryGetValue(specificTarget, out var dp))
                    {
                        dp.GetComponentInChildren<CombatPawnAnimator>()?.PlayHit();
                        _feedbackManager.SpawnDamageNumber(dp, deadRes.DamageDealt, deadRes.IsCrit);
                    }
                }
                else
                {
                    foreach (var (deadActor, deadResult) in _pendingDeathResults)
                    {
                        if (!_actorPawns.TryGetValue(deadActor, out var dp)) continue;
                        dp.GetComponentInChildren<CombatPawnAnimator>()?.PlayHit();
                        _feedbackManager.SpawnDamageNumber(dp, deadResult.DamageDealt, deadResult.IsCrit);
                    }
                }
            }

            // Death handling
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

            // Flush deferred deaths
            if (isLastHit && _pendingDeaths.Count > 0)
            {
                if (specificTarget != null)
                {
                    if (_pendingDeaths.Contains(specificTarget) && _actorPawns.TryGetValue(specificTarget, out var dp))
                    {
                        dp.GetComponentInChildren<CombatPawnAnimator>()?.PlayDeath();
                        StartCoroutine(HidePawnDelayed(dp.gameObject, 1.2f));
                    }
                    _pendingDeaths.Remove(specificTarget);
                    _pendingDeathResults.Remove(specificTarget);
                }
                else
                {
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

        private void OnImpactReceived(ImpactContext ctx)
        {
            Transform targetPawn = null;
            bool hasPawn = ctx.Target != null && _actorPawns.TryGetValue(ctx.Target, out targetPawn);
            if (ctx.DamageDealt > 0f && ctx.Target != null && hasPawn)
            {
                targetPawn.GetComponentInChildren<CombatPawnAnimator>()?.PlayHit();
                _feedbackManager.SpawnDamageNumber(targetPawn, ctx.DamageDealt, ctx.IsCrit);
            }

            if (ctx.Target != null && _actorHPBars.TryGetValue(ctx.Target, out var targetBar))
            {
                if (ctx.DamageDealt > 0f) targetBar.ApplyVisualDamage(ctx.DamageDealt);
                if (ctx.HealApplied > 0f) targetBar.ApplyVisualHeal(ctx.HealApplied);
            }

            if (ctx.LifeStealApplied > 0f && ctx.Attacker != null && _actorHPBars.TryGetValue(ctx.Attacker, out var attackerBar))
            {
                attackerBar.ApplyVisualHeal(ctx.LifeStealApplied);
            }
        }

        private IEnumerator HidePawnDelayed(GameObject pawn, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (pawn != null) pawn.SetActive(false);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private AnimationClip[] GetSkillClips(PendingAction pending)
        {
            if (pending.Skill != null)
                return pending.Skill.AnimSequence;
            return null;
        }

        private int GetImpactClipIndex(PendingAction pending)
        {
            if (pending.Skill != null)
                return pending.Skill.ImpactAfterClipIndex;
            return 0;
        }

        private int ResolveReturnClipIndex(PendingAction pending, AnimationClip[] clips)
        {
            int raw = pending.Skill != null ? pending.Skill.ReturnLungeClipIndex : -1;
            // -1 → "return after all clips": use clips.Length as the sentinel so `afterAll` triggers a
            // clean sequential return (approach back home) once the whole sequence has played. Without
            // this the pawn lunges in, hits, and never walks back (returnLunge stays null).
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

        private float GetSkillIntroDelay(int rank)
        {
            if (rank == 2) return _silverFaceHold;
            if (rank >= 3) return _goldFaceHold;
            return 0f;
        }

        // ── timeline-skill signals (Damage / VFX choreography) ──────────────────────

        private void ScheduleSkillSignals(
            PlayableDirector director,
            SkillData skill,
            PendingAction pending,
            Transform casterPawn,
            Vector3 targetPos)
        {
            _skillDamageFiredThisPlay = false;
            _skillHitIndex = 0;

            if (!(director.playableAsset is TimelineAsset timeline)) return;
            int hits = Mathf.Max(1, skill.HitCount);

            foreach (var track in timeline.GetOutputTracks())
            {
                if (!(track is SignalTrack sig)) continue;
                foreach (var marker in sig.GetMarkers())
                {
                    if (!(marker is SignalEmitter em) || em.asset == null) continue;
                    float  t    = (float)em.time;
                    string name = em.asset.name;

                    if (name == "Skill_Damage")
                    {
                        StartCoroutine(FireSkillBeat(t, () =>
                        {
                            RaiseImpactHit(pending, _skillHitIndex, hits, null);
                            _skillHitIndex++;
                            _skillDamageFiredThisPlay = true;
                        }));
                    }
                    else if (name == "Skill_VFXStart")
                    {
                        StartCoroutine(FireSkillBeat(t, () =>
                        {
                            var startVfx = skill.VfxConfig;
                            if (startVfx != null)
                                _feedbackManager.PlayOnStartVFX(startVfx, casterPawn, targetPos);
                        }));
                    }
                    else if (TryParseVfxCueIndex(name, out int cueIdx))
                    {
                        StartCoroutine(FireSkillBeat(t, () => SpawnSkillVFXCue(skill, cueIdx, casterPawn, targetPos)));
                    }
                    else if (TryParseSfxCueIndex(name, out int sfxIdx))
                    {
                        StartCoroutine(FireSkillBeat(t, () => PlaySkillSFXCue(skill, sfxIdx, casterPawn, targetPos)));
                    }
                    else
                    {
                        Debug.LogWarning($"[CombatAnimationDriver] Unmapped skill signal '{name}'.");
                    }
                }
            }
        }

        private IEnumerator FireSkillBeat(float delay, Action action)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay); // GameTime — matches the director clock
            action?.Invoke();
        }

        private static bool TryParseVfxCueIndex(string signalName, out int index)
        {
            index = -1;
            const string prefix = "Skill_VFX_";
            return signalName != null && signalName.StartsWith(prefix)
                   && int.TryParse(signalName.Substring(prefix.Length), out index);
        }

        private static bool TryParseSfxCueIndex(string signalName, out int index)
        {
            index = -1;
            const string prefix = "Skill_SFX_";
            return signalName != null && signalName.StartsWith(prefix)
                   && int.TryParse(signalName.Substring(prefix.Length), out index);
        }

        private void SpawnSkillVFXCue(SkillData skill, int index, Transform casterPawn, Vector3 targetPos)
        {
            var cues = (skill as ITimelineSkill)?.VfxCues;
            if (cues == null || index < 0 || index >= cues.Length)
            {
                Debug.LogWarning($"[CombatAnimationDriver] Skill_VFX_{index} fired but the skill has no vfxCues[{index}].");
                return;
            }
            var cue = cues[index];
            if (cue == null || cue.prefab == null) return;

            if (cue.anchor == VFXAnchor.AllEnemies)
            {
                if (_ctx != null)
                    foreach (var e in _ctx.Enemies)
                        if (e != null && e.IsAlive && _actorPawns.TryGetValue(e, out var et))
                            InstantiateCue(cue, et.position, casterPawn, targetPos);
                return;
            }

            InstantiateCue(cue, ResolveVFXAnchor(cue.anchor, casterPawn, targetPos), casterPawn, targetPos);
        }

        // Plays a timed SFX cue (fired by a "Skill_SFX_<index>" timeline signal) through the
        // AudioManager, at the cue's anchor. Same model as SpawnSkillVFXCue.
        private void PlaySkillSFXCue(SkillData skill, int index, Transform casterPawn, Vector3 targetPos)
        {
            var cues = (skill as ITimelineSkill)?.SfxCues;
            if (cues == null || index < 0 || index >= cues.Length)
            {
                Debug.LogWarning($"[CombatAnimationDriver] Skill_SFX_{index} fired but the skill has no sfxCues[{index}].");
                return;
            }
            var cue = cues[index];
            if (cue == null || cue.clip == null) return;

            var audio = ResolveAudio();
            if (audio == null) return;

            Vector3 pos   = ResolveVFXAnchor(cue.anchor, casterPawn, targetPos);
            float   pitch = cue.pitchJitter > 0f
                ? 1f + UnityEngine.Random.Range(-cue.pitchJitter, cue.pitchJitter)
                : 1f;
            audio.PlayAt(cue.clip, pos, cue.volume, pitch);
        }

        private IAudioService _audio;
        private IAudioService ResolveAudio() => _audio ??= AudioManager.GetOrCreate();

        private void InstantiateCue(SkillVFXCue cue, Vector3 basePos, Transform casterPawn, Vector3 targetPos)
        {
            var go = Instantiate(cue.prefab, basePos + cue.worldOffset, CueRotation(cue, casterPawn, targetPos));
            if (cue.scale != Vector3.zero) go.transform.localScale = cue.scale;
            if (cue.autoDestroyAfter > 0f) Destroy(go, cue.autoDestroyAfter);
        }

        private Quaternion CueRotation(SkillVFXCue cue, Transform casterPawn, Vector3 targetPos)
        {
            if (!cue.faceTarget) return Quaternion.Euler(cue.rotationOffset);
            Vector3    from = casterPawn != null ? casterPawn.position : targetPos;
            Vector3    dir  = targetPos - from; dir.y = 0f;
            Quaternion look = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
            return look * Quaternion.Euler(cue.rotationOffset);
        }

        private Vector3 ResolveVFXAnchor(VFXAnchor anchor, Transform casterPawn, Vector3 targetPos)
        {
            switch (anchor)
            {
                case VFXAnchor.Caster:        return casterPawn != null ? casterPawn.position : targetPos;
                case VFXAnchor.EnemiesCenter: return GetEnemiesCenter(targetPos);
                default:                      return targetPos;
            }
        }

        private Vector3 GetEnemiesCenter(Vector3 fallback)
        {
            if (_ctx == null) return fallback;
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var e in _ctx.Enemies)
                if (e != null && e.IsAlive && _actorPawns.TryGetValue(e, out var t)) { sum += t.position; n++; }
            return n > 0 ? sum / n : fallback;
        }

        private Vector3 GetTargetPosition(PendingAction pending)
        {
            bool isAoe = pending.TargetType == TargetType.AllEnemies || pending.TargetType == TargetType.AllAllies;
            if (isAoe)
            {
                var targets = pending.TargetType == TargetType.AllEnemies ? (IReadOnlyList<ICombatActor>)_ctx.Enemies : _ctx.Players;
                Vector3 centroid = Vector3.zero;
                int     count    = 0;
                foreach (var a in targets)
                {
                    if (a.IsAlive && _actorPawns.TryGetValue(a, out var p))
                    { 
                        centroid += p.position; 
                        count++; 
                    }
                }
                return count > 0 ? centroid / count : Vector3.zero;
            }

            if (pending.Target != null && _actorPawns.TryGetValue(pending.Target, out var tp))
                return tp.position;

            return Vector3.zero;
        }

        private static float SumClipDurations(AnimationClip[] clips, int from, int to)
        {
            if (clips == null) return 0f;
            float sum = 0f;
            for (int i = Mathf.Max(0, from); i <= Mathf.Min(to, clips.Length - 1); i++)
                sum += clips[i]?.length ?? 0f;
            return sum;
        }
    }
}
