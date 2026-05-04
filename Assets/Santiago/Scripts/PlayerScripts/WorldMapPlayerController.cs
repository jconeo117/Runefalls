using UnityEngine;

/// <summary>
/// WorldMapPlayerController
/// Movimiento estilo Mount & Blade Warband - Mapa Mundi
/// 
/// Adjuntar a: El GameObject del jugador (el icono/avatar en el mapa)
/// Requiere: Rigidbody (Is Kinematic = true) o mover con Transform directamente
/// </summary>
public class WorldMapPlayerController : MonoBehaviour
{
    [Header("=== VELOCIDAD ===")]
    [Tooltip("Velocidad máxima de movimiento en el mapa")]
    public float moveSpeed = 8f;

    [Tooltip("Qué tan rápido acelera hasta la velocidad máxima (mayor = más responsivo)")]
    public float acceleration = 12f;

    [Tooltip("Qué tan rápido frena al soltar las teclas (mayor = frena más rápido)")]
    public float deceleration = 16f;

    [Header("=== ROTACIÓN ===")]
    [Tooltip("Velocidad de rotación para que el personaje mire hacia donde se mueve")]
    public float rotationSpeed = 720f;

    [Tooltip("Rotar el personaje según la dirección de movimiento")]
    public bool rotateTowardMovement = true;

    [Header("=== INCLINACIÓN (Bank) ===")]
    [Tooltip("Inclinación lateral al girar (como M&B). Poner en 0 para desactivar.")]
    public float bankAngle = 12f;

    [Tooltip("Velocidad de la inclinación")]
    public float bankSpeed = 5f;

    [Header("=== SQUASH & STRETCH ===")]
    [Tooltip("Aplica leve squash/stretch al acelerar/frenar para dar vida")]
    public bool useSquashStretch = true;

    [Tooltip("Intensidad del squash & stretch")]
    [Range(0f, 0.3f)]
    public float squashStretchAmount = 0.08f;

    [Header("=== TERRENO ===")]
    [Tooltip("Si el jugador debe seguir la normal del terreno (para mapas con relieve)")]
    public bool alignToTerrain = false;

    [Tooltip("Layer mask del terreno")]
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

    // Referencia a la cámara para que WASD sea relativo a la vista
    private Camera _cam;

    // ── Propiedades públicas (para que la cámara u otros scripts puedan leer) ──
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
        ReadInput();
        ApplyMovement();

        if (rotateTowardMovement) HandleRotation();
        if (useSquashStretch) HandleSquashStretch();
        if (alignToTerrain) AlignToTerrain();
    }

    // ─── INPUT ───────────────────────────────────────────────────────────────────

    private void ReadInput()
    {
        float h = 0f;
        float v = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;

        // Dirección relativa a la cámara (ignorando eje Y de la cámara)
        Vector3 camForward = _cam != null
            ? Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up).normalized
            : Vector3.forward;
        Vector3 camRight = _cam != null
            ? Vector3.ProjectOnPlane(_cam.transform.right, Vector3.up).normalized
            : Vector3.right;

        _inputDirection = (camForward * v + camRight * h);

        // Limitar magnitud a 1 para que el diagonal no sea más rápido
        if (_inputDirection.magnitude > 1f)
            _inputDirection.Normalize();
    }

    // ─── MOVIMIENTO ──────────────────────────────────────────────────────────────

    private void ApplyMovement()
    {
        bool hasInput = _inputDirection.magnitude > 0.01f;
        _isMoving = hasInput;

        if (hasInput)
        {
            // Suavizar el input para arranque gradual
            _smoothedInput = Vector3.MoveTowards(
                _smoothedInput,
                _inputDirection,
                acceleration * Time.deltaTime
            );
        }
        else
        {
            // Frenar suavemente
            _smoothedInput = Vector3.MoveTowards(
                _smoothedInput,
                Vector3.zero,
                deceleration * Time.deltaTime
            );
        }

        // Velocidad final con curva de aceleración suave (easing)
        float speedCurve = _smoothedInput.magnitude; // 0..1
        // Aplicar ease-in-out para que la aceleración se sienta más "weighty"
        speedCurve = Mathf.SmoothStep(0f, 1f, speedCurve);

        _velocity = _smoothedInput.normalized * (speedCurve * moveSpeed);

        // Mover el transform
        transform.position += _velocity * Time.deltaTime;
    }

    // ─── ROTACIÓN ────────────────────────────────────────────────────────────────

    private void HandleRotation()
    {
        if (_velocity.magnitude < 0.1f) return;

        // Rotación hacia la dirección de movimiento
        Quaternion targetRotation = Quaternion.LookRotation(_velocity.normalized, Vector3.up);

        // Calcular inclinación lateral (banking al girar)
        float bankInput = 0f;
        if (_isMoving)
        {
            // Producto cruzado entre forward actual y forward objetivo → cuánto estamos girando
            Vector3 currentForward = transform.forward;
            Vector3 targetForward = _velocity.normalized;
            bankInput = Vector3.Cross(currentForward, targetForward).y;
        }

        _currentBankAngle = Mathf.Lerp(
            _currentBankAngle,
            -bankInput * bankAngle,
            bankSpeed * Time.deltaTime
        );

        // Combinar rotación de movimiento + banking
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
        float speed01 = _velocity.magnitude / moveSpeed; // 0..1

        // Al acelerar: ligeramente aplastado en X/Z, estirado en Y
        // Al frenar:   ligeramente estirado en X/Z, aplastado en Y
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
            // Posición Y sobre el terreno
            Vector3 pos = transform.position;
            pos.y = hit.point.y + terrainOffset;
            transform.position = pos;

            // Opcional: inclinar el personaje según la normal del terreno
            // Quaternion terrainAlign = Quaternion.FromToRotation(Vector3.up, hit.normal);
            // transform.rotation = terrainAlign * transform.rotation;
        }
    }

    // ─── GIZMOS (para debug en editor) ───────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Mostrar velocidad
        UnityEditor.Handles.color = Color.green;
        UnityEditor.Handles.DrawLine(
            transform.position,
            transform.position + _velocity
        );

        // Mostrar dirección de input
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawLine(
            transform.position,
            transform.position + _inputDirection
        );
    }
#endif
}