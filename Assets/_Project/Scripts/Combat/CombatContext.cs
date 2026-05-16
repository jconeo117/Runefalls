using System.Collections.Generic;

namespace Runefall.Combat
{
    public class CombatContext
    {
        public IReadOnlyList<ICombatActor> Players  { get; }
        public IReadOnlyList<ICombatActor> Enemies  { get; }
        public IReadOnlyList<ICombatActor> AllActors { get; }

        // Written by TurnManager only.
        public ICombatActor ActiveActor { get; set; }
        public int TurnNumber { get; set; }

        public bool IsOver    => !AnyAlive(Players) || !AnyAlive(Enemies);
        public bool PlayerWon => IsOver && !AnyAlive(Enemies);

        public CombatContext(IEnumerable<ICombatActor> players, IEnumerable<ICombatActor> enemies)
        {
            var p = new List<ICombatActor>(players);
            var e = new List<ICombatActor>(enemies);
            var all = new List<ICombatActor>(p.Count + e.Count);
            all.AddRange(p);
            all.AddRange(e);

            Players   = p.AsReadOnly();
            Enemies   = e.AsReadOnly();
            AllActors = all.AsReadOnly();
        }

        private static bool AnyAlive(IReadOnlyList<ICombatActor> actors)
        {
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].IsAlive) return true;
            return false;
        }
    }
}
