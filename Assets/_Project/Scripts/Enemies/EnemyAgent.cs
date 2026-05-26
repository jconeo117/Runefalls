using System;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Enemies
{
    /// <summary>
    /// Runtime enemy participant. Implements ICombatActor (identity + model)
    /// and IEnemyTurnHandler (AI decision each enemy phase).
    ///
    /// Blockout AI: all BehaviorTreeTypes map to a single-target PendingAction.
    /// Switch structure kept so each type can diverge without touching TurnManager.
    /// </summary>
    public class EnemyAgent : ICombatActor, IEnemyTurnHandler
    {
        private readonly EnemyData _data;

        public string         Name        => _data.enemyName;
        public bool           IsAlive     => Model.IsAlive;
        public CharacterModel Model       { get; }
        public ElementType    Element     => _data.element;
        public ActorEffects   Effects     { get; }

        // Manual value set in EnemyData SO — not calculated from stats.
        public float          CombatClass => _data.combatClass;

        public EnemyAgent(EnemyData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            _data   = data;
            Model   = new CharacterModel(data.enemyName, data.stats, data.maxMP);
            Effects = new ActorEffects(this);
        }

        public PendingAction TakeTurn(CombatContext context, ICombatActor target)
        {
            if (!IsAlive || target == null)
                return default;

            return _data.behaviorTree switch
            {
                BehaviorTreeType.GoblinScout  => BuildAttack(target),
                BehaviorTreeType.OrcGuardian  => BuildAttack(target),
                BehaviorTreeType.ShadowMage   => BuildAttack(target),
                BehaviorTreeType.BossElite    => BuildAttack(target),
                _                             => BuildAttack(target)
            };
        }

        private PendingAction BuildAttack(ICombatActor target) =>
            new PendingAction(
                caster:     this,
                target:     target,
                skill:      _data.skill1,
                ultimate:   null,
                rank:       1,
                targetType: TargetType.SingleEnemy,
                isUltimate: false);
    }
}
