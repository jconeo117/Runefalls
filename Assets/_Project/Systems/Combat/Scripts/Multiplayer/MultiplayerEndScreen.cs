using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Full-screen victory / defeat overlay shown to every client (alive or dead) when combat ends.
    /// Built at runtime — no prefab needed. Buttons:
    ///   Victory → "Volver al Lobby" (each client disconnects + loads the lobby locally).
    ///   Defeat  → "Volver al Lobby" + "Reintentar" (host reloads the BossFight scene for all).
    /// </summary>
    public class MultiplayerEndScreen : MonoBehaviour
    {
        private const string LobbySceneName = "Multiplayer_Lobby";

        private Button                   _retryBtn;
        private Text                     _statusLabel;
        private ServerCombatOrchestrator _orch;

        public static MultiplayerEndScreen Show(bool won)
        {
            var go = new GameObject("MultiplayerEndScreen");
            var screen = go.AddComponent<MultiplayerEndScreen>();
            screen.Build(won);
            return screen;
        }

        private void OnDestroy()
        {
            if (_orch != null) _orch.OnRetryStatus -= OnRetryStatus;
        }

        private void Build(bool won)
        {
            // Canvas overlay on top of everything.
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            gameObject.AddComponent<GraphicRaycaster>();

            // Dim background.
            var bg = NewImage(transform, "BG", new Color(0.02f, 0.02f, 0.04f, 0.82f));
            Stretch(bg.rectTransform);

            // Title.
            var title = NewText(transform, "Title", won ? "¡VICTORIA!" : "DERROTA", 96, FontStyle.Bold,
                won ? new Color(1f, 0.86f, 0.35f) : new Color(0.95f, 0.3f, 0.3f), TextAnchor.MiddleCenter);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 0.5f); trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(0f, 120f);
            trt.sizeDelta = new Vector2(1200f, 200f);

            // Buttons row.
            float y = -90f;
            if (won)
            {
                MakeButton("Volver al Lobby", new Vector2(0f, y), new Color(0.2f, 0.4f, 0.7f), ReturnToLobby);
            }
            else
            {
                _retryBtn = MakeButton("Reintentar",      new Vector2(-180f, y), new Color(0.25f, 0.6f, 0.3f), Retry);
                MakeButton("Volver al Lobby", new Vector2( 180f, y), new Color(0.6f, 0.25f, 0.25f), ReturnToLobby);

                // Retry consensus status (both players must agree).
                _statusLabel = NewText(transform, "RetryStatus", "", 26, FontStyle.Normal,
                    new Color(0.9f, 0.9f, 0.7f), TextAnchor.MiddleCenter);
                var srt = _statusLabel.rectTransform;
                srt.anchorMin = new Vector2(0.5f, 0.5f); srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = new Vector2(0f, -180f);
                srt.sizeDelta = new Vector2(900f, 50f);

                _orch = ServerCombatOrchestrator.Instance;
                if (_orch != null) _orch.OnRetryStatus += OnRetryStatus;
            }
        }

        // ── Button actions ───────────────────────────────────────────────────────

        private void Retry()
        {
            // Cast this player's vote. The host reloads only once BOTH players have voted.
            ServerCombatOrchestrator.Instance?.RetryServerRpc();
            if (_retryBtn != null) _retryBtn.interactable = false;
            if (_statusLabel != null) _statusLabel.text = "Esperando al otro jugador…";
        }

        private void OnRetryStatus(int ready, int total)
        {
            if (_statusLabel == null) return;
            _statusLabel.text = ready >= total
                ? "¡Ambos listos! Reiniciando…"
                : $"Listos {ready}/{total} — esperando al otro jugador…";
        }

        private void ReturnToLobby()
        {
            // Each client disconnects and returns to its own lobby (co-op session ends).
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene(LobbySceneName, LoadSceneMode.Single);
        }

        // ── UI builders ──────────────────────────────────────────────────────────

        private Button MakeButton(string label, Vector2 anchoredPos, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var img = NewImage(transform, $"Btn_{label}", color);
            var rt  = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(320f, 84f);

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var txt = NewText(img.transform, "Label", label, 30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(txt.rectTransform);
            return btn;
        }

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text NewText(Transform parent, string name, string content, int size, FontStyle style,
            Color color, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var txt       = go.AddComponent<Text>();
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text      = content;
            txt.fontSize  = size;
            txt.fontStyle = style;
            txt.color     = color;
            txt.alignment = anchor;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow   = VerticalWrapMode.Overflow;
            return txt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
