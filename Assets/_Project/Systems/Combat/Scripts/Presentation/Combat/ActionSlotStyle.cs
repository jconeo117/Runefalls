using UnityEngine;
using UnityEngine.UI;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Procedural look of the singleplayer action slots — no custom sprites.
    /// Pure styling: paints an inner <see cref="Image"/> as an idle card-back, as a
    /// "card moved here" state, or restores it. Stateless; the presenter owns the
    /// slot lifecycle and decides when to apply each look.
    /// </summary>
    public static class ActionSlotStyle
    {
        public static readonly Color IdleFill  = new(0.08f, 0.11f, 0.14f, 0.95f);
        public static readonly Color IdleFrame = new(0.34f, 0.55f, 0.60f, 0.70f);
        public static readonly Color Emblem    = new(0.42f, 0.64f, 0.68f, 0.28f);
        public static readonly Color MoveFill  = new(0.16f, 0.55f, 0.82f, 0.92f);
        public static readonly Color MoveGlow  = new(0.45f, 0.88f, 1.00f, 1.00f);

        private static Sprite Rounded() => ProceduralSprites.RoundedRect();
        private static Sprite Disc()    => ProceduralSprites.Disc();

        /// <summary>Empty slot = rounded slate-teal card back: dark fill + teal frame + faint central emblem.</summary>
        public static void ApplyIdle(Image inner)
        {
            if (inner == null) return;

            inner.sprite = Rounded();
            inner.type   = Image.Type.Sliced;
            inner.color  = IdleFill;

            var frame = inner.GetComponent<Outline>() ?? inner.gameObject.AddComponent<Outline>();
            frame.effectColor    = IdleFrame;
            frame.effectDistance = new Vector2(2f, -2f);

            // Central emblem: a faint disc with its own ring outline — suggests a rune sigil.
            var emT = inner.transform.Find("Emblem");
            if (emT == null)
            {
                var emGO = new GameObject("Emblem");
                emGO.transform.SetParent(inner.transform, false);
                var emRT = emGO.AddComponent<RectTransform>();
                emRT.anchorMin = new Vector2(0.5f, 0.5f);
                emRT.anchorMax = new Vector2(0.5f, 0.5f);
                emRT.sizeDelta = new Vector2(34f, 34f);
                emRT.anchoredPosition = Vector2.zero;
                var emImg = emGO.AddComponent<Image>();
                emImg.sprite = Disc();
                emImg.color  = Emblem;
                emImg.raycastTarget = false;
                var emRing = emGO.AddComponent<Outline>();
                emRing.effectColor    = new Color(IdleFrame.r, IdleFrame.g, IdleFrame.b, 0.5f);
                emRing.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }

        /// <summary>Reset a slot's dynamic look back to the idle card-back (after a move/skill cleared it).</summary>
        public static void RestoreIdle(Image inner)
        {
            if (inner == null) return;

            inner.color = IdleFill;
            var frame = inner.GetComponent<Outline>();
            if (frame != null)
            {
                frame.effectColor    = IdleFrame;
                frame.effectDistance = new Vector2(2f, -2f);
            }
            var em = inner.transform.Find("Emblem");
            if (em != null) em.gameObject.SetActive(true);
        }

        /// <summary>
        /// "A card was moved into this slot": cyan fill + glowing frame, emblem hidden,
        /// and a "→" glyph stamped on the slot. <paramref name="slot"/> is the slot root,
        /// <paramref name="inner"/> its inner image (may be null).
        /// </summary>
        public static void ApplyMove(Transform slot, Image inner)
        {
            if (inner != null)
            {
                inner.color = MoveFill;
                var frame = inner.GetComponent<Outline>();
                if (frame != null)
                {
                    frame.effectColor    = MoveGlow;
                    frame.effectDistance = new Vector2(3f, -3f);
                }
                var em = inner.transform.Find("Emblem");
                if (em != null) em.gameObject.SetActive(false);
            }

            if (slot == null) return;

            var lbl = slot.Find("MoveLabel");
            if (lbl == null)
            {
                var lblGO   = new GameObject("MoveLabel");
                lblGO.transform.SetParent(slot, false);
                var labelRt = lblGO.AddComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;

                // Glow halo around the glyph.
                var glow = lblGO.AddComponent<Outline>();
                glow.effectColor    = new Color(0.6f, 0.95f, 1f, 0.9f);
                glow.effectDistance = new Vector2(1.5f, -1.5f);

                var txt       = lblGO.AddComponent<Text>();
                txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize  = 30;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color     = Color.white;
                txt.text      = "→";   // move-into-slot glyph
                txt.raycastTarget = false;
            }
            else
            {
                lbl.gameObject.SetActive(true);
                lbl.GetComponent<Text>().text = "→";
            }
        }
    }
}
