using System;
using System.Collections.Generic;

namespace ShellGame.Core
{
    /// <summary>
    /// Простой сервис-локатор для доступа к синглтон-подобным сервисам
    /// (пул наперстков, аудио-сервис и т.д.) без FindObjectOfType и без
    /// жёсткой связки через MonoBehaviour-синглтоны.
    ///
    /// Регистрируется один раз в бутстрап-сцене (см. GameBootstrap, если
    /// понадобится), после чего любой класс может получить сервис через
    /// ServiceLocator.Get&lt;T&gt;().
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            _services[typeof(T)] = service ?? throw new ArgumentNullException(nameof(service));
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var raw))
            {
                service = (T)raw;
                return true;
            }

            service = null;
            return false;
        }

        public static T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var raw))
                return (T)raw;

            throw new InvalidOperationException(
                $"Сервис {typeof(T).Name} не зарегистрирован в ServiceLocator. " +
                "Убедитесь, что бутстрап-сцена выполнилась раньше запроса.");
        }

        public static void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }

        /// <summary>Полная очистка — вызывать при выходе из игры/переходе между сценами, если нужно.</summary>
        public static void Clear()
        {
            _services.Clear();
        }
    }
}
