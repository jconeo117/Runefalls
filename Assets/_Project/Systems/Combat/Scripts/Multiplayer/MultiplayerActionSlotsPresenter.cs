using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Data;
using Runefall.Presentation.Combat;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// Manages the shared 6-slot action board UI.
    /// Waits for MultiplayerActionSlotsSync.Instance, creates slot GameObjects
    /// dynamically, and reflects server state on all clients.
    /// Intercepts card play requests from CombatHUDPresenter and routes them
    /// through PlaceCardServerRpc.
    /// </summary>
    public class MultiplayerActionSlotsPresenter : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform    slotContainer;
        [SerializeField] private GameObject   actionSlotPrefab;

        [Header("Local context (set by MultiplayerLocalCombatSetup)")]
        public TurnManager    LocalTurnManager;
        public MultiplayerCombatRegistry Registry;

        private MultiplayerActionSlotsSync    _sync;
        private readonly List<SlotView>       _slotViews = new();
        private ulong                         _localClientId;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => MultiplayerActionSlotsSync.Instance != null);

            _sync          = MultiplayerActionSlotsSync.Instance;
            _localClientId = NetworkManager.Singleton.LocalClientId;

            _sync.OnSlotUpdated   += RefreshSlot;
            _sync.OnActionsChanged += RefreshActionsDisplay;

            yield return new WaitUntil(() => _sync.SlotCount > 0);

            BuildSlotViews();
            Debug.Log($"[ActionSlotsPresenter] {_slotViews.Count} slots construidos.");
        }

        private void OnDestroy()
        {
            if (_sync == null) return;
            _sync.OnSlotUpdated   -= RefreshSlot;
            _sync.OnActionsChanged -= RefreshActionsDisplay;
        }

        // ── Public: called by MultiplayerLocalCombatSetup when player plays card ──

        /// <summary>
        /// Route a card play through the network slot board.
        /// Returns the chosen slot index, or -1 if placement failed (caller should not consume the card).
        /// </summary>
        public int RequestPlaceCard(BattleCard card, int preferredSlot = -1)
        {
            var sync = MultiplayerActionSlotsSync.Instance;
            if (sync == null)
            {
                Debug.LogWarning("[ActionSlotsPresenter] MultiplayerActionSlotsSync not ready.");
                return -1;
            }
            if (sync.SlotCount == 0)
            {
                Debug.LogWarning("[ActionSlotsPresenter] Slots not initialized yet (SlotCount=0).");
                return -1;
            }
            int remaining = sync.GetActionsRemaining(_localClientId);
            if (remaining <= 0)
            {
                Debug.LogWarning($"[ActionSlotsPresenter] Sin acciones restantes (clientId={_localClientId}).");
                return -1;
            }

            int target = -1;
            if (preferredSlot >= 0 && preferredSlot < sync.SlotCount &&
                !sync.GetSlot(preferredSlot).IsOccupied)
                target = preferredSlot;
            else
            {
                for (int i = 0; i < sync.SlotCount; i++)
                    if (!sync.GetSlot(i).IsOccupied) { target = i; break; }
            }

            if (target < 0)
            {
                Debug.LogWarning($"[ActionSlotsPresenter] No hay slots libres.");
                return -1;
            }

            var netCard = new NetworkBattleCard
            {
                CardId       = card.Id,
                SkillName    = card.Skill    != null ? (Unity.Collections.FixedString32Bytes)card.Skill.skillName    : default,
                UltimateName = card.Ultimate != null ? (Unity.Collections.FixedString32Bytes)card.Ultimate.ultimateName : default,
                Rank         = card.Rank,
                IsUltimate   = card.IsUltimate,
                Element      = (int)(card.IsUltimate
                    ? (card.Ultimate?.element ?? ElementType.Neutral)
                    : (card.Skill?.element    ?? ElementType.Neutral))
            };

            sync.PlaceCardServerRpc(target, netCard);
            return target;
        }

        // ── Private ────────────────────────────────────────────────────────────

        private void BuildSlotViews()
        {
            // slotContainer is optional — if null, visuals are owned by MultiplayerCombatHUDPresenter
            if (slotContainer == null || actionSlotPrefab == null) return;

            foreach (Transform child in slotContainer)
                Destroy(child.gameObject);
            _slotViews.Clear();

            for (int i = 0; i < _sync.SlotCount; i++)
            {
                var go   = Instantiate(actionSlotPrefab, slotContainer);
                var view = go.GetComponent<SlotView>() ?? go.AddComponent<SlotView>();
                int idx  = i;
                view.Init(i, () => OnSlotClicked(idx));
                _slotViews.Add(view);
                RefreshSlot(i);
            }
        }

        private void RefreshSlot(int idx)
        {
            if (idx >= _slotViews.Count || _sync == null) return;
            var state = _sync.GetSlot(idx);
            _slotViews[idx].SetState(state, _localClientId);
        }

        private void RefreshActionsDisplay()
        {
            // Optionally update an "actions remaining" counter per player
        }

        private void OnSlotClicked(int slotIdx)
        {
            // Future: open a card selection popup for this specific slot
            Debug.Log($"[ActionSlotsPresenter] Slot {slotIdx} clickeado.");
        }

        // ── Inner type ─────────────────────────────────────────────────────────

        /// <summary>
        /// Small view component attached to each ActionSlotPrefab instance.
        /// Renders occupied/empty state and highlights local player's cards.
        /// </summary>
        private class SlotView : MonoBehaviour
        {
            private Text       _label;
            private Image      _bg;
            private System.Action _onClick;

            private static readonly Color ColEmpty    = new Color(0.15f, 0.15f, 0.20f, 0.85f);
            private static readonly Color ColOwn      = new Color(0.20f, 0.45f, 0.80f, 0.95f);
            private static readonly Color ColOther    = new Color(0.55f, 0.25f, 0.70f, 0.95f);

            public void Init(int index, System.Action onClick)
            {
                _onClick = onClick;
                _label   = GetComponentInChildren<Text>();
                _bg      = GetComponent<Image>() ?? gameObject.AddComponent<Image>();

                var btn = GetComponent<UnityEngine.UI.Button>() ??
                          gameObject.AddComponent<UnityEngine.UI.Button>();
                btn.onClick.AddListener(() => _onClick?.Invoke());

                if (_label != null) _label.text = $"Slot {index + 1}";
                _bg.color = ColEmpty;
            }

            public void SetState(NetworkSlotState state, ulong localCid)
            {
                if (state.IsOccupied)
                {
                    string cardName = state.Card.IsUltimate
                        ? state.Card.UltimateName.ToString()
                        : state.Card.SkillName.ToString();
                    if (_label != null) _label.text = cardName;
                    _bg.color = state.OwnerClientId == localCid ? ColOwn : ColOther;
                }
                else
                {
                    if (_label != null) _label.text = string.Empty;
                    _bg.color = ColEmpty;
                }
            }
        }
    }
}
