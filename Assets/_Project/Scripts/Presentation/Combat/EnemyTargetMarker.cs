using UnityEngine;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Attach to each Enemy_X root in the scene.
    /// CombatBootstrapper calls Init() at runtime to set the index and create the indicator disc.
    /// CombatHUDPresenter calls SetSelected() to toggle the indicator.
    /// </summary>
    public class EnemyTargetMarker : MonoBehaviour
    {
        public int Index { get; private set; }

        private GameObject _indicator;

        public void Init(int index, GameObject indicatorDisc)
        {
            Index      = index;
            _indicator = indicatorDisc;
            _indicator?.SetActive(false);
        }

        public void SetSelected(bool selected)
        {
            if (_indicator != null) _indicator.SetActive(selected);
        }
    }
}
