using UnityEngine;

namespace Runefall.Presentation.Dungeon
{
    public class RoomRegistry : MonoBehaviour
    {
        [SerializeField] private RoomVolume[] _rooms;

        /// <summary>Returns the RoomVolume whose center is closest to worldPosition.</summary>
        public RoomVolume FindNearest(Vector3 worldPosition)
        {
            if (_rooms == null || _rooms.Length == 0)
            {
                Debug.LogWarning("[RoomRegistry] No rooms registered.", this);
                return null;
            }

            RoomVolume nearest  = null;
            float      bestSqr  = float.MaxValue;

            foreach (var room in _rooms)
            {
                if (room == null) continue;
                float d = Vector3.SqrMagnitude(worldPosition - room.transform.position);
                if (d < bestSqr) { bestSqr = d; nearest = room; }
            }

            return nearest;
        }
    }
}
