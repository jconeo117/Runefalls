using UnityEngine;

public class TextScroller : MonoBehaviour
{
    [Header("El objeto que baja")]
    public GameObject Lines;

    [Header("Velocidad de bajada")]
    public float speed = 3f;

    void Update()
    {
        if (Lines == null) return;

        // le bajamos la Y
        Vector3 p = Lines.transform.position;
        p.y -= speed * Time.deltaTime;
        Lines.transform.position = p;
    }
}