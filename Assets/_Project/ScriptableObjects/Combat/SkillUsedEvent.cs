using Runefall.Combat;
using Runefall.Core;
using UnityEngine;

namespace Runefall.Data
{
    [CreateAssetMenu(menuName = "Runefall/Events/SkillUsedEvent")]
    public class SkillUsedEvent : GameEvent<SkillUsedPayload> { }
}
