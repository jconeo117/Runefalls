using UnityEngine;
using Runefall.Characters;

namespace Runefall.Data
{
    public enum RuneType { Passive, ActiveSkill, Aura }

    [System.Serializable]
    public class RuneEffect
    {
        [TextArea] public string effectDescription;
        public StatModifier statModifier;
        public ElementType bonusElement;
        public float bonusValue;
    }

    [CreateAssetMenu(menuName = "Runefall/Cards/Rune")]
    public class RuneData : ScriptableObject
    {
        [Header("Identity")]
        public string runeName;
        public Sprite runeArt;
        public RuneType runeType;
        public ElementType element;
        public RarityType rarity;

        [Header("Slot")]
        public int slotCost = 1;

        [Header("Effect")]
        public RuneEffect effect;

        [Header("Gacha")]
        [Range(0f, 1f)] public float baseDropRate;
    }
}
