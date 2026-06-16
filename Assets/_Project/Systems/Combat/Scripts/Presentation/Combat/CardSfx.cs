using System.Collections.Generic;
using UnityEngine;
using Runefall.Audio;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Lightweight holder for card UI sounds (hover / use / upgrade), played 2D through the
    /// AudioManager. Clips are installed once by <see cref="CardSfxInstaller"/> so CardView and the
    /// HUD can fire them without threading references through the (MP-frozen) presenter.
    ///
    /// A short per-clip cooldown collapses duplicate triggers (e.g. a merge that both ranks up the
    /// surviving card and slides the consumed one) into a single play.
    /// </summary>
    public static class CardSfx
    {
        public static AudioClip Hover;
        public static AudioClip Use;
        public static AudioClip Upgrade;

        private const float k_Cooldown = 0.09f;
        private static readonly Dictionary<AudioClip, float> _lastPlay = new();

        public static void Play(AudioClip clip)
        {
            if (clip == null) return;
            float now = Time.unscaledTime;
            if (_lastPlay.TryGetValue(clip, out var t) && now - t < k_Cooldown) return;
            _lastPlay[clip] = now;
            AudioManager.GetOrCreate()?.Play2D(clip);
        }
    }
}
