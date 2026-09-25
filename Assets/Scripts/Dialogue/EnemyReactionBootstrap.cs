using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShellGame.Dialogue
{
    /// <summary>
    /// Кэш конфигов реакций из Resources — чтобы не грузить их заново на
    /// каждой сцене.
    /// </summary>
    public static class EnemyReactionConfigLibrary
    {
        private static List<EnemyReactionConfig> _cache;

        public static IReadOnlyList<EnemyReactionConfig> All
        {
            get
            {
                if (_cache == null)
                    _cache = Load();
                return _cache;
            }
        }

        public static void Clear() => _cache = null;

        private static List<EnemyReactionConfig> Load()
        {
            var configs = new List<EnemyReactionConfig>(
                Resources.LoadAll<EnemyReactionConfig>(EnemyReactionConfig.ResourcesFolder));

            configs.RemoveAll(config => config == null);
            configs.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return configs;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _cache = null;
    }

    /// <summary>
    /// Автоматическая интеграция реакций: на каждой сцене для каждого
    /// EnemyReactionConfig из Resources/Configs/EnemyReactions, у которого эта
    /// сцена указана в Scenes, поднимается свой EnemyReactionDirector.
    /// Новый враг на новом уровне = новый ассет конфига, ни строчки кода.
    ///
    /// Если директор для этого конфига уже лежит на сцене (добавлен вручную),
    /// второй не создаётся.
    ///
    /// Сцену отслеживаем по имени активной сцены в Update, а не через
    /// SceneManager.sceneLoaded: параметр LoadMode этого события в Unity 6
    /// доступен только в редакторе и не компилируется в билд.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyReactionBootstrap : MonoBehaviour
    {
        private static EnemyReactionBootstrap _instance;

        private string _lastSceneName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_instance != null)
                return;

            var host = new GameObject("EnemyReactionBootstrap");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<EnemyReactionBootstrap>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
        }

        private void Update()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name == _lastSceneName)
                return;

            _lastSceneName = scene.name;
            BindDirectorsFor(scene.name);
        }

        /// <summary>Поднимает директоров для всех конфигов, где указана эта сцена.</summary>
        public static void BindDirectorsFor(string sceneName)
        {
            var configs = EnemyReactionConfigLibrary.All;
            for (int i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                if (config == null || !config.AppliesToScene(sceneName))
                    continue;

                if (HasDirector(config))
                    continue;

                // Создаём неактивным: Awake компонента должен увидеть уже
                // назначенный конфиг, иначе он сначала пожалуется на пустой.
                var host = new GameObject($"EnemyReactionDirector ({config.name})");
                host.SetActive(false);
                var director = host.AddComponent<EnemyReactionDirector>();
                director.SetConfig(config);
                host.SetActive(true);
            }
        }

        private static bool HasDirector(EnemyReactionConfig config)
        {
            var directors = Object.FindObjectsByType<EnemyReactionDirector>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var director in directors)
            {
                if (director != null && director.Config == config)
                    return true;
            }

            return false;
        }
    }
}
