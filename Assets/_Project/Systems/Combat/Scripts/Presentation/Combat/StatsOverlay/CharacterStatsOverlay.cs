using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.Cinemachine;
using Runefall.Characters;
using Runefall.Combat;
using Runefall.Data;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Character-stats overlay (combat). Hold on a pawn during the player turn to open a
    /// full-screen inspection: the camera dollies to frame the live 3D pawn from the front
    /// and procedural panels render its stats, skill cards and passive around it. An X button
    /// closes it. The overlay is agnostic — it reads an <see cref="ICharacterStatsProvider"/>
    /// off the held pawn's <see cref="CombatantStatsSource"/>, never a concrete actor type.
    /// </summary>
    public class CharacterStatsOverlay : MonoBehaviour
    {
        [Header("Open gesture")]
        [SerializeField] private float _holdThreshold     = 0.32f;   // seconds to register a hold
        [SerializeField] private float _holdMoveTolerance = 14f;     // px of drift that cancels the hold

        [Header("Camera framing")]
        [SerializeField] private float _frameDistance = 2.4f;   // how far in front of the pawn
        [SerializeField] private float _frameHeight   = 1.5f;   // camera height above pawn pivot
        [SerializeField] private float _faceHeight    = 1.5f;   // look-at height on the pawn
        [SerializeField] private float _frameDuration = 0.40f;  // dolly lerp time

        // injected
        private TurnManager           _tm;
        private CombatCameraController _camController;
        private CombatPresenterBase   _hud;
        private CardView              _cardPrefab;
        private Camera                _cam;
        private System.Action<bool>  _setHpBarsVisible;   // hides world-space HP bars while open

        // open gesture state
        private bool    _pressing;
        private float   _holdTimer;
        private Vector3 _downPos;

        // open state
        private bool         _isOpen;
        private GameObject   _overlayRoot;
        private Coroutine    _frameRoutine;
        private bool         _prevBrainEnabled;
        private bool         _prevCamControllerEnabled;
        private CinemachineBrain _brain;

        // target cycling (arrows flanking the name)
        private readonly List<CombatantStatsSource>    _sources      = new();
        private readonly List<ICharacterStatsProvider> _inspectables = new();
        private int                                    _currentIndex;

        private static readonly Color k_Green = new(0.45f, 0.90f, 0.45f);

        // Shared, fixed-position skill description box (centre gap). Rebuilt with the overlay.
        private GameObject _skillDescBox;
        private Text       _skillDescTitle;
        private Text       _skillDescBody;

        // Box geometry — kept here so build + resize agree.
        private const float kDescW    = 720f;   // narrower than the centre gap so it clears both side panels
        private const float kDescPadX = 26f, kDescPadTop = 18f, kDescTitleH = 34f, kDescGap = 10f, kDescPadBot = 18f;
        private const int   kDescBodyFont = 23;

        // ── setup ────────────────────────────────────────────────────────────

        public void Initialize(TurnManager tm, CombatCameraController camController,
                               CombatPresenterBase hud, CardView cardPrefab)
        {
            _tm            = tm;
            _camController = camController;
            _hud           = hud;
            _cardPrefab    = cardPrefab;
            _cam           = Camera.main;
            if (_isOpen) Close();   // a re-init (restart) while open → reset cleanly
        }

        /// <summary>Provide every inspectable pawn so the side arrows can cycle targets without
        /// closing. Called by the bootstrapper after binding all CombatantStatsSources.</summary>
        public void SetInspectables(List<CombatantStatsSource> sources)
        {
            _sources.Clear();
            if (sources != null) _sources.AddRange(sources);
        }

        /// <summary>Callback the bootstrapper supplies to show/hide world-space HP bars while the
        /// overlay is open. Kept as a delegate so the overlay stays decoupled from the bootstrapper.</summary>
        public void SetHpBarsVisibilityCallback(System.Action<bool> cb) => _setHpBarsVisible = cb;

        private void OnDisable() => ForceClose();

        public void ForceClose()
        {
            if (_isOpen) Close();
            _pressing = false;
        }

        // ── open gesture ─────────────────────────────────────────────────────

        private void Update()
        {
            if (_isOpen) return;
            if (_tm == null || _tm.Phase != CombatPhase.PlayerTurn) { _pressing = false; return; }
            if (_cam == null) _cam = Camera.main;

            if (Input.GetMouseButtonDown(0))
            {
                if (IsPointerOverUI()) _pressing = false;
                else { _pressing = true; _downPos = Input.mousePosition; _holdTimer = 0f; }
            }
            else if (_pressing && Input.GetMouseButton(0))
            {
                if (((Vector2)Input.mousePosition - (Vector2)_downPos).magnitude > _holdMoveTolerance)
                    _pressing = false;                                       // dragged → not a hold
                else
                {
                    _holdTimer += Time.unscaledDeltaTime;
                    if (_holdTimer >= _holdThreshold) { _pressing = false; TryOpen(); }
                }
            }
            else if (Input.GetMouseButtonUp(0)) _pressing = false;
        }

        private static bool IsPointerOverUI()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void TryOpen()
        {
            if (_cam == null) return;
            var ray  = _cam.ScreenPointToRay(_downPos);
            var hits = Physics.RaycastAll(ray, 100f);

            // Pick colliders are invisible proxy capsules that overlap on screen: the player's
            // foreground capsule (over-the-shoulder cam) sits between the camera and a background
            // enemy, so the CLOSEST hit is always the player. Instead, among every inspectable the
            // ray pierces, take the one whose pawn projects nearest the press point — i.e. the pawn
            // the user actually pressed on.
            CombatantStatsSource best = null;
            float bestSqr = float.MaxValue;
            foreach (var h in hits)
            {
                var s = h.collider.GetComponentInParent<CombatantStatsSource>();
                if (s?.Provider?.FocusTarget == null) continue;
                Vector3 sp = _cam.WorldToScreenPoint(s.Provider.FocusTarget.position + Vector3.up);
                if (sp.z <= 0f) continue;   // behind the camera
                float d = ((Vector2)sp - (Vector2)_downPos).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = s; }
            }
            if (best?.Provider == null) return;
            Open(best.Provider);
        }

        // ── open / close ─────────────────────────────────────────────────────

        private void Open(ICharacterStatsProvider p)
        {
            if (_isOpen) return;
            _isOpen = true;

            // Order every inspectable left→right NOW, while the camera is still at its gameplay pose
            // (before we dolly), so the side arrows step through pawns the way they're laid out.
            BuildInspectables(p);

            // Take camera control from the combat systems.
            if (_camController != null) { _prevCamControllerEnabled = _camController.enabled; _camController.enabled = false; }
            if (_cam != null)
            {
                _brain = _cam.GetComponent<CinemachineBrain>();
                if (_brain != null) { _prevBrainEnabled = _brain.enabled; _brain.enabled = false; }
            }

            _hud?.HideAllUI();
            _setHpBarsVisible?.Invoke(false);   // world-space HP bars off during inspection
            RenderCurrent();
        }

        private void Close()
        {
            _isOpen = false;

            if (_frameRoutine != null) { StopCoroutine(_frameRoutine); _frameRoutine = null; }
            if (_overlayRoot != null) { Destroy(_overlayRoot); _overlayRoot = null; }

            _inspectables.Clear();
            _currentIndex = 0;

            _hud?.ShowAllUI();
            _setHpBarsVisible?.Invoke(true);   // restore world-space HP bars

            // Hand control back; the combat camera resumes its gameplay framing.
            if (_brain != null) _brain.enabled = _prevBrainEnabled;
            if (_camController != null) _camController.enabled = _prevCamControllerEnabled;
        }

        // ── target cycling ───────────────────────────────────────────────────

        private void BuildInspectables(ICharacterStatsProvider current)
        {
            _inspectables.Clear();
            foreach (var s in _sources)
                if (s != null && s.Provider != null) _inspectables.Add(s.Provider);

            // Left → right by screen X, so arrow-left goes to the pawn on the left and back.
            if (_cam != null)
                _inspectables.Sort((a, b) => ScreenX(a).CompareTo(ScreenX(b)));

            _currentIndex = Mathf.Max(0, _inspectables.IndexOf(current));
        }

        private float ScreenX(ICharacterStatsProvider p)
            => p?.FocusTarget != null ? _cam.WorldToScreenPoint(p.FocusTarget.position).x : 0f;

        private void Step(int dir)
        {
            if (_inspectables.Count <= 1) return;
            _currentIndex = (_currentIndex + dir + _inspectables.Count) % _inspectables.Count;
            RenderCurrent();
        }

        // Re-frames the current pawn and rebuilds the panels. Camera takeover stays as-is, so
        // cycling never leaves the overlay.
        private void RenderCurrent()
        {
            if (_inspectables.Count == 0) return;
            var p = _inspectables[_currentIndex];

            if (p.FocusTarget != null && _cam != null)
            {
                if (_frameRoutine != null) StopCoroutine(_frameRoutine);
                _frameRoutine = StartCoroutine(FramePawn(p.FocusTarget));
            }

            if (_overlayRoot != null) { Destroy(_overlayRoot); _overlayRoot = null; }
            BuildOverlay(p);
        }

        private IEnumerator FramePawn(Transform focus)
        {
            Vector3 fwd = focus.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
            {
                fwd = _cam.transform.position - focus.position; fwd.y = 0f;
            }
            fwd.Normalize();

            Vector3 targetPos  = focus.position + fwd * _frameDistance + Vector3.up * _frameHeight;
            Vector3 lookAt     = focus.position + Vector3.up * _faceHeight;
            Vector3 startPos   = _cam.transform.position;
            Quaternion startRot = _cam.transform.rotation;
            Quaternion targetRot = Quaternion.LookRotation(lookAt - targetPos);

            float t = 0f;
            while (t < _frameDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _frameDuration));
                _cam.transform.position = Vector3.Lerp(startPos, targetPos, k);
                _cam.transform.rotation = Quaternion.Slerp(startRot, targetRot, k);
                yield return null;
            }
            _cam.transform.position = targetPos;
            _cam.transform.rotation = targetRot;
            _frameRoutine = null;
        }

        // ── UI construction ──────────────────────────────────────────────────

        private void BuildOverlay(ICharacterStatsProvider p)
        {
            _overlayRoot = new GameObject("CharacterStatsOverlay_Canvas");
            var canvas = _overlayRoot.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = _overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;
            _overlayRoot.AddComponent<GraphicRaycaster>();

            // Dim backdrop (also blocks clicks reaching the 3D scene).
            var backdrop = Panel(_overlayRoot.transform, "Backdrop",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.03f, 0.05f, 0.62f));
            backdrop.raycastTarget = true;

            var root = backdrop.transform;

            BuildName(root, p);
            BuildNavArrows(root);
            BuildStatBlock(root, p);
            BuildSkillPanel(root, p);
            BuildPassivePanel(root, p);
            BuildSharedSkillDescBox(root);
            BuildCloseButton(root);
        }

        private void BuildName(Transform root, ICharacterStatsProvider p)
        {
            var t = Text(root, "Name", p.DisplayName?.Replace("_", " ") ?? "—",
                48, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f); rt.sizeDelta = new Vector2(900f, 70f);
            rt.anchoredPosition = new Vector2(0f, -36f);
            AddShadow(t.gameObject);
        }

        // ‹ › flanking the name: cycle the inspected pawn without leaving the overlay.
        private void BuildNavArrows(Transform root)
        {
            if (_inspectables.Count <= 1) return;
            NavArrow(root, "NavPrev", "‹", -1, -1f);
            NavArrow(root, "NavNext", "›", +1, +1f);
        }

        private void NavArrow(Transform root, string name, string glyph, int dir, float side)
        {
            var img = Panel(root, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero, new Color(0.10f, 0.13f, 0.18f, 0.92f));
            var rt = img.rectTransform;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(66f, 66f);
            rt.anchoredPosition = new Vector2(side * 300f, -39f);   // flank the name
            Frame(img, new Color(0.55f, 0.68f, 0.85f, 0.85f));

            var t = Text(img.transform, "g", glyph, 46, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            t.raycastTarget = false;
            AddShadow(t.gameObject);

            var btn = img.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => Step(dir));
        }

        private void BuildStatBlock(Transform root, ICharacterStatsProvider p)
        {
            var panel = Panel(root, "StatBlock",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(0.06f, 0.08f, 0.11f, 0.86f));
            var prt = panel.rectTransform;
            prt.pivot = new Vector2(0f, 0.5f);
            prt.sizeDelta = new Vector2(460f, 320f);
            prt.anchoredPosition = new Vector2(70f, 0f);
            Frame(panel, ElementColor(p.Element));

            var bs = p.BaseStats; var es = p.EffectiveStats;
            StatRow(prt, "ATK", bs?.ofensivas.ataque ?? 0f, es?.ofensivas.ataque ?? 0f, 96f);
            StatRow(prt, "DEF", bs?.defensivas.defensa ?? 0f, es?.defensivas.defensa ?? 0f, 16f);

            // PS row: current / max (+ green if max is buffed).
            float maxBase = bs?.vitales.ps ?? 0f;
            float maxEff  = es?.vitales.ps ?? p.MaxHP;
            string psDelta = maxEff > maxBase + 0.5f ? $"  <color=#73e673>(+{maxEff - maxBase:F0})</color>" : "";
            RowText(prt, "PSRow", $"PS   {p.CurrentHP:F0}/{maxEff:F0}{psDelta}", -64f);

            // Hover the stat block → reveal the full sub-stats.
            var popup = BuildSubstatsPopup(root, prt, es);
            AddHover(panel.gameObject, popup);
        }

        private void StatRow(RectTransform parent, string label, float baseVal, float effVal, float y)
        {
            string delta = effVal > baseVal + 0.5f ? $"  <color=#73e673>(+{effVal - baseVal:F0})</color>"
                          : effVal < baseVal - 0.5f ? $"  <color=#ff8a8a>({effVal - baseVal:F0})</color>"
                          : "";
            RowText(parent, label + "Row", $"{label}   {effVal:F0}{delta}", y);
        }

        private void RowText(RectTransform parent, string name, string content, float y)
        {
            var t = Text(parent, name, content, 40, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f); rt.sizeDelta = new Vector2(420f, 56f);
            rt.anchoredPosition = new Vector2(28f, y);
            AddShadow(t.gameObject);
        }

        private GameObject BuildSubstatsPopup(Transform root, RectTransform statBlock, CharacterStats es)
        {
            var panel = Panel(root, "Substats",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(0.04f, 0.06f, 0.09f, 0.94f));
            var rt = panel.rectTransform;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(440f, 480f);
            rt.anchoredPosition = new Vector2(70f + 460f + 20f, 0f);   // right of the stat block
            Frame(panel, new Color(0.3f, 0.45f, 0.5f, 0.8f));

            var sb = new System.Text.StringBuilder();
            if (es != null)
            {
                sb.AppendLine($"Perforación      {es.ofensivas.perforacion * 100f:F0}%");
                sb.AppendLine($"Prob. crítico    {es.ofensivas.critChance * 100f:F0}%");
                sb.AppendLine($"Daño crítico     {es.ofensivas.critDaño * 100f:F0}%");
                sb.AppendLine($"Resistencia      {es.defensivas.resistencia * 100f:F0}%");
                sb.AppendLine($"Def. crítica     {es.defensivas.defensaCrit * 100f:F0}%");
                sb.AppendLine($"Res. crítica     {es.defensivas.resistenciaCrit * 100f:F0}%");
                sb.AppendLine($"Robo de vida     {es.vitales.roboDeVida * 100f:F0}%");
                sb.AppendLine($"Regeneración     {es.vitales.tasaRegen * 100f:F0}%");
                sb.AppendLine($"Recuperación     {es.vitales.tasaRecuperacion * 100f:F0}%");
            }

            var t = Text(panel.transform, "List", sb.ToString(), 27, TextAnchor.UpperLeft,
                new Color(0.86f, 0.92f, 0.96f), FontStyle.Normal);
            t.lineSpacing = 1.3f;
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(26f, 20f); trt.offsetMax = new Vector2(-18f, -20f);

            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        // Skills float along the BOTTOM (no background panel): two tight groups of 3 + the ultimate,
        // cards overlapping ~20% like the combat hand, with the group label BELOW each group.
        private void BuildSkillPanel(Transform root, ICharacterStatsProvider p)
        {
            var go  = new GameObject("SkillStrip");
            go.transform.SetParent(root, false);
            var prt = go.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot     = new Vector2(0.5f, 0f);
            prt.sizeDelta = new Vector2(1000f, 200f);
            prt.anchoredPosition = new Vector2(0f, 8f);   // sit on the bottom edge

            // Children anchor to the strip's CENTRE: card just above it, label just below.
            const float cardY =  20f;
            const float lblY  = -78f;
            const float g1 = -285f, g2 = 75f, gu = 380f;   // group centres (×1.3 of the previous tuning)

            SkillRow(prt, p.Skill1, g1, cardY);
            SkillLabel(prt, "Habilidad 1", g1, lblY);
            SkillRow(prt, p.Skill2, g2, cardY);
            SkillLabel(prt, "Habilidad 2", g2, lblY);
            UltCard(prt, p.Ultimate, gu, cardY);
            SkillLabel(prt, "Movimiento definitivo", gu, lblY);
        }

        private void SkillLabel(RectTransform parent, string text, float x, float y)
        {
            var t = Text(parent, text + "_lbl", text, 22, TextAnchor.MiddleCenter,
                new Color(0.86f, 0.90f, 0.94f), FontStyle.Bold);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(380f, 32f);
            rt.anchoredPosition = new Vector2(x, y);
            AddShadow(t.gameObject);   // readable now that there's no panel behind it
        }

        private void SkillRow(RectTransform parent, SkillData skill, float centerX, float y)
        {
            if (skill == null) return;
            const float step = 95f;   // ~20% overlap, matching the combat hand's card padding
            float[] dx = { -step, 0f, step };
            for (int rank = 1; rank <= 3; rank++)
                PlaceCard(parent, new BattleCard(skill, rank), ElementColor(skill.element),
                    new Vector2(centerX + dx[rank - 1], y));
        }

        private void UltCard(RectTransform parent, UltimateData ult, float x, float y)
        {
            if (ult == null) return;
            PlaceCard(parent, new BattleCard(ult), ElementColor(ult.element), new Vector2(x, y));
        }

        private void PlaceCard(RectTransform parent, BattleCard card, Color color, Vector2 pos)
        {
            if (_cardPrefab != null)
            {
                var cv = Instantiate(_cardPrefab, parent);
                cv.Setup(card, color);
                cv.OnReorderRequested = null;
                var btn = cv.GetComponent<Button>();
                if (btn != null) { btn.onClick.RemoveAllListeners(); btn.interactable = false; }
                var rt = (RectTransform)cv.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(130f, 170f);
                rt.anchoredPosition = pos;
                cv.restScaleY = 1.1f;
                cv.transform.localScale = cv.RestScale(0.91f);
                cv.SetArtScale(1.15f, 1.25f);   // enlarge card art only

                // Hover the card → fill the shared, fixed description box (centre gap).
                AddSkillHover(cv.gameObject, card);
                return;
            }

            // Fallback: procedural chip if no card prefab was provided.
            var chip = Panel(parent, "CardChip", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, color);
            var crt = chip.rectTransform;
            crt.sizeDelta = new Vector2(72f, 94f);
            crt.anchoredPosition = pos;
            Frame(chip, Color.white * 0.5f);
            var lbl = Text(chip.transform, "r", "R" + card.Rank, 20, TextAnchor.LowerCenter, Color.white, FontStyle.Bold);
            lbl.rectTransform.anchorMin = Vector2.zero; lbl.rectTransform.anchorMax = Vector2.one;
            lbl.rectTransform.offsetMin = lbl.rectTransform.offsetMax = Vector2.zero;
        }

        private void BuildPassivePanel(Transform root, ICharacterStatsProvider p)
        {
            if (p.Passive == null) return;

            const float panelW   = 380f;
            const float padX     = 20f, padR = 16f, padBot = 18f;
            const float chipTop  = 14f, chipH = 62f;
            const float descTop  = chipTop + chipH + 14f;   // header band, then the description
            const int   descFont = 20;

            // Description with effect names highlighted; panel grows to fit it (no overflow).
            string descStr = ColorizeEffects(p.Passive.description ?? "");
            float  descH   = MeasureTextHeight(descStr, panelW - padX - padR, descFont, FontStyle.Normal);
            float  panelH  = Mathf.Clamp(descTop + descH + padBot, 200f, 840f);

            // Passive panel on the RIGHT (the old skill location).
            var panel = Panel(root, "PassivePanel",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(0.06f, 0.08f, 0.11f, 0.86f));
            var prt = panel.rectTransform;
            prt.pivot = new Vector2(1f, 0.5f);
            prt.sizeDelta = new Vector2(panelW, panelH);
            prt.anchoredPosition = new Vector2(-60f, 0f);
            Frame(panel, ElementColor(p.Element));

            // Procedural placeholder chip (swap for the final sprite later).
            var chip = Panel(panel.transform, "PassiveChip", new Vector2(0f, 1f), new Vector2(0f, 1f),
                Vector2.zero, Vector2.zero, ElementColor(p.Element));
            var chrt = chip.rectTransform;
            chrt.pivot = new Vector2(0f, 1f);
            chrt.sizeDelta = new Vector2(chipH, chipH);
            chrt.anchoredPosition = new Vector2(20f, -chipTop);
            Frame(chip, new Color(0f, 0f, 0f, 0.5f));
            var glyph = Text(chip.transform, "g",
                string.IsNullOrEmpty(p.Passive.passiveName) ? "★" : p.Passive.passiveName.Substring(0, 1).ToUpperInvariant(),
                28, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            glyph.rectTransform.anchorMin = Vector2.zero; glyph.rectTransform.anchorMax = Vector2.one;
            glyph.rectTransform.offsetMin = glyph.rectTransform.offsetMax = Vector2.zero;

            var nameT = Text(panel.transform, "PassiveName", p.Passive.passiveName ?? "Pasiva",
                24, TextAnchor.MiddleLeft, new Color(0.95f, 0.9f, 0.6f), FontStyle.Bold);
            var nrt = nameT.rectTransform;
            nrt.anchorMin = new Vector2(0f, 1f); nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(0f, 1f);
            nrt.offsetMin = new Vector2(94f, -(chipTop + chipH)); nrt.offsetMax = new Vector2(-14f, -chipTop);

            var descT = Text(panel.transform, "PassiveDesc", descStr,
                descFont, TextAnchor.UpperLeft, new Color(0.85f, 0.9f, 0.94f), FontStyle.Normal);
            var drt = descT.rectTransform;
            drt.anchorMin = new Vector2(0f, 1f); drt.anchorMax = new Vector2(1f, 1f);
            drt.pivot = new Vector2(0.5f, 1f);
            drt.sizeDelta = new Vector2(-(padX + padR), descH);
            drt.anchoredPosition = new Vector2((padX - padR) * 0.5f, -descTop);
        }

        // ── effect-name highlighting + text measuring ────────────────────────

        /// <summary>
        /// Colour the effect names inside a passive description. Names are detected
        /// data-drivenly as the terms the description itself defines as "Name:" — so any
        /// passive that introduces its effects that way highlights them, with no hard-coded list.
        /// </summary>
        private static string ColorizeEffects(string desc)
        {
            if (string.IsNullOrEmpty(desc)) return desc;

            var names = new System.Collections.Generic.List<string>();
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(desc,
                         @"(?:^|[.\n]|[""“”'])\s*([A-ZÁÉÍÓÚÑ][\p{L} ]{1,28}?)\s*:"))
            {
                string n = m.Groups[1].Value.Trim();
                if (n.Length > 0 && !names.Contains(n)) names.Add(n);
            }
            if (names.Count == 0) return desc;

            // Longest first so "Monarca del hielo" wins over "Monarca"; single pass = no nested tags.
            names.Sort((a, b) => b.Length.CompareTo(a.Length));
            string pattern = string.Join("|", names.ConvertAll(System.Text.RegularExpressions.Regex.Escape));

            return System.Text.RegularExpressions.Regex.Replace(desc, pattern,
                mm => $"<color=#79DFFF>{mm.Value}</color>");
        }

        /// <summary>Wrapped pixel height of <paramref name="text"/> at <paramref name="width"/>, for sizing a panel.</summary>
        private static float MeasureTextHeight(string text, float width, int fontSize, FontStyle style)
        {
            var go = new GameObject("measure") { hideFlags = HideFlags.HideAndDontSave };
            var t  = go.AddComponent<Text>();
            t.font               = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize           = fontSize;
            t.fontStyle          = style;
            t.supportRichText    = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            t.text               = text;

            var settings = t.GetGenerationSettings(new Vector2(width, 0f));
            float h = t.cachedTextGeneratorForLayout.GetPreferredHeight(text, settings) / t.pixelsPerUnit;

            Object.Destroy(go);
            return h;
        }

        // ── skill description hover box ──────────────────────────────────────

        // One fixed box living in the centre gap (between the stat block on the left and the passive
        // panel on the right). Hidden until a card is hovered; content swaps per card, position never
        // moves. Built once per overlay; rebuilt with it.
        private void BuildSharedSkillDescBox(Transform root)
        {
            var panel = Panel(root, "SkillDescBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(0.04f, 0.06f, 0.09f, 0.96f));
            var prt = panel.rectTransform;
            prt.pivot = new Vector2(0.5f, 0f);                       // bottom-anchored → grows UP, gap above cards fixed
            prt.sizeDelta = new Vector2(kDescW, 220f);
            prt.anchoredPosition = new Vector2(40f, -285f);         // centre gap, sitting just above the card strip
            Frame(panel, new Color(0.55f, 0.68f, 0.85f, 0.85f));
            panel.raycastTarget = false;                           // never steal hover from the cards

            var titleT = Text(panel.transform, "Title", "", 26, TextAnchor.UpperLeft,
                new Color(0.95f, 0.9f, 0.6f), FontStyle.Bold);
            var trt = titleT.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(kDescPadX, -(kDescPadTop + kDescTitleH));
            trt.offsetMax = new Vector2(-kDescPadX, -kDescPadTop);
            titleT.raycastTarget = false;
            AddShadow(titleT.gameObject);

            var bodyT = Text(panel.transform, "Body", "", kDescBodyFont, TextAnchor.UpperLeft,
                new Color(0.86f, 0.92f, 0.96f), FontStyle.Normal);
            bodyT.lineSpacing = 1.15f;
            var brt = bodyT.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;   // fill below the title; panel resizes
            brt.offsetMin = new Vector2(kDescPadX, kDescPadBot);
            brt.offsetMax = new Vector2(-kDescPadX, -(kDescPadTop + kDescTitleH + kDescGap));
            bodyT.raycastTarget = false;

            _skillDescBox   = panel.gameObject;
            _skillDescTitle = titleT;
            _skillDescBody  = bodyT;
            panel.gameObject.SetActive(false);
        }

        private void AddSkillHover(GameObject target, BattleCard card)
        {
            var trigger = target.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowSkillDesc(card));
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => HideSkillDesc());
            trigger.triggers.Add(exit);
        }

        private void ShowSkillDesc(BattleCard card)
        {
            if (_skillDescBox == null) return;

            string title = card.IsUltimate
                ? (card.Ultimate?.ultimateName ?? "Definitivo")
                : (card.Skill?.skillName?.Replace("_", " ") ?? "Habilidad");
            string body = ColorizeEffects(BuildSkillDescription(card));

            _skillDescTitle.text = title;
            _skillDescBody.text  = body;

            // Resize height to the text; width + centre stay fixed (pivot at centre).
            float bodyH = MeasureTextHeight(body, kDescW - kDescPadX * 2f, kDescBodyFont, FontStyle.Normal);
            float h     = Mathf.Clamp(kDescPadTop + kDescTitleH + kDescGap + bodyH + kDescPadBot, 140f, 360f);
            ((RectTransform)_skillDescBox.transform).sizeDelta = new Vector2(kDescW, h);

            _skillDescBox.transform.SetAsLastSibling();
            _skillDescBox.SetActive(true);
        }

        private void HideSkillDesc()
        {
            if (_skillDescBox != null) _skillDescBox.SetActive(false);
        }

        // ── description text generation ──────────────────────────────────────

        // Authored description wins; otherwise build the base line from the card's data.
        private static string BuildSkillDescription(BattleCard card)
        {
            if (card.IsUltimate)
            {
                var ult = card.Ultimate;
                if (ult == null) return "";
                if (!string.IsNullOrWhiteSpace(ult.description)) return ult.description;
                float upct = ResolveDamagePercentFromEffect(ult.effect, 3, out var usrc);
                return ComposeDamageLine(ult.element, upct, usrc, ult.targetType);
            }

            var skill = card.Skill;
            if (skill == null) return "";
            if (!string.IsNullOrWhiteSpace(skill.description)) return skill.description;

            int rank = Mathf.Clamp(card.Rank, 1, 3);
            float pct = ResolveDamagePercent(skill, rank, out var src);
            return ComposeDamageLine(skill.element, pct, src, skill.targetType);
        }

        private static string ComposeDamageLine(ElementType element, float pct, StatSource src, TargetType target)
        {
            string targetWord = TargetWord(target);
            if (pct <= 0f) return $"Aplica su efecto a {targetWord}.";
            return $"Hace daño {ElementWordDamage(element)} equivalente al {pct:F0}% del {StatWord(src)} a {targetWord}.";
        }

        private static float ResolveDamagePercent(SkillData skill, int rank, out StatSource src)
        {
            src = StatSource.Attack;
            var effs = skill.effectsByRank;
            if (effs == null || effs.Length == 0) return 0f;
            int idx = Mathf.Clamp(rank - 1, 0, effs.Length - 1);
            return ResolveDamagePercentFromEffect(effs[idx], rank, out src);
        }

        private static float ResolveDamagePercentFromEffect(SkillEffect se, int rank, out StatSource src)
        {
            src = StatSource.Attack;
            if (se == null) return 0f;

            if (se.effects != null)
            {
                foreach (var e in se.effects)
                {
                    if (e is DamageEffectDef dmg && dmg.multiplierByRank != null && dmg.multiplierByRank.Length > 0)
                    {
                        int i = Mathf.Clamp(rank - 1, 0, dmg.multiplierByRank.Length - 1);
                        src = dmg.statSource;
                        return dmg.multiplierByRank[i] * 100f;
                    }
                }
            }

            // Legacy fallback (effects[] empty).
            return se.damageMultiplier > 0f ? se.damageMultiplier * 100f : 0f;
        }

        private static string ElementWordDamage(ElementType e) => e switch
        {
            ElementType.Fire   => "de fuego",
            ElementType.Ice    => "de hielo",
            ElementType.Shadow => "de sombra",
            ElementType.Light  => "de luz",
            ElementType.Earth  => "de tierra",
            _                  => "físico",
        };

        private static string TargetWord(TargetType t) => t switch
        {
            TargetType.AllEnemies  => "todos los enemigos",
            TargetType.RandomEnemy => "un enemigo aleatorio",
            TargetType.Self        => "sí mismo",
            TargetType.AllAllies   => "todos los aliados",
            _                      => "un enemigo",
        };

        private static string StatWord(StatSource s) => s switch
        {
            StatSource.Defense => "defensa",
            StatSource.HP      => "PS",
            _                  => "ataque",
        };

        private void BuildCloseButton(Transform root)
        {
            var img = Panel(root, "Close", new Vector2(1f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, new Color(0.5f, 0.12f, 0.12f, 0.95f));
            var rt = img.rectTransform;
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(64f, 64f);
            rt.anchoredPosition = new Vector2(-28f, -28f);
            Frame(img, new Color(1f, 0.5f, 0.5f, 0.8f));

            var x = Text(img.transform, "x", "✕", 34, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            x.rectTransform.anchorMin = Vector2.zero; x.rectTransform.anchorMax = Vector2.one;
            x.rectTransform.offsetMin = x.rectTransform.offsetMax = Vector2.zero;
            x.raycastTarget = false;

            var btn = img.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(Close);
        }

        // ── tiny UI helpers ──────────────────────────────────────────────────

        private static Image Panel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = ProceduralSprites.RoundedRect();
            img.type   = Image.Type.Sliced;
            img.color  = color;
            var rt = img.rectTransform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offMin;    rt.offsetMax = offMax;
            return img;
        }

        private static Text Text(Transform parent, string name, string content, int size,
            TextAnchor anchor, Color color, FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font       = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text       = content;
            t.fontSize   = size;
            t.fontStyle  = style;
            t.alignment  = anchor;
            t.color      = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            t.supportRichText    = true;
            return t;
        }

        private static void Frame(Image img, Color color)
        {
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor    = color;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private static void AddShadow(GameObject go)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor    = new Color(0f, 0f, 0f, 0.7f);
            s.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private static void AddHover(GameObject go, GameObject popup)
        {
            var trigger = go.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (popup != null) popup.SetActive(true); });
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { if (popup != null) popup.SetActive(false); });
            trigger.triggers.Add(exit);
        }

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
