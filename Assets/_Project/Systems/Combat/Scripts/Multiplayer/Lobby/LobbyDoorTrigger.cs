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

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
                lobbyUI?.Show();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
                lobbyUI?.Hide();
        }
    }
}
