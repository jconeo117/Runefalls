using System.Collections.Generic;
using UnityEngine;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    public class PawnVisualManager
    {
        private readonly RuntimeAnimatorController _combatBaseController;

        public PawnVisualManager(RuntimeAnimatorController combatBaseController)
        {
            _combatBaseController = combatBaseController;
        }

        public void InitPawnAnimators(
            IReadOnlyDictionary<ICombatActor, Transform> actorPawns,
            IReadOnlyDictionary<ICombatActor, CharacterData> actorCharData,
            IReadOnlyDictionary<ICombatActor, EnemyData> actorEnemyData)
        {
            if (actorPawns == null) return;

            foreach (var (actor, pawn) in actorPawns)
            {
                if (pawn == null) continue;
                var anim = pawn.GetComponentInChildren<CombatPawnAnimator>();
                if (anim == null) continue;

                if (actorCharData != null && actorCharData.TryGetValue(actor, out var cd))
                    anim.InitFromCharacter(cd, _combatBaseController);
                else if (actorEnemyData != null && actorEnemyData.TryGetValue(actor, out var ed))
                    anim.InitFromEnemy(ed, _combatBaseController);
            }
        }
    }
}
