using UnityEngine;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Presentation.Combat;

namespace Runefall.Data
{
    public enum SkillType { Offensive, OffensiveEffect, Debuff, Support }

    /// <summary>
    /// Who the skill affects. SingleEnemy requires player to pick (or random fallback).
    /// AllEnemies hits every alive enemy. RandomEnemy skips selection — always random.
    /// Self and AllAllies target the player side.
    /// </summary>
    public enum TargetType
    {
        SingleEnemy = 0,   // default — player picks target, random if none selected
        AllEnemies  = 1,   // AoE — no target selection needed
        RandomEnemy = 2,   // auto-random, no UI selection
        Self        = 3,   // caster only (heals, buffs)
        AllAllies   = 4,   // all player actors
    }

    public abstract class SkillData : ScriptableObject
    {
        [Header("Identity")]
        public string skillName;
        public Sprite cardArt;
        public SkillType type;
        public ElementType element;
        [Tooltip("Optional. Leave empty to auto-generate the base text (daño/elemento/% del ataque/objetivo) in the UI.")]
        [TextArea] public string description;

        [Header("Targeting")]
        public TargetType targetType = TargetType.SingleEnemy;
        [Tooltip("Ranged skills skip the approach movement.")]
        public bool isRanged;

        [Header("Ultimate")]
        public float ultimateChargeAmount;

        [Header("Effects by Rank")]
        public SkillEffect[] effectsByRank; // [0]=rank1, [1]=rank2, [2]=rank3

        /// <summary>
        /// Executes the numerical gameplay effects (damage, healing, buffs, status effects).
        /// Standard calculations reside in the base class, but subclasses can fully override to
        /// implement custom formulas, multi-target damage calculations, or unique status effects.
        /// </summary>
        public virtual (float dmg, bool crit, float lifeSteal, float heal) ExecuteGameplayEffect(
            ICombatActor caster, ICombatActor target, int rank, float hitFraction = 1f)
        {
            if (effectsByRank == null || effectsByRank.Length == 0)
            {
                UnityEngine.Debug.LogWarning($"SkillData '{skillName}' has no effectsByRank. Executing default empty effect.");
                return (0f, false, 0f, 0f);
            }

            int idx = System.Math.Max(0, System.Math.Min(rank - 1, effectsByRank.Length - 1));
            return CombatResolver.ApplyEffect(effectsByRank[idx], caster, target, rank, hitFraction);
        }

        /// <summary>
        /// Orchestrates the visual sequence (animations, VFX, screen shakes, Cinemachine camera tracks).
        /// </summary>
        public abstract void PlayPresentation(
            CombatAnimationDriver driver, 
            ICombatActor caster, 
            ICombatActor target, 
            int rank, 
            System.Action onComplete);

        // ── SOLID Polymorphism properties ─────────────────────────────────────────
        public virtual AnimationClip[] AnimSequence => null;
        public virtual int ImpactAfterClipIndex => 0;
        public virtual int ReturnLungeClipIndex => -1;
        public virtual int HitCount => 1;
        public virtual ImpactTriggerData ImpactTrigger => null;
        public virtual SkillVFXConfig VfxConfig => null;
    }
}
