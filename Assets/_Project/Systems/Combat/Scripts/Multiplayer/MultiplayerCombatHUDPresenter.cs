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

        private ServerCombatOrchestrator _orch;

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => MultiplayerActionSlotsSync.Instance != null);
            _sync = MultiplayerActionSlotsSync.Instance;
            _sync.OnSlotUpdated += OnNetworkSlotUpdated;

            // Wait until server has initialized slot count, then rebuild to correct total
            yield return new WaitUntil(() => _sync.SlotCount > 0);
            RebuildActionSlots();

            // Roll each card out of its slot as the server consumes it during resolution.
            yield return new WaitUntil(() => ServerCombatOrchestrator.Instance != null);
            _orch = ServerCombatOrchestrator.Instance;
            _orch.OnExecuteCard += OnCardExecuted;
        }

        private void OnDestroy()
        {
            if (_sync != null)
                _sync.OnSlotUpdated -= OnNetworkSlotUpdated;
            if (_orch != null)
                _orch.OnExecuteCard -= OnCardExecuted;
        }

        // ── Per-card consumption animation (Phase 2) ─────────────────────────────

        // Server resolves slots one at a time → fade + roll that card off to the left,
        // matching the singleplayer used-card animation instead of a single bulk shrink.
        private void OnCardExecuted(int slotIndex, ulong ownerClientId, NetworkBattleCard card)
        {
            if (slotIndex < 0 || slotIndex >= _activeSlots.Count) return;
            if (_activeSlots[slotIndex] != null) StartCoroutine(RollOutSlot(slotIndex));
        }

        // MP suppresses the base bulk shrink: each slot deactivates as its card is consumed,
        // and the remaining slots roll left to fill the gap (mirrors SP FadeOutActionSlot).
        public override void SetActionSlotsActive(bool active)
        {
            if (active) StartCoroutine(ReactivateSlotsNextFrame());
            // inactive: no-op — RollOutSlot handles per-card deactivation during resolution.
        }

        private IEnumerator RollOutSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _activeSlots.Count) yield break;
            var slot = _activeSlots[slotIndex];
            if (slot == null) yield break;

            _pendingSlotViews.Remove(slotIndex);

            // Fade the whole slot out.
            var cg = slot.GetComponent<CanvasGroup>() ?? slot.gameObject.AddComponent<CanvasGroup>();
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.2f)
            {
                if (slot == null) yield break;
                cg.alpha = 1f - Mathf.Clamp01(t);
                yield return null;
            }
            cg.alpha = 0f;

            // Snapshot world positions of slots to the right, then deactivate this one.
            var remaining = new List<(Transform t, Vector3 from)>();
            for (int i = slotIndex + 1; i < _activeSlots.Count; i++)
            {
                var s = _activeSlots[i];
                if (s != null && s.gameObject.activeSelf) remaining.Add((s, s.position));
            }
            slot.gameObject.SetActive(false);
            if (remaining.Count == 0) yield break;

            // Recompute layout targets, then animate remaining slots left with HLG disabled.
            var crt = actionSlotContainer as RectTransform;
            if (crt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(crt);

            var to = new Vector3[remaining.Count];
            for (int i = 0; i < remaining.Count; i++) to[i] = remaining[i].t.position;

            var hlg = actionSlotContainer != null ? actionSlotContainer.GetComponent<HorizontalLayoutGroup>() : null;
            if (hlg != null) hlg.enabled = false;
            for (int i = 0; i < remaining.Count; i++) remaining[i].t.position = remaining[i].from;

            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.2f)
            {
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                for (int i = 0; i < remaining.Count; i++)
                    if (remaining[i].t != null) remaining[i].t.position = Vector3.Lerp(remaining[i].from, to[i], s);
                yield return null;
            }
            for (int i = 0; i < remaining.Count; i++)
                if (remaining[i].t != null) remaining[i].t.position = to[i];
            if (hlg != null) hlg.enabled = true;
        }

        // New turn: bring all slots back (reactivate, restore alpha, clear leftover cards, relayout).
        private IEnumerator ReactivateSlotsNextFrame()
        {
            yield return null; // let the server's slot reset replicate first

            // Slot count may have shrunk (a player died, 6 → 3) — rebuild to match.
            RebuildActionSlots();

            var hlg = actionSlotContainer != null ? actionSlotContainer.GetComponent<HorizontalLayoutGroup>() : null;
            if (hlg != null) hlg.enabled = true;

            for (int i = 0; i < _activeSlots.Count; i++)
            {
                var slot = _activeSlots[i];
                if (slot == null) continue;
                slot.gameObject.SetActive(true);
                var cg = slot.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;

                for (int c = slot.childCount - 1; c >= 0; c--)
                {
                    var child = slot.GetChild(c);
                    if (child.GetComponent<CardView>() != null) Destroy(child.gameObject);
                }
                if (i < _slotImages.Length && _slotImages[i] != null)
                {
                    _slotImages[i].gameObject.SetActive(true);
                    _slotImages[i].color = new Color(0.06f, 0.06f, 0.10f, 0.92f);
                }
            }
            var crt = actionSlotContainer as RectTransform;
            if (crt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(crt);
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
            // Guard against shrink events (board resized on player death) where the index
            // can momentarily exceed the live slot list or the local view.
            if (_sync == null || idx < 0 || idx >= _sync.SlotCount || idx >= _activeSlots.Count) return;

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
