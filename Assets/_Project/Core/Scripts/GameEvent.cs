using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runefall.Core
{
    /// <summary>
    /// Evento sin parámetros. Crear como asset: Events/GameEvent
    /// Uso: myEvent.Raise(); myEvent.Subscribe(OnFired);
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Events/GameEvent")]
    public class GameEvent : ScriptableObject
    {
        private readonly List<Action> listeners = new();

        public void Raise()
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i]?.Invoke();
        }

        public void Subscribe(Action listener)
        {
            if (!listeners.Contains(listener))
                listeners.Add(listener);
        }

        public void Unsubscribe(Action listener)
        {
            listeners.Remove(listener);
        }
    }

    /// <summary>
    /// Evento tipado. No se puede crear como asset directamente — subclasear con [CreateAssetMenu].
    /// Uso: myEvent.Raise(value); myEvent.Subscribe(OnValue);
    /// </summary>
    public class GameEvent<T> : ScriptableObject
    {
        private readonly List<Action<T>> listeners = new();

        public void Raise(T value)
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i]?.Invoke(value);
        }

        public void Subscribe(Action<T> listener)
        {
            if (!listeners.Contains(listener))
                listeners.Add(listener);
        }

        public void Unsubscribe(Action<T> listener)
        {
            listeners.Remove(listener);
        }
    }
}
