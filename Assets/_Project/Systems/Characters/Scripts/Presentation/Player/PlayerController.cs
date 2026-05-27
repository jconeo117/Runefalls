using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Runefall.Core;

namespace Runefall.Presentation.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputReader input;

        [Header("Movimiento")]
        [SerializeField] private float moveSpeed    = 5f;
        [SerializeField] private float acceleration = 8f;
        [SerializeField] private float deceleration = 12f;

        [Header("Rotación")]
        [SerializeField] private float rotationSpeed = 10f;

        [Header("Dash")]
        [SerializeField] private float dashDistance       = 4f;
        [SerializeField] private float dashDuration       = 0.15f;
        [SerializeField] private float dashBetweenCooldown = 0.5f;   // espera entre dashs
        [SerializeField] private float dashChargeRecovery  = 1.8f;   // recuperación por carga

        // ── Estado interno ───────────────────────────────────────────────────
        private CharacterController _cc;
        private Transform           _camTransform;
        private Vector3             _moveDirection;
        private float               _currentSpeed;
        private float               _verticalVelocity;

        private const float Gravity    = -20f;
        private const int   MaxCharges = 3;

        private int         _charges          = MaxCharges;
        private float       _betweenDashTimer = 0f;
        private bool        _isDashing;
        private readonly List<float> _chargeTimers = new();   // countdown por carga gastada

        // ── API pública ──────────────────────────────────────────────────────
        public float CurrentSpeed  => _currentSpeed;
        public bool  IsDashing     => _isDashing;
        public int   DashCharges   => _charges;

        // ── Ciclo de vida ────────────────────────────────────────────────────

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();

            if (input == null)
            {
                Debug.LogError("[PlayerController] InputReader no asignado.", this);
                enabled = false;
                return;
            }

            _camTransform = Camera.main != null ? Camera.main.transform : null;
            if (_camTransform == null)
                Debug.LogError("[PlayerController] Camera.main no encontrada.", this);
        }

        private void OnEnable()  => input.DashEvent += OnDashInput;
        private void OnDisable() => input.DashEvent -= OnDashInput;

        private void Update()
        {
            TickDashTimers();
            ApplyGravity();
            if (!_isDashing)
                HandleMovement();
            HandleRotation();
        }

        // ── Input ────────────────────────────────────────────────────────────

        private void OnDashInput()
        {
            if (_isDashing || _charges <= 0 || _betweenDashTimer > 0f) return;
            StartCoroutine(PerformDash());
        }

        // ── Movimiento ───────────────────────────────────────────────────────

        private void HandleMovement()
        {
            if (_camTransform == null) return;

            Vector3 camFwd   = Vector3.ProjectOnPlane(_camTransform.forward, Vector3.up).normalized;
            Vector3 camRight = _camTransform.right;
            Vector3 inputDir = camFwd * input.MoveInput.y + camRight * input.MoveInput.x;

            if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();
            _moveDirection = inputDir;

            float targetSpeed = inputDir.sqrMagnitude > 0.01f ? moveSpeed : 0f;
            if (targetSpeed == 0f)
                _currentSpeed = 0f;
            else
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, acceleration * Time.deltaTime);

            _cc.Move(_moveDirection * _currentSpeed * Time.deltaTime);
        }

        private void HandleRotation()
        {
            if (_moveDirection.sqrMagnitude < 0.01f) return;
            Quaternion target  = Quaternion.LookRotation(_moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += Gravity * Time.deltaTime;

            if (!_isDashing)
                _cc.Move(Vector3.up * _verticalVelocity * Time.deltaTime);
        }

        // ── Dash ─────────────────────────────────────────────────────────────

        private void TickDashTimers()
        {
            if (_betweenDashTimer > 0f)
                _betweenDashTimer -= Time.deltaTime;

            for (int i = _chargeTimers.Count - 1; i >= 0; i--)
            {
                _chargeTimers[i] -= Time.deltaTime;
                if (_chargeTimers[i] <= 0f)
                {
                    _chargeTimers.RemoveAt(i);
                    _charges = Mathf.Min(MaxCharges, _charges + 1);
                }
            }
        }

        private IEnumerator PerformDash()
        {
            _isDashing = true;
            _charges--;
            _betweenDashTimer = dashBetweenCooldown;
            _chargeTimers.Add(dashChargeRecovery);

            Vector3 dir   = _moveDirection.sqrMagnitude > 0.01f ? _moveDirection : transform.forward;
            float   speed = dashDistance / dashDuration;
            float   timer = 0f;

            while (timer < dashDuration)
            {
                _cc.Move(dir * speed * Time.deltaTime + Vector3.up * _verticalVelocity * Time.deltaTime);
                timer += Time.deltaTime;
                yield return null;
            }

            _isDashing = false;
        }
    }
}
