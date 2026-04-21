using UnityEngine;

namespace Runefall.Data
{
    [CreateAssetMenu(menuName = "Runefall/Cards/Passive")]
    public class PassiveData : ScriptableObject
    {
        public string passiveName;
        [TextArea] public string description;
        public bool resonanceCompatible;
    }
}
