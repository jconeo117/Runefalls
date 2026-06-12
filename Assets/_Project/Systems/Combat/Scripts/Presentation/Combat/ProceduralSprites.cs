using UnityEngine;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Procedurally generated, cached UI sprites. Replaces
    /// <c>Resources.GetBuiltinResource&lt;Sprite&gt;("UI/Skin/UISprite.psd" / "Knob.psd")</c>,
    /// which fails to load at runtime in this Unity 6 / URP project (logs
    /// "could not be loaded from the resource file" and returns null → flat rects + console spam).
    /// One small texture is generated per shape, the first time it is requested.
    /// </summary>
    public static class ProceduralSprites
    {
        private static Sprite _rounded;
        private static Sprite _disc;

        /// <summary>White rounded rectangle with a 9-slice border (use with Image.Type.Sliced).</summary>
        public static Sprite RoundedRect()
        {
            if (_rounded != null) return _rounded;

            const int s = 32;
            const float r = 8f;
            var tex = NewTex(s);
            var px  = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    px[y * s + x] = White(RoundedAlpha(x, y, s, r));
            tex.SetPixels32(px); tex.Apply();

            _rounded = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            _rounded.name = "ProcRoundedRect";
            return _rounded;
        }

        /// <summary>White filled disc (sigil / emblem).</summary>
        public static Sprite Disc()
        {
            if (_disc != null) return _disc;

            const int s = 32;
            float c   = (s - 1) * 0.5f;
            float rad = s * 0.48f;
            var tex = NewTex(s);
            var px  = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    px[y * s + x] = White(Mathf.Clamp01(rad - d));   // 1px edge AA
                }
            tex.SetPixels32(px); tex.Apply();

            _disc = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            _disc.name = "ProcDisc";
            return _disc;
        }

        private static Texture2D NewTex(int s) => new(s, s, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };

        private static Color32 White(float a) => new(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));

        // Full alpha on the straight edges / interior; AA fade only inside the four corner radii.
        private static float RoundedAlpha(int x, int y, int s, float r)
        {
            float dx = Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (s - r), 0f);
            float dy = Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (s - r), 0f);
            if (dx <= 0f || dy <= 0f) return 1f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(r - d + 0.5f);
        }
    }
}
