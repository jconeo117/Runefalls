using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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

        // Injected by CombatBootstrapper via Init()
        private CombatContext                                     _ctx;
        private IReadOnlyDictionary<ICombatActor, Transform>      _actorPawns;
        private IReadOnlyDictionary<ICombatActor, CharacterData>  _actorCharData;
        private IReadOnlyDictionary<ICombatActor, EnemyData>      _actorEnemyData;
        private IReadOnlyDictionary<ICombatActor, HPBarPresenter> _actorHPBars;
        private ICombatPresenter                                   _presenter;

        private readonly Queue<CombatActionResult> _animQueue = new();
        private Camera _camera;

        // ── init ─────────────────────────────────────────────────────────────────

        public void Init(
            CombatContext                                     ctx,
            IReadOnlyDictionary<ICombatActor, Transform>      actorPawns,
            IReadOnlyDictionary<ICombatActor, CharacterData>  actorCharData,
            IReadOnlyDictionary<ICombatActor, EnemyData>      actorEnemyData,
            IReadOnlyDictionary<ICombatActor, HPBarPresenter> actorHPBars,
            ICombatPresenter                                   presenter)
        {
            _ctx            = ctx;
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
                if (!enemies[i].IsAlive) continue;
                executeTurn(enemies[i]);                     // fires TM.OnActionResolved → Enqueue()
                yield return StartCoroutine(DrainQueue(null));
            }

            onComplete();
        }

        // ── public API called by CombatBootstrapper ───────────────────────────────

        /// <summary>Queues a resolved combat action for visual animation.</summary>
        public void Enqueue(CombatActionResult result) => _animQueue.Enqueue(result);

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
                var group = DrainActionGroup();
                yield return StartCoroutine(PlayActionGroup(group));
                if (fadeSlots)
                    _presenter?.NotifyActionAnimationComplete(slotIndex++);
            }
            onComplete?.Invoke();
        }

        // Groups consecutive AoE results from the same caster; single-target stays alone.
        private List<CombatActionResult> DrainActionGroup()
        {
            var group = new List<CombatActionResult>();
            if (_animQueue.Count == 0) return group;

            var first = _animQueue.Dequeue();
            group.Add(first);

            if (first.IsAoe)
            {
                while (_animQueue.Count > 0
                    && _animQueue.Peek().Caster == first.Caster
                    && _animQueue.Peek().IsAoe)
                    group.Add(_animQueue.Dequeue());
            }

            return group;
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

        private IEnumerator PlayActionGroup(List<CombatActionResult> group)
        {
            if (group.Count == 0) yield break;

            var first = group[0];
            if (first.Caster == null || !_actorPawns.TryGetValue(first.Caster, out var casterPawn))
                yield break;
            if (!casterPawn.gameObject.activeSelf) yield break;

            bool isRanged   = first.Skill != null && first.Skill.isRanged;
            var  casterAnim = casterPawn.GetComponentInChildren<CombatPawnAnimator>();
            var  clips      = GetSkillClips(first);
            int  impactIdx  = GetImpactClipIndex(first);
            var  trigger    = ResolveTrigger(first, casterPawn, GetTargetTransform(first), clips);
            float blend     = casterAnim != null ? casterAnim.BlendDuration : 0f;

            Vector3    targetPos        = GetTargetPosition(first);
            Quaternion originalRotation = casterPawn.rotation;

            // SlashVFX AE on the attack clip drives onStartVFX timing precisely
            var vfxConfig = ResolveVFXConfig(first);
            void HandleSlashVFX() => vfxPlayer?.PlayOnStartVFX(vfxConfig, casterPawn, targetPos);
            if (casterAnim != null) casterAnim.OnSlashVFX += HandleSlashVFX;
            try
            {

            if (isRanged)
            {
                // Ranged: snap rotate toward target, stay in place
                RotateCasterToward(casterPawn, targetPos);

                if (trigger != null)
                {
                    trigger.Arm(() => RaiseImpactGroup(group));
                    bool animDone = casterAnim == null;
                    if (casterAnim != null)
                        StartCoroutine(RunThenSignal(casterAnim.PlaySkillSequence(clips), () => animDone = true));
                    yield return new WaitUntil(() => animDone && trigger.HasFired);
                }
                else
                {
                    RaiseImpactGroup(group);
                    if (casterAnim != null)
                        yield return StartCoroutine(casterAnim.PlaySkillSequence(clips));
                }
            }
            else
            {
                int returnIdx = ResolveReturnClipIndex(first, clips);

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

                // Return: rotate toward origin using any time available before the lunge starts,
                // so rotation completes without delaying physical movement.
                Coroutine returnLunge = null;
                if (returnIdx >= 0)
                {
                    float rawReturn  = SumClipDurations(clips, 0, returnIdx - 1);
                    float rotStart   = Mathf.Max(0f, rawReturn - blend);
                    float returnDur  = clips != null && returnIdx < clips.Length
                                       ? clips[returnIdx]?.length ?? 0.5f : 0.5f;
                    // Start rotation as early as possible so it finishes before lunge begins.
                    float rotBegin   = Mathf.Max(0f, rotStart - returnRotateDuration);
                    float lungeStart = rotBegin + returnRotateDuration; // lunge after rotation
                    StartCoroutine(SmoothRotateTo(casterPawn, origin, rotBegin, returnRotateDuration));
                    returnLunge = StartCoroutine(DelayedLungeTo(casterPawn, origin, lungeStart, returnDur, snapOnEnd: true));
                }

                if (trigger != null)
                {
                    trigger.Arm(() => RaiseImpactGroup(group));
                    bool animDone = casterAnim == null;
                    if (casterAnim != null)
                        StartCoroutine(RunThenSignal(casterAnim.PlaySkillSequence(clips), () => animDone = true));
                    yield return new WaitUntil(() => animDone && trigger.HasFired);
                }
                else
                {
                    if (casterAnim != null)
                        yield return StartCoroutine(casterAnim.PlaySkillSequence(
                            clips,
                            () => RaiseImpactGroup(group),
                            impactIdx));
                    else
                        RaiseImpactGroup(group);
                }

                // PlayableGraph gone — CrossFade to walk for any remaining lunge time.
                if (returnLunge != null) casterAnim?.PlayApproach();

                if (returnLunge != null) yield return returnLunge;
                casterAnim?.PlayReturn();
                casterPawn.rotation = originalRotation;
            }

            foreach (var r in group)
            {
                if (r.Target != null && !r.Target.IsAlive
                    && _actorPawns.TryGetValue(r.Target, out var deadPawn))
                {
                    deadPawn.GetComponentInChildren<CombatPawnAnimator>()?.PlayDeath();
                    StartCoroutine(HidePawnDelayed(deadPawn.gameObject, 1.2f));
                }
            }

            } // try
            finally
            {
                if (casterAnim != null) casterAnim.OnSlashVFX -= HandleSlashVFX;
            }
        }

        // ── trigger helpers ───────────────────────────────────────────────────────

        private SkillVFXConfig ResolveVFXConfig(CombatActionResult first)
        {
            return first.Skill?.vfxConfig;
        }

        private IImpactTrigger ResolveTrigger(CombatActionResult first, Transform casterPawn, Transform targetPawn, AnimationClip[] allClips = null)
        {
            return first.Skill?.impactTrigger?.Create(casterPawn, targetPawn, this, allClips);
        }

        private Transform GetTargetTransform(CombatActionResult result)
        {
            if (result.Target != null && _actorPawns.TryGetValue(result.Target, out var tp))
                return tp;
            bool isEnemy = result.Caster is EnemyAgent;
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

        private void RaiseImpactGroup(List<CombatActionResult> group)
        {
            foreach (var r in group)
            {
                Vector3 hitPos = r.Target != null && _actorPawns.TryGetValue(r.Target, out var tp)
                    ? tp.position + Vector3.up * 1.2f
                    : Vector3.zero;

                var ctx = ImpactContext.FromResult(r, hitPos);
                if (impactEvent != null)
                    impactEvent.Raise(ctx);
                else
                    OnImpactReceived(ctx);
            }
        }

        /// <summary>
        /// Subscriber to ImpactEvent SO. Applies visual hit reaction, damage numbers, and HP bar updates.
        /// Future VFX/audio systems subscribe to the same ImpactEvent SO without touching this class.
        /// </summary>
        private void OnImpactReceived(ImpactContext ctx)
        {
            if (ctx.DamageDealt > 0f && ctx.Target != null
                && _actorPawns.TryGetValue(ctx.Target, out var targetPawn))
            {
                targetPawn.GetComponentInChildren<CombatPawnAnimator>()?.PlayHit();
                StartCoroutine(FloatDamageNumber(targetPawn, ctx.DamageDealt, ctx.IsCrit));
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

        private IEnumerator HidePawnDelayed(GameObject pawn, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (pawn != null) pawn.SetActive(false);
        }

        private Vector3 GetTargetPosition(CombatActionResult result)
        {
            bool casterIsEnemy = result.Caster is EnemyAgent;

            if (result.IsAoe)
            {
                var targets = casterIsEnemy
                    ? (IReadOnlyList<ICombatActor>)_ctx.Players
                    : _ctx.Enemies;
                Vector3 centroid = Vector3.zero;
                int     count    = 0;
                foreach (var a in targets)
                    if (a.IsAlive && _actorPawns.TryGetValue(a, out var p))
                        { centroid += p.position; count++; }
                return count > 0 ? centroid / count : Vector3.zero;
            }

            if (result.Target != null && _actorPawns.TryGetValue(result.Target, out var tp))
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

        private AnimationClip[] GetSkillClips(CombatActionResult result)
        {
            if (result.Skill != null)
                return result.Skill.animSequence;

            // Ultimate: Skill is null — look up via CharacterData or EnemyData
            if (_actorCharData.TryGetValue(result.Caster, out var cd) && cd.ultimate != null)
                return cd.ultimate.animSequence;
            if (_actorEnemyData.TryGetValue(result.Caster, out var ed) && ed.ultimate != null)
                return ed.ultimate.animSequence;

            return null;
        }

        private int GetImpactClipIndex(CombatActionResult result)
        {
            return result.Skill?.impactAfterClipIndex ?? 0;
        }

        private int ResolveReturnClipIndex(CombatActionResult result, AnimationClip[] clips)
        {
            int raw = result.Skill?.returnAtClipIndex ?? -1;
            if (raw < 0 && clips != null && clips.Length > 0)
                return clips.Length - 1;
            return raw;
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

        private IEnumerator FloatDamageNumber(Transform pawn, float damage, bool isCrit)
        {
            var go = new GameObject("DmgNum");
            go.transform.position   = pawn.position + Vector3.up * 2f;
            go.transform.localScale = Vector3.one * 0.012f;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode      = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder    = 200;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 80f);

            var txtGO = new GameObject("T");
            txtGO.transform.SetParent(go.transform, false);
            var txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;

            var text       = txtGO.AddComponent<Text>();
            text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text      = isCrit ? $"CRIT {damage:F0}!" : $"{damage:F0}";
            text.fontSize  = isCrit ? 52 : 40;
            text.color     = isCrit ? new Color(1f, 0.45f, 0f) : Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;

            float   duration = 1.4f;
            float   elapsed  = 0f;
            Vector3 start    = go.transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p  = elapsed / duration;
                go.transform.position = start + Vector3.up * (2.5f * p);
                if (_camera != null)
                    go.transform.rotation = _camera.transform.rotation;
                text.color = new Color(text.color.r, text.color.g, text.color.b, 1f - p);
                yield return null;
            }

            Destroy(go);
        }
    }
}
