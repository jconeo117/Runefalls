using System;
using UnityEngine;
using UnityEngine.Playables;
using Runefall.Data;

namespace Runefall.Combat
{
    /// <summary>
    /// Abstraction for any combat actor that progresses through multiple phases.
    /// Allows presentation layers to run phase transitions without downcasting to concrete boss agents.
    /// </summary>
    public interface IMultiPhaseActor : ICombatActor
    {
        int CurrentPhase { get; }
        bool IsTransitioning { get; }
        EnemyData CurrentPhaseData { get; }
        
        void CheckPhaseTransition(Action onTransitionCinematicFinished);
        void CompleteTransitionCinematic();
        
        event Action<int> OnPhaseTransitionStarted;
        event Action<int> OnPhaseTransitionCompleted;

        PlayableAsset GetTransitionTimeline(int nextPhase);
        GameObject GetPhasePrefab(int nextPhase);
    }
}
