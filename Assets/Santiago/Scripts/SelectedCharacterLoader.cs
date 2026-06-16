using UnityEngine;
using Runefall.Data;
using Runefall.Presentation.Player;

/// <summary>
/// Toma el modelo elegido en el gacha y lo integra como el modelo de exploración
/// hijo del Player: lo reparenta, le aplica el explorationAnimatorController del
/// CharacterData, cablea el CharacterAnimationController (puente root-motion → PlayerController)
/// y setea la party en ExplorationPlayer (para combate). Desactiva el rig placeholder.
/// </summary>
public class SelectedCharacterLoader : MonoBehaviour
{
    [Header("Modelos en la escena (mismo orden que el gacha)")]
    public GameObject KaelModel;   // índice 0 (carta izquierda)
    public GameObject LyraModel;   // índice 1 (carta del medio)
    public GameObject VornModel;   // índice 2 (carta derecha)

    [Header("CharacterData (mismo orden: 0=Kael, 1=Lyra, 2=Vorn)")]
    [Tooltip("Usado para sacar explorationAnimatorController y setear ExplorationPlayer.Party.")]
    public CharacterData[] characters;

    [Header("Player")]
    [Tooltip("Player root. Vacío = busca por tag 'Player'.")]
    public Transform playerRoot;

    void Start()
    {
        var player = playerRoot != null
            ? playerRoot
            : (GameObject.FindGameObjectWithTag("Player") != null
                ? GameObject.FindGameObjectWithTag("Player").transform
                : null);

        if (player == null)
        {
            Debug.LogError("[SelectedCharacterLoader] Player no encontrado (asigná playerRoot o tag 'Player').");
            return;
        }

        // Apagar los 3 candidatos — nunca al Player (guarda contra refs mal asignadas).
        SafeDeactivate(KaelModel, player);
        SafeDeactivate(LyraModel, player);
        SafeDeactivate(VornModel, player);

        int        idx   = CardDrawAnimation.SelectedCharacterIndex;
        GameObject model = GetModelForIndex(idx);
        CharacterData data = (characters != null && idx >= 0 && idx < characters.Length)
            ? characters[idx]
            : null;

        if (model == null || model == player.gameObject || model.GetComponent<PlayerController>() != null)
        {
            Debug.LogWarning($"[SelectedCharacterLoader] Modelo inválido para índice {idx} " +
                             "(¿ref apunta al Player o falta el modelo?). No se cargó personaje.");
            return;
        }

        var pc = player.GetComponent<PlayerController>();
        var ep = player.GetComponent<ExplorationPlayer>();

        // Desactivar rigs placeholder existentes bajo el Player (p.ej. RiggedIdle) para que
        // no haya dos CharacterAnimationController moviendo al Player (doble root-motion).
        foreach (var placeholder in player.GetComponentsInChildren<CharacterAnimationController>(true))
            placeholder.gameObject.SetActive(false);

        // Reparentar el modelo como hijo del Player, preservando su tamaño en mundo.
        Vector3 worldScale = model.transform.lossyScale;
        model.transform.SetParent(player, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        Vector3 ps = player.lossyScale;
        model.transform.localScale = new Vector3(
            Mathf.Approximately(ps.x, 0f) ? worldScale.x : worldScale.x / ps.x,
            Mathf.Approximately(ps.y, 0f) ? worldScale.y : worldScale.y / ps.y,
            Mathf.Approximately(ps.z, 0f) ? worldScale.z : worldScale.z / ps.z);
        model.SetActive(true);

        // Animator de exploración (locomoción + root motion).
        var anim = model.GetComponentInChildren<Animator>(true);
        if (anim != null)
        {
            if (data != null && data.explorationAnimatorController != null)
                anim.runtimeAnimatorController = data.explorationAnimatorController;
            else
                Debug.LogWarning($"[SelectedCharacterLoader] '{model.name}' sin explorationAnimatorController " +
                                 "en su CharacterData — no habrá locomoción.");
            anim.applyRootMotion = true;
        }
        else
        {
            Debug.LogWarning($"[SelectedCharacterLoader] '{model.name}' no tiene Animator.");
        }

        // Puente root-motion → PlayerController (en el GO del Animator para recibir OnAnimatorMove).
        if (anim != null && pc != null)
        {
            var bridge = anim.GetComponent<CharacterAnimationController>();
            if (bridge == null) bridge = anim.gameObject.AddComponent<CharacterAnimationController>();
            bridge.Bind(anim, pc);
        }

        // Party para combate + controller de exploración consistente (Primary = elegido).
        if (ep != null && data != null) ep.SetParty(new[] { data }, anim);

        Debug.Log($"[SelectedCharacterLoader] '{model.name}' cargado como personaje de exploración (índice {idx}).");
    }

    static void SafeDeactivate(GameObject go, Transform player)
    {
        if (go == null) return;
        if (go == player.gameObject || go.GetComponent<PlayerController>() != null) return; // nunca apagar al Player
        go.SetActive(false);
    }

    GameObject GetModelForIndex(int idx)
    {
        switch (idx)
        {
            case 0: return KaelModel;
            case 1: return LyraModel;
            case 2: return VornModel;
            default: return null;
        }
    }
}
