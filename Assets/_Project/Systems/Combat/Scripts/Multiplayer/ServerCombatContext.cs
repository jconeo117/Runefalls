using System.Collections.Generic;
using Runefall.Combat;
using Runefall.Data;
using Runefall.Enemies;
using UnityEngine;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Server-authoritative combat state.
    /// Owned by ServerCombatOrchestrator — never replicated, never accessed on clients.
    /// Holds the canonical HP and actor references for resolving damage.
    /// </summary>
    public class ServerCombatContext
    {
        /// <summary>Players indexed by NGO clientId (0, 1…).</summary>
        public readonly Dictionary<ulong, PlayerActor> Players = new();

        /// <summary>Enemies in spawn order.</summary>
        public readonly List<EnemyAgent> Enemies = new();

        /// <summary>Unified context for CombatResolver compatibility.</summary>
        public readonly CombatContext CombatCtx;

        public int Round { get; private set; }

        public ServerCombatContext(
            MultiplayerCombatRegistry   registry,
            Dictionary<ulong, string>   clientIdToCharName,
            List<EnemyData>             enemyDatas)
        {
            var playerActors = new List<ICombatActor>();
            foreach (var kvp in clientIdToCharName)
            {
                var cd = registry.GetCharacter(kvp.Value);
                if (cd == null)
                {
                    Debug.LogError($"[ServerCombatCtx] CharacterData '{kvp.Value}' no encontrado en registry.");
                    continue;
                }
                var actor = new PlayerActor(cd);
                Players[kvp.Key] = actor;
                playerActors.Add(actor);
            }

            var enemyActors = new List<ICombatActor>();
            foreach (var ed in enemyDatas)
            {
                if (ed == null) continue;
                var agent = new EnemyAgent(ed);
                Enemies.Add(agent);
                enemyActors.Add(agent);
            }

            CombatCtx = new CombatContext(playerActors, enemyActors);
            Round = 1;
        }

        public PlayerActor GetPlayer(ulong clientId) =>
            Players.TryGetValue(clientId, out var p) ? p : null;

        public EnemyAgent GetEnemy(int index) =>
            index >= 0 && index < Enemies.Count ? Enemies[index] : null;

        public void AdvanceRound() => Round++;

        /// <summary>
        /// Resolves a networked card against a random alive enemy.
        /// Returns (damage, enemyIndex) or (-1, -1) on failure.
        /// </summary>
        // ── Multi-hit player card ────────────────────────────────────────────────
        // BeginPlayerCard selects caster/target/skill once (no damage). Each impact frame
        // calls ApplyPlayerCardHit, which applies one hit's fraction (1/totalHits) of the
        // skill so a 3-AE skill deals 3 separate hits + 3 damage numbers.

        private PlayerActor  _cardCaster;
        private EnemyAgent   _cardTarget;
        private SkillData    _cardSkill;
        private UltimateData _cardUlt;
        private int          _cardRank;
        private int          _cardTotalHits = 1;

        public bool BeginPlayerCard(
            NetworkBattleCard netCard, ulong ownerClientId, MultiplayerCombatRegistry registry,
            out int enemyIndex, out int totalHits)
        {
            enemyIndex = -1; totalHits = 1;
            _cardCaster = GetPlayer(ownerClientId);
            _cardSkill = null; _cardUlt = null;
            if (_cardCaster == null || !_cardCaster.IsAlive) return false;

            _cardTarget = GetRandomAliveEnemy();
            if (_cardTarget == null) return false;
            enemyIndex = Enemies.IndexOf(_cardTarget);

            if (netCard.IsUltimate)
            {
                _cardUlt = _cardCaster.Data != null && _cardCaster.Data.ultimate != null
                           && _cardCaster.Data.ultimate.ultimateName == netCard.UltimateName.ToString()
                           ? _cardCaster.Data.ultimate
                           : registry.GetUltimate(netCard.UltimateName.ToString());
                if (_cardUlt == null) return false;
                _cardTotalHits = 1;
            }
            else
            {
                string n = netCard.SkillName.ToString();
                _cardSkill = MatchSkill(_cardCaster.Data, n) ?? registry.GetSkill(n);
                if (_cardSkill == null) return false;
                _cardRank      = netCard.Rank;
                // Total hits = number of impact AEs in the clips (one damage number per AE),
                // matching what the client dispatches — not the designer hitCount hint.
                _cardTotalHits = CountImpactAEs(
                    (_cardSkill as DefaultSkillData)?.animSequence, _cardSkill.isRanged);
            }
            totalHits = _cardTotalHits;
            return true;
        }

        public (int damage, int enemyIndex, bool isCrit) ApplyPlayerCardHit()
        {
            if (_cardTarget == null || _cardCaster == null) return (-1, -1, false);
            float frac = _cardTotalHits > 0 ? 1f / _cardTotalHits : 1f;
            CombatActionResult result = _cardUlt != null
                ? CombatResolver.ExecuteUltimate(_cardUlt, _cardCaster, _cardTarget)
                : CombatResolver.Execute(_cardSkill, _cardRank, _cardCaster, _cardTarget, frac);
            int idx = Enemies.IndexOf(_cardTarget);
            return ((int)result.DamageDealt, idx, result.IsCrit);
        }

        // ── Multi-hit enemy attack ───────────────────────────────────────────────

        private EnemyAgent   _enemyAtkActor;
        private ICombatActor _enemyAtkTarget;
        private SkillData    _enemyAtkSkill;
        private UltimateData _enemyAtkUlt;
        private int          _enemyAtkRank;
        private int          _enemyAtkTotalHits = 1;

        public bool BeginEnemyAttack(int enemyIndex, out ulong targetClientId, out int totalHits)
        {
            targetClientId = ulong.MaxValue; totalHits = 1;
            _enemyAtkActor = GetEnemy(enemyIndex);
            _enemyAtkSkill = null; _enemyAtkUlt = null;
            if (_enemyAtkActor == null || !_enemyAtkActor.IsAlive) return false;
            if (_enemyAtkActor is not IEnemyTurnHandler handler) return false;

            _enemyAtkTarget = GetRandomAlivePlayer();
            if (_enemyAtkTarget == null) return false;

            var pending = handler.TakeTurn(CombatCtx, _enemyAtkTarget);
            if (pending.Skill == null && pending.Ultimate == null) return false;

            _enemyAtkRank = pending.Rank;
            if (pending.IsUltimate) { _enemyAtkUlt = pending.Ultimate; _enemyAtkTotalHits = 1; }
            else
            {
                _enemyAtkSkill     = pending.Skill;
                _enemyAtkTotalHits = CountImpactAEs(
                    (pending.Skill as DefaultSkillData)?.animSequence, pending.Skill.isRanged);
            }

            foreach (var kvp in Players)
                if (kvp.Value == _enemyAtkTarget) { targetClientId = kvp.Key; break; }
            totalHits = _enemyAtkTotalHits;
            return true;
        }

        public (int damage, ulong targetClientId, bool isCrit) ApplyEnemyAttackHit()
        {
            if (_enemyAtkActor == null || _enemyAtkTarget == null) return (-1, ulong.MaxValue, false);
            float frac = _enemyAtkTotalHits > 0 ? 1f / _enemyAtkTotalHits : 1f;
            CombatActionResult result = _enemyAtkUlt != null
                ? CombatResolver.ExecuteUltimate(_enemyAtkUlt, _enemyAtkActor, _enemyAtkTarget)
                : CombatResolver.Execute(_enemyAtkSkill, _enemyAtkRank, _enemyAtkActor, _enemyAtkTarget, frac);

            ulong cid = ulong.MaxValue;
            foreach (var kvp in Players)
                if (kvp.Value == _enemyAtkTarget) { cid = kvp.Key; break; }
            return ((int)result.DamageDealt, cid, result.IsCrit);
        }

        private static SkillData MatchSkill(CharacterData cd, string name)
        {
            if (cd == null) return null;
            if (cd.skill1 != null && cd.skill1.skillName == name) return cd.skill1;
            if (cd.skill2 != null && cd.skill2.skillName == name) return cd.skill2;
            return null;
        }

        // Counts every impact Animation Event in the clips — one hit (and one damage number)
        // per AE call. Matches the names the client dispatches: ranged "Shoot", melee
        // "ImpactFrame"/"Hit" (CombatPawnAnimator). Falls back to 1 when none are present.
        private static int CountImpactAEs(UnityEngine.AnimationClip[] clips, bool isRanged)
        {
            if (clips == null) return 1;
            int count = 0;
            foreach (var c in clips)
            {
                if (c == null) continue;
                foreach (var ev in c.events)
                {
                    string fn = ev.functionName;
                    bool impact = isRanged
                        ? fn == "Shoot"
                        : (fn == "ImpactFrame" || fn == "Hit");
                    if (impact) count++;
                }
            }
            return count > 0 ? count : 1;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private EnemyAgent GetRandomAliveEnemy()
        {
            var alive = new List<EnemyAgent>();
            foreach (var e in Enemies)
                if (e.IsAlive) alive.Add(e);
            if (alive.Count == 0) return null;
            return alive[new System.Random().Next(alive.Count)];
        }

        private ICombatActor GetRandomAlivePlayer()
        {
            var alive = new List<PlayerActor>();
            foreach (var p in Players.Values)
                if (p.IsAlive) alive.Add(p);
            if (alive.Count == 0) return null;
            return alive[new System.Random().Next(alive.Count)];
        }
    }
}
