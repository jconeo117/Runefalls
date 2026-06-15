using UnityEngine;

public class SelectedCharacterLoader : MonoBehaviour
{
    [Header("Modelos en la escena (mismo orden que el gacha)")]
    public GameObject KaelModel;   // índice 0 (carta izquierda)
    public GameObject LyraModel;   // índice 1 (carta del medio)
    public GameObject VornModel;   // índice 2 (carta derecha)

    void Start()
    {
        // apagamos los 3 por las dudas (aunque ya estén apagados en la escena)
        if (KaelModel != null) KaelModel.SetActive(false);
        if (LyraModel != null) LyraModel.SetActive(false);
        if (VornModel != null) VornModel.SetActive(false);

        // leemos qué salió en el gacha (sobrevivió al cambio de escena porque es static)
        int idx = CardDrawAnimation.SelectedCharacterIndex;

        GameObject elegido = GetModelForIndex(idx);

        if (elegido != null)
        {
            elegido.SetActive(true);
            Debug.Log("Modelo activado: " + elegido.name + " (índice " + idx + ")");
        }
        else
        {
            // esto solo pasaría si entrás a la escena sin pasar por el gacha (índice -1)
            Debug.LogWarning("SelectedCharacterLoader: índice inválido (" + idx + "). " +
                             "¿Entraste sin pasar por el gacha? No se activó ningún modelo.");
        }
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