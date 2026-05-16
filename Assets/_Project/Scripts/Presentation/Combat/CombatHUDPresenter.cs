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

        private TurnManager      _tm;
        private CombatContext    _ctx;
        private CardHandAnimator _animator;

        private readonly List<CardView>                                        _cardViews        = new();
        private readonly List<(SkillData skill, int rank, Vector3 worldPos)>   _prevCardInfo     = new();
        private readonly Dictionary<(string, int), int>                        _pendingMergeFlash = new();
        private readonly List<(int index, ICombatActor target)>                _pending          = new();
        private readonly List<Transform>                                        _activeSlots      = new();

        private ICombatActor  _selectedTarget;
        private Image[]       _slotImages    = Array.Empty<Image>();
        private readonly List<Image> _orbImages = new();
        private int           _movesThisTurn = 0;
        private StringBuilder _log           = new();

        private static readonly Color _orbFull = new Color(0.72f, 0.32f, 1f,  1f);
        private static readonly Color _orbDim  = new Color(0.22f, 0.12f, 0.35f, 1f);

        private Vector3   _slotOrigLocalPos;
        private Vector3   _slotOrigScale;
        private Coroutine _slotAnim;

        // ── CombatPresenterBase ─────────────────────────────────────────────

        public override void Initialize(TurnManager tm, CombatContext ctx)
        {
            _tm       = tm;
            _ctx      = ctx;
            _animator = new CardHandAnimator(animConfig);

            EnsureContainerLayout();
            BuildOrbRow();

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
            Debug.Log($"[Merge] {skillName} → R{newRank}");
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

        private void QueueCard(CardView cv)
        {
            int slotIndex = _pending.Count + _movesThisTurn;
            if (slotIndex >= _activeSlots.Count) return;

            // Stop any in-flight animation before reparenting to the action slot.
            _animator.CancelCard(cv);

            int tmIndex = cv.HandIndex;

            int cvIndex = _cardViews.IndexOf(cv);
            _cardViews.Remove(cv);
            if (cvIndex >= 0 && cvIndex < _prevCardInfo.Count)
                _prevCardInfo.RemoveAt(cvIndex);

            cv.transform.SetParent(_activeSlots[slotIndex], false);
            var rt = cv.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
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
            int toDomain   = FindDropDomainIndex(cv);

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
        }

        private void ShowMoveInSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _activeSlots.Count) return;

            if (slotIndex < _slotImages.Length && _slotImages[slotIndex] != null)
            {
                _slotImages[slotIndex].gameObject.SetActive(true);
                _slotImages[slotIndex].color = new Color(0.15f, 0.30f, 0.50f, 0.85f);
            }

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

        private int FindDropDomainIndex(CardView dragged)
        {
            Vector2 center   = (Vector2)dragged.transform.position;
            float   bestDist = float.MaxValue;
            int     bestIdx  = dragged.HandIndex;

            foreach (var cv in _cardViews)
            {
                if (cv == dragged || cv == null) continue;
                float d = Vector2.Distance(center, (Vector2)cv.transform.position);
                if (d < bestDist) { bestDist = d; bestIdx = cv.HandIndex; }
            }

            return bestIdx;
        }

        // ── Card hand ────────────────────────────────────────────────────────

        private void RefreshCardHand(bool animate = false)
        {
            if (cardHandContainer == null || cardPrefab == null || _tm?.Hand == null) return;

            var hlg    = cardHandContainer.GetComponent<HorizontalLayoutGroup>();
            var csf    = cardHandContainer.GetComponent<ContentSizeFitter>();
            var handRt = cardHandContainer as RectTransform;

            // Re-enable layout before any position measurements.
            SetLayout(hlg, csf, true);

            var oldPool = SnapshotPositions();
            RebuildCardViews();

            if (handRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(handRt);
            UpdateWorldPositions();

            // Compute what to animate, then hand off to the animator.
            // Ghost merges only on non-animate path — on draw path, direct punch fires after draw.
            IReadOnlyList<CardView>                drawSeq  = animate ? BuildDrawSequence() : Array.Empty<CardView>();
            IReadOnlyDictionary<CardView, Vector3> slideMap = animate ? new Dictionary<CardView, Vector3>() : BuildSlideMap(oldPool);

            var (directMerges, mergeGhosts) = BuildMergeAnimations(oldPool, enableGhosts: !animate);

            _pendingMergeFlash.Clear();
            _animator.PlayRefresh(drawSeq, directMerges, mergeGhosts, slideMap, hlg, csf);
        }

        // ── RefreshCardHand helpers ───────────────────────────────────────────

        // Snapshot (skill, rank) → previous world positions before cards are destroyed.
        private Dictionary<(SkillData, int), Queue<Vector3>> SnapshotPositions()
        {
            var pool = new Dictionary<(SkillData, int), Queue<Vector3>>();
            for (int k = 0; k < _cardViews.Count && k < _prevCardInfo.Count; k++)
            {
                if (_cardViews[k] == null) continue;
                var (sk, rk, pos) = _prevCardInfo[k];
                if (!pool.TryGetValue((sk, rk), out var q))
                    pool[(sk, rk)] = q = new Queue<Vector3>();
                q.Enqueue(pos);
            }
            return pool;
        }

        // Destroy existing CardViews and instantiate fresh ones from domain state.
        private void RebuildCardViews()
        {
            foreach (var cv in _cardViews)
            {
                if (cv == null) continue;
                cv.transform.SetParent(null);
                Destroy(cv.gameObject);
            }
            _cardViews.Clear();
            _prevCardInfo.Clear();

            var  slots  = _tm.Hand.Slots;
            bool canAct = _tm.Phase == CombatPhase.PlayerTurn && _tm.Hand.ActionsRemaining > 0;
            var  display = _pending.Count > 0 ? BuildVirtualHand() : BuildDirectDisplay(slots);

            // Reverse instantiation so HLG child order matches domain order left-to-right.
            for (int i = display.Count - 1; i >= 0; i--)
            {
                var (domIdx, visRank) = display[i];
                var slot = slots[domIdx];
                var elem = slot.IsUltimate
                    ? (slot.Ultimate?.element ?? ElementType.Neutral)
                    : (slot.Skill?.element    ?? ElementType.Neutral);

                var cv = Instantiate(cardPrefab, cardHandContainer);
                cv.HandIndex = domIdx;
                cv.Setup(slot.IsUltimate ? slot : new BattleCard(slot.Skill, visRank), ElementColor(elem));

                var btn = cv.GetComponent<Button>();
                if (btn != null) { btn.interactable = canAct; btn.onClick.AddListener(() => QueueCard(cv)); }
                cv.OnReorderRequested = ReorderCard;

                _cardViews.Add(cv);
                _prevCardInfo.Add((slot.IsUltimate ? null : slot.Skill, slots[domIdx].Rank, Vector3.zero));
            }
        }

        private static List<(int domainIdx, int visRank)> BuildDirectDisplay(IReadOnlyList<BattleCard> slots)
        {
            var display = new List<(int, int)>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
                display.Add((i, slots[i].Rank));
            return display;
        }

        // Capture world positions after ForceRebuildLayoutImmediate.
        private void UpdateWorldPositions()
        {
            for (int k = 0; k < _cardViews.Count && k < _prevCardInfo.Count; k++)
            {
                var p = _prevCardInfo[k];
                _prevCardInfo[k] = (p.skill, p.rank, _cardViews[k].transform.position);
            }
        }

        // Cards that should slide in from off-screen, ordered by arrival time (index 0 = first).
        private IReadOnlyList<CardView> BuildDrawSequence()
        {
            int newCount = Mathf.Clamp(_tm.Hand.NewCardsThisRefill, 0, _cardViews.Count);
            if (newCount == 0) return Array.Empty<CardView>();

            // _cardViews is stored in reverse domain order: _cardViews[0] = highest domain index.
            // New cards are the highest domain indices (appended by Refill).
            // Stagger order: rightmost new card arrives first → _cardViews[newCount-1] is index 0.
            var seq = new List<CardView>(newCount);
            for (int k = 0; k < newCount; k++)
                seq.Add(_cardViews[newCount - 1 - k]);
            return seq;
        }

        // Cards that shifted position but did NOT merge — slide from old world position.
        // Merge results are excluded: they are handled as ghost animations in BuildMergeAnimations.
        private Dictionary<CardView, Vector3> BuildSlideMap(
            Dictionary<(SkillData, int), Queue<Vector3>> oldPool)
        {
            float minDist = animConfig != null ? animConfig.slideMinDistance : 2f;
            var slideMap  = new Dictionary<CardView, Vector3>();

            for (int k = 0; k < _cardViews.Count && k < _prevCardInfo.Count; k++)
            {
                var cv = _cardViews[k];
                var (sk, rk, newPos) = _prevCardInfo[k];

                // Exact identity match — card may have shifted position without merging.
                if (oldPool.TryGetValue((sk, rk), out var q) && q.Count > 0)
                {
                    var old = q.Dequeue();
                    if (Vector3.Distance(old, newPos) > minDist)
                        slideMap[cv] = old;
                }
                // rk > 1 (merge results) intentionally excluded — ghost handles those.
            }
            return slideMap;
        }

        // Consume pending merge events. Returns direct punches and ghost-driven merges.
        // enableGhosts=true (non-animate path): ghost slides from consumed card's old position.
        // enableGhosts=false (animate/draw path): punch fires after draw animation.
        private (List<(CardView cv, Color color)> direct,
                 List<(CardView ghost, CardView result, Color color)> ghosts)
            BuildMergeAnimations(Dictionary<(SkillData, int), Queue<Vector3>> oldPool, bool enableGhosts)
        {
            var direct = new List<(CardView, Color)>();
            var ghosts = new List<(CardView, CardView, Color)>();
            if (_pendingMergeFlash.Count == 0) return (direct, ghosts);

            float minDist = animConfig != null ? animConfig.slideMinDistance : 2f;

            for (int k = 0; k < _cardViews.Count && k < _prevCardInfo.Count; k++)
            {
                var (sk, rk, resultPos) = _prevCardInfo[k];
                if (sk == null) continue;

                var key = (sk.skillName, rk);
                if (!_pendingMergeFlash.TryGetValue(key, out int cnt) || cnt <= 0) continue;

                if (cnt == 1) _pendingMergeFlash.Remove(key);
                else          _pendingMergeFlash[key] = cnt - 1;

                Color    color  = ElementColor(sk.element);
                CardView result = _cardViews[k];

                bool usedGhost = false;
                if (enableGhosts && rk > 1
                    && oldPool.TryGetValue((sk, rk - 1), out var q) && q.Count > 0)
                {
                    var srcPos = q.Dequeue();
                    if (Vector3.Distance(srcPos, resultPos) > minDist)
                    {
                        ghosts.Add((CreateGhostCard(sk, rk - 1, color, srcPos), result, color));
                        usedGhost = true;
                    }
                }

                if (!usedGhost) direct.Add((result, color));
            }

            return (direct, ghosts);
        }

        // Instantiate a temporary card at worldPosition on the canvas root.
        // The ghost is owned by the animator (self-destructs on arrival).
        private CardView CreateGhostCard(SkillData skill, int rank, Color color, Vector3 worldPosition)
        {
            var canvas = cardHandContainer.GetComponentInParent<Canvas>();
            var parent = canvas != null ? canvas.transform : cardHandContainer;

            var ghost = Instantiate(cardPrefab, parent, false);
            ghost.HandIndex       = -1;
            ghost.OnReorderRequested = null;
            ghost.Setup(new BattleCard(skill, rank), color);
            ghost.transform.position = worldPosition;
            ghost.transform.SetAsLastSibling();

            var btn = ghost.GetComponent<Button>();
            if (btn != null) btn.interactable = false;

            var cg = ghost.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;

            return ghost;
        }

        private static void SetLayout(HorizontalLayoutGroup hlg, ContentSizeFitter csf, bool on)
        {
            if (hlg != null) hlg.enabled = on;
            if (csf != null) csf.enabled = on;
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

        private void RebuildActionSlots()
        {
            if (actionSlotContainer == null || actionSlotPrefab == null) return;

            int alive = 0;
            for (int i = 0; i < _ctx.Players.Count; i++)
                if (_ctx.Players[i].IsAlive) alive++;

            if (alive == _activeSlots.Count) return;

            foreach (var s in _activeSlots) if (s != null) Destroy(s.gameObject);
            _activeSlots.Clear();
            for (int i = actionSlotContainer.childCount - 1; i >= 0; i--)
                Destroy(actionSlotContainer.GetChild(i).gameObject);

            for (int i = 0; i < alive; i++)
            {
                var go = Instantiate(actionSlotPrefab, actionSlotContainer);
                if (go.GetComponent<CanvasGroup>() == null)
                    go.AddComponent<CanvasGroup>();
                _activeSlots.Add(go.transform);
            }

            _slotImages = new Image[alive];
            for (int i = 0; i < alive; i++)
            {
                var inner = _activeSlots[i].Find("Inner");
                _slotImages[i] = inner?.GetComponent<Image>();
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
                hlg.spacing               = 12f;
                hlg.childForceExpandWidth = false;
            }

            if (cardHandContainer != null)
            {
                var hlg = cardHandContainer.GetComponent<HorizontalLayoutGroup>()
                       ?? cardHandContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment        = TextAnchor.MiddleRight;
                hlg.childForceExpandWidth = false;

                var csf = cardHandContainer.GetComponent<ContentSizeFitter>()
                       ?? cardHandContainer.gameObject.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        private void ClearSlots()
        {
            for (int i = 0; i < _activeSlots.Count; i++)
            {
                var slot = _activeSlots[i];
                if (slot == null) continue;
                for (int c = slot.childCount - 1; c >= 0; c--)
                {
                    var go = slot.GetChild(c).gameObject;
                    if (go.GetComponent<CardView>() != null || go.name == "MoveLabel")
                        Destroy(go);
                }
                if (i < _slotImages.Length && _slotImages[i] != null)
                {
                    _slotImages[i].gameObject.SetActive(true);
                    _slotImages[i].color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
                }
            }
        }

        public override void SetActionSlotsActive(bool active)
        {
            if (actionSlotContainer == null) return;
            if (_slotAnim != null) StopCoroutine(_slotAnim);
            _slotAnim = StartCoroutine(active ? AnimateSlotExpand() : AnimateSlotShrink());
        }

        public override void NotifyActionAnimationComplete(int actionIndex)
        {
            Debug.Log($"[Slots-DBG] NotifyActionAnimationComplete({actionIndex}) — activeSlots={_activeSlots.Count}");
            if (actionIndex < 0 || actionIndex >= _activeSlots.Count)
            {
                Debug.LogWarning($"[Slots-DBG] index {actionIndex} out of range (count={_activeSlots.Count}), skip");
                return;
            }
            if (_activeSlots[actionIndex] == null)
            {
                Debug.LogWarning($"[Slots-DBG] slot[{actionIndex}] is null, skip");
                return;
            }
            Debug.Log($"[Slots-DBG] Starting FadeOutActionSlot({actionIndex}) on GO={_activeSlots[actionIndex].name}");
            StartCoroutine(FadeOutActionSlot(actionIndex));
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

        private static Color ElementColor(ElementType element) => element switch
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
