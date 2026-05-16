using UnityEngine;
using Runefall.Characters;

namespace Runefall.Data
{
    [CreateAssetMenu(menuName = "Runefall/Cards/Ultimate")]
    public class UltimateData : ScriptableObject
    {
        public string ultimateName;
        public Sprite cardArt;
        public ElementType element;
        public TargetType targetType = TargetType.SingleEnemy;
        public SkillEffect effect;

        [Header("Animations")]
        public AnimationClip[] animSequence;
    }
}
