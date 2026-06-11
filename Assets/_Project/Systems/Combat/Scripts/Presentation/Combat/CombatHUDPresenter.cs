using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
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

        protected TurnManager    _tm;
        private CombatContext    _ctx;
        private CanvasGroup      _rootGroup;

        private readonly List<CardView>                 _cardViews        = new();
        private readonly Dictionary<(string, int), int> _pendingMergeFlash = new();
        private readonly List<(int index, ICombatActor target)> _pending  = new();
        protected readonly List<Transform>              _activeSlots      = new();

        [Header("Layout Settings")]
        [SerializeField] private float cardSpacing = 130f;
        [SerializeField] private float actionSlotSpacing = -10f;
        [SerializeField] private float cardScale = 0.8f;
        [SerializeField] private float slideSpeed  = 12f;

        private ICombatActor  _selectedTarget;
        protected Image[]     _slotImages = Array.Empty<Image>();
        private readonly List<Image> _orbImages = new();
        private int           _movesThisTurn = 0;
        private StringBuilder _log           = new();

        private static readonly Color _orbFull = new Color(0.72f, 0.32f, 1f,  1f);
        private static readonly Color _orbDim  = new Color(0.22f, 0.12f, 0.35f, 1f);

        private Vector3   _slotOrigLocalPos;
        private Vector3   _slotOrigScale;
        private Coroutine _slotAnim;
        private readonly List<(CardView cv, int finalRank, Color elemColor)> _newlyDrawnCards = new();
        private readonly HashSet<Transform> _drawingCards = new();

        // ── CombatPresenterBase ─────────────────────────────────────────────

        public override void Initialize(TurnManager tm, CombatContext ctx)
        {
            _tm        = tm;
            _ctx       = ctx;
            _rootGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            EnsureContainerLayout();
            BuildOrbRow();

            _newlyDrawnCards.Clear();
            _drawingCards.Clear();

            if (cardPrefab != null)
            {
                cardScale = cardPrefab.transform.localScale.x;
                
                // 20% overlap: spacing is 80% of the visual width (base width * scale)
                var cardRt = cardPrefab.GetComponent<RectTransform>();
                float baseWidth = cardRt != null ? cardRt.rect.width : 145f;
                cardSpacing = baseWidth * cardScale * 0.8f;
            }

            if (actionSlotContainer != null)
            {
                _slotOrigLocalPos = actionSlotContainer.localPosition;
                _slotOrigScale    = actionSlotContainer.localScale;
            }

            if (combatResultText != null)
                combatResultText.gameObject.SetActive(false);
        }

        public override void OnGaugeChanged(ICombatActor actor, int orbs)
        {
            for (int i = 0; i < _orbImages.Count; i++)
                if (_orbImages[i] != null)
                    _orbImages[i].color = i < orbs ? _orbFull : _orbDim;
        }

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
        {
            Log($"MERGE! {skillName} → Rank {newRank}");
            var key = (skillName, newRank);
            _pendingMergeFlash.TryGetValue(key, out int count);
            _pendingMergeFlash[key] = count + 1;
        }

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

        private void ExecuteQueue()
        {
            var indices = new List<int>(_pending.Count);
            var targets = new List<ICombatActor>(_pending.Count);
            foreach (var p in _pending) { indices.Add(p.index); targets.Add(p.target); }
            _pending.Clear();

            for (int i = 0; i < indices.Count; i++)
            {
                int adjIdx     = indices[i];
                int sizeBefore = _tm.Hand.Slots.Count;
                _tm.SubmitSkill(adjIdx, targets[i]);
                int netRemoved = sizeBefore - _tm.Hand.Slots.Count;
                for (int j = i + 1; j < indices.Count; j++)
                    if (indices[j] > adjIdx) indices[j] -= netRemoved;
            }
        }

        // ── Drag → reorder ───────────────────────────────────────────────────

        private void ReorderCard(CardView cv)
        {
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

        private void ShowMoveInSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _activeSlots.Count) return;

            if (slotIndex < _slotImages.Length && _slotImages[slotIndex] != null)
                _slotImages[slotIndex].color = new Color(0.15f, 0.30f, 0.50f, 0.85f);

            var slot = _activeSlots[slotIndex];
            var lbl  = slot.Find("MoveLabel");
            if (lbl == null)
            {
                var lblGO     = new GameObject("MoveLabel");
                lblGO.transform.SetParent(slot, false);
                var labelRt   = lblGO.AddComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;
                var txt       = lblGO.AddComponent<Text>();
                txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize  = 20;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color     = new Color(0.55f, 0.80f, 1f);
                txt.text      = "MOVE";
            }
            else
            {
                lbl.GetComponent<Text>().text = "MOVE";
            }
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

            var containerRt = cardHandContainer as RectTransform;
            float containerHeight = containerRt != null ? containerRt.rect.height : 180f;
            float containerPivotY = containerRt != null ? containerRt.pivot.y : 0.5f;

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

                if (_drawingCards.Contains(cv.transform)) continue; // Let sequential draw coroutine handle it!

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

            var containerRt = cardHandContainer as RectTransform;
            float rawH = containerRt != null ? containerRt.rect.height : 170f;
            float containerHeight = Mathf.Clamp(rawH, 50f, 200f);

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
                    {
                        matchedView.PlayRankUpAnimation(elemColor, animConfig);
                    }

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
                    StartCoroutine(AnimateMergeSlide(oldCv, mergeTarget, elemColor));
                }
                else
                {
                    StartCoroutine(AnimatePlaySlide(oldCv));
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
                StartCoroutine(AnimateDrawSequence(new List<(CardView cv, int finalRank, Color elemColor)>(_newlyDrawnCards)));
                _newlyDrawnCards.Clear();
            }
        }

        private IEnumerator AnimateMergeSlide(CardView oldCv, CardView targetCv, Color elemColor)
        {
            if (oldCv == null) yield break;

            var cg = oldCv.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;

            float elapsed = 0f;
            float duration = animConfig != null ? animConfig.slideDuration : 0.22f;

            Vector3 startPos = oldCv.transform.localPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float norm = Mathf.Clamp01(elapsed / duration);

                if (oldCv == null) yield break;
                if (targetCv == null)
                {
                    Destroy(oldCv.gameObject);
                    yield break;
                }

                oldCv.transform.localPosition = Vector3.Lerp(startPos, targetCv.transform.localPosition, norm);
                if (cg != null) cg.alpha = 1f - norm;

                yield return null;
            }

            if (oldCv != null) Destroy(oldCv.gameObject);

            if (targetCv != null)
            {
                int finalRank = 1;
                if (_tm != null && _tm.Hand != null && targetCv.HandIndex >= 0 && targetCv.HandIndex < _tm.Hand.Slots.Count)
                {
                    finalRank = _tm.Hand.Slots[targetCv.HandIndex].Rank;
                }
                else
                {
                    finalRank = targetCv.Card.Rank + 1;
                }
                
                targetCv.Setup(targetCv.Card.WithRank(finalRank), elemColor);
                targetCv.PlayRankUpAnimation(elemColor, animConfig);
            }
        }

        private IEnumerator AnimatePlaySlide(CardView oldCv)
        {
            if (oldCv == null) yield break;

            var cg = oldCv.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;

            float elapsed = 0f;
            float duration = 0.25f;

            Vector3 startPos = oldCv.transform.localPosition;
            Vector3 targetPos = startPos + new Vector3(0f, 300f, 0f); // Slide up towards action slots

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float norm = Mathf.Clamp01(elapsed / duration);

                if (oldCv == null) yield break;

                oldCv.transform.localPosition = Vector3.Lerp(startPos, targetPos, norm);
                if (cg != null) cg.alpha = 1f - norm;

                yield return null;
            }

            if (oldCv != null) Destroy(oldCv.gameObject);
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

        private IEnumerator AnimateDrawSequence(List<(CardView cv, int finalRank, Color elemColor)> cards)
        {
            foreach (var item in cards)
            {
                if (item.cv == null) continue;
                
                var transform = item.cv.transform;
                _drawingCards.Add(transform);
                
                yield return StartCoroutine(AnimateSingleCardDraw(item.cv, item.finalRank, item.elemColor));
                
                _drawingCards.Remove(transform);
            }
        }

        private IEnumerator AnimateSingleCardDraw(CardView cv, int finalRank, Color elemColor)
        {
            if (cv == null) yield break;

            float elapsed = 0f;
            float duration = 0.35f; // duration of the slide-in per card

            var cardRt = cv.GetComponent<RectTransform>();
            float cardWidth = cardRt != null ? cardRt.rect.width : 145f;

            // 1. Draw the main card as Rank 1
            int visualIdx = _cardViews.IndexOf(cv);
            if (visualIdx < 0) visualIdx = _cardViews.Count; // fallback
            
            Vector3 targetPos = GetCardTargetLocalPos(visualIdx, _cardViews.Count, cardWidth);
            Vector3 startPos = new Vector3(targetPos.x - 400f, targetPos.y, 0f); // start 400 units to the left

            cv.transform.localPosition = startPos;
            cv.transform.localScale = Vector3.one * cardScale * 0.5f; // start smaller for a nice pop-in effect
            var cg = cv.GetComponent<CanvasGroup>() ?? cv.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float norm = Mathf.Clamp01(elapsed / duration);
                
                // Ease-out cubic curve
                float t = 1f - Mathf.Pow(1f - norm, 3f);

                if (cv == null) yield break;

                // Re-calculate targetPos dynamically in case other cards shifting
                visualIdx = _cardViews.IndexOf(cv);
                if (visualIdx >= 0)
                {
                    targetPos = GetCardTargetLocalPos(visualIdx, _cardViews.Count, cardWidth);
                    startPos = new Vector3(targetPos.x - 400f, targetPos.y, 0f);
                }

                cv.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                cv.transform.localScale = Vector3.Lerp(Vector3.one * cardScale * 0.5f, Vector3.one * cardScale, t);
                cg.alpha = norm;

                yield return null;
            }

            if (cv != null)
            {
                visualIdx = _cardViews.IndexOf(cv);
                if (visualIdx >= 0)
                {
                    cv.transform.localPosition = GetCardTargetLocalPos(visualIdx, _cardViews.Count, cardWidth);
                }
                cv.transform.localScale = Vector3.one * cardScale;
                cg.alpha = 1f;
            }

            // 2. If the final rank is greater than 1, draw temporary cards and merge them sequentially!
            for (int r = 2; r <= finalRank; r++)
            {
                if (cv == null) yield break;

                // Instantiate a temporary card representing the merging card
                var tempCv = Instantiate(cardPrefab, cardHandContainer);
                tempCv.Setup(cv.Card.WithRank(1), elemColor); // Starts as Rank 1!
                
                // Hide initially until slide starts
                var tempCg = tempCv.GetComponent<CanvasGroup>() ?? tempCv.gameObject.AddComponent<CanvasGroup>();
                tempCg.alpha = 0f;
                tempCv.transform.localScale = Vector3.zero;

                float tempElapsed = 0f;
                float tempDuration = 0.35f;

                Vector3 tempTarget = cv.transform.localPosition;
                Vector3 tempStart = new Vector3(tempTarget.x - 400f, tempTarget.y, 0f);

                tempCv.transform.localPosition = tempStart;
                tempCv.transform.localScale = Vector3.one * cardScale * 0.5f;

                while (tempElapsed < tempDuration)
                {
                    tempElapsed += Time.deltaTime;
                    float norm = Mathf.Clamp01(tempElapsed / tempDuration);
                    float t = 1f - Mathf.Pow(1f - norm, 3f);

                    if (tempCv == null) yield break;
                    if (cv == null)
                    {
                        Destroy(tempCv.gameObject);
                        yield break;
                    }

                    tempTarget = cv.transform.localPosition;
                    tempStart = new Vector3(tempTarget.x - 400f, tempTarget.y, 0f);

                    tempCv.transform.localPosition = Vector3.Lerp(tempStart, tempTarget, t);
                    tempCv.transform.localScale = Vector3.Lerp(Vector3.one * cardScale * 0.5f, Vector3.one * cardScale, t);
                    tempCg.alpha = norm;

                    yield return null;
                }

                // Temporary card has landed on the main card!
                if (tempCv != null) Destroy(tempCv.gameObject);

                if (cv != null)
                {
                    // Upgrade cv to the rank reached so far and play rank-up blink!
                    cv.Setup(cv.Card.WithRank(r), elemColor);
                    cv.PlayRankUpAnimation(elemColor, animConfig);
                    
                    // Small delay to let the rank-up animation breathe before the next draw
                    yield return new WaitForSeconds(0.12f);
                }
            }
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
                // Inner stays visible — dark color = empty slot, blue = MOVE state.
                if (_slotImages[i] != null)
                    _slotImages[i].color = new Color(0.06f, 0.06f, 0.10f, 0.92f);
            }
        }

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
                var go       = new GameObject("Orb_" + i);
                go.transform.SetParent(gaugeContainer, false);
                var rt       = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(18f, 18f);
                var img      = go.AddComponent<Image>();
                img.color    = _orbDim;
                _orbImages.Add(img);
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

                // Remove RectMask2D, Mask, and Image components to allow cards to overlap/scale-bounce beyond bounds without clipping.
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

                // FadeOutActionSlot may have deactivated this slot and zeroed its alpha.
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
                    _slotImages[i].color = new Color(0.06f, 0.06f, 0.10f, 0.92f);
                }
            }
        }

        public override void SetActionSlotsActive(bool active)
        {
            if (actionSlotContainer == null) return;
            if (_slotAnim != null) StopCoroutine(_slotAnim);
            _slotAnim = StartCoroutine(active ? AnimateSlotExpand() : AnimateSlotShrink());

            // Fade move slots immediately when shrinking — they have no skill animation to trigger their fade.
            if (!active)
                for (int i = 0; i < _movesThisTurn && i < _activeSlots.Count; i++)
                    StartCoroutine(FadeOutActionSlot(i));
        }

        public override void NotifyActionAnimationComplete(int actionIndex)
        {
            // Skill animations are indexed 0, 1, 2… but move slots occupy the first
            // _movesThisTurn indices, so offset to reach the correct skill slot.
            int slotIndex = actionIndex + _movesThisTurn;
            if (slotIndex < 0 || slotIndex >= _activeSlots.Count) return;
            if (_activeSlots[slotIndex] == null) return;
            StartCoroutine(FadeOutActionSlot(slotIndex));
        }

        private IEnumerator FadeOutActionSlot(int actionIndex)
        {
            var slot = _activeSlots[actionIndex];

            // Fade
            var cg = slot.GetComponent<CanvasGroup>();
            if (cg == null) { slot.gameObject.SetActive(false); yield break; }
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.2f)
            {
                if (slot == null) yield break;
                cg.alpha = 1f - Mathf.Clamp01(t);
                yield return null;
            }
            if (slot == null) yield break;
            cg.alpha = 0f;

            // Snapshot world positions of slots to the right BEFORE deactivating
            var remaining = new List<(Transform t, Vector3 from)>();
            for (int i = actionIndex + 1; i < _activeSlots.Count; i++)
            {
                var s = _activeSlots[i];
                if (s != null && s.gameObject.activeSelf)
                    remaining.Add((s, s.position));
            }

            slot.gameObject.SetActive(false);

            if (remaining.Count == 0) yield break;

            // Force HLG to compute new positions, then snapshot targets
            var crt = actionSlotContainer as RectTransform;
            if (crt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(crt);

            var to = new Vector3[remaining.Count];
            for (int i = 0; i < remaining.Count; i++)
                to[i] = remaining[i].t.position;

            // Slide: disable HLG, restore old pos, animate, re-enable
            var hlg = actionSlotContainer != null
                ? actionSlotContainer.GetComponent<HorizontalLayoutGroup>()
                : null;

            if (hlg != null) hlg.enabled = false;
            for (int i = 0; i < remaining.Count; i++)
                remaining[i].t.position = remaining[i].from;

            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.2f)
            {
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                for (int i = 0; i < remaining.Count; i++)
                {
                    if (remaining[i].t == null) continue;
                    remaining[i].t.position = Vector3.Lerp(remaining[i].from, to[i], s);
                }
                yield return null;
            }
            for (int i = 0; i < remaining.Count; i++)
            {
                if (remaining[i].t != null) remaining[i].t.position = to[i];
            }

            if (hlg != null) hlg.enabled = true;
        }

        private IEnumerator AnimateSlotShrink()
        {
            var rt       = actionSlotContainer as RectTransform;
            var parentRt = rt != null ? rt.parent as RectTransform : null;

            Vector3 targetPos;
            if (rt != null && parentRt != null)
            {
                float halfW  = parentRt.rect.width  * 0.5f;
                float halfH  = parentRt.rect.height * 0.5f;
                float scaledW = rt.rect.width  * slotMiniScale;
                float scaledH = rt.rect.height * slotMiniScale;
                targetPos = new Vector3(
                    -halfW + slotMiniMargin.x + scaledW * rt.pivot.x,
                    -halfH + slotMiniMargin.y + scaledH * rt.pivot.y,
                    0f);
            }
            else
            {
                targetPos = actionSlotContainer.localPosition;
            }

            yield return StartCoroutine(AnimateSlotTo(targetPos, _slotOrigScale * slotMiniScale));
        }

        private IEnumerator AnimateSlotExpand() =>
            AnimateSlotTo(_slotOrigLocalPos, _slotOrigScale);

        private IEnumerator AnimateSlotTo(Vector3 targetPos, Vector3 targetScale)
        {
            Vector3 startPos   = actionSlotContainer.localPosition;
            Vector3 startScale = actionSlotContainer.localScale;
            float   elapsed    = 0f;

            while (elapsed < slotAnimDuration)
            {
                elapsed += Time.deltaTime;
                float st = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / slotAnimDuration));
                actionSlotContainer.localPosition = Vector3.Lerp(startPos,   targetPos,   st);
                actionSlotContainer.localScale    = Vector3.Lerp(startScale,  targetScale, st);
                yield return null;
            }

            actionSlotContainer.localPosition = targetPos;
            actionSlotContainer.localScale    = targetScale;
        }

        // ── Log ──────────────────────────────────────────────────────────────

        private void Log(string line)
        {
            _log.AppendLine(line);
            var lines = _log.ToString().Split('\n');
            if (lines.Length > 22)
            {
                _log.Clear();
                for (int i = lines.Length - 21; i < lines.Length; i++)
                    _log.AppendLine(lines[i]);
            }
            if (logText != null) logText.text = _log.ToString();
        }

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
