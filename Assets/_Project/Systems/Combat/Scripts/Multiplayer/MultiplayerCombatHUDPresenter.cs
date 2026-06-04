using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Core;
using Runefall.Presentation.Combat;

namespace Runefall.Multiplayer
{
    /// <summary>
    /// MP variant of CombatHUDPresenter.
    /// Card hand remains local (private per player).
    /// Action slots are driven by MultiplayerActionSlotsSync (server-authoritative, shared).
    /// Card clicks route to MultiplayerActionSlotsPresenter instead of local TurnManager queue.
    /// </summary>
    public class MultiplayerCombatHUDPresenter : CombatHUDPresenter
    {
        private MultiplayerActionSlotsPresenter           _slotsPresenter;
        private MultiplayerActionSlotsSync                _sync;
        // Clones placed optimistically in slots; keyed by slot index.
        // Prevents OnNetworkSlotUpdated from creating a duplicate for the local player's card.
        private readonly Dictionary<int, CardView>        _pendingSlotViews = new();

        public void InjectSlotsPresenter(MultiplayerActionSlotsPresenter sp)
        {
            _slotsPresenter = sp;
        }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => MultiplayerActionSlotsSync.Instance != null);
            _sync = MultiplayerActionSlotsSync.Instance;
            _sync.OnSlotUpdated += OnNetworkSlotUpdated;

            // Wait until server has initialized slot count, then rebuild to correct total
            yield return new WaitUntil(() => _sync.SlotCount > 0);
            RebuildActionSlots();
        }

        private void OnDestroy()
        {
            if (_sync != null)
                _sync.OnSlotUpdated -= OnNetworkSlotUpdated;
        }

        // ── Overrides ──────────────────────────────────────────────────────────

        /// <summary>
        /// Uses network slot count (3 per player) instead of local ActionsPerTurn.
        /// Falls back to local count if sync not yet ready.
        /// </summary>
        protected override void RebuildActionSlots()
        {
            if (actionSlotContainer == null || actionSlotPrefab == null) return;

            var syncInst = MultiplayerActionSlotsSync.Instance;
            int slotCount = syncInst != null && syncInst.SlotCount > 0
                ? syncInst.SlotCount
                : (_tm?.Hand != null ? _tm.Hand.ActionsPerTurn : 3);

            if (slotCount == _activeSlots.Count) return;

            foreach (var s in _activeSlots)
                if (s != null) Destroy(s.gameObject);
            _activeSlots.Clear();

            for (int i = actionSlotContainer.childCount - 1; i >= 0; i--)
                Destroy(actionSlotContainer.GetChild(i).gameObject);

            for (int i = 0; i < slotCount; i++)
            {
                var go = Instantiate(actionSlotPrefab, actionSlotContainer);
                if (go.GetComponent<CanvasGroup>() == null)
                    go.AddComponent<CanvasGroup>();
                _activeSlots.Add(go.transform);
            }

            _slotImages = new Image[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                var inner = _activeSlots[i].Find("Inner");
                _slotImages[i] = inner?.GetComponent<Image>();
                if (_slotImages[i] != null)
                    _slotImages[i].color = new Color(0.06f, 0.06f, 0.10f, 0.92f);
            }
        }

        /// <summary>
        /// Routes card to shared network board via RPC.
        /// Also submits locally so the card leaves the hand display.
        /// </summary>
        protected override void QueueCard(CardView cv)
        {
            if (_slotsPresenter == null)
            {
                Debug.LogWarning("[MultiplayerCombatHUDPresenter] SlotsPresenter not injected — falling back to local queue.");
                base.QueueCard(cv);
                return;
            }

            // Ask server to reserve a slot. Returns -1 on failure — abort, leave card in hand.
            int slotIdx = _slotsPresenter.RequestPlaceCard(cv.Card);
            if (slotIdx < 0) return;

            // Optimistic visual: clone the CardView into the slot immediately.
            // The original cv stays in the hand container for RefreshCardHand / AnimatePlaySlide.
            if (slotIdx < _activeSlots.Count && cardPrefab != null)
            {
                var clone = Instantiate(cardPrefab, _activeSlots[slotIdx]);
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localScale    = Vector3.one;
                var rt = clone.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta        = new Vector2(130f, 170f);
                }

                var elem = cv.Card.IsUltimate
                    ? (cv.Card.Ultimate?.element ?? ElementType.Neutral)
                    : (cv.Card.Skill?.element    ?? ElementType.Neutral);
                clone.Setup(cv.Card, ElementColor(elem));

                var btn = clone.GetComponent<Button>();
                if (btn != null) btn.interactable = false;

                if (slotIdx < _slotImages.Length && _slotImages[slotIdx] != null)
                    _slotImages[slotIdx].gameObject.SetActive(false);

                _pendingSlotViews[slotIdx] = clone;
            }

            // Submit locally — removes card from domain hand.
            // OnActionPending fires (not OnActionResolved — no animation driver in MP),
            // so refresh manually.
            if (_tm != null)
            {
                _tm.SubmitSkill(cv.HandIndex);
                RefreshCardHand();
            }
        }

        // ── Network slot visual sync ────────────────────────────────────────────

        private void OnNetworkSlotUpdated(int idx)
        {
            if (_sync == null || idx < 0 || idx >= _activeSlots.Count) return;

            var state         = _sync.GetSlot(idx);
            var slotTransform = _activeSlots[idx];
            ulong localId     = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;

            bool hasPending = _pendingSlotViews.TryGetValue(idx, out var pendingClone) && pendingClone != null;

            if (!state.IsOccupied)
            {
                // Slot cleared (turn reset) — destroy pending clone and all card children
                if (hasPending)
                {
                    Destroy(pendingClone.gameObject);
                    _pendingSlotViews.Remove(idx);
                }
                for (int c = slotTransform.childCount - 1; c >= 0; c--)
                {
                    var child = slotTransform.GetChild(c);
                    if (child.GetComponent<CardView>() != null)
                        Destroy(child.gameObject);
                }
                if (idx < _slotImages.Length && _slotImages[idx] != null)
                {
                    _slotImages[idx].gameObject.SetActive(true);
                    _slotImages[idx].color = new Color(0.06f, 0.06f, 0.10f, 0.92f);
                }
                return;
            }

            // Slot occupied ─────────────────────────────────────────────────────

            // Local player's card: the optimistic clone is already in the slot.
            if (hasPending && state.OwnerClientId == localId)
            {
                _pendingSlotViews.Remove(idx);
                // Apply white tint to confirm ownership
                var bg = pendingClone.GetComponent<Image>();
                if (bg != null) bg.color = Color.white;
                return;
            }

            // Remote player's card (or pending clone was lost): build from network data.
            // Destroy any stale clone first.
            if (hasPending) { Destroy(pendingClone.gameObject); _pendingSlotViews.Remove(idx); }

            for (int c = slotTransform.childCount - 1; c >= 0; c--)
            {
                var child = slotTransform.GetChild(c);
                if (child.GetComponent<CardView>() != null)
                    Destroy(child.gameObject);
            }

            if (cardPrefab == null) return;

            if (idx < _slotImages.Length && _slotImages[idx] != null)
                _slotImages[idx].gameObject.SetActive(false);

            var cv = Instantiate(cardPrefab, slotTransform);
            cv.transform.localPosition = Vector3.zero;
            cv.transform.localScale    = Vector3.one;
            var rt = cv.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta        = new Vector2(130f, 170f);
            }

            // Element is stored directly in NetworkBattleCard — no registry needed for color.
            var remoteElem  = state.Card.ElementType;
            var remoteColor = ElementColor(remoteElem);

            if (ServiceLocator.TryGet<MultiplayerCombatRegistry>(out var registry))
            {
                var battleCard = state.Card.Resolve(registry);
                if (battleCard.Skill == null && battleCard.Ultimate == null)
                    Debug.LogWarning($"[MP-HUD] Skill '{state.Card.SkillName}' no encontrado en registry — verifíca que todos los skills estén en MultiplayerCombatRegistry.skills");
                cv.Setup(battleCard, remoteColor);
            }
            else
            {
                // Registry not available — setup with network data directly
                cv.Setup(new BattleCard(state.Card.CardId, null, null, state.Card.Rank, state.Card.IsUltimate), remoteColor);
            }

            // Tint artBackground purple to identify remote player's card.
            // artBackground is a child Image — not the root Image.
            if (cv.artBackground != null)
                cv.artBackground.color = new Color(0.80f, 0.65f, 1f, 1f);

            var btn = cv.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }
    }
}
