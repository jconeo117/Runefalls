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
    }

    public interface ICameraDirector
    {
        IEnumerator PerformCameraShake(float duration, float magnitude);
        IEnumerator BlendToPose(CameraPose pose, Transform casterAnchor, float duration);
        IEnumerator StartOrbit(Transform focusPoint, float speed);
        void StopOrbit();
    }
}
