using UnityEngine;

public class PlayerElevator : MonoBehaviour
{
    [Header("Objeto que se desactiva al entrar el player")]
    public GameObject objectToDeactivate;   // acá ponés Jail1 en una zona, Jail2 en la otra

    [Header("Tag del player")]
    public string playerTag = "Player";

    void OnTriggerEnter(Collider other)
    {
        // si lo que entró a la zona es el player, desactivamos el objeto
        if (other.CompareTag(playerTag))
        {
            if (objectToDeactivate != null)
                objectToDeactivate.SetActive(false);
            else
                Debug.LogWarning("JailTrigger: no asignaste el objeto a desactivar en " + gameObject.name);
        }
    }
}