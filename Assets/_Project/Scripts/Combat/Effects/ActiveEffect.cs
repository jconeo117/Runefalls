using Runefall.Characters;

namespace Runefall.Combat
{
    public class ActiveEffect
    {
        public EffectDefinition Source;
        public EffectTag        Tag;
        public EffectFlag       Flag;
        public ICombatActor     Applier;
        public int              TurnsRemaining;
        public int              Stacks;
        public float            StoredValue;  // DoT tick damage, shield amount, arrebato percent
        public bool             TickDamage;   // true = StoredValue applied as damage each Tick()
        public StatModifier     StatMod;      // non-null: added/removed from CharacterModel on apply/expire
        public ICombatActor     LinkedActor;  // arrebato: the paired actor
        public string           GroupId;      // arrebato: shared id — removing one removes both
    }
}
