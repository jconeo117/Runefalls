using System.Collections;
using UnityEngine;

namespace Runefall.Presentation.Combat
{
    public enum CameraPose { Static, PushIn, DynamicOrbit }

    [System.Serializable]
    public struct CameraConfig
    {
        public bool useSkillCamera;
        public float bronzeHeight;
        public float bronzeBack;
        public float bronzeRight;
        public float faceDist;
        public float faceHeight;
        public float camBlendIn;
        public float silverFaceHold;
        public float silverReturnDur;
        public float goldFaceHold;
        public float goldOrbitSpeed;
        // Gold "whip + settle": fast arc around the caster braking into an over-the-shoulder impact shot.
        public float goldEndBack;     // settle distance behind the caster
        public float goldEndRight;    // settle offset to the caster's right shoulder
        public float goldEndHeight;   // settle height above the caster's feet
        public float goldWhipMinArc;  // minimum sweep in degrees (forces a real whip, not a small turn)
    }

    public interface ICameraDirector
    {
        IEnumerator PerformCameraShake(float duration, float magnitude);
        IEnumerator BlendToPose(CameraPose pose, Transform casterAnchor, float duration);
        IEnumerator StartOrbit(Transform focusPoint, float speed);
        void StopOrbit();
    }
}
