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
        public float goldEndBack;     // impact shot: distance toward the caster side from the impact point
        public float goldEndRight;    // impact shot: lateral offset
        public float goldEndHeight;   // impact shot: height
        public float goldWhipMinArc;  // (legacy, unused by the cut sequence)
        // Gold "cut sequence" (cinematic hard cuts: low hero -> wind-up -> impact + shake).
        public float goldCut1Hold;      // seconds the low-hero anticipation cut holds
        public float goldCut2Hold;      // seconds the wind-up close-up cut holds
        public float goldShakeMagnitude;// camera shake on the impact cut
    }

    public interface ICameraDirector
    {
        IEnumerator PerformCameraShake(float duration, float magnitude);
        IEnumerator BlendToPose(CameraPose pose, Transform casterAnchor, float duration);
        IEnumerator StartOrbit(Transform focusPoint, float speed);
        void StopOrbit();
    }
}
