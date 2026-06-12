using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Coordinates the combat HUD. It owns the domain references and the action
    /// queue, and routes presentation concerns to focused collaborators:
    ///   • <see cref="CombatLogView"/>      — the rolling battle log
    ///   • <see cref="UltimateGaugeView"/>  — the 7-orb ultimate gauge
    ///   • <see cref="CardHandAnimator"/>   — card slide / merge / draw motion
    ///   • <see cref="ActionSlotsAnimator"/>— slot fade-out / shrink / expand motion
    ///   • <see cref="ActionSlotStyle"/>    — procedural slot look (idle / move)
    ///
    /// The card-hand layout, the action-slot lifecycle and the play/move queue stay
    /// here because they are the coordination this presenter exists to perform — and
    /// because <c>MultiplayerCombatHUDPresenter</c> extends this surface. New screens
    /// (e.g. the character-stats overlay) plug in as further collaborators alongside
    /// the views above rather than swelling this class.
    /// </summary>
    public class CombatHUDPresenter : CombatPresenterBase
    {
        [Header("Card Hand")]
        public Transform cardHandContainer;
        public CardView  cardPrefab;

        [Header("Action Slots")]
        public Transform  actionSlotContainer;
        public GameObject actionSlotPrefab;

        [Header("HUD")]
        public Text roundLabel;
        public Text logText;
        public Text combatResultText;

        [Header("Ultimate Gauge")]
        public Transform gaugeContainer;   // horizontal strip; 7 orb images created at runtime

        [Header("Animation")]
        public CardAnimationConfig animConfig;

        [Header("Slot Shrink")]
        [Tooltip("Scale factor when slots miniaturize during action resolution (0.7 = 30% smaller).")]
        public float   slotMiniScale    = 0.8f;
        [Tooltip("Pixels from bottom-left canvas edge when miniaturized.")]
        public Vector2 slotMiniMargin   = new Vector2(200f, 200f);
        [Tooltip("Duration of expand/shrink animation in seconds.")]
        public float   slotAnimDuration = 0.3f;

        [Header("Layout Settings")]
        [SerializeField] private float cardSpacing = 130f;
        [SerializeField] private float actionSlotSpacing = -10f;
        [SerializeField] private float cardScale = 0.8f;
        [SerializeField] private float slideSpeed  = 12f;

        protected TurnManager    _tm;
        private CombatContext    _ctx;
        private CanvasGroup      _rootGroup;

        private readonly List<CardView>                 _cardViews   = new();
        private readonly List<(int index, ICombatActor target)> _pending = new();
        protected readonly List<Transform>              _activeSlots = new();

        private ICombatActor  _selectedTarget;
        protected Image[]     _slotImages = Array.Empty<Image>();
        private int           _movesThisTurn = 0;
        private bool          _isExecutingQueue = false;   // true while plays commit one-by-one (gauge fills in real time)

        private Coroutine _slotAnim;
        private readonly List<(CardView cv, int finalRank, Color elemColor)> _newlyDrawnCards = new();

        // ── collaborators (constructed in Initialize) ───────────────────────
        private CombatLogView       _logView;
        private UltimateGaugeView   _gaugeView;
        private CardHandAnimator    _cardAnimator;
        private ActionSlotsAnimator _slotsAnimator;

        // ── CombatPresenterBase ─────────────────────────────────────────────

        public override void Initialize(TurnManager tm, CombatContext ctx)
        {
            _tm        = tm;
            _ctx       = ctx;
            _rootGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            EnsureContainerLayout();

            _gaugeView = new UltimateGaugeView(gaugeContainer);
            _logView   = new CombatLogView(logText);

            _newlyDrawnCards.Clear();

            if (cardPrefab != null)
            {
                cardScale = cardPrefab.transform.localScale.x;

                // 20% overlap: spacing is 80% of the visual width (base width * scale)
                var cardRt = cardPrefab.GetComponent<RectTransform>();
                float baseWidth = cardRt != null ? cardRt.rect.width : 145f;
                cardSpacing = baseWidth * cardScale * 0.8f;
            }

            _cardAnimator = new CardHandAnimator(
                this, cardHandContainer, cardPrefab, animConfig, cardScale,
                _cardViews, GetCardTargetLocalPos, ResolveMergeFinalRank);

            _slotsAnimator = new ActionSlotsAnimator(
                actionSlotContainer, _activeSlots, slotMiniScale, slotMiniMargin, slotAnimDuration);

            if (combatResultText != null)
                combatResultText.gameObject.SetActive(false);
        }

        public override void OnGaugeChanged(ICombatActor actor, int orbs)
            => _gaugeView?.SetOrbs(orbs);

        public override void HideAllUI()
        {
            // Stop any in-flight slot animation so it doesn't fight the alpha=0.
            if (_slotAnim != null) { StopCoroutine(_slotAnim); _slotAnim = null; }
            if (_rootGroup != null) { _rootGroup.alpha = 0f; _rootGroup.blocksRaycasts = false; }

            // Card hand and action slots live outside _rootGroup, so alpha=0 doesn't hide them —
            // deactivate their containers fully.
            if (cardHandContainer != null)    cardHandContainer.gameObject.SetActive(false);
            if (actionSlotContainer != null)  actionSlotContainer.gameObject.SetActive(false);
        }

        public override void ShowAllUI()
        {
            if (_rootGroup != null) { _rootGroup.alpha = 1f; _rootGroup.blocksRaycasts = true; }
            if (cardHandContainer != null)    cardHandContainer.gameObject.SetActive(true);
            if (actionSlotContainer != null)  actionSlotContainer.gameObject.SetActive(true);
        }

        public override void OnPlayerTurnStarted(int round)
        {
            _pending.Clear();
            _movesThisTurn  = 0;
            _isExecutingQueue = false;
            _selectedTarget = null;
            RebuildActionSlots();
            ClearSlots();
            if (roundLabel != null) roundLabel.text = $"Round {round}";
            RefreshCardHand(animate: true);
            Log($"── Round {round} ──");
        }

        public override void OnActionResolved(CombatActionResult result)
        {
            if (_selectedTarget != null && !_selectedTarget.IsAlive)
                _selectedTarget = null;

            if (result.DamageDealt > 0f)
            {
                string crit = result.IsCrit ? " [CRIT]" : "";
                Log($"{result.Caster?.Name} → {result.Target?.Name}: {result.DamageDealt:F0}{crit}");
            }
            if (result.HealApplied      > 0f) Log($"Heal +{result.HealApplied:F0}");
            if (result.LifeStealApplied > 0f) Log($"Lifesteal +{result.LifeStealApplied:F0}");

            RefreshCardHand();
        }

        public override void OnCardMerged(string skillName, int newRank)
            => Log($"MERGE! {skillName} → Rank {newRank}");

        public override void OnCombatEnded(bool playerWon)
        {
            ClearSlots();
            RefreshCardHand();
            string msg = playerWon ? "VICTORY" : "DEFEAT";
            Log($"── {msg} ──");
            if (combatResultText != null)
            {
                combatResultText.text = msg;
                combatResultText.gameObject.SetActive(true);
            }
        }

        // ── Enemy selection ──────────────────────────────────────────────────

        public override void SelectEnemy(int index)
        {
            if (index < 0) { _selectedTarget = null; return; }
            if (_ctx == null || index >= _ctx.Enemies.Count) return;
            _selectedTarget = _ctx.Enemies[index];
        }

        // ── Card click → queue ───────────────────────────────────────────────

        protected virtual void QueueCard(CardView cv)
        {
            if (_isExecutingQueue) return;   // ignore clicks while the queue is committing
            int slotIndex = _pending.Count + _movesThisTurn;
            if (slotIndex >= _activeSlots.Count) return;

            // Stop any in-flight animation before reparenting to the action slot.
            cv.StopAllAnimations();

            int tmIndex = cv.HandIndex;

            _cardViews.Remove(cv);

            var rt = cv.GetComponent<RectTransform>();
            cv.transform.SetParent(_activeSlots[slotIndex], false);
            cv.targetScale = 1.0f;
            cv.transform.localScale = Vector3.one;
            if (rt != null)
            {
                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta        = new Vector2(130f, 170f); // match slot size
            }
            var btn = cv.GetComponent<Button>();
            if (btn != null) { btn.onClick.RemoveAllListeners(); btn.interactable = false; }
            cv.OnReorderRequested = null;

            if (slotIndex < _slotImages.Length && _slotImages[slotIndex] != null)
                _slotImages[slotIndex].gameObject.SetActive(false);

            if (cardHandContainer is RectTransform crt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(crt);

            _pending.Add((tmIndex, _selectedTarget));

            if (_tm.Hand.ActionsRemaining > 0 && _pending.Count >= _tm.Hand.ActionsRemaining)
                ExecuteQueue();
            else
                RefreshCardHand();
        }

        private const float _queueStepDelay = 0.15f;   // gap between committed plays so the gauge fills section-by-section

        private void ExecuteQueue()
        {
            if (_isExecutingQueue) return;
            StartCoroutine(ExecuteQueueRoutine());
        }

        private IEnumerator ExecuteQueueRoutine()
        {
            _isExecutingQueue = true;

            var indices = new List<int>(_pending.Count);
            var targets = new List<ICombatActor>(_pending.Count);
            foreach (var p in _pending) { indices.Add(p.index); targets.Add(p.target); }
            _pending.Clear();

            for (int i = 0; i < indices.Count; i++)
            {
                int adjIdx     = indices[i];
                int sizeBefore = _tm.Hand.Slots.Count;
                _tm.SubmitSkill(adjIdx, targets[i]);   // each commit fills one gauge section → bar updates in real time
                int netRemoved = sizeBefore - _tm.Hand.Slots.Count;
                for (int j = i + 1; j < indices.Count; j++)
                    if (indices[j] > adjIdx) indices[j] -= netRemoved;

                if (i < indices.Count - 1)
                    yield return new WaitForSeconds(_queueStepDelay);
            }

            _isExecutingQueue = false;
        }

        // ── Drag → reorder ───────────────────────────────────────────────────

        private void ReorderCard(CardView cv)
        {
            if (_isExecutingQueue) return;   // ignore moves while the queue is committing
            if (cardHandContainer == null) return;

            int fromDomain = cv.HandIndex;

            // Map the visual X position of drop to visual index
            int toVisual = GetVirtualVisualIndex(cv.transform.position.x);

            // Convert visual index back to domain index (indices are reversed)
            int toDomain = _cardViews.Count - 1 - toVisual;
            toDomain = Mathf.Clamp(toDomain, 0, _tm.Hand.Slots.Count - 1);

            int arBefore = _tm.Hand.ActionsRemaining;
            if (_tm.SubmitMove(fromDomain, toDomain))
            {
                for (int i = 0; i < _pending.Count; i++)
                {
                    int pi = _pending[i].index;
                    if (fromDomain < pi) pi--;
                    if (toDomain   <= pi) pi++;
                    _pending[i] = (pi, _pending[i].target);
                }

                if (_tm.Phase == CombatPhase.PlayerTurn
                    && _tm.Hand.ActionsRemaining == arBefore - 1)
                {
                    int slotIndex = _pending.Count + _movesThisTurn;
                    ShowMoveInSlot(slotIndex);
                    _movesThisTurn++;
                }

                RefreshCardHand();

                if (_pending.Count > 0
                    && _tm.Hand.ActionsRemaining > 0
                    && _pending.Count >= _tm.Hand.ActionsRemaining)
                    ExecuteQueue();
            }
            else
            {
                // Slide back if the move was invalid
                RefreshCardHand();
            }
        }

        /// <summary>Light a slot up as "a card was moved here" (visuals owned by <see cref="ActionSlotStyle"/>).</summary>
        private void ShowMoveInSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _activeSlots.Count) return;
            Image inner = slotIndex < _slotImages.Length ? _slotImages[slotIndex] : null;
            ActionSlotStyle.ApplyMove(_activeSlots[slotIndex], inner);
        }

        // ── Card hand ────────────────────────────────────────────────────────

        private void Update()
        {
            if (_tm == null || _tm.Hand == null) return;
            UpdateCardLayout();
        }

        private void UpdateCardLayout()
        {
            int totalCards = _cardViews.Count;
            if (totalCards == 0) return;

            CardView draggedCard = null;
            int draggedVisualIdx = -1;

            for (int i = 0; i < totalCards; i++)
            {
                if (_cardViews[i] != null && _cardViews[i].IsDragging)
                {
                    draggedCard = _cardViews[i];
                    draggedVisualIdx = i;
                    break;
                }
            }

            for (int i = 0; i < totalCards; i++)
            {
                CardView cv = _cardViews[i];
                if (cv == null) continue;

                // Let the draw coroutine own a card's position while it slides in.
                if (_cardAnimator != null && _cardAnimator.IsAnimatingDraw(cv.transform)) continue;

                var cardRt = cv.GetComponent<RectTransform>();

                if (cv == draggedCard) continue; // Drag handles position frame-by-frame

                int visualSlotIdx = i;
                if (draggedCard != null)
                {
                    int virtualVisualIdx = GetVirtualVisualIndex(draggedCard.transform.position.x);

                    if (i >= virtualVisualIdx && i < draggedVisualIdx)
                        visualSlotIdx = i + 1;
                    else if (i <= virtualVisualIdx && i > draggedVisualIdx)
                        visualSlotIdx = i - 1;
                }

                // Right-aligned layout extending left; container pivot dynamically handled
                float halfWidth = cardRt != null ? cardRt.rect.width * 0.5f : 65f;
                Vector3 targetLocalPos = GetCardTargetLocalPos(visualSlotIdx, totalCards, halfWidth * 2f);

                // Smoothly slide card towards its target slot
                cv.transform.localPosition = Vector3.Lerp(cv.transform.localPosition, targetLocalPos, Time.deltaTime * slideSpeed);
                cv.transform.localRotation = Quaternion.Lerp(cv.transform.localRotation, Quaternion.identity, Time.deltaTime * slideSpeed);
                cv.transform.localScale = Vector3.Lerp(cv.transform.localScale, Vector3.one * cardScale, Time.deltaTime * slideSpeed);
            }
        }

        private int GetVirtualVisualIndex(float dragWorldX)
        {
            float localDragX = cardHandContainer.InverseTransformPoint(new Vector3(dragWorldX, 0f, 0f)).x;

            int total = _cardViews.Count;
            float bestDist = float.MaxValue;
            int bestSlot = 0;

            float halfWidth = 65f;
            if (total > 0 && _cardViews[0] != null)
            {
                var cardRt = _cardViews[0].GetComponent<RectTransform>();
                if (cardRt != null) halfWidth = cardRt.rect.width * 0.5f;
            }
            float rightLimit = -(halfWidth + 20f);

            for (int s = 0; s < total; s++)
            {
                float slotX = (s - (total - 1)) * cardSpacing + rightLimit;
                float dist = Mathf.Abs(localDragX - slotX);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestSlot = s;
                }
            }
            return bestSlot;
        }

        protected void RefreshCardHand(bool animate = false)
        {
            if (cardHandContainer == null || cardPrefab == null || _tm?.Hand == null) return;

            var slots = _tm.Hand.Slots;
            bool canAct = _tm.Phase == CombatPhase.PlayerTurn && _tm.Hand.ActionsRemaining > 0;

            var oldViews = new List<CardView>(_cardViews);
            _cardViews.Clear();

            var newViews = new CardView[slots.Count];
            var display = _pending.Count > 0 ? BuildVirtualHand() : BuildDirectDisplay(slots);

            for (int i = 0; i < display.Count; i++)
            {
                var (domIdx, visRank) = display[i];
                var slot = slots[domIdx];
                CardView matchedView = null;

                for (int j = 0; j < oldViews.Count; j++)
                {
                    if (oldViews[j] != null && oldViews[j].Card.Id == slot.Id)
                    {
                        matchedView = oldViews[j];
                        oldViews.RemoveAt(j);
                        break;
                    }
                }

                var elem = slot.IsUltimate
                    ? (slot.Ultimate?.element ?? ElementType.Neutral)
                    : (slot.Skill?.element    ?? ElementType.Neutral);
                Color elemColor = ElementColor(elem);

                if (matchedView != null)
                {
                    // Card persists! Setup visual properties
                    int oldRank = matchedView.Card.Rank;
                    matchedView.targetScale = cardScale;
                    matchedView.Setup(slot.IsUltimate ? slot : slot.WithRank(visRank), elemColor);
                    matchedView.HandIndex = domIdx;

                    var btn = matchedView.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.interactable = canAct;
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => QueueCard(matchedView));
                    }

                    // Rank-Up blink & pop scale animation
                    if (visRank > oldRank && !slot.IsUltimate)
                        matchedView.PlayRankUpAnimation(elemColor, animConfig);

                    newViews[i] = matchedView;
                }
                else
                {
                    // Newly drawn card!
                    var cv = Instantiate(cardPrefab, cardHandContainer);
                    cv.targetScale = cardScale;

                    // Hide initially until sequential draw starts
                    var cg = cv.GetComponent<CanvasGroup>() ?? cv.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                    cv.transform.localScale = Vector3.zero;

                    cv.Setup(slot.IsUltimate ? slot : slot.WithRank(1), elemColor);
                    cv.HandIndex = domIdx;
                    cv.OnReorderRequested = ReorderCard;

                    var btn = cv.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.interactable = canAct;
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => QueueCard(cv));
                    }

                    _newlyDrawnCards.Add((cv, visRank, elemColor));
                    newViews[i] = cv;
                }
            }

            // 2. Animate and destroy consumed/used old views
            foreach (var oldCv in oldViews)
            {
                if (oldCv == null) continue;

                CardView mergeTarget = null;
                for (int i = 0; i < newViews.Length; i++)
                {
                    if (newViews[i] != null && !newViews[i].Card.IsUltimate && !oldCv.Card.IsUltimate
                        && newViews[i].Card.Skill == oldCv.Card.Skill)
                    {
                        mergeTarget = newViews[i];
                        break;
                    }
                }

                if (mergeTarget != null)
                {
                    var elem = mergeTarget.Card.IsUltimate
                        ? (mergeTarget.Card.Ultimate?.element ?? ElementType.Neutral)
                        : (mergeTarget.Card.Skill?.element    ?? ElementType.Neutral);
                    Color elemColor = ElementColor(elem);
                    _cardAnimator?.PlayMergeSlide(oldCv, mergeTarget, elemColor);
                }
                else
                {
                    _cardAnimator?.PlayUsedSlide(oldCv);
                }
            }

            // 3. Rebuild active _cardViews list in visually left-to-right order
            for (int i = display.Count - 1; i >= 0; i--)
            {
                if (newViews[i] != null)
                {
                    _cardViews.Add(newViews[i]);
                    newViews[i].transform.SetSiblingIndex(display.Count - 1 - i);
                }
            }

            if (_newlyDrawnCards.Count > 0)
            {
                _cardAnimator?.PlayDrawSequence(new List<(CardView cv, int finalRank, Color elemColor)>(_newlyDrawnCards));
                _newlyDrawnCards.Clear();
            }
        }

        /// <summary>The rank a merge target lands on — its hand slot's rank, or one above its current rank.</summary>
        private int ResolveMergeFinalRank(CardView targetCv)
        {
            if (_tm?.Hand != null && targetCv.HandIndex >= 0 && targetCv.HandIndex < _tm.Hand.Slots.Count)
                return _tm.Hand.Slots[targetCv.HandIndex].Rank;
            return targetCv.Card.Rank + 1;
        }

        private Vector3 GetCardTargetLocalPos(int visualSlotIdx, int totalCards, float cardWidth)
        {
            var containerRt = cardHandContainer as RectTransform;
            float containerHeight = containerRt != null ? containerRt.rect.height : 180f;
            float containerPivotY = containerRt != null ? containerRt.pivot.y : 0.5f;

            float halfWidth = cardWidth * 0.5f;
            float rightLimit = -(halfWidth + 20f);

            float targetX = (visualSlotIdx - (totalCards - 1)) * cardSpacing + rightLimit;
            float targetY = containerHeight * (0.5f - containerPivotY);

            return new Vector3(targetX, targetY, 0f);
        }

        private static List<(int domainIdx, int visRank)> BuildDirectDisplay(IReadOnlyList<BattleCard> slots)
        {
            var display = new List<(int, int)>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
                display.Add((i, slots[i].Rank));
            return display;
        }

        // ── Virtual hand (pending actions preview) ───────────────────────────

        private List<(int domainIdx, int visRank)> BuildVirtualHand()
        {
            var slots = _tm.Hand.Slots;
            var vhand = new List<(int, int)>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
                vhand.Add((i, slots[i].Rank));

            var indices = new List<int>(_pending.Count);
            foreach (var p in _pending) indices.Add(p.index);

            for (int i = 0; i < indices.Count; i++)
            {
                int adjIdx = indices[i];
                if (adjIdx < 0 || adjIdx >= vhand.Count) continue;

                int sizeBefore = vhand.Count;
                vhand.RemoveAt(adjIdx);
                ApplyVirtualMerges(vhand, slots);
                int netRemoved = sizeBefore - vhand.Count;

                for (int j = i + 1; j < indices.Count; j++)
                    if (indices[j] > adjIdx) indices[j] -= netRemoved;
            }

            return vhand;
        }

        private static void ApplyVirtualMerges(
            List<(int domainIdx, int visRank)> vhand,
            IReadOnlyList<BattleCard> slots)
        {
            bool merged;
            do
            {
                merged = false;
                for (int i = 0; i < vhand.Count - 1; i++)
                {
                    var (ia, ra) = vhand[i];
                    var (ib, rb) = vhand[i + 1];
                    if (slots[ia].IsUltimate || slots[ib].IsUltimate) continue;
                    if (slots[ia].Skill != slots[ib].Skill || ra != rb || ra >= 3) continue;
                    vhand[i] = (ia, ra + 1);
                    vhand.RemoveAt(i + 1);
                    merged = true;
                    break;
                }
            } while (merged);
        }

        // ── Action slots ─────────────────────────────────────────────────────

        protected virtual void RebuildActionSlots()
        {
            if (actionSlotContainer == null || actionSlotPrefab == null) return;

            int slotCount = _tm?.Hand != null ? _tm.Hand.ActionsPerTurn : 3;

            if (slotCount == _activeSlots.Count) return;

            foreach (var s in _activeSlots) if (s != null) Destroy(s.gameObject);
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
                    ActionSlotStyle.ApplyIdle(_slotImages[i]);   // rounded frame + emblem (no sprites needed)
            }
        }

        private void EnsureContainerLayout()
        {
            if (actionSlotContainer != null)
            {
                var hlg = actionSlotContainer.GetComponent<HorizontalLayoutGroup>()
                       ?? actionSlotContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment        = TextAnchor.MiddleCenter;
                hlg.spacing               = actionSlotSpacing;
                hlg.childForceExpandWidth = false;
            }

            if (cardHandContainer != null)
            {
                var hlg = cardHandContainer.GetComponent<HorizontalLayoutGroup>()
                       ?? cardHandContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.enabled = false;

                var csf = cardHandContainer.GetComponent<ContentSizeFitter>()
                       ?? cardHandContainer.gameObject.AddComponent<ContentSizeFitter>();
                csf.enabled = false;

                // Remove RectMask2D, Mask, and Image components to allow cards to overlap/scale-bounce
                // beyond bounds without clipping.
                var mask2D = cardHandContainer.GetComponent<RectMask2D>();
                if (mask2D != null) Destroy(mask2D);

                var mask = cardHandContainer.GetComponent<Mask>();
                if (mask != null) Destroy(mask);

                var image = cardHandContainer.GetComponent<Image>();
                if (image != null) Destroy(image);
            }
        }

        private void ClearSlots()
        {
            for (int i = 0; i < _activeSlots.Count; i++)
            {
                var slot = _activeSlots[i];
                if (slot == null) continue;

                // FadeOutSlot may have deactivated this slot and zeroed its alpha.
                slot.gameObject.SetActive(true);
                var cg = slot.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;

                for (int c = slot.childCount - 1; c >= 0; c--)
                {
                    var go = slot.GetChild(c).gameObject;
                    if (go.GetComponent<CardView>() != null || go.name == "MoveLabel")
                        Destroy(go);
                }
                if (i < _slotImages.Length && _slotImages[i] != null)
                {
                    _slotImages[i].gameObject.SetActive(true);
                    ActionSlotStyle.RestoreIdle(_slotImages[i]);
                }
            }
        }

        public override void SetActionSlotsActive(bool active)
        {
            if (actionSlotContainer == null || _slotsAnimator == null) return;
            if (_slotAnim != null) StopCoroutine(_slotAnim);
            _slotAnim = StartCoroutine(active ? _slotsAnimator.Expand() : _slotsAnimator.Shrink());

            // Fade move slots immediately when shrinking — they have no skill animation to trigger their fade.
            if (!active)
                for (int i = 0; i < _movesThisTurn && i < _activeSlots.Count; i++)
                    StartCoroutine(_slotsAnimator.FadeOutSlot(i));
        }

        public override void NotifyActionAnimationComplete(int actionIndex)
        {
            // Skill animations are indexed 0, 1, 2… but move slots occupy the first
            // _movesThisTurn indices, so offset to reach the correct skill slot.
            int slotIndex = actionIndex + _movesThisTurn;
            if (slotIndex < 0 || slotIndex >= _activeSlots.Count) return;
            if (_activeSlots[slotIndex] == null) return;
            if (_slotsAnimator != null) StartCoroutine(_slotsAnimator.FadeOutSlot(slotIndex));
        }

        // ── Log ──────────────────────────────────────────────────────────────

        private void Log(string line) => _logView?.Append(line);

        // ── Helpers ──────────────────────────────────────────────────────────

        protected static Color ElementColor(ElementType element) => element switch
        {
            ElementType.Fire   => new Color(0.78f, 0.25f, 0.10f),
            ElementType.Ice    => new Color(0.16f, 0.43f, 0.75f),
            ElementType.Shadow => new Color(0.35f, 0.17f, 0.48f),
            ElementType.Light  => new Color(0.90f, 0.85f, 0.30f),
            ElementType.Earth  => new Color(0.29f, 0.48f, 0.16f),
            _                  => new Color(0.35f, 0.35f, 0.35f),
        };
    }
}
