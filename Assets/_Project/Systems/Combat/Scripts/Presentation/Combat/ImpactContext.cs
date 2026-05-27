using UnityEngine;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    public enum AttackType { Melee, Ranged, AoE }

    public readonly struct ImpactContext
    {
        public readonly ICombatActor Attacker;
        public readonly ICombatActor Target;
        public readonly SkillData    Skill;
        public readonly float        DamageDealt;
        public readonly bool         IsCrit;
        public readonly float        LifeStealApplied;
        public readonly float        HealApplied;
        public readonly ElementType  Element;
        public readonly AttackType   Type;
        public readonly Vector3      HitPosition;

        public ImpactContext(
            ICombatActor attacker, ICombatActor target, SkillData skill,
            float damage, bool isCrit, float lifeSteal, float heal,
            ElementType element, AttackType type, Vector3 hitPosition)
        {
            Attacker         = attacker;
            Target           = target;
            Skill            = skill;
            DamageDealt      = damage;
            IsCrit           = isCrit;
            LifeStealApplied = lifeSteal;
            HealApplied      = heal;
            Element          = element;
            Type             = type;
            HitPosition      = hitPosition;
        }

        public static ImpactContext FromResult(CombatActionResult r, Vector3 hitPosition)
        {
            AttackType type = r.IsAoe                                ? AttackType.AoE
                            : r.Skill != null && r.Skill.isRanged   ? AttackType.Ranged
                            : AttackType.Melee;

            return new ImpactContext(
                r.Caster, r.Target, r.Skill,
                r.DamageDealt, r.IsCrit, r.LifeStealApplied, r.HealApplied,
                r.Caster != null ? r.Caster.Element : default,
                type, hitPosition);
        }
    }
}
