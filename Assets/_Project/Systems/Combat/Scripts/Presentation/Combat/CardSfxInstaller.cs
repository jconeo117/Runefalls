using UnityEngine;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Assigns the card UI sound clips into <see cref="CardSfx"/> at startup. Drop this anywhere in
    /// the combat scene and wire the three clips (hover / use / upgrade) from Shared/Audio/Cards.
    /// </summary>
    public class CardSfxInstaller : MonoBehaviour
    {
        [SerializeField] private AudioClip _hover;
        [SerializeField] private AudioClip _use;
        [SerializeField] private AudioClip _upgrade;

        private void Awake()
        {
            CardSfx.Hover   = _hover;
            CardSfx.Use     = _use;
            CardSfx.Upgrade = _upgrade;
        }
    }
}
