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
        public (int damage, int enemyIndex) ResolvePlayerCard(
            NetworkBattleCard         netCard,
            ulong                     ownerClientId,
            MultiplayerCombatRegistry registry)
        {
            var caster = GetPlayer(ownerClientId);
            if (caster == null || !caster.IsAlive) return (-1, -1);

            var target = GetRandomAliveEnemy();
            if (target == null) return (-1, -1);

            CombatActionResult result;
            if (netCard.IsUltimate)
            {
                var ult = registry.GetUltimate(netCard.UltimateName.ToString());
                if (ult == null) return (-1, -1);
                result = CombatResolver.ExecuteUltimate(ult, caster, target);
            }
            else
            {
                var skill = registry.GetSkill(netCard.SkillName.ToString());
                if (skill == null) return (-1, -1);
                result = CombatResolver.Execute(skill, netCard.Rank, caster, target);
            }

            int idx = Enemies.IndexOf(target as EnemyAgent);
            return ((int)result.DamageDealt, idx);
        }

        /// <summary>
        /// Enemy at enemyIndex attacks a random alive player.
        /// Returns (damage, targetClientId) or (-1, ulong.MaxValue) on failure.
        /// </summary>
        public (int damage, ulong targetClientId) ResolveEnemyAttack(int enemyIndex)
        {
            var enemy = GetEnemy(enemyIndex);
            if (enemy == null || !enemy.IsAlive) return (-1, ulong.MaxValue);

            if (enemy is not IEnemyTurnHandler handler) return (-1, ulong.MaxValue);

            var target = GetRandomAlivePlayer();
            if (target == null) return (-1, ulong.MaxValue);

            var pending = handler.TakeTurn(CombatCtx, target);
            if (pending.Skill == null && pending.Ultimate == null) return (-1, ulong.MaxValue);

            CombatActionResult result = pending.IsUltimate
                ? CombatResolver.ExecuteUltimate(pending.Ultimate, enemy, target)
                : CombatResolver.Execute(pending.Skill, pending.Rank, enemy, target);

            ulong targetCid = ulong.MaxValue;
            foreach (var kvp in Players)
                if (kvp.Value == target) { targetCid = kvp.Key; break; }

            return ((int)result.DamageDealt, targetCid);
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
