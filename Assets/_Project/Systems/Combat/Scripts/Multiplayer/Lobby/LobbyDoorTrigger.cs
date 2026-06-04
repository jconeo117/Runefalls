using UnityEngine;

namespace Runefall.Multiplayer.Lobby
{
    [RequireComponent(typeof(BoxCollider))]
    public class LobbyDoorTrigger : MonoBehaviour
    {
        [SerializeField] private LobbyUIController lobbyUI;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private bool _shown;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player") || _shown) return;
            _shown = true;
            lobbyUI?.Show();
        }

        private void OnTriggerStay(Collider other)
        {
            if (!other.CompareTag("Player") || _shown) return;
            _shown = true;
            lobbyUI?.Show();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _shown = false;
            lobbyUI?.Hide();
        }
    }
}
