using UnityEngine;
using Runefall.Characters;

namespace Runefall.Data
{
    public enum SkillType { Offensive, OffensiveEffect, Debuff, Support }

    [CreateAssetMenu(menuName = "Runefall/Cards/Skill")]
    public class SkillData : ScriptableObject
    {
        [Header("Identity")]
        public string skillName;
        public Sprite cardArt;
        public SkillType type;
        public ElementType element;

        [Header("Ultimate")]
        public float ultimateChargeAmount;

        [Header("Effects by Rank")]
        public SkillEffect[] effectsByRank; // [0]=rank1, [1]=rank2, [2]=rank3
    }
}
