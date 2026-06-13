using UnityEngine;

/// <summary>
/// Controlador de movimiento en tercera persona profesional.
/// Usa CharacterController para colisiones limpias.
/// Requiere una cámara con tag "MainCamera" (idealmente con Cinemachine).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Velocidad al caminar")]
    public float walkSpeed = 4f;
    [Tooltip("Velocidad al correr (Shift)")]
    public float sprintSpeed = 8f;
    [Tooltip("Qué tan rápido acelera/desacelera. Más alto = más responsivo")]
    public float speedChangeRate = 10f;
    [Tooltip("Suavizado de rotación del personaje (más bajo = más rápido)")]
    [Range(0.0f, 0.3f)] public float rotationSmoothTime = 0.08f;

    [Header("Salto y Gravedad")]
    [Tooltip("Altura máxima del salto en metros")]
    public float jumpHeight = 1.4f;
    [Tooltip("Gravedad personalizada (más negativa = caída más rápida)")]
    public float gravity = -22f;
    [Tooltip("Tiempo entre saltos")]
    public float jumpCooldown = 0.1f;
    [Tooltip("Tiempo de gracia después de caer de una plataforma")]
    public float coyoteTime = 0.15f;
    [Tooltip("Tiempo que se 'recuerda' un input de salto antes de tocar suelo")]
    public float jumpBufferTime = 0.15f;

    [Header("Detección de Suelo")]
    [Tooltip("Offset de la esfera de ground check desde la base")]
    public float groundedOffset = -0.08f;
    [Tooltip("Radio de la esfera de ground check (similar al radio del CharacterController)")]
    public float groundedRadius = 0.28f;
    [Tooltip("Capas que cuentan como suelo")]
    public LayerMask groundLayers = ~0;

    // Referencias
    private CharacterController controller;
    private Transform cam;

    // Estado de movimiento
    private float currentSpeed;
    private float targetRotation;
    private float rotationVelocity;
    private Vector3 verticalVelocity;

    // Timers
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private float jumpCooldownCounter;

    public bool IsGrounded { get; private set; }
    public float CurrentSpeed => new Vector2(controller.velocity.x, controller.velocity.z).magnitude;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (Camera.main != null)
            cam = Camera.main.transform;
        else
            Debug.LogWarning("[ThirdPersonController] No se encontró Camera.main. Asignale el tag MainCamera a tu cámara.");

        // Bloqueamos el cursor para una experiencia tipo juego
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        GroundCheck();
        HandleJumpAndGravity();
        HandleMovement();
    }

    private void GroundCheck()
    {
        Vector3 spherePosition = new Vector3(transform.position.x,
                                              transform.position.y + groundedOffset,
                                              transform.position.z);
        IsGrounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers,
                                          QueryTriggerInteraction.Ignore);

        // Coyote time: damos un pequeño margen tras dejar el suelo
        if (IsGrounded)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter -= Time.deltaTime;
    }

    private void HandleJumpAndGravity()
    {
        // Jump buffer: si el jugador pulsa salto justo antes de aterrizar, lo recordamos
        if (Input.GetKeyDown(KeyCode.Space))
            jumpBufferCounter = jumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;

        jumpCooldownCounter -= Time.deltaTime;

        if (IsGrounded)
        {
            // Mantener al personaje pegado al suelo evita "rebotes" raros
            if (verticalVelocity.y < 0f)
                verticalVelocity.y = -2f;

            // Salto si hay buffer activo, coyote time válido y no estamos en cooldown
            if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f && jumpCooldownCounter <= 0f)
            {
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpBufferCounter = 0f;
                jumpCooldownCounter = jumpCooldown;
            }
        }

        // Aplicar gravedad siempre
        verticalVelocity.y += gravity * Time.deltaTime;
    }

    private void HandleMovement()
    {
        // --- Input ---
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(h, v);
        // Normalizamos solo si supera 1 (para que las teclas sigan funcionando igual que un stick)
        if (input.sqrMagnitude > 1f) input.Normalize();

        bool isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;

        // Si no hay input, la velocidad objetivo es 0
        if (input == Vector2.zero) targetSpeed = 0f;

        // --- Suavizado de velocidad (aceleración/desaceleración) ---
        float currentHorizontalSpeed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;
        const float speedOffset = 0.1f;

        if (currentHorizontalSpeed < targetSpeed - speedOffset ||
            currentHorizontalSpeed > targetSpeed + speedOffset)
        {
            currentSpeed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * input.magnitude,
                                       Time.deltaTime * speedChangeRate);
            currentSpeed = Mathf.Round(currentSpeed * 1000f) / 1000f;
        }
        else
        {
            currentSpeed = targetSpeed;
        }

        // --- Dirección relativa a la cámara ---
        Vector3 inputDirection = new Vector3(input.x, 0f, input.y).normalized;
        Vector3 moveDirection = Vector3.zero;

        if (input != Vector2.zero && cam != null)
        {
            // Rotamos el input según el yaw de la cámara
            targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg
                             + cam.eulerAngles.y;

            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation,
                                                    ref rotationVelocity, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, rotation, 0f);

            moveDirection = Quaternion.Euler(0f, targetRotation, 0f) * Vector3.forward;
        }

        // --- Aplicar movimiento ---
        Vector3 finalVelocity = moveDirection.normalized * currentSpeed + verticalVelocity;
        controller.Move(finalVelocity * Time.deltaTime);
    }

    // Visualización del ground check en el editor para tunearlo cómodo
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsGrounded ? new Color(0f, 1f, 0f, 0.4f) : new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawSphere(new Vector3(transform.position.x,
                                       transform.position.y + groundedOffset,
                                       transform.position.z),
                          groundedRadius);
    }
}