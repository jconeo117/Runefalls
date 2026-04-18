using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Runefall.Core
{
    /// <summary>
    /// ScriptableObject que encapsula el Input System.
    /// Nada fuera de este archivo lee InputAction directamente.
    /// Asignar como asset en el inspector de PlayerController.
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Input/InputReader")]
    public class InputReader : ScriptableObject, GameInputActions.IPlayerActions
    {
        // ── Eventos que el resto del juego consume ───────────────────────────
        public event Action<Vector2> MoveEvent;
        public event Action<Vector2> LookEvent;
        public event Action          JumpEvent;
        public event Action          DashEvent;
        public event Action          DodgeEvent;     // alias de DashEvent — compatibilidad
        public event Action          InteractEvent;
        public event Action<int>     UseCardEvent;   // 0=skill1, 1=skill2, 2=ultimate
        public event Action          PauseEvent;

        // ── Estado de lectura directa (polling opcional) ─────────────────────
        public Vector2 MoveInput   { get; private set; }
        public Vector2 LookInput   { get; private set; }

        private GameInputActions inputActions;

        private void OnEnable()
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Player.SetCallbacks(this);
            }
            EnablePlayerInput();
        }

        private void OnDisable()
        {
            DisableAllInput();
        }

        // ── Control de mapas de acción ───────────────────────────────────────

        public void EnablePlayerInput()
        {
            inputActions.Player.Enable();
            inputActions.UI.Disable();
        }

        public void EnableUIInput()
        {
            inputActions.UI.Enable();
            inputActions.Player.Disable();
        }

        public void DisableAllInput()
        {
            inputActions.Player.Disable();
            inputActions.UI.Disable();
        }

        // ── Callbacks de IPlayerActions ──────────────────────────────────────

        public void OnMove(InputAction.CallbackContext ctx)
        {
            MoveInput = ctx.ReadValue<Vector2>();
            MoveEvent?.Invoke(MoveInput);
        }

        public void OnLook(InputAction.CallbackContext ctx)
        {
            LookInput = ctx.ReadValue<Vector2>();
            LookEvent?.Invoke(LookInput);
        }

        public void OnJump(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                JumpEvent?.Invoke();
        }

        public void OnDash(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
            {
                DashEvent?.Invoke();
                DodgeEvent?.Invoke();   // alias para compatibilidad
            }
        }

        public void OnInteract(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                InteractEvent?.Invoke();
        }

        public void OnUseSkill1(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                UseCardEvent?.Invoke(0);
        }

        public void OnUseSkill2(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                UseCardEvent?.Invoke(1);
        }

        public void OnUseUltimate(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                UseCardEvent?.Invoke(2);
        }

        public void OnPause(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
                PauseEvent?.Invoke();
        }
    }
}
