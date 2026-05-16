using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Runefall.Combat;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Drives the blockout combat UI.
    /// Layout (bottom-up): card hand buttons → action slots row → HP section → log.
    /// Call Initialize() from CombatBootstrapper after TurnManager is wired.
    /// </summary>
    public class CombatBlockoutPresenter : CombatPresenterBase
    {
        [Header("Card Hand")]
        public Transform  cardHandContainer;   // horizontal layout, bottom strip
        public GameObject cardButtonPrefab;    // Button prefab with child Text

        [Header("Action Slots")]
        public Transform  actionSlotContainer;
        public GameObject actionSlotPrefab;

        [Header("HP Display")]
        public Text[] playerHPTexts;           // one per player (3 field)
        public Text[] enemyHPTexts;            // one per enemy (3)

        [Header("Ultimate Gauge")]
        public Transform gaugeContainer;   // horizontal strip; orbs created at runtime

        [Header("Info")]
        public Text roundLabel;
        public Text logText;
        public Text combatResultText;
        public Button endTurnButton;

        // ── runtime ─────────────────────────────────────────────────────────────
        private TurnManager           _tm;
        private CombatContext         _ctx;
        private readonly List<Button>     _cardButtons      = new();
        private readonly List<GameObject> _actionSlotGOs    = new();
        private readonly List<Text>       _actionSlotLabels = new();
        private readonly List<Image>      _orbImages        = new();
        private readonly StringBuilder    _log              = new();

        private static readonly Color _orbFull = new(0.72f, 0.32f, 1f,  1f);   // bright purple
        private static readonly Color _orbDim  = new(0.22f, 0.12f, 0.35f, 1f); // dark purple

        // ── public API ──────────────────────────────────────────────────────────

        public override void Initialize(TurnManager tm, CombatContext ctx)
        {
            _tm  = tm;
            _ctx = ctx;
            if (endTurnButton != null)
                endTurnButton.onClick.AddListener(() => _tm?.EndPlayerTurn());
            BuildOrbRow();
        }

        public override void OnPlayerTurnStarted(int round)
        {
            RebuildActionSlots();
            ClearActionSlots();
            if (roundLabel != null) roundLabel.text = $"Round {round}";
            if (endTurnButton != null) endTurnButton.interactable = true;
            RefreshCardHand();
            RefreshHP();
            Log($"── Round {round}: player turn ──");
        }

        public override void OnActionResolved(CombatActionResult result)
        {
            RefreshHP();

            string casterName = result.Caster?.Name ?? "?";
            string targetName = result.Target?.Name ?? "?";

            if (result.DamageDealt > 0f)
            {
                string critTag = result.IsCrit ? " [CRIT]" : "";
                Log($"{casterName} → {targetName}: {result.DamageDealt:F0} dmg{critTag}");
            }
            if (result.HealApplied > 0f)
                Log($"{casterName} heals {targetName}: +{result.HealApplied:F0}");
            if (result.LifeStealApplied > 0f)
                Log($"{casterName} steals {result.LifeStealApplied:F0} HP");

            // Update caster's action slot
            int slotIdx = GetAlivePlayerSlotIndex(result.Caster);
            if (slotIdx >= 0 && slotIdx < _actionSlotLabels.Count && _actionSlotLabels[slotIdx] != null)
            {
                string slotLabel = result.Skill != null
                    ? $"{result.Skill.skillName} R{result.Rank}"
                    : result.IsUltimate() ? "ULTIMATE" : "Attack";
                _actionSlotLabels[slotIdx].text = slotLabel;
            }

            RefreshCardHand();
        }

        public override void OnCardMerged(string skillName, int newRank)
        {
            Log($"MERGE! {skillName} → Rank {newRank}");
            RefreshCardHand();
        }

        public override void OnCombatEnded(bool playerWon)
        {
            if (endTurnButton != null) endTurnButton.interactable = false;
            foreach (var btn in _cardButtons) btn.interactable = false;

            string msg = playerWon ? "VICTORY" : "DEFEAT";
            Log($"── {msg} ──");
            if (combatResultText != null)
            {
                combatResultText.text = msg;
                combatResultText.gameObject.SetActive(true);
            }
        }

        public override void OnGaugeChanged(ICombatActor actor, int orbs)
        {
            for (int i = 0; i < _orbImages.Count; i++)
                if (_orbImages[i] != null)
                    _orbImages[i].color = i < orbs ? _orbFull : _orbDim;
        }

        // ── private ─────────────────────────────────────────────────────────────

        private void BuildOrbRow()
        {
            if (gaugeContainer == null) return;

            var hlg = gaugeContainer.GetComponent<HorizontalLayoutGroup>()
                   ?? gaugeContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 4f;
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;

            for (int i = 0; i < 7; i++)
            {
                var go  = new GameObject($"Orb_{i}");
                go.transform.SetParent(gaugeContainer, false);
                var rt       = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(18f, 18f);
                var img      = go.AddComponent<Image>();
                img.color    = _orbDim;
                _orbImages.Add(img);
            }
        }

        private void RefreshCardHand()
        {
            if (cardHandContainer == null || cardButtonPrefab == null || _tm?.Hand == null) return;

            // Destroy old buttons
            foreach (var btn in _cardButtons)
                if (btn != null) Destroy(btn.gameObject);
            _cardButtons.Clear();

            var hand = _tm.Hand.Slots;
            for (int i = 0; i < hand.Count; i++)
            {
                int capturedIndex = i; // closure capture
                var slot          = hand[i];

                var go  = Instantiate(cardButtonPrefab, cardHandContainer);
                var btn = go.GetComponent<Button>();
                var lbl = go.GetComponentInChildren<Text>();

                if (lbl != null)
                {
                    if (slot.IsUltimate)
                        lbl.text = "ULTI";
                    else if (slot.Skill != null)
                        lbl.text = $"{slot.Skill.skillName}\nR{slot.Rank}";
                    else
                        lbl.text = "?";
                }

                // Tint by rank: R2 = yellow, R3 = gold, ultimate = bright gold
                var colors = btn.colors;
                colors.normalColor = slot.IsUltimate   ? new Color(1f, 0.82f, 0f)
                                   : slot.Rank == 3    ? new Color(1f, 0.60f, 0f)
                                   : slot.Rank == 2    ? new Color(0.95f, 0.90f, 0.40f)
                                   : Color.white;
                btn.colors = colors;

                bool canAct = _tm.Phase == CombatPhase.PlayerTurn && _tm.Hand.ActionsRemaining > 0;
                btn.interactable = canAct;

                btn.onClick.AddListener(() =>
                {
                    var target = GetFirstAliveEnemy();
                    if (target == null) return;
                    _tm.SubmitSkill(capturedIndex, target);
                });

                _cardButtons.Add(btn);
            }
        }

        private void RefreshHP()
        {
            if (_ctx == null) return;

            if (playerHPTexts != null)
                for (int i = 0; i < playerHPTexts.Length && i < _ctx.Players.Count; i++)
                {
                    if (playerHPTexts[i] == null) continue;
                    var m = _ctx.Players[i].Model;
                    playerHPTexts[i].text = $"{_ctx.Players[i].Name}\n{m.CurrentHP:F0}/{m.MaxHP:F0}";
                }

            if (enemyHPTexts != null)
                for (int i = 0; i < enemyHPTexts.Length && i < _ctx.Enemies.Count; i++)
                {
                    if (enemyHPTexts[i] == null) continue;
                    var m = _ctx.Enemies[i].Model;
                    string hp = _ctx.Enemies[i].IsAlive ? $"{m.CurrentHP:F0}/{m.MaxHP:F0}" : "DEAD";
                    enemyHPTexts[i].text = $"{_ctx.Enemies[i].Name}\n{hp}";
                }
        }

        private void RebuildActionSlots()
        {
            if (actionSlotContainer == null || actionSlotPrefab == null) return;

            var hlg = actionSlotContainer.GetComponent<HorizontalLayoutGroup>()
                   ?? actionSlotContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing        = 8f;
            hlg.childForceExpandWidth = false;

            int alive = 0;
            for (int i = 0; i < _ctx.Players.Count; i++)
                if (_ctx.Players[i].IsAlive) alive++;

            if (alive == _actionSlotGOs.Count) return;

            foreach (var go in _actionSlotGOs)
                if (go != null) Destroy(go);
            _actionSlotGOs.Clear();
            _actionSlotLabels.Clear();

            for (int i = 0; i < alive; i++)
            {
                var go  = Instantiate(actionSlotPrefab, actionSlotContainer);
                var txt = go.GetComponentInChildren<Text>();
                _actionSlotGOs.Add(go);
                _actionSlotLabels.Add(txt);
            }
        }

        private void ClearActionSlots()
        {
            foreach (var t in _actionSlotLabels)
                if (t != null) t.text = "—";
        }

        private int GetAlivePlayerSlotIndex(ICombatActor actor)
        {
            if (actor == null || _ctx == null) return -1;
            int slot = 0;
            for (int i = 0; i < _ctx.Players.Count; i++)
            {
                if (!_ctx.Players[i].IsAlive) continue;
                if (_ctx.Players[i] == actor) return slot;
                slot++;
            }
            return -1;
        }

        private void Log(string line)
        {
            _log.AppendLine(line);
            // Keep last 20 lines
            var lines = _log.ToString().Split('\n');
            if (lines.Length > 22)
            {
                _log.Clear();
                for (int i = lines.Length - 21; i < lines.Length; i++)
                    _log.AppendLine(lines[i]);
            }
            if (logText != null) logText.text = _log.ToString();
        }

        private ICombatActor GetFirstAliveEnemy()
        {
            if (_ctx == null) return null;
            for (int i = 0; i < _ctx.Enemies.Count; i++)
                if (_ctx.Enemies[i].IsAlive) return _ctx.Enemies[i];
            return null;
        }
    }

    // Extension to detect ultimate result without changing CombatActionResult struct
    internal static class CombatActionResultExt
    {
        internal static bool IsUltimate(this CombatActionResult r) => r.Skill == null && r.Rank == 3;
    }
}
