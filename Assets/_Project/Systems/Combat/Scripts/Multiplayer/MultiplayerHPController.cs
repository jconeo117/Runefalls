using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Runefall.Presentation.Combat;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Manages all HP bar visuals in multiplayer combat.
    ///
    /// Player characters: world-space billboard bars, one per NetworkedCombatPawn.
    /// Boss: large styled screen-space bar anchored to the top-center of the HUD canvas.
    /// Floating damage numbers rise above the struck pawn whenever HP drops.
    ///
    /// HP values come from ServerCombatOrchestrator RPCs — no local actor needed.
    /// Created and initialized by MultiplayerLocalCombatSetup.
    /// </summary>
    public class MultiplayerHPController : MonoBehaviour
    {
        // World-space bars + smooth fillers per player (clientId → …)
        private readonly Dictionary<ulong, SmoothFill> _playerFills = new();
        private readonly Dictionary<ulong, Transform>  _playerPawns = new();
        private readonly Dictionary<ulong, int>        _playerLastHp = new();

        // Boss
        private SmoothFill _bossFill;
        private Transform  _bossPawn;
        private Text       _bossHpText;
        private int        _bossLastHp = -1;

        // Canvas used for screen-space boss bar (found from the HUD instance)
        private Canvas     _hudCanvas;
        private GameObject _floatingDamagePrefab;

        // ── Initialization ─────────────────────────────────────────────────────

        public void Initialize(Canvas hudCanvas, GameObject floatingDamagePrefab = null)
        {
            _hudCanvas            = hudCanvas;
            _floatingDamagePrefab = floatingDamagePrefab;
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
                    CreateBossScreenBar(pawn);
                    bossBarCreated = true;
                }
            }

            Debug.Log($"[MPHPCtrl] {_playerFills.Count} player bars + boss bar={bossBarCreated}.");
        }

        private void CreateWorldSpaceBar(NetworkedCombatPawn pawn)
        {
            ulong cid = pawn.NetworkObject.OwnerClientId;

            var canvasGO = new GameObject($"HPBar_P{cid}");
            canvasGO.transform.SetParent(pawn.transform, false);
            canvasGO.transform.localPosition = new Vector3(0f, 2.4f, 0f);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            var rt = (RectTransform)canvasGO.transform;
            rt.sizeDelta  = new Vector2(200f, 20f);
            rt.localScale = Vector3.one * 0.008f;

            var bgFill = CreateBarLayer(canvasGO.transform, "BG", new Color(0.08f, 0.08f, 0.08f, 0.85f));
            bgFill.anchorMin = Vector2.zero; bgFill.anchorMax = Vector2.one;
            bgFill.offsetMin = bgFill.offsetMax = Vector2.zero;

            var fillRT = CreateBarLayer(canvasGO.transform, "Fill", new Color(0.18f, 0.78f, 0.30f));
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;

            _playerFills[cid] = fillRT.gameObject.AddComponent<SmoothFill>();
            _playerPawns[cid] = pawn.transform;

            canvasGO.AddComponent<BillboardBar>();
        }

        // Large, styled boss bar anchored top-center: dark track, red fill, gold frame, name + HP text.
        private void CreateBossScreenBar(NetworkedCombatPawn pawn)
        {
            _bossPawn = pawn.transform;
            if (_hudCanvas == null)
            {
                Debug.LogWarning("[MPHPCtrl] HUD canvas no asignado — boss bar omitida.");
                return;
            }

            string bossName = pawn.GetComponent<EnemySlot>()?.data?.enemyName ?? "BOSS";

            // Container: top-center, wide.
            var containerGO = new GameObject("BossHPBar");
            containerGO.transform.SetParent(_hudCanvas.transform, false);
            var container = containerGO.AddComponent<RectTransform>();
            container.anchorMin        = new Vector2(0.5f, 1f);
            container.anchorMax        = new Vector2(0.5f, 1f);
            container.pivot            = new Vector2(0.5f, 1f);
            container.anchoredPosition = new Vector2(0f, -24f);
            container.sizeDelta        = new Vector2(620f, 46f);

            // Gold frame (slightly larger backing).
            var frame = CreateBarLayer(containerGO.transform, "Frame", new Color(0.78f, 0.62f, 0.22f, 1f));
            frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(-3f, -3f); frame.offsetMax = new Vector2(3f, 3f);

            // Dark track.
            var track = CreateBarLayer(containerGO.transform, "Track", new Color(0.06f, 0.05f, 0.07f, 0.96f));
            track.anchorMin = Vector2.zero; track.anchorMax = Vector2.one;
            track.offsetMin = track.offsetMax = Vector2.zero;

            // Red fill (left-anchored, width via anchorMax.x).
            var fillRT = CreateBarLayer(containerGO.transform, "Fill", new Color(0.80f, 0.12f, 0.14f));
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = new Vector2(2f, 2f); fillRT.offsetMax = new Vector2(-2f, -2f);
            fillRT.pivot     = new Vector2(0f, 0.5f);
            _bossFill = fillRT.gameObject.AddComponent<SmoothFill>();

            // Top sheen on the fill for a bit of depth.
            var sheen = CreateBarLayer(fillRT, "Sheen", new Color(1f, 1f, 1f, 0.12f));
            sheen.anchorMin = new Vector2(0f, 0.55f); sheen.anchorMax = Vector2.one;
            sheen.offsetMin = sheen.offsetMax = Vector2.zero;

            // Boss name (centered).
            var nameTxt = CreateText(containerGO.transform, bossName.ToUpperInvariant(), 18, FontStyle.Bold,
                new Color(1f, 0.93f, 0.7f), TextAnchor.MiddleCenter);
            nameTxt.rectTransform.anchorMin = Vector2.zero; nameTxt.rectTransform.anchorMax = Vector2.one;
            nameTxt.rectTransform.offsetMin = nameTxt.rectTransform.offsetMax = Vector2.zero;
            var nameShadow = nameTxt.gameObject.AddComponent<Shadow>();
            nameShadow.effectColor    = new Color(0f, 0f, 0f, 0.8f);
            nameShadow.effectDistance = new Vector2(1.5f, -1.5f);

            // HP number (right side).
            _bossHpText = CreateText(containerGO.transform, "", 13, FontStyle.Bold,
                new Color(1f, 0.95f, 0.95f), TextAnchor.MiddleRight);
            _bossHpText.rectTransform.anchorMin = Vector2.zero; _bossHpText.rectTransform.anchorMax = Vector2.one;
            _bossHpText.rectTransform.offsetMin = new Vector2(0f, 0f);
            _bossHpText.rectTransform.offsetMax = new Vector2(-12f, 0f);

            Debug.Log("[MPHPCtrl] Boss HP bar (styled) creada en top-center.");
        }

        // ── HP event handlers ──────────────────────────────────────────────────

        private void OnPlayerHpChanged(ulong clientId, int newHp, int maxHp, int damage, bool isCrit)
        {
            _playerLastHp[clientId] = newHp;
            if (_playerFills.TryGetValue(clientId, out var fill) && fill != null)
                fill.SetTarget(maxHp > 0 ? (float)newHp / maxHp : 0f);

            if (damage > 0 && _playerPawns.TryGetValue(clientId, out var pawn) && pawn != null)
                SpawnDamageNumber(pawn, damage, isCrit, 2.3f);
        }

        private void OnEnemyHpChanged(int enemyIndex, int newHp, int maxHp, int damage, bool isCrit)
        {
            _bossLastHp = newHp;
            if (_bossFill != null)
                _bossFill.SetTarget(maxHp > 0 ? (float)newHp / maxHp : 0f);
            if (_bossHpText != null)
                _bossHpText.text = $"{Mathf.Max(0, newHp)} / {maxHp}";

            if (damage > 0 && _bossPawn != null)
                SpawnDamageNumber(_bossPawn, damage, isCrit, 3.0f);
        }

        // ── Floating damage numbers (FloatingDamage.prefab + DamageNumber) ────────

        private void SpawnDamageNumber(Transform pawn, int amount, bool isCrit, float heightOffset)
        {
            if (_floatingDamagePrefab == null) return;

            Vector3 pos = pawn.position + Vector3.up * heightOffset
                        + new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f);
            var go = Instantiate(_floatingDamagePrefab, pos, Quaternion.identity);

            var dn = go.GetComponent<DamageNumber>();
            if (dn != null) dn.Show(amount.ToString(), isCrit ? 11f : 8f, isCrit);
            else Destroy(go, 1.5f);
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

        private static Text CreateText(Transform parent, string content, int size, FontStyle style,
            Color color, TextAnchor anchor)
        {
            var go  = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var txt        = go.AddComponent<Text>();
            txt.font       = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text       = content;
            txt.fontSize   = size;
            txt.fontStyle  = style;
            txt.color      = color;
            txt.alignment  = anchor;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow   = VerticalWrapMode.Overflow;
            return txt;
        }

        // ── Smooth fill: lerps anchorMax.x toward a target percentage ─────────────

        private class SmoothFill : MonoBehaviour
        {
            private RectTransform _rt;
            private float _target = 1f;

            private void Awake() { _rt = (RectTransform)transform; }

            public void SetTarget(float pct) => _target = Mathf.Clamp01(pct);

            private void Update()
            {
                var max = _rt.anchorMax;
                float next = Mathf.MoveTowards(max.x, _target, Time.deltaTime * 1.6f);
                if (!Mathf.Approximately(next, max.x))
                {
                    max.x = next;
                    _rt.anchorMax = max;
                }
            }
        }

        // ── Billboard helper ───────────────────────────────────────────────────

        private class BillboardBar : MonoBehaviour
        {
            private void LateUpdate()
            {
                var cam = Camera.main;
                if (cam != null) transform.rotation = cam.transform.rotation;
            }
        }
    }
}
