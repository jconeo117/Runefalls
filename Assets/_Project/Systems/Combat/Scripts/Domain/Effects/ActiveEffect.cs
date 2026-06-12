using Runefall.Characters;

namespace Runefall.Combat
{
    public class ActiveEffect
    {
        public EffectDefinition Source;       // identity — used for stacking, HasEffect<T>(), UI display
        public EffectTag        Tag;          // Advantage / Disadvantage / Stance / Neutral
        public ICombatActor     Applier;
        public int              TurnsRemaining;     // -1 = permanent (never expires via Tick)
        public int              Stacks        = 1;
        public float            StoredValue;        // DoT tick damage, shield amount, arrebato percent
        public bool             TickDamage;         // true = StoredValue applied as damage each Tick()
        public float            IncomingDamageBonus;// additive bonus to DamageReceivedMultiplier per stack
        public StatModifier     StatMod;            // non-null: added/removed from CharacterModel on apply/expire
        public ICombatActor     LinkedActor;        // arrebato: the paired actor
        public string           GroupId;            // arrebato: shared id — removing one removes both

        // While active on an actor, these conditionally modify that actor's OUTGOING damage
        // (gathered by DamageEffectDef). Null = none. E.g. Monarca del Hielo: x2 vs frozen.
        public ConditionalDamageModifier[] OutgoingDamageModifiers;
    }
}
