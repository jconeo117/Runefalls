using UnityEngine;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Marks a pawn as inspectable and carries its <see cref="ICharacterStatsProvider"/>.
    /// The overlay raycasts the world, finds this component on the hit pawn and reads its
    /// provider — so any pawn with this component (+ a collider) is inspectable, regardless
    /// of whether it is the player, an enemy or anything else.
    /// </summary>
    public class CombatantStatsSource : MonoBehaviour
    {
        public ICharacterStatsProvider Provider { get; private set; }

        public void Bind(ICharacterStatsProvider provider)
        {
            Provider = provider;
            EnsurePickCollider();
        }

        /// <summary>
        /// Guarantee a collider the inspect raycast can hit. Most combat pawns are pure
        /// renderers with no physics, so add a humanoid-sized capsule if none exists.
        /// </summary>
        private void EnsurePickCollider()
        {
            if (GetComponentInChildren<Collider>() != null) return;

            var capsule        = gameObject.AddComponent<CapsuleCollider>();
            capsule.direction  = 1;           // Y axis
            capsule.height     = 2f;
            capsule.radius     = 0.5f;
            capsule.center     = new Vector3(0f, 1f, 0f);
            capsule.isTrigger  = true;        // pick-only — never participates in combat physics
        }
    }
}
