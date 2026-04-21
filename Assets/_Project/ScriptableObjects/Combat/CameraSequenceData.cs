using UnityEngine;

namespace Runefall.Data
{
    public enum CameraShotType { Static, PushIn, DynamicOrbit, OverShoulder, LowAngle }

    [System.Serializable]
    public class CameraShot
    {
        public CameraShotType type;
        public float duration;
        public float fov = 60f;
        public float blendIn = 0.1f;
        [Range(-1f, 1f)] public float dutchAngle;
    }

    [CreateAssetMenu(menuName = "Runefall/Camera/CameraSequence")]
    public class CameraSequenceData : ScriptableObject
    {
        public CameraShot[] shots;
        public float returnDuration = 0.3f;
    }
}
