using System;
using System.Collections.Generic;

namespace Runefall.Core
{
    /// <summary>
    /// Registro global de servicios. Sustituye a Singletons.
    /// Uso: ServiceLocator.Register<ICombatSystem>(new CombatSystem());
    ///      ServiceLocator.Get<ICombatSystem>().DoSomething();
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> services = new();

        public static void Register<T>(T service)
        {
            services[typeof(T)] = service;
        }

        public static T Get<T>()
        {
            if (services.TryGetValue(typeof(T), out var s))
                return (T)s;

            throw new Exception($"[ServiceLocator] Service '{typeof(T).Name}' not registered.");
        }

        public static bool TryGet<T>(out T service)
        {
            if (services.TryGetValue(typeof(T), out var s))
            {
                service = (T)s;
                return true;
            }
            service = default;
            return false;
        }

        public static void Unregister<T>()
        {
            services.Remove(typeof(T));
        }

        /// <summary>Limpia todos los servicios. Usar entre escenas o en teardown de tests.</summary>
        public static void Clear()
        {
            services.Clear();
        }
    }
}
