using UnityEngine;

namespace Runefall.Presentation.Dungeon
{
    /// <summary>
    /// Marks a room's walkable volume. BoxCollider defines the bounds.
    /// Used by CombatArenaAssembler to compute optimal arena layout inside the room.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class RoomVolume : MonoBehaviour
    {
        [Tooltip("Min distance between each arena line and the nearest wall.")]
        [SerializeField] private float _wallMargin = 3f;

        private BoxCollider _col;

        private void Awake() => _col = GetComponent<BoxCollider>();

        public Bounds WorldBounds => _col != null
            ? _col.bounds
            : new Bounds(transform.position, Vector3.one * 10f);

        /// <summary>
        /// Computes world-space player and enemy line centers that fit inside this room.
        /// Orients the arena along the room's longest horizontal axis.
        /// lineHalfSpan = half of the desired distance between the two lines.
        /// </summary>
        public (Vector3 playerCenter, Vector3 enemyCenter, Vector3 forward)
            GetArenaLayout(float lineHalfSpan)
        {
            if (_col == null) _col = GetComponent<BoxCollider>();

            var b      = WorldBounds;
            // Use the box floor (bottom face) as the arena ground Y so pawns stand on the room's
            // floor regardless of world height. For a box sitting on a y=0 floor (center.y=half),
            // b.min.y == 0 → identical to the old hardcoded 0.
            var center = new Vector3(b.center.x, b.min.y, b.center.z);

            // Orient along longest horizontal axis
            bool zLonger = b.size.z >= b.size.x;
            var  forward  = zLonger ? Vector3.forward : Vector3.right;

            float halfRoom = (zLonger ? b.extents.z : b.extents.x) - _wallMargin;
            float offset   = Mathf.Min(lineHalfSpan, Mathf.Max(halfRoom, 1f));

            return (center - forward * offset,
                    center + forward * offset,
                    forward);
        }
    }
}
