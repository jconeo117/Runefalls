using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// The ultimate-gauge orb strip (7 orbs). Builds the orb row once into the
    /// wired container and lights orbs by fill count. Single responsibility:
    /// the gauge readout — it knows nothing about how the gauge fills.
    /// </summary>
    public sealed class UltimateGaugeView
    {
        private const int k_OrbCount = 7;

        private static readonly Color k_Full = new(0.72f, 0.32f, 1f,    1f);
        private static readonly Color k_Dim  = new(0.22f, 0.12f, 0.35f, 1f);

        private readonly List<Image> _orbs = new();

        public UltimateGaugeView(Transform container)
        {
            if (container == null) return;

            var hlg = container.GetComponent<HorizontalLayoutGroup>()
                   ?? container.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 4f;
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;

            for (int i = 0; i < k_OrbCount; i++)
            {
                var go       = new GameObject("Orb_" + i);
                go.transform.SetParent(container, false);
                var rt       = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(18f, 18f);
                var img      = go.AddComponent<Image>();
                img.color    = k_Dim;
                _orbs.Add(img);
            }
        }

        /// <summary>Light the first <paramref name="filled"/> orbs; dim the rest.</summary>
        public void SetOrbs(int filled)
        {
            for (int i = 0; i < _orbs.Count; i++)
                if (_orbs[i] != null)
                    _orbs[i].color = i < filled ? k_Full : k_Dim;
        }
    }
}
