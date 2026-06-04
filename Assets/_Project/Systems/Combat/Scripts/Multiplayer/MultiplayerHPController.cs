using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Manages all HP bar visuals in multiplayer combat.
    ///
    /// Player characters: world-space billboard bars, one per NetworkedCombatPawn.
    /// Boss: screen-space bar anchored to top-left of the HUD canvas.
    ///
    /// HP values come from ServerCombatOrchestrator RPCs — no local actor needed.
    /// Created and initialized by MultiplayerLocalCombatSetup.
    /// </summary>
    public class MultiplayerHPController : MonoBehaviour
    {
        // World-space bars per player (clientId → fill Image)
        private readonly Dictionary<ulong, Image> _playerFills = new();

        // Boss screen-space bar
        private Image _bossFill;

        // Canvas used for screen-space boss bar (found from the HUD instance)
        private Canvas _hudCanvas;

        // ── Initialization ─────────────────────────────────────────────────────

        public void Initialize(Canvas hudCanvas)
        {
            _hudCanvas = hudCanvas;
            StartCoroutine(SetupWhenReady());
        }

        private IEnumerator SetupWhenReady()
        {
            yield return new WaitUntil(() => ServerCombatOrchestrator.Instance != null);

            var orch = ServerCombatOrchestrator.Instance;
            orch.OnPlayerHpChanged += OnPlayerHpChanged;
            orch.OnEnemyHpChanged  += OnEnemyHpChanged;

            // Brief wait for NetworkedCombatPawns to fully initialize their visuals.
            yield return new WaitForSeconds(0.5f);
            BuildHPBars();
        }

        private void OnDestroy()
        {
            if (ServerCombatOrchestrator.Instance == null) return;
            ServerCombatOrchestrator.Instance.OnPlayerHpChanged -= OnPlayerHpChanged;
            ServerCombatOrchestrator.Instance.OnEnemyHpChanged  -= OnEnemyHpChanged;
        }

        // ── HP bar creation ────────────────────────────────────────────────────

        private void BuildHPBars()
        {
            var pawns = Object.FindObjectsByType<NetworkedCombatPawn>(FindObjectsSortMode.None);
            bool bossBarCreated = false;

            foreach (var pawn in pawns)
            {
                if (pawn.IsPlayerTeam.Value)
                    CreateWorldSpaceBar(pawn);
                else if (!bossBarCreated)
                {
                    CreateBossScreenBar();
                    bossBarCreated = true;
                }
            }

            Debug.Log($"[MPHPCtrl] {_playerFills.Count} player bars + boss bar={bossBarCreated}.");
        }

        private void CreateWorldSpaceBar(NetworkedCombatPawn pawn)
        {
            ulong cid = pawn.NetworkObject.OwnerClientId;

            // Canvas
            var canvasGO = new GameObject($"HPBar_P{cid}");
            canvasGO.transform.SetParent(pawn.transform, false);
            canvasGO.transform.localPosition = new Vector3(0f, 2.4f, 0f);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            var rt = (RectTransform)canvasGO.transform;
            rt.sizeDelta   = new Vector2(200f, 20f);
            rt.localScale  = Vector3.one * 0.008f;

            // Background
            var bgFill = CreateBarLayer(canvasGO.transform, "BG", new Color(0.08f, 0.08f, 0.08f, 0.85f));
            bgFill.anchorMin = Vector2.zero;
            bgFill.anchorMax = Vector2.one;
            bgFill.offsetMin = bgFill.offsetMax = Vector2.zero;

            // Green fill
            var fillRT = CreateBarLayer(canvasGO.transform, "Fill", new Color(0.18f, 0.78f, 0.30f));
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;

            _playerFills[cid] = fillRT.GetComponent<Image>();

            // Billboard component so bar always faces camera
            canvasGO.AddComponent<BillboardBar>();

            Debug.Log($"[MPHPCtrl] World HP bar creado para player {cid}.");
        }

        private void CreateBossScreenBar()
        {
            if (_hudCanvas == null)
            {
                Debug.LogWarning("[MPHPCtrl] HUD canvas no asignado — boss bar omitida.");
                return;
            }

            // Container anchored top-left
            var containerGO = new GameObject("BossHPBar");
            containerGO.transform.SetParent(_hudCanvas.transform, false);

            var container = containerGO.AddComponent<RectTransform>();
            container.anchorMin        = new Vector2(0f, 1f);
            container.anchorMax        = new Vector2(0f, 1f);
            container.pivot            = new Vector2(0f, 1f);
            container.anchoredPosition = new Vector2(20f, -20f);
            container.sizeDelta        = new Vector2(280f, 28f);

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(containerGO.transform, false);
            var labelRT            = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin      = new Vector2(0f, 1f);
            labelRT.anchorMax      = new Vector2(1f, 1f);
            labelRT.pivot          = new Vector2(0f, 0f);
            labelRT.anchoredPosition = new Vector2(0f, 4f);
            labelRT.sizeDelta      = new Vector2(0f, 18f);
            var labelTxt           = labelGO.AddComponent<Text>();
            labelTxt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelTxt.text          = "BOSS";
            labelTxt.fontSize      = 13;
            labelTxt.fontStyle     = FontStyle.Bold;
            labelTxt.color         = new Color(1f, 0.85f, 0.3f);
            labelTxt.alignment     = TextAnchor.MiddleLeft;

            // Background
            var bgRT = CreateBarLayer(containerGO.transform, "BG", new Color(0.08f, 0.08f, 0.08f, 0.9f));
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

            // Red fill
            var fillRT = CreateBarLayer(containerGO.transform, "Fill", new Color(0.85f, 0.15f, 0.15f));
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;

            _bossFill = fillRT.GetComponent<Image>();

            Debug.Log("[MPHPCtrl] Boss HP bar creada en top-left.");
        }

        // ── HP event handlers ──────────────────────────────────────────────────

        private void OnPlayerHpChanged(ulong clientId, int newHp, int maxHp)
        {
            if (!_playerFills.TryGetValue(clientId, out var fill) || fill == null) return;
            float pct = maxHp > 0 ? (float)newHp / maxHp : 0f;
            SetFill(fill, pct);
        }

        private void OnEnemyHpChanged(int enemyIndex, int newHp, int maxHp)
        {
            if (_bossFill == null) return;
            float pct = maxHp > 0 ? (float)newHp / maxHp : 0f;
            SetFill(_bossFill, pct);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static RectTransform CreateBarLayer(Transform parent, string layerName, Color color)
        {
            var go  = new GameObject(layerName);
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = color;
            return rt;
        }

        private static void SetFill(Image fill, float pct)
        {
            var rt        = fill.GetComponent<RectTransform>();
            rt.anchorMax  = new Vector2(Mathf.Clamp01(pct), 1f);
        }

        // ── Billboard helper ───────────────────────────────────────────────────

        private class BillboardBar : MonoBehaviour
        {
            private void LateUpdate()
            {
                var cam = Camera.main;
                if (cam != null)
                    transform.rotation = cam.transform.rotation;
            }
        }
    }
}
