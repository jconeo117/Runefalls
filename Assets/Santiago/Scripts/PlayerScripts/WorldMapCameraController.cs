using UnityEngine;

/// <summary>
/// WorldMapCameraController
/// Cámara estilo Mount & Blade Warband - Mapa Mundi
/// 
/// Adjuntar a: La cámara principal (Camera.main) O a un GameObject vacío padre de la cámara
/// Requiere: Arrastrar el Transform del jugador al campo "target"
/// 
/// SETUP RECOMENDADO:
///   [CameraRig]          ← Este script va aquí
///       └─ [Camera]      ← La cámara real, hija del rig
/// </summary>
public class WorldMapCameraController : MonoBehaviour
{
    // ─── TARGET ──────────────────────────────────────────────────────────────────

    [Header("=== TARGET ===")]
    [Tooltip("Transform del jugador a seguir")]
    public Transform target;

    [Header("=== FOLLOW (Seguimiento) ===")]
    [Tooltip("Velocidad de seguimiento al jugador. Mayor = más pegada. 3-8 es lo ideal.")]
    [Range(1f, 20f)]
    public float followSpeed = 5f;

    [Tooltip("Cuando el jugador está quieto, la cámara espera un poco antes de centrar")]
    public float followDeadzone = 0.1f;

    [Tooltip("Offset de posición respecto al jugador (para anticipar el movimiento)")]
    public Vector3 followOffset = Vector3.zero;

    [Tooltip("Cuánto se adelanta la cámara en la dirección de movimiento")]
    [Range(0f, 5f)]
    public float lookAheadDistance = 2.5f;

    [Tooltip("Qué tan suave es el look-ahead")]
    [Range(1f, 15f)]
    public float lookAheadSpeed = 4f;

    [Header("=== ZOOM ===")]
    [Tooltip("Distancia mínima al objetivo (zoom máximo)")]
    public float minZoom = 5f;

    [Tooltip("Distancia máxima al objetivo (zoom mínimo)")]
    public float maxZoom = 30f;

    [Tooltip("Distancia inicial de zoom")]
    public float defaultZoom = 15f;

    [Tooltip("Sensibilidad de la rueda del mouse para el zoom")]
    public float zoomSensitivity = 3f;

    [Tooltip("Suavidad del zoom (mayor = más suave)")]
    [Range(1f, 20f)]
    public float zoomSmoothing = 8f;

    [Header("=== ÁNGULO DE LA CÁMARA ===")]
    [Tooltip("Ángulo de inclinación vertical de la cámara (pitch). 45-70 funciona bien para mapa")]
    [Range(20f, 85f)]
    public float pitchAngle = 55f;

    [Tooltip("Rotación horizontal inicial (yaw). 0 = Norte")]
    public float yawAngle = 0f;

    [Tooltip("¿Permitir rotar la cámara con click del medio?")]
    public bool allowMiddleMouseRotation = true;

    [Tooltip("Sensibilidad de rotación con el mouse")]
    public float rotationSensitivity = 0.5f;

    [Tooltip("Suavidad de la rotación")]
    [Range(1f, 20f)]
    public float rotationSmoothing = 8f;

    [Header("=== ORBIT (Click Izquierdo - estilo Blender) ===")]
    [Tooltip("Mantener click izquierdo + arrastrar para orbitar alrededor del jugador")]
    public bool allowLeftClickOrbit = true;

    [Tooltip("Sensibilidad del orbit horizontal (yaw)")]
    [Range(0.1f, 5f)]
    public float orbitYawSensitivity = 1.8f;

    [Tooltip("Sensibilidad del orbit vertical (pitch)")]
    [Range(0.1f, 5f)]
    public float orbitPitchSensitivity = 1.2f;

    [Tooltip("Ángulo mínimo de pitch en orbit (evita que la cámara se voltee)")]
    [Range(5f, 40f)]
    public float orbitPitchMin = 10f;

    [Tooltip("Ángulo máximo de pitch en orbit (evita que mire desde abajo del suelo)")]
    [Range(50f, 89f)]
    public float orbitPitchMax = 85f;

    [Tooltip("Cuántos píxeles debe moverse el mouse antes de considerar que es orbit (evita clicks accidentales)")]
    [Range(2f, 15f)]
    public float orbitDragThreshold = 5f;

    [Tooltip("Al soltar el click, la cámara vuelve suavemente al pitch original")]
    public bool resetPitchOnRelease = false;

    [Tooltip("Velocidad de retorno al pitch original al soltar (si resetPitchOnRelease = true)")]
    [Range(1f, 10f)]
    public float pitchResetSpeed = 3f;

    [Header("=== SACUDIDA (Screen Shake) ===")]
    [Tooltip("¿Habilitar sistema de screen shake para eventos?")]
    public bool enableScreenShake = true;

    [Header("=== BORDES DEL MAPA (Opcional) ===")]
    [Tooltip("Limitar la cámara a los bordes del mapa")]
    public bool useBounds = false;
    public Bounds mapBounds = new Bounds(Vector3.zero, new Vector3(200f, 0f, 200f));

    // ─── Internos ────────────────────────────────────────────────────────────────

    private Vector3 _currentPosition;         // Posición suavizada actual del rig
    private float _currentZoom;             // Zoom suavizado actual
    private float _targetZoom;              // Zoom objetivo
    private float _currentYaw;             // Yaw suavizado
    private float _targetYaw;              // Yaw objetivo
    private float _currentPitch;           // Pitch suavizado (para orbit vertical)
    private float _targetPitch;            // Pitch objetivo
    private float _defaultPitch;           // Pitch base del Inspector

    // ─── Orbit (click izquierdo) ─────────────────────────────────────────────────
    private bool _isOrbiting = false;
    private bool _orbitDragStarted = false;
    private Vector2 _orbitMouseOrigin = Vector2.zero;

    private Vector3 _lookAheadOffset;         // Offset de anticipación
    private Vector3 _shakeOffset;             // Offset del screen shake
    private float _shakeMagnitude;
    private float _shakeDuration;
    private float _shakeTimer;

    // Cache del PlayerController para leer velocidad
    private WorldMapPlayerController _playerController;

    // La cámara hija (si este script está en el Rig)
    private Camera _camera;

    // ─── SINGLETON para llamar Shake desde cualquier lado ────────────────────────
    public static WorldMapCameraController Instance { get; private set; }

    // ────────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;

        _currentZoom = defaultZoom;
        _targetZoom = defaultZoom;
        _currentYaw = yawAngle;
        _targetYaw = yawAngle;
        _defaultPitch = pitchAngle;
        _currentPitch = pitchAngle;
        _targetPitch = pitchAngle;
        _camera = GetComponentInChildren<Camera>();

        if (target != null)
        {
            _currentPosition = target.position;
            _playerController = target.GetComponent<WorldMapPlayerController>();
        }

        // Aplicar posición inicial inmediatamente (sin lerp en el primer frame)
        ApplyCameraTransform(immediate: true);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        HandleZoomInput();
        HandleOrbitInput();
        HandleRotationInput();
        HandleFollowAndLookAhead();
        HandleScreenShake();

        ApplyCameraTransform();
    }

    // ─── ZOOM ────────────────────────────────────────────────────────────────────

    private void HandleZoomInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            _targetZoom -= scroll * zoomSensitivity * _targetZoom * 0.3f; // Zoom proporcional (se siente natural)
            _targetZoom = Mathf.Clamp(_targetZoom, minZoom, maxZoom);
        }

        // Suavizar el zoom con SmoothDamp para que no sea lineal
        _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, zoomSmoothing * Time.deltaTime);
    }

    // ─── ORBIT (CLICK IZQUIERDO - ESTILO BLENDER) ────────────────────────────────

    private void HandleOrbitInput()
    {
        if (!allowLeftClickOrbit) return;

        // ── Inicio del click ────────────────────────────────────────────────────
        if (Input.GetMouseButtonDown(0))
        {
            _isOrbiting = true;
            _orbitDragStarted = false;
            _orbitMouseOrigin = Input.mousePosition;
        }

        // ── Sosteniendo el click ────────────────────────────────────────────────
        if (_isOrbiting && Input.GetMouseButton(0))
        {
            Vector2 currentMousePos = Input.mousePosition;
            Vector2 delta = currentMousePos - _orbitMouseOrigin;

            // Comprobar si superamos el threshold (evita orbit en clicks normales)
            if (!_orbitDragStarted && delta.magnitude > orbitDragThreshold)
            {
                _orbitDragStarted = true;
            }

            if (_orbitDragStarted)
            {
                // Delta this frame (raw)
                float rawDeltaX = Input.GetAxis("Mouse X");
                float rawDeltaY = Input.GetAxis("Mouse Y");

                // Orbit horizontal (Yaw) — arrastrar izquierda/derecha
                _targetYaw += rawDeltaX * orbitYawSensitivity * 100f * Time.deltaTime;

                // Orbit vertical (Pitch) — arrastrar arriba/abajo
                // Invertido: arrastrar hacia arriba baja la cámara (más picado)
                _targetPitch -= rawDeltaY * orbitPitchSensitivity * 100f * Time.deltaTime;
                _targetPitch = Mathf.Clamp(_targetPitch, orbitPitchMin, orbitPitchMax);
            }
        }

        // ── Soltar el click ─────────────────────────────────────────────────────
        if (Input.GetMouseButtonUp(0))
        {
            _isOrbiting = false;
            _orbitDragStarted = false;

            // Opcional: volver al pitch original al soltar
            if (resetPitchOnRelease)
                _targetPitch = _defaultPitch;
        }

        // ── Suavizar pitch ──────────────────────────────────────────────────────
        _currentPitch = Mathf.Lerp(
            _currentPitch,
            _targetPitch,
            rotationSmoothing * Time.deltaTime
        );
    }

    // ─── ROTACIÓN (CLICK MEDIO / Q·E) ────────────────────────────────────────────

    private void HandleRotationInput()
    {
        if (!allowMiddleMouseRotation) return;

        if (Input.GetMouseButton(2)) // Middle click sostenido
        {
            float mouseX = Input.GetAxis("Mouse X");
            _targetYaw += mouseX * rotationSensitivity * 100f * Time.deltaTime;
        }

        // También con Q/E
        if (Input.GetKey(KeyCode.Q)) _targetYaw -= 60f * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) _targetYaw += 60f * Time.deltaTime;

        _currentYaw = Mathf.LerpAngle(_currentYaw, _targetYaw, rotationSmoothing * Time.deltaTime);
    }

    // ─── SEGUIMIENTO CON LOOK-AHEAD ──────────────────────────────────────────────

    private void HandleFollowAndLookAhead()
    {
        // Leer velocidad del jugador si está disponible
        Vector3 playerVelocity = Vector3.zero;
        if (_playerController != null)
            playerVelocity = _playerController.Velocity;

        // Calcular offset de anticipación (look-ahead)
        // La cámara se adelanta suavemente en la dirección de movimiento
        Vector3 targetLookAhead = Vector3.zero;
        if (playerVelocity.magnitude > 0.1f)
        {
            targetLookAhead = playerVelocity.normalized
                              * Mathf.Clamp(playerVelocity.magnitude / 8f, 0f, 1f) // Normalizado por velocidad max
                              * lookAheadDistance;
        }

        _lookAheadOffset = Vector3.Lerp(
            _lookAheadOffset,
            targetLookAhead,
            lookAheadSpeed * Time.deltaTime
        );

        // Posición objetivo = jugador + offset fijo + look-ahead
        Vector3 desiredPosition = target.position + followOffset + _lookAheadOffset;

        // Clamp a los límites del mapa si está habilitado
        if (useBounds)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, mapBounds.min.x, mapBounds.max.x);
            desiredPosition.z = Mathf.Clamp(desiredPosition.z, mapBounds.min.z, mapBounds.max.z);
        }

        // Suavizar la posición con lerp
        // Usamos una velocidad de seguimiento que se adapta: más rápido cuando está lejos
        float distance = Vector3.Distance(_currentPosition, desiredPosition);
        float adaptiveSpeed = followSpeed * (1f + distance * 0.2f); // Acelera si está muy lejos (catch-up)

        _currentPosition = Vector3.Lerp(
            _currentPosition,
            desiredPosition,
            Mathf.Clamp01(adaptiveSpeed * Time.deltaTime)
        );
    }

    // ─── SCREEN SHAKE ────────────────────────────────────────────────────────────

    private void HandleScreenShake()
    {
        if (!enableScreenShake || _shakeTimer <= 0f)
        {
            _shakeOffset = Vector3.zero;
            return;
        }

        _shakeTimer -= Time.deltaTime;

        // Decaimiento suave
        float progress = _shakeTimer / _shakeDuration;
        float currentMagnitude = _shakeMagnitude * progress;

        _shakeOffset = new Vector3(
            (Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f) * 2f,
            (Mathf.PerlinNoise(0f, Time.time * 30f) - 0.5f) * 2f,
            0f
        ) * currentMagnitude;
    }

    /// <summary>
    /// Llamar desde cualquier script para sacudir la cámara.
    /// Ej: WorldMapCameraController.Instance.Shake(0.3f, 0.4f);
    /// </summary>
    public void Shake(float magnitude, float duration)
    {
        if (!enableScreenShake) return;
        _shakeMagnitude = magnitude;
        _shakeDuration = duration;
        _shakeTimer = duration;
    }

    // ─── APLICAR TRANSFORM FINAL ─────────────────────────────────────────────────

    private void ApplyCameraTransform(bool immediate = false)
    {
        // Calcular la posición del rig (desde donde orbita la cámara)
        // La cámara está a "_currentZoom" de distancia, elevada según pitchAngle

        Quaternion rigRotation = Quaternion.Euler(0f, _currentYaw, 0f);

        // Posición del rig en el suelo (sobre el jugador)
        Vector3 rigPosition = _currentPosition;

        if (immediate)
        {
            transform.position = rigPosition;
            transform.rotation = rigRotation;
        }
        else
        {
            transform.position = rigPosition;
            transform.rotation = rigRotation;
        }

        // Posicionar la cámara hija (si existe el setup Rig > Camera)
        if (_camera != null)
        {
            // Offset 3D basado en pitch y zoom
            float pitchRad = _currentPitch * Mathf.Deg2Rad;
            float camY = _currentZoom * Mathf.Sin(pitchRad);
            float camZ = -_currentZoom * Mathf.Cos(pitchRad);

            Vector3 localCamPos = new Vector3(0f, camY, camZ) + _shakeOffset;

            if (immediate)
                _camera.transform.localPosition = localCamPos;
            else
                _camera.transform.localPosition = Vector3.Lerp(
                    _camera.transform.localPosition,
                    localCamPos,
                    zoomSmoothing * Time.deltaTime
                );

            // La cámara siempre mira hacia el rig
            Quaternion camRotation = Quaternion.Euler(_currentPitch, 0f, 0f);
            _camera.transform.localRotation = Quaternion.Slerp(
                _camera.transform.localRotation,
                camRotation,
                rotationSmoothing * Time.deltaTime
            );
        }
        else
        {
            // Setup simple: script directo en la cámara
            float pitchRad = _currentPitch * Mathf.Deg2Rad;
            float camY = _currentZoom * Mathf.Sin(pitchRad);
            float camZ = -_currentZoom * Mathf.Cos(pitchRad);

            // Rotar por el yaw actual
            Vector3 offset = rigRotation * new Vector3(0f, camY, camZ);
            Vector3 targetPos = rigPosition + offset + _shakeOffset;

            if (immediate)
                transform.position = targetPos;
            else
                transform.position = Vector3.Lerp(
                    transform.position,
                    targetPos,
                    zoomSmoothing * Time.deltaTime
                );

            transform.LookAt(rigPosition);
        }
    }

    // ─── GIZMOS ──────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (target == null) return;

        // Dibujar el área de seguimiento
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawWireSphere(target.position, lookAheadDistance);

        // Dibujar los límites del mapa
        if (useBounds)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireCube(mapBounds.center, mapBounds.size);
        }

        // Línea desde rig a cámara
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, Application.isPlaying ? transform.position : target.position);
    }
#endif
}