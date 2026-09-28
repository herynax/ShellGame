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
    /// Директоры реакций для отдельных сцен (обучение, тестовые уровни):
    /// для каждого EnemyReactionConfig из Resources/Configs/EnemyReactions,
    /// у которого эта сцена явно указана в Scenes, поднимается свой директор.
    ///
    /// В сцене Game (забег с картой) этот механизм отключён: там директора
    /// создаёт EncounterHost по EncounterDefinition.ReactionConfig, то есть
    /// по текущему врагу, а не по имени сцены. Конфиги с пустым Scenes
    /// автоматически на сцены не вешаются.
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
            if (scene.name == ShellGame.Run.RunManager.GameSceneName) return;
            BindDirectorsFor(scene.name);
        }

        /// <summary>Поднимает директоров для конфигов, где эта сцена указана явно.</summary>
        public static void BindDirectorsFor(string sceneName)
        {
            var configs = EnemyReactionConfigLibrary.All;
            for (int i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                if (config == null)
                    continue;

                // Конфиги врагов забега (без списка сцен) сюда не попадают:
                // их создаёт EncounterHost.
                if (config.Scenes == null || config.Scenes.Count == 0)
                    continue;

                if (!config.AppliesToScene(sceneName))
                    continue;

                if (HasDirector(config))
                    continue;

                CreateDirector(config);
            }
        }

        /// <summary>
        /// Создаёт директора реплик для конфига. Вызывается EncounterHost при
        /// входе в энкаунтер. Уничтожать надо gameObject директора.
        /// </summary>
        public static EnemyReactionDirector CreateDirector(EnemyReactionConfig config)
        {
            if (config == null)
                return null;

            // Создаём неактивным: Awake компонента должен увидеть уже
            // назначенный конфиг, иначе он сначала пожалуется на пустой.
            var host = new GameObject($"EnemyReactionDirector ({config.name})");
            host.SetActive(false);
            var director = host.AddComponent<EnemyReactionDirector>();
            director.SetConfig(config);
            host.SetActive(true);
            return director;
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