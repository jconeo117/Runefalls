using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Runefall.Data;
using Runefall.Presentation.Combat;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Per-client animation coordinator for multiplayer combat.
    ///
    /// Subscribes to ServerCombatOrchestrator events and drives CombatPawnAnimator on the
    /// appropriate NetworkedCombatPawn instances. All clients see the same animations because
    /// every RPC fires on ClientsAndHost and the choreography is deterministic (timed off clip
    /// lengths, not local framerate guesses).
    ///
    /// This reproduces the singleplayer visual choreography (CombatAnimationDriver):
    ///   - Melee: rotate toward target, lunge in to lungeStopDistance, play the skill's clip
    ///     sequence, fire the target hit reaction at the impact clip, then rotate + lunge home
    ///     and return to idle. The "warrior returns to line" behaviour.
    ///   - Ranged: snap-rotate toward target and play the clip sequence in place (no lunge),
    ///     firing the hit reaction at the impact clip. The "archer plays correct clips" behaviour.
    ///
    /// Damage and HP stay server-authoritative — the orchestrator resolves them and broadcasts
    /// HP separately. This controller is purely cosmetic and only gates turn flow by signalling
    /// completion: only the card owner sends CardAnimationCompleteServerRpc; any client sends
    /// EnemyAttackCompleteServerRpc (first one wins — server uses a flag).
    /// </summary>
    public class MultiplayerAnimationController : MonoBehaviour
    {
        // Choreography constants — mirror CombatAnimationDriver Inspector defaults.
        private const float LungeStopDistance    = 1.5f;
        private const float ReturnRotateDuration = 0.3f;
        private const float FallbackApproachLen  = 0.5f;
        private const float EnemyLungeDuration   = 0.45f;
        private const float NoClipImpactPause    = 0.2f;

        private ulong                     _localClientId;
        private RuntimeAnimatorController  _combatBaseController;
        private MultiplayerCombatRegistry _registry;

        // Cached pawn lookups — refreshed on first use (pawns may spawn after this MonoBehaviour).
        private readonly Dictionary<ulong, NetworkedCombatPawn> _playerPawns = new();
        private readonly List<NetworkedCombatPawn>              _enemyPawns  = new();
        private readonly HashSet<CombatPawnAnimator>            _initialized = new();
        private bool _pawnsScanned;

        // ── Initialization ─────────────────────────────────────────────────────

        /// <param name="combatBaseController">
        /// Shared base controller with Idle/Approach/Hit/Death states + placeholder clips.
        /// Applied per pawn via AnimatorOverrideController so PlayApproach/PlayHit/PlayReturn/
        /// PlayDeath resolve to the character's own clips. May be null — clip sequences still
        /// play, only the state cross-fades degrade gracefully to no-ops.
        /// </param>
        public void Initialize(ulong localClientId,
                               RuntimeAnimatorController combatBaseController = null,
                               MultiplayerCombatRegistry registry = null)
        {
            _localClientId        = localClientId;
            _combatBaseController = combatBaseController;
            _registry             = registry;
            StartCoroutine(SubscribeWhenOrchestratorReady());
        }

        private IEnumerator SubscribeWhenOrchestratorReady()
        {
            yield return new WaitUntil(() => ServerCombatOrchestrator.Instance != null);
            var orch = ServerCombatOrchestrator.Instance;
            orch.OnExecuteCard    += OnExecuteCard;
            orch.OnEnemyAttacking += OnEnemyAttacking;
            Debug.Log("[MPAnimCtrl] Suscrito a OnExecuteCard y OnEnemyAttacking.");
        }

        private void OnDestroy()
        {
            if (ServerCombatOrchestrator.Instance == null) return;
            ServerCombatOrchestrator.Instance.OnExecuteCard    -= OnExecuteCard;
            ServerCombatOrchestrator.Instance.OnEnemyAttacking -= OnEnemyAttacking;
        }

        // ── Card execution (Phase 2) ───────────────────────────────────────────

        private void OnExecuteCard(int slotIndex, ulong ownerClientId, NetworkBattleCard card)
        {
            StartCoroutine(PlayCardAnimation(slotIndex, ownerClientId, card));
        }

        private IEnumerator PlayCardAnimation(int slotIndex, ulong ownerClientId, NetworkBattleCard card)
        {
            ScanPawnsIfNeeded();

            var attackerPawn = GetPlayerPawn(ownerClientId);
            var targetPawn   = GetFirstAliveEnemyPawn();
            var attackerAnim = GetPawnAnimator(attackerPawn);
            var targetAnim   = GetPawnAnimator(targetPawn);

            EnsureInit(attackerPawn);
            EnsureInit(targetPawn);

            ResolveClips(attackerPawn, card, out var clips, out bool isRanged, out int impactIdx);
            float approachReturnLen = GetApproachClipLength(attackerPawn) ?? FallbackApproachLen;

            if (attackerPawn != null && targetPawn != null)
                yield return StartCoroutine(PlayAttackChoreography(
                    attackerPawn.transform, attackerAnim,
                    targetPawn.transform,   targetAnim,
                    clips, isRanged, impactIdx, approachReturnLen));
            else
                yield return new WaitForSeconds(0.4f); // pawns missing — keep turn flow alive

            // Only the card owner reports animation complete — server awaits this signal.
            if (_localClientId == ownerClientId)
            {
                Debug.Log($"[MPAnimCtrl] CardAnimComplete slot={slotIndex}");
                ServerCombatOrchestrator.Instance?.CardAnimationCompleteServerRpc(slotIndex);
            }
        }

        // ── Enemy attack (Phase 3) ─────────────────────────────────────────────

        private void OnEnemyAttacking(int enemyIndex, ulong targetClientId, int damage)
        {
            StartCoroutine(PlayEnemyAttackAnimation(enemyIndex, targetClientId, damage));
        }

        private IEnumerator PlayEnemyAttackAnimation(int enemyIndex, ulong targetClientId, int damage)
        {
            ScanPawnsIfNeeded();

            var enemyPawn  = GetEnemyPawn(enemyIndex);
            var targetPawn = GetPlayerPawn(targetClientId);
            var enemyAnim  = GetPawnAnimator(enemyPawn);
            var targetAnim = GetPawnAnimator(targetPawn);

            EnsureInit(enemyPawn);
            EnsureInit(targetPawn);

            // Enemy attack uses its EnemyData.skill1 clips (orchestrator broadcasts no skill id).
            ResolveEnemyClips(enemyPawn, out var clips, out bool isRanged, out int impactIdx);
            float approachReturnLen = GetApproachClipLength(enemyPawn) ?? EnemyLungeDuration;

            if (enemyPawn != null && targetPawn != null)
                yield return StartCoroutine(PlayAttackChoreography(
                    enemyPawn.transform, enemyAnim,
                    targetPawn.transform, targetAnim,
                    clips, isRanged, impactIdx, approachReturnLen));
            else
                yield return new WaitForSeconds(0.4f);

            // Any client can unblock the server — first one wins.
            Debug.Log($"[MPAnimCtrl] EnemyAttackComplete enemy={enemyIndex}");
            ServerCombatOrchestrator.Instance?.EnemyAttackCompleteServerRpc();
        }

        // ── Choreography ───────────────────────────────────────────────────────

        private IEnumerator PlayAttackChoreography(
            Transform attacker, CombatPawnAnimator attackerAnim,
            Transform target,   CombatPawnAnimator targetAnim,
            AnimationClip[] clips, bool isRanged, int impactIdx, float approachReturnLen)
        {
            if (attacker == null) yield break;

            Vector3    targetPos   = target != null ? target.position : attacker.position;
            Quaternion originalRot = attacker.rotation;

            void OnImpact() => targetAnim?.PlayHit();

            // ── Ranged: snap rotate, play clips in place, no lunge ──
            if (isRanged)
            {
                RotateToward(attacker, targetPos);
                if (attackerAnim != null && clips != null && clips.Length > 0)
                    yield return StartCoroutine(attackerAnim.PlaySkillSequence(clips, OnImpact, impactIdx));
                else
                {
                    OnImpact();
                    yield return new WaitForSeconds(0.3f);
                }
                attacker.rotation = originalRot;
                yield break;
            }

            // ── Melee: rotate + lunge in, attack, return to line ──
            Vector3 origin      = attacker.position;
            Vector3 lungeTarget = ComputeLungeTarget(origin, targetPos);

            if (clips != null && clips.Length > 0)
            {
                float approachDelay = SumClipDurations(clips, 0, impactIdx - 1);
                float approachDur   = (impactIdx >= 0 && impactIdx < clips.Length && clips[impactIdx] != null)
                                      ? clips[impactIdx].length : 0f;
                float blend         = attackerAnim != null ? attackerAnim.BlendDuration : 0f;
                float rotDur        = Mathf.Max(approachDelay, blend);

                StartCoroutine(SmoothRotateTo(attacker, targetPos, 0f, rotDur));
                if (approachDur > 0f)
                    StartCoroutine(DelayedLungeTo(attacker, lungeTarget, approachDelay, approachDur));

                if (attackerAnim != null)
                    yield return StartCoroutine(attackerAnim.PlaySkillSequence(clips, OnImpact, impactIdx));
                else
                    OnImpact();
            }
            else
            {
                // No clips (enemy / fallback): run in, hit, with approach-state legs.
                attackerAnim?.PlayApproach();
                yield return StartCoroutine(SmoothRotateTo(attacker, targetPos, 0f, ReturnRotateDuration));
                yield return StartCoroutine(LungeTo(attacker, lungeTarget, EnemyLungeDuration));
                OnImpact();
                yield return new WaitForSeconds(NoClipImpactPause);
            }

            // Sequential return: rotate home, run back, settle to idle.
            yield return StartCoroutine(SmoothRotateTo(attacker, origin, 0f, ReturnRotateDuration));
            attackerAnim?.PlayApproach();
            yield return StartCoroutine(LungeTo(attacker, origin, approachReturnLen));
            attackerAnim?.PlayReturn();
            attacker.rotation = originalRot;
        }

        // ── Skill resolution ───────────────────────────────────────────────────

        // Resolves clips from the ATTACKER's own data (CharacterData.skill1/skill2/ultimate),
        // not the registry — registry.skills is often empty, and the character always carries
        // its own skill assets. Registry is a last-resort fallback.
        private void ResolveClips(NetworkedCombatPawn attackerPawn, NetworkBattleCard card,
            out AnimationClip[] clips, out bool isRanged, out int impactIdx)
        {
            clips     = null;
            isRanged  = false;
            impactIdx = 0;

            var cd = attackerPawn != null ? attackerPawn.GetComponent<CharacterSlot>()?.data : null;
            var ed = attackerPawn != null ? attackerPawn.GetComponent<EnemySlot>()?.data     : null;

            if (!card.IsUltimate)
            {
                string n   = card.SkillName.ToString();
                var    dsd = cd != null ? MatchSkill(cd.skill1, cd.skill2, n) : null;
                if (dsd == null && ed != null) dsd = MatchSkill(ed.skill1, ed.skill2, n);
                if (dsd == null && _registry != null) dsd = _registry.GetSkill(n) as DefaultSkillData;

                if (dsd != null)
                {
                    clips     = dsd.animSequence;
                    isRanged  = dsd.isRanged;
                    impactIdx = dsd.impactAfterClipIndex;
                }
            }
            else
            {
                string n  = card.UltimateName.ToString();
                var ult = cd != null && cd.ultimate != null && cd.ultimate.ultimateName == n
                          ? cd.ultimate
                          : _registry != null ? _registry.GetUltimate(n) : null;
                if (ult != null)
                {
                    clips     = ult.animSequence;
                    impactIdx = Mathf.Max(0, (clips?.Length ?? 1) - 1); // ultimate impacts on last clip
                }
            }
        }

        // Enemy attack: use EnemyData.skill1 (preferred) or skill2 as a DefaultSkillData.
        private void ResolveEnemyClips(NetworkedCombatPawn enemyPawn,
            out AnimationClip[] clips, out bool isRanged, out int impactIdx)
        {
            clips     = null;
            isRanged  = false;
            impactIdx = 0;

            var ed = enemyPawn != null ? enemyPawn.GetComponent<EnemySlot>()?.data : null;
            if (ed == null) return;

            var dsd = (ed.skill1 as DefaultSkillData) ?? (ed.skill2 as DefaultSkillData);
            if (dsd != null)
            {
                clips     = dsd.animSequence;
                isRanged  = dsd.isRanged;
                impactIdx = dsd.impactAfterClipIndex;
            }
        }

        private static DefaultSkillData MatchSkill(SkillData a, SkillData b, string name)
        {
            if (a is DefaultSkillData da && a.skillName == name) return da;
            if (b is DefaultSkillData db && b.skillName == name) return db;
            return null;
        }

        private float? GetApproachClipLength(NetworkedCombatPawn pawn)
        {
            if (pawn == null) return null;
            var cs = pawn.GetComponent<CharacterSlot>();
            if (cs != null && cs.data != null && cs.data.animApproach != null)
                return cs.data.animApproach.length;
            var es = pawn.GetComponent<EnemySlot>();
            if (es != null && es.data != null && es.data.animApproach != null)
                return es.data.animApproach.length;
            return null;
        }

        // ── Pawn discovery ─────────────────────────────────────────────────────

        private void ScanPawnsIfNeeded()
        {
            if (_pawnsScanned) return;
            _pawnsScanned = true;
            RebuildPawnCache();
        }

        private void RebuildPawnCache()
        {
            _playerPawns.Clear();
            _enemyPawns.Clear();

            var all = Object.FindObjectsByType<NetworkedCombatPawn>(FindObjectsSortMode.None);
            foreach (var pawn in all)
            {
                if (pawn.IsPlayerTeam.Value)
                    _playerPawns[pawn.NetworkObject.OwnerClientId] = pawn;
                else
                    _enemyPawns.Add(pawn);
            }

            _enemyPawns.Sort((a, b) => a.SlotIndex.Value.CompareTo(b.SlotIndex.Value));
            Debug.Log($"[MPAnimCtrl] Pawns escaneados: {_playerPawns.Count} players, {_enemyPawns.Count} enemies.");
        }

        // Applies the combat base controller + per-character clip overrides so the state-machine
        // cross-fades (Approach/Hit/Return/Death) resolve to the right clips. Idempotent per pawn.
        private void EnsureInit(NetworkedCombatPawn pawn)
        {
            if (pawn == null) return;
            var anim = GetPawnAnimator(pawn);
            if (anim == null || _initialized.Contains(anim)) return;

            var cs = pawn.GetComponent<CharacterSlot>();
            if (cs != null && cs.data != null)
                anim.InitFromCharacter(cs.data, _combatBaseController);
            else
            {
                var es = pawn.GetComponent<EnemySlot>();
                if (es != null && es.data != null)
                    anim.InitFromEnemy(es.data, _combatBaseController);
            }
            _initialized.Add(anim);
        }

        private NetworkedCombatPawn GetPlayerPawn(ulong clientId)
        {
            if (_playerPawns.TryGetValue(clientId, out var p)) return p;
            RebuildPawnCache();
            return _playerPawns.TryGetValue(clientId, out p) ? p : null;
        }

        private NetworkedCombatPawn GetEnemyPawn(int index)
        {
            if (index < _enemyPawns.Count) return _enemyPawns[index];
            RebuildPawnCache();
            return index < _enemyPawns.Count ? _enemyPawns[index] : null;
        }

        private NetworkedCombatPawn GetFirstAliveEnemyPawn()
        {
            foreach (var p in _enemyPawns)
                if (p != null && p.gameObject.activeInHierarchy) return p;
            return _enemyPawns.Count > 0 ? _enemyPawns[0] : null;
        }

        private static CombatPawnAnimator GetPawnAnimator(NetworkedCombatPawn pawn)
        {
            if (pawn == null) return null;
            return pawn.GetComponentInChildren<CombatPawnAnimator>();
        }

        // ── Movement / rotation helpers (mirror CombatAnimationDriver) ──────────

        private static Vector3 ComputeLungeTarget(Vector3 origin, Vector3 targetPos)
        {
            Vector3 dir = origin - targetPos;
            if (dir.sqrMagnitude < 0.0001f) return origin;
            return targetPos + dir.normalized * LungeStopDistance;
        }

        private static void RotateToward(Transform t, Vector3 target)
        {
            Vector3 dir = target - t.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                t.rotation = Quaternion.LookRotation(dir);
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

        private IEnumerator DelayedLungeTo(Transform pawn, Vector3 target, float delay, float duration, bool snapOnEnd = false)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            yield return StartCoroutine(LungeTo(pawn, target, duration));
            if (snapOnEnd && pawn != null) pawn.position = target;
        }

        private static IEnumerator LungeTo(Transform pawn, Vector3 target, float duration)
        {
            if (pawn == null) yield break;
            if (duration <= 0f) { pawn.position = target; yield break; }
            Vector3 origin = pawn.position;
            for (float t = 0f; t < 1f;)
            {
                if (pawn == null) yield break;
                t             = Mathf.Min(1f, t + Time.deltaTime / duration);
                pawn.position = Vector3.Lerp(origin, target, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }

        private static float SumClipDurations(AnimationClip[] clips, int startInclusive, int endInclusive)
        {
            if (clips == null) return 0f;
            float sum = 0f;
            for (int i = startInclusive; i <= endInclusive && i < clips.Length; i++)
                if (i >= 0 && clips[i] != null) sum += clips[i].length;
            return sum;
        }
    }
}
