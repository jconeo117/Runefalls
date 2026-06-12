using System.Collections;
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

        private static readonly Color k_Green = new(0.45f, 0.90f, 0.45f);

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
            var ray = _cam.ScreenPointToRay(_downPos);
            if (!Physics.Raycast(ray, out var hit, 100f)) return;
            var src = hit.collider.GetComponentInParent<CombatantStatsSource>();
            if (src?.Provider == null) return;
            Open(src.Provider);
        }

        // ── open / close ─────────────────────────────────────────────────────

        private void Open(ICharacterStatsProvider p)
        {
            _isOpen = true;

            // Take camera control from the combat systems.
            if (_camController != null) { _prevCamControllerEnabled = _camController.enabled; _camController.enabled = false; }
            if (_cam != null)
            {
                _brain = _cam.GetComponent<CinemachineBrain>();
                if (_brain != null) { _prevBrainEnabled = _brain.enabled; _brain.enabled = false; }
            }

            if (p.FocusTarget != null && _cam != null)
            {
                if (_frameRoutine != null) StopCoroutine(_frameRoutine);
                _frameRoutine = StartCoroutine(FramePawn(p.FocusTarget));
            }

            _hud?.HideAllUI();
            BuildOverlay(p);
        }

        private void Close()
        {
            _isOpen = false;

            if (_frameRoutine != null) { StopCoroutine(_frameRoutine); _frameRoutine = null; }
            if (_overlayRoot != null) { Destroy(_overlayRoot); _overlayRoot = null; }

            _hud?.ShowAllUI();

            // Hand control back; the combat camera resumes its gameplay framing.
            if (_brain != null) _brain.enabled = _prevBrainEnabled;
            if (_camController != null) _camController.enabled = _prevCamControllerEnabled;
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
            BuildStatBlock(root, p);
            BuildSkillPanel(root, p);
            BuildPassivePanel(root, p);
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
            rt.sizeDelta = new Vector2(360f, 340f);
            rt.anchoredPosition = new Vector2(70f + 460f + 16f, 0f);   // right of the stat block
            Frame(panel, new Color(0.3f, 0.45f, 0.5f, 0.8f));

            var sb = new System.Text.StringBuilder();
            if (es != null)
            {
                sb.AppendLine($"Perforación      {es.ofensivas.perforacion:F1}");
                sb.AppendLine($"Prob. crítico    {es.ofensivas.critChance * 100f:F0}%");
                sb.AppendLine($"Daño crítico     {es.ofensivas.critDaño * 100f:F0}%");
                sb.AppendLine($"Resistencia      {es.defensivas.resistencia:F1}");
                sb.AppendLine($"Def. crítica     {es.defensivas.defensaCrit:F1}");
                sb.AppendLine($"Res. crítica     {es.defensivas.resistenciaCrit:F1}");
                sb.AppendLine($"Robo de vida     {es.vitales.roboDeVida * 100f:F0}%");
                sb.AppendLine($"Regeneración     {es.vitales.tasaRegen * 100f:F0}%");
                sb.AppendLine($"Recuperación     {es.vitales.tasaRecuperacion * 100f:F0}%");
            }

            var t = Text(panel.transform, "List", sb.ToString(), 22, TextAnchor.UpperLeft,
                new Color(0.86f, 0.92f, 0.96f), FontStyle.Normal);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(20f, 16f); trt.offsetMax = new Vector2(-16f, -16f);

            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        // Skills live along the BOTTOM: skill1 (3) · skill2 (3) · ultimate, in a horizontal row.
        private void BuildSkillPanel(Transform root, ICharacterStatsProvider p)
        {
            var panel = Panel(root, "SkillPanel",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero,
                new Color(0.06f, 0.08f, 0.11f, 0.86f));
            var prt = panel.rectTransform;
            prt.pivot = new Vector2(0.5f, 0f);
            prt.sizeDelta = new Vector2(1180f, 232f);
            prt.anchoredPosition = new Vector2(0f, 40f);
            Frame(panel, ElementColor(p.Element));

            const float labelY = 186f;
            const float cardY  = 96f;
            SkillLabel(prt, "HABILIDAD 1", -430f, labelY);
            SkillRow(prt, p.Skill1, -430f, cardY);
            SkillLabel(prt, "HABILIDAD 2", -40f, labelY);
            SkillRow(prt, p.Skill2, -40f, cardY);
            SkillLabel(prt, "ULTIMATE", 320f, labelY);
            UltCard(prt, p.Ultimate, 320f, cardY);
        }

        private void SkillLabel(RectTransform parent, string text, float x, float y)
        {
            var t = Text(parent, text + "_lbl", text, 22, TextAnchor.MiddleCenter,
                new Color(0.72f, 0.82f, 0.88f), FontStyle.Bold);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(320f, 32f);
            rt.anchoredPosition = new Vector2(x, y);
        }

        private void SkillRow(RectTransform parent, SkillData skill, float centerX, float y)
        {
            if (skill == null) return;
            float[] dx = { -96f, 0f, 96f };
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
                cv.transform.localScale = Vector3.one * 0.55f;
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

            // Passive panel sits on the RIGHT (the old skill location): chip + name on top, description below.
            var panel = Panel(root, "PassivePanel",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(0.06f, 0.08f, 0.11f, 0.86f));
            var prt = panel.rectTransform;
            prt.pivot = new Vector2(1f, 0.5f);
            prt.sizeDelta = new Vector2(380f, 440f);
            prt.anchoredPosition = new Vector2(-60f, 0f);
            Frame(panel, ElementColor(p.Element));

            // Procedural placeholder chip (swap for the final sprite later).
            var chip = Panel(panel.transform, "PassiveChip", new Vector2(0f, 1f), new Vector2(0f, 1f),
                Vector2.zero, Vector2.zero, ElementColor(p.Element));
            var chrt = chip.rectTransform;
            chrt.pivot = new Vector2(0f, 1f);
            chrt.sizeDelta = new Vector2(62f, 62f);
            chrt.anchoredPosition = new Vector2(20f, -18f);
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
            nrt.offsetMin = new Vector2(94f, -78f); nrt.offsetMax = new Vector2(-14f, -18f);

            var descT = Text(panel.transform, "PassiveDesc", p.Passive.description ?? "",
                20, TextAnchor.UpperLeft, new Color(0.85f, 0.9f, 0.94f), FontStyle.Normal);
            var drt = descT.rectTransform;
            drt.anchorMin = new Vector2(0f, 0f); drt.anchorMax = new Vector2(1f, 1f);
            drt.offsetMin = new Vector2(20f, 16f); drt.offsetMax = new Vector2(-16f, -92f);
        }

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
            img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
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
