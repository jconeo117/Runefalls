using System;
using Runefall.Characters;
using Runefall.Data;

namespace Runefall.Combat
{
    public class PlayerActor : ICombatActor
    {
        private readonly CharacterData _data;

        public string         Name        => _data.characterName;
        public bool           IsAlive     => Model.IsAlive;
        public CharacterModel Model       { get; }
        public ElementType    Element     => _data.element;
        public ActorEffects   Effects     { get; }
        public float          CombatClass => Model.Stats.ClaseDeCombate;

        public PlayerActor(CharacterData data)
        {
            if (data == null)           throw new ArgumentNullException(nameof(data));
            if (data.baseStats == null) throw new ArgumentException("CharacterData.baseStats is null.", nameof(data));
            _data   = data;
            Model   = new CharacterModel(data.characterName, data.baseStats);
            Effects = new ActorEffects(this);
        }
    }
}
