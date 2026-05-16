using UnityEngine;
using Runefall.Characters;

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

    [CreateAssetMenu(menuName = "Runefall/Cards/Skill")]
    public class SkillData : ScriptableObject
    {
        [Header("Identity")]
        public string skillName;
        public Sprite cardArt;
        public SkillType type;
        public ElementType element;

        [Header("Targeting")]
        public TargetType targetType = TargetType.SingleEnemy;
        [Tooltip("Ranged skills skip the approach movement.")]
        public bool isRanged;
        [Tooltip("Index of the clip after which impact (damage visuals) is applied.\n" +
                 "-1 = before any clip | 0 = after clip 0 | 1 = after clip 1, etc.")]
        public int impactAfterClipIndex = 0;
        [Tooltip("Index of the clip during which the return movement runs.\n" +
                 "-1 = use last clip in sequence (default).")]
        public int returnAtClipIndex = -1;

        [Header("Impact Trigger")]
        [Tooltip("Precise moment damage resolves. Null = legacy clip-index fallback.\n" +
                 "AnimEvent = contact frame | Projectile = arrow arrival | Timer = fixed delay.")]
        public ImpactTriggerData impactTrigger;

        [Header("VFX")]
        [Tooltip("VFX prefabs for this skill. Null = no visual effects.")]
        public SkillVFXConfig vfxConfig;

        [Header("Ultimate")]
        public float ultimateChargeAmount;

        [Header("Animations")]
        public AnimationClip[] animSequence;

        [Header("Effects by Rank")]
        public SkillEffect[] effectsByRank; // [0]=rank1, [1]=rank2, [2]=rank3
    }
}
