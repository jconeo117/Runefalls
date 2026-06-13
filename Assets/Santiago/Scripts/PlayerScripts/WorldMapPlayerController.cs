using UnityEngine;

/// <summary>
/// WorldMapPlayerController
/// Movimiento estilo League of Legends - Mapa Mundi
/// Click DERECHO sobre el suelo para mover al personaje hacia ese punto.
/// Mantené el click derecho para que siga al cursor.
///
/// Adjuntar a: El GameObject del jugador (el icono/avatar en el mapa)
/// </summary>
public class WorldMapPlayerController : MonoBehaviour
{
    [Header("=== MOVIMIENTO (Click derecho estilo LoL) ===")]
    [Tooltip("Velocidad máxima de movimiento")]
    public float moveSpeed = 8f;

    [Tooltip("Qué tan rápido acelera hasta la velocidad máxima")]
    public float acceleration = 12f;

    [Tooltip("Qué tan rápido frena al llegar / soltar")]
    public float deceleration = 16f;

    [Tooltip("Distancia a la que se considera que ya llegó al destino")]
    public float stoppingDistance = 0.15f;

    [Header("=== INPUT ===")]
    [Tooltip("Botón del mouse para mover (1 = derecho, como LoL). 0 = izquierdo.")]
    public int moveMouseButton = 1;

    [Tooltip("Layer(s) del suelo/mapa sobre los que se puede hacer click para mover")]
    public LayerMask groundLayer = ~0;

    [Header("=== MARCADOR DE DESTINO (opcional) ===")]
    [Tooltip("Prefab que aparece donde hiciste click (un círculo, flecha, etc.). Puede quedar vacío.")]
    public GameObject clickMarkerPrefab;

    [Tooltip("Cuánto dura el marcador en pantalla")]
    public float markerLifetime = 1f;

    [Header("=== ROTACIÓN ===")]
    [Tooltip("Velocidad de rotación para que el personaje mire hacia donde se mueve")]
    public float rotationSpeed = 720f;

    [Tooltip("Rotar el personaje según la dirección de movimiento")]
    public bool rotateTowardMovement = true;

    [Header("=== INCLINACIÓN (Bank) ===")]
    [Tooltip("Inclinación lateral al girar. Poner en 0 para desactivar.")]
    public float bankAngle = 12f;

    [Tooltip("Velocidad de la inclinación")]
    public float bankSpeed = 5f;

    [Header("=== SQUASH & STRETCH ===")]
    [Tooltip("Aplica leve squash/stretch al acelerar/frenar para dar vida")]
    public bool useSquashStretch = true;

    [Range(0f, 0.3f)]
    public float squashStretchAmount = 0.08f;

    [Header("=== TERRENO ===")]
    [Tooltip("Si el jugador debe seguir la altura del terreno (mapas con relieve)")]
    public bool alignToTerrain = false;

    [Tooltip("Layer mask del terreno (para la altura)")]
    public LayerMask terrainLayer = ~0;

    [Tooltip("Altura sobre el terreno")]
    public float terrainOffset = 0.1f;

    // ─── Internos ───────────────────────────────────────────────────────────────
    private Vector3 _velocity = Vector3.zero;
    private Vector3 _inputDirection = Vector3.zero;
    private Vector3 _smoothedInput = Vector3.zero;
    private Vector3 _originalScale;
    private float _currentBankAngle = 0f;
    private bool _isMoving = false;

    private Vector3 _destination;
    private bool _hasDestination = false;

    private Camera _cam;

    // ── Propiedades públicas ──
    public Vector3 Velocity => _velocity;
    public bool IsMoving => _isMoving;
    public float SpeedNormalized => _velocity.magnitude / moveSpeed;

    // ────────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _originalScale = transform.localScale;
        _cam = Camera.main;
    }

    private void Update()
    {
        // Recuperar la cámara si por algún motivo no estaba lista en Awake
        if (_cam == null) _cam = Camera.main;

        ReadClickInput();
        ApplyMovement();

        if (rotateTowardMovement) HandleRotation();
        if (useSquashStretch) HandleSquashStretch();
        if (alignToTerrain) AlignToTerrain();
    }

    // ─── INPUT (click para mover) ────────────────────────────────────────────────

    private void ReadClickInput()
    {
        if (_cam == null) return;

        // Click NUEVO (tap): fija destino + crea el marcador
        if (Input.GetMouseButtonDown(moveMouseButton))
        {
            if (RaycastGround(out Vector3 point))
            {
                SetDestination(point);
                SpawnMarker(point);
            }
        }
        // Mantener APRETADO: solo sigue al cursor (sin volver a crear marcador)
        else if (Input.GetMouseButton(moveMouseButton))
        {
            if (RaycastGround(out Vector3 point))
                SetDestination(point);
        }
    }

    private bool RaycastGround(out Vector3 point)
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            point = hit.point;
            return true;
        }
        point = Vector3.zero;
        return false;
    }

    public void SetDestination(Vector3 worldPoint)
    {
        _destination = worldPoint;
        _hasDestination = true;
    }

    private int _clickCount = 0;

    private void SpawnMarker(Vector3 point)
    {
        _clickCount++;

        if (clickMarkerPrefab == null)
        {
            Debug.LogWarning(
                $"[Marker] Click #{_clickCount}: clickMarkerPrefab es null. " +
                "Si funcionó la primera vez y después no, casi seguro asignaste un OBJETO DE LA ESCENA " +
                "(arrastrado desde la Hierarchy) que se autodestruyó. Asigná el PREFAB desde la ventana Project.");
            return;
        }

        Debug.Log($"[Marker] Click #{_clickCount}: instanciando '{clickMarkerPrefab.name}' en {point}");

        // Mantener la rotación original del prefab (importante para efectos orientados)
        GameObject marker = Instantiate(clickMarkerPrefab, point, clickMarkerPrefab.transform.rotation);
        marker.SetActive(true); // por si el prefab estaba desactivado

        // Forzar que TODOS los sistemas de partículas se reproduzcan desde cero.
        var systems = marker.GetComponentsInChildren<ParticleSystem>(true);
        Debug.Log($"[Marker] Click #{_clickCount}: encontrados {systems.Length} ParticleSystem(s).");

        float particleLife = 0f;
        foreach (var ps in systems)
        {
            ps.Clear(true);
            ps.Play(true);
            float dur = ps.main.duration + ps.main.startLifetime.constantMax;
            if (dur > particleLife) particleLife = dur;
        }

        // Evitar que el marcador "se coma" los clicks siguientes desactivando sus colliders.
        foreach (var col in marker.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        // Destruir: markerLifetime si lo definiste; si no, la duración del efecto; si no, 3s.
        float life = markerLifetime > 0f ? markerLifetime
                   : (particleLife > 0f ? particleLife : 3f);
        Destroy(marker, life);
    }

    // ─── MOVIMIENTO ──────────────────────────────────────────────────────────────

    private void ApplyMovement()
    {
        // Calcular dirección hacia el destino (ignorando la altura)
        bool hasInput = false;

        if (_hasDestination)
        {
            Vector3 toDest = _destination - transform.position;
            toDest.y = 0f;

            if (toDest.magnitude > stoppingDistance)
            {
                _inputDirection = toDest.normalized;
                hasInput = true;
            }
            else
            {
                // Llegó: detenerse
                _hasDestination = false;
                _inputDirection = Vector3.zero;
            }
        }

        _isMoving = hasInput;

        if (hasInput)
        {
            _smoothedInput = Vector3.MoveTowards(
                _smoothedInput,
                _inputDirection,
                acceleration * Time.deltaTime
            );
        }
        else
        {
            _smoothedInput = Vector3.MoveTowards(
                _smoothedInput,
                Vector3.zero,
                deceleration * Time.deltaTime
            );
        }

        // Curva de aceleración suave (easing) para que se sienta con peso
        float speedCurve = Mathf.SmoothStep(0f, 1f, _smoothedInput.magnitude);
        _velocity = _smoothedInput.normalized * (speedCurve * moveSpeed);

        transform.position += _velocity * Time.deltaTime;
    }

    // ─── ROTACIÓN ────────────────────────────────────────────────────────────────

    private void HandleRotation()
    {
        if (_velocity.magnitude < 0.1f) return;

        Quaternion targetRotation = Quaternion.LookRotation(_velocity.normalized, Vector3.up);

        // Inclinación lateral (banking) al girar
        float bankInput = 0f;
        if (_isMoving)
        {
            Vector3 currentForward = transform.forward;
            Vector3 targetForward = _velocity.normalized;
            bankInput = Vector3.Cross(currentForward, targetForward).y;
        }

        _currentBankAngle = Mathf.Lerp(
            _currentBankAngle,
            -bankInput * bankAngle,
            bankSpeed * Time.deltaTime
        );

        Quaternion bankRotation = Quaternion.AngleAxis(_currentBankAngle, Vector3.forward);
        targetRotation = targetRotation * bankRotation;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    // ─── SQUASH & STRETCH ────────────────────────────────────────────────────────

    private void HandleSquashStretch()
    {
        float speed01 = _velocity.magnitude / moveSpeed;

        float stretchY = 1f + (speed01 * squashStretchAmount);
        float squashXZ = 1f - (speed01 * squashStretchAmount * 0.5f);

        Vector3 targetScale = new Vector3(
            _originalScale.x * squashXZ,
            _originalScale.y * stretchY,
            _originalScale.z * squashXZ
        );

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            10f * Time.deltaTime
        );
    }

    // ─── ALINEACIÓN AL TERRENO ───────────────────────────────────────────────────

    private void AlignToTerrain()
    {
        if (Physics.Raycast(
            transform.position + Vector3.up * 2f,
            Vector3.down,
            out RaycastHit hit,
            10f,
            terrainLayer))
        {
            Vector3 pos = transform.position;
            pos.y = hit.point.y + terrainOffset;
            transform.position = pos;
        }
    }

    // ─── GIZMOS ──────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = Color.green;
        UnityEditor.Handles.DrawLine(transform.position, transform.position + _velocity);

        if (_hasDestination)
        {
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.DrawWireDisc(_destination, Vector3.up, 0.5f);
        }
    }
#endif
}