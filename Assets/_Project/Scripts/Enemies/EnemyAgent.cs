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
    /// Blockout AI: all BehaviorTreeTypes resolve to BasicAttack.
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

        public CombatActionResult TakeTurn(CombatContext context, ICombatActor target)
        {
            if (!IsAlive || !target.IsAlive)
                return new CombatActionResult(this, target, null, 1, 0f, false, 0f, 0f);

            return _data.behaviorTree switch
            {
                BehaviorTreeType.GoblinScout  => BasicAttack(target),
                BehaviorTreeType.OrcGuardian  => BasicAttack(target),
                BehaviorTreeType.ShadowMage   => BasicAttack(target),
                BehaviorTreeType.BossElite    => BasicAttack(target),
                _                             => BasicAttack(target)
            };
        }

        private CombatActionResult BasicAttack(ICombatActor target)
        {
            var dr = CombatFormulas.CalculateDamage(
                Model.EffectiveStats, target.Model.EffectiveStats,
                Element, target.Element);

            target.Model.TakeDamage(dr.damage);

            float lifeSteal = 0f;
            if (dr.lifeSteal > 0f)
            {
                lifeSteal = dr.lifeSteal;
                Model.Heal(lifeSteal);
            }

            return new CombatActionResult(this, target, _data.skill1, 1, dr.damage, dr.isCrit, lifeSteal, 0f);
        }
    }
}
