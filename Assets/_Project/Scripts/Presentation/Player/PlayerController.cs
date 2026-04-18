using System.Collections;
using UnityEngine;
using Runefall.Core;

namespace Runefall.Presentation.Player
{
    /// <summary>
    /// Mueve al jugador con aceleración/deceleración suave, salto y dash.
    /// El personaje rota hacia donde se mueve; la cámara es independiente.
    /// Asignar InputReader SO en el inspector.
    /// Requiere CharacterController en el mismo GameObject.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Referencia de input")]
        [SerializeField] private InputReader input;

        [Header("Movimiento")]
        [SerializeField] private float moveSpeed      = 5f;
        [SerializeField] private float acceleration   = 8f;
        [SerializeField] private float deceleration   = 12f;

        [Header("Rotación")]
        [SerializeField] private float rotationSpeed  = 10f;

        [Header("Salto")]
        [SerializeField] private float jumpHeight        = 1.8f;
        [SerializeField] private float gravity           = -20f;
        [SerializeField] private Transform groundCheck;      // Transform vacío en los pies del personaje
        [SerializeField] private float groundCheckRadius    = 0.2f;
        [SerializeField] private LayerMask groundMask       = ~0;  // todo por defecto

        [Header("Dash")]
        [SerializeField] private float dashDistance   = 4f;
        [SerializeField] private float dashDuration   = 0.15f;
        [SerializeField] private float dashCooldown   = 1.2f;

        // ── Estado interno ───────────────────────────────────────────────────
        private CharacterController cc;
        private Transform cachedCamTransform;
        private Vector3 moveDirection;       // dirección normalizada en world-space
        private float   currentSpeed;        // velocidad actual (con aceleración)
        private Vector3 verticalVelocity;    // velocidad vertical (salto + gravedad)
        private bool    isDashing;
        private float   dashCooldownTimer;
        private bool    isGrounded;

        // ── API pública ──────────────────────────────────────────────────────
        /// <summary>Velocidad actual de movimiento horizontal. Leída por CharacterAnimationController.</summary>
        public float CurrentSpeed  => currentSpeed;
        public bool  IsGrounded    => isGrounded;
        public bool  IsDashing     => isDashing;

        // ── Ciclo de vida ────────────────────────────────────────────────────

        private void Awake()
        {
            cc = GetComponent<CharacterController>();

            if (input == null)
            {
                Debug.LogError("[PlayerController] InputReader no asignado.", this);
                enabled = false;
                return;
            }

            cachedCamTransform = Camera.main != null ? Camera.main.transform : null;
            if (cachedCamTransform == null)
                Debug.LogError("[PlayerController] Camera.main no encontrada.", this);
        }

        private void OnEnable()
        {
            input.JumpEvent += OnJump;
            input.DashEvent += OnDashInput;
        }

        private void OnDisable()
        {
            input.JumpEvent -= OnJump;
            input.DashEvent -= OnDashInput;
        }

        private void Update()
        {
            if (dashCooldownTimer > 0f)
                dashCooldownTimer -= Time.deltaTime;

            CheckGround();

            if (!isDashing)
            {
                HandleVertical();
                HandleMovement();
            }

            HandleRotation();
        }

        // ── Input callbacks ──────────────────────────────────────────────────

        private void OnJump()
        {
            Debug.Log($"[PlayerController] Jump — grounded={isGrounded}");
            if (isGrounded)
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        private void OnDashInput()
        {
            Debug.Log($"[PlayerController] Dash — isDashing={isDashing} cooldown={dashCooldownTimer:F2}");
            if (!isDashing && dashCooldownTimer <= 0f)
                StartCoroutine(PerformDash());
        }

        // ── Lógica de movimiento ─────────────────────────────────────────────

        private void HandleMovement()
        {
            Transform cam = cachedCamTransform;
            if (cam == null) return;

            Vector3 camFwd   = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 camRight = cam.right;
            Vector3 inputDir = camFwd * input.MoveInput.y + camRight * input.MoveInput.x;

            if (inputDir.sqrMagnitude > 1f)
                inputDir.Normalize();

            moveDirection = inputDir;

            float targetSpeed = inputDir.sqrMagnitude > 0.01f ? moveSpeed : 0f;
            float accel       = targetSpeed > currentSpeed ? acceleration : deceleration;
            currentSpeed      = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.deltaTime);

            cc.Move(moveDirection * currentSpeed * Time.deltaTime);
        }

        private void HandleRotation()
        {
            if (moveDirection.sqrMagnitude < 0.01f) return;

            Quaternion targetRot = Quaternion.LookRotation(moveDirection);
            transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        private void CheckGround()
        {
            Transform origin = groundCheck != null ? groundCheck : transform;
            isGrounded = Physics.CheckSphere(origin.position, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
        }

        private void HandleVertical()
        {
            if (isGrounded && verticalVelocity.y < 0f)
                verticalVelocity.y = -2f;   // mantiene contacto con el suelo

            verticalVelocity.y += gravity * Time.deltaTime;
            cc.Move(verticalVelocity * Time.deltaTime);
        }

        private IEnumerator PerformDash()
        {
            isDashing = true;

            // Dirección: última moveDirection o forward si está quieto
            Vector3 dir = moveDirection.sqrMagnitude > 0.01f
                ? moveDirection
                : transform.forward;

            float speed = dashDistance / dashDuration;
            float timer = 0f;

            while (timer < dashDuration)
            {
                cc.Move(dir * speed * Time.deltaTime);
                timer += Time.deltaTime;
                yield return null;
            }

            isDashing         = false;
            dashCooldownTimer = dashCooldown;
        }
    }
}
