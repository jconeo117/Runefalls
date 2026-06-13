using UnityEngine;

/// <summary>
/// TopDownCameraController
/// Cámara cenital fija (top-down) que sigue al jugador.
/// Rueda del ratón = zoom (acercar / alejar) con límites.
/// No se puede orbitar ni mirar libre: ángulo fijo, vista desde arriba.
///
/// Adjuntar a: La Main Camera.
/// </summary>
public class TopDownCameraController : MonoBehaviour
{
    [Header("=== OBJETIVO ===")]
    [Tooltip("El jugador al que sigue. Si lo dejás vacío, busca el WorldMapPlayerController automáticamente.")]
    public Transform target;

    [Header("=== ÁNGULO DE LA CÁMARA ===")]
    [Tooltip("90 = perfectamente cenital (mirando recto hacia abajo). 55-65 = inclinada estilo LoL.")]
    [Range(40f, 90f)]
    public float tiltAngle = 90f;

    [Header("=== ZOOM (rueda del ratón) ===")]
    [Tooltip("Distancia/altura actual de la cámara")]
    public float zoomDistance = 18f;

    [Tooltip("Zoom mínimo (lo más cerca que se puede acercar)")]
    public float minZoom = 8f;

    [Tooltip("Zoom máximo (lo más lejos que se puede alejar)")]
    public float maxZoom = 35f;

    [Tooltip("Sensibilidad de la rueda")]
    public float zoomSpeed = 60f;

    [Tooltip("Suavizado del zoom (mayor = más instantáneo)")]
    public float zoomSmooth = 10f;

    [Header("=== SEGUIMIENTO ===")]
    [Tooltip("Suavizado al seguir al jugador (mayor = más pegado)")]
    public float followSmooth = 10f;

    [Tooltip("Si está activo, la cámara se pega al jugador sin suavizado")]
    public bool instantFollow = false;

    [Tooltip("Desplazamiento extra del punto que mira (útil para descentrar un poco)")]
    public Vector3 lookOffset = Vector3.zero;

    private float _targetZoom;

    private void Awake()
    {
        _targetZoom = zoomDistance;

        if (target == null)
        {
            var player = FindObjectOfType<WorldMapPlayerController>();
            if (player != null) target = player.transform;
        }
    }

    private void Update()
    {
        HandleZoom();
    }

    private void LateUpdate()
    {
        FollowTarget();
    }

    // ─── ZOOM ────────────────────────────────────────────────────────────────────

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            // Scroll hacia arriba = acercar (menos distancia)
            _targetZoom -= scroll * zoomSpeed;
            _targetZoom = Mathf.Clamp(_targetZoom, minZoom, maxZoom);
        }

        zoomDistance = Mathf.Lerp(zoomDistance, _targetZoom, zoomSmooth * Time.deltaTime);
    }

    // ─── SEGUIMIENTO ─────────────────────────────────────────────────────────────

    private void FollowTarget()
    {
        if (target == null) return;

        // Dirección desde el objetivo hacia la cámara según el ángulo de inclinación
        float pitch = tiltAngle * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(0f, Mathf.Sin(pitch), -Mathf.Cos(pitch));

        Vector3 lookPoint = target.position + lookOffset;
        Vector3 desiredPos = lookPoint + dir * zoomDistance;

        transform.position = instantFollow
            ? desiredPos
            : Vector3.Lerp(transform.position, desiredPos, followSmooth * Time.deltaTime);

        // Rotación fija mirando hacia abajo con el ángulo elegido
        transform.rotation = Quaternion.Euler(tiltAngle, 0f, 0f);
    }
}