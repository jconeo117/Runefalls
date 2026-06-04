using UnityEngine;

namespace Runefall.Multiplayer.Lobby
{
    [RequireComponent(typeof(CharacterController))]
    public class LobbyPlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed   = 5f;
        [SerializeField] private float gravity     = -20f;
        [SerializeField] private float rotateSpeed = 12f;

        private CharacterController _cc;
        private Transform           _cam;
        private float               _verticalVel;

        private void Awake()  => _cc  = GetComponent<CharacterController>();
        private void Start()  => _cam = Camera.main?.transform;

        private void Update()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            var   input = new Vector3(h, 0f, v);

            Vector3 move = Vector3.zero;
            if (input.sqrMagnitude > 0.01f && _cam != null)
            {
                var flat    = Vector3.ProjectOnPlane(_cam.forward, Vector3.up).normalized;
                var right   = Vector3.ProjectOnPlane(_cam.right,   Vector3.up).normalized;
                move = (flat * v + right * h).normalized;

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(move),
                    rotateSpeed * Time.deltaTime);
            }

            if (_cc.isGrounded && _verticalVel < 0f) _verticalVel = -2f;
            _verticalVel += gravity * Time.deltaTime;

            _cc.Move((move * moveSpeed + Vector3.up * _verticalVel) * Time.deltaTime);
        }
    }
}
