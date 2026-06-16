using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class SkillsInfo : MonoBehaviour
{
    [Header("=== TEXTO + IMAGEN COMPARTIDOS (se usan para los 3 personajes) ===")]
    public TMP_Text infoText;            // el TextMeshPro fijo donde aparece todo
    public RawImage infoBackground;      // la RawImage que aparece junto con el texto
    public CanvasGroup canvasGroup;      // opcional: si lo dej�s vac�o, se crea uno en el infoText


    [Header("Fade (leve y sutil)")]
    public float fadeInDuration = 0.15f;
    public float fadeOutDuration = 0.15f;


    [Header("=== KAEL � Iconos (RawImage) ===")]
    public RawImage skill1Icon;
    public RawImage skill2Icon;
    public RawImage skill3Icon;
    public RawImage passiveIcon;


    [Header("KAEL � Textos de cada skill")]
    [TextArea(2, 5)] public string skill1Description;
    [TextArea(2, 5)] public string skill2Description;
    [TextArea(2, 5)] public string skill3Description;
    [TextArea(2, 5)] public string passiveDescription;


    [Header("=== LYRA � Iconos (RawImage) ===")]
    public RawImage lyraSkill1Icon;
    public RawImage lyraSkill2Icon;
    public RawImage lyraSkill3Icon;
    public RawImage lyraPassiveIcon;


    [Header("LYRA � Textos de cada skill")]
    [TextArea(2, 5)] public string lyraSkill1Description;
    [TextArea(2, 5)] public string lyraSkill2Description;
    [TextArea(2, 5)] public string lyraSkill3Description;
    [TextArea(2, 5)] public string lyraPassiveDescription;


    [Header("=== VORN � Iconos (RawImage) ===")]
    public RawImage vornSkill1Icon;
    public RawImage vornSkill2Icon;
    public RawImage vornSkill3Icon;
    public RawImage vornPassiveIcon;


    [Header("VORN � Textos de cada skill")]
    [TextArea(2, 5)] public string vornSkill1Description;
    [TextArea(2, 5)] public string vornSkill2Description;
    [TextArea(2, 5)] public string vornSkill3Description;
    [TextArea(2, 5)] public string vornPassiveDescription;


    private RawImage[] icons;
    private string[] descriptions;
    private Camera canvasCam;
    private int currentIndex = -1;
    private float alpha = 0f;            // alpha maestro que comparten texto e imagen


    private A_SkillsInfoAudio _audio; // opcional; si no est� adjunto, simplemente no suena


    void Start()
    {
        // juntamos los 12 iconos y sus 12 textos (Kael, Lyra, Vorn) en el mismo orden
        icons = new RawImage[]
        {
            skill1Icon, skill2Icon, skill3Icon, passiveIcon,
            lyraSkill1Icon, lyraSkill2Icon, lyraSkill3Icon, lyraPassiveIcon,
            vornSkill1Icon, vornSkill2Icon, vornSkill3Icon, vornPassiveIcon
        };


        descriptions = new string[]
        {
            skill1Description, skill2Description, skill3Description, passiveDescription,
            lyraSkill1Description, lyraSkill2Description, lyraSkill3Description, lyraPassiveDescription,
            vornSkill1Description, vornSkill2Description, vornSkill3Description, vornPassiveDescription
        };


        // canvas group para hacer el fade del texto
        if (canvasGroup == null && infoText != null)
        {
            canvasGroup = infoText.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = infoText.gameObject.AddComponent<CanvasGroup>();
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;             // arranca invisible
            canvasGroup.blocksRaycasts = false; // que nunca bloquee clicks de otra UI
        }


        // la imagen tambi�n arranca invisible
        SetBackgroundAlpha(0f);


        // c�mara del canvas (para detectar el mouse sobre los iconos)
        RawImage anyIcon = GetFirstIcon();
        if (anyIcon != null)
        {
            Canvas canvas = anyIcon.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                canvasCam = canvas.rootCanvas.worldCamera;
        }


        _audio = GetComponent<A_SkillsInfoAudio>(); // null si no est� adjunto, y est� bien
    }


    void Update()
    {
        Vector2 mouse = Input.mousePosition;


        // �sobre qu� icono (de cualquier personaje) est� el mouse?
        int hovered = -1;
        for (int i = 0; i < icons.Length; i++)
        {
            // solo contamos iconos que est�n activos (el del personaje prendido en este momento)
            if (icons[i] == null || !icons[i].isActiveAndEnabled) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(icons[i].rectTransform, mouse, canvasCam))
            {
                hovered = i;
                break;
            }
        }


        // si cambi� el icono apuntado, actualizamos el texto compartido
        if (hovered != -1 && hovered != currentIndex)
        {
            currentIndex = hovered;
            if (infoText != null) infoText.text = descriptions[hovered];
            _audio?.OnHover();
        }
        if (hovered == -1) currentIndex = -1;


        // fade in si hay un icono debajo del mouse, fade out si no (texto e imagen al mismo tiempo)
        float target = (hovered != -1) ? 1f : 0f;
        float dur = (hovered != -1) ? fadeInDuration : fadeOutDuration;
        float speed = (dur > 0f) ? 1f / dur : 1000f;
        alpha = Mathf.MoveTowards(alpha, target, speed * Time.deltaTime);


        if (canvasGroup != null) canvasGroup.alpha = alpha;
        SetBackgroundAlpha(alpha);
    }


    void SetBackgroundAlpha(float a)
    {
        if (infoBackground == null) return;
        Color c = infoBackground.color;
        c.a = a;
        infoBackground.color = c;
    }


    RawImage GetFirstIcon()
    {
        for (int i = 0; i < icons.Length; i++)
            if (icons[i] != null) return icons[i];
        return null;
    }
}
