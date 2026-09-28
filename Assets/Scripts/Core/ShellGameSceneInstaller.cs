using System.Collections.Generic;
using ShellGame.Feedback;
using ShellGame.Gameplay;
using ShellGame.Map;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Zenject;

namespace ShellGame.Core
{
    public sealed class ShellGameSceneInstaller : MonoInstaller
    {
        [SerializeField] private ShellGame.Items.UnlocksConfig _unlocksConfig;

        public override void InstallBindings()
        {
            // --- Мета и прогресс ---
            var unlocksConfig = _unlocksConfig != null ? _unlocksConfig : ResolveUnlocksConfigFallback();
            Container.BindInstance(unlocksConfig).IfNotBound();
            Container.BindInterfacesTo<ShellGame.Meta.GlobalProgressService>().AsSingle();
            Container.BindInterfacesTo<ShellGame.Meta.UnlockManager>().AsSingle();

            Container.Bind<GameSessionProgression>()
                .FromInstance(GameSessionProgression.Instance)
                .AsSingle()
                .IfNotBound();

            // --- Карта: MapGenerationConfig ---
            var mapConfigAsset = Resources.Load<MapGenerationConfigAsset>("Configs/MapGenerationConfig");
            if (mapConfigAsset != null)
            {
                Container.BindInstance(mapConfigAsset.ToConfig()).IfNotBound();
            }
            else
            {
                Container.BindInstance(new MapGenerationConfig()).IfNotBound();
            }

            // --- Сцена: компоненты, которые лежат в сцене постоянно ---
            // Всё, что живёт внутри EncounterRig (генератор раундов, перемешивание,
            // здоровье, ИИ врага, кнопка старта, спавнер предметов, указатель хода),
            // сюда добавлять нельзя: при загрузке сцены рига ещё нет.
            // Их раздаёт EncounterHost.
            BindSceneComponent<GameManager>();
            BindSceneComponent<RoundInputSystem>();
            BindSceneComponent<TurnSpotlightController>();
            BindSceneComponent<ShellGame.Feedback.PlayerDamageFeedback>();

            // --- Инжект в постоянный загрузчик сцен ---
            if (SceneLoader.Instance != null)
                Container.Inject(SceneLoader.Instance);

            // --- Cinemachine и Render Pipeline ---
            Container.Bind<CinemachineBrain>().FromComponentInHierarchy().AsSingle().IfNotBound();
            Container.Bind<CinemachineCamera>().FromComponentsInHierarchy().AsTransient().IfNotBound();
            Container.Bind<CinemachineVirtualCameraBase>().FromComponentsInHierarchy().AsTransient().IfNotBound();
            Container.Bind<CinemachineStationaryLook>().FromComponentsInHierarchy().AsTransient().IfNotBound();
            Container.Bind<CanvasGroup>().FromComponentsInHierarchy().AsTransient().IfNotBound();
            Container.Bind<Camera>().FromComponentInHierarchy().AsSingle().IfNotBound();
            Container.Bind<Volume>().FromComponentInHierarchy().AsSingle().IfNotBound();
        }

        private void BindSceneComponent<T>() where T : Component
        {
            // .IfNotBound() нужен, чтобы вручную заданные биндинги не конфликтовали
            Container.Bind<T>().FromComponentInHierarchy().AsSingle().IfNotBound();
        }

        /// <summary>
        /// _unlocksConfig как [SerializeField] здесь не работает: контекст создаётся
        /// в рантайме через AddComponent, и поле всегда пустое. Поэтому конфиг
        /// подгружается из Resources: Assets/Resources/Configs/UnlocksConfig.asset.
        /// </summary>
        private static ShellGame.Items.UnlocksConfig ResolveUnlocksConfigFallback()
        {
            var config = Resources.Load<ShellGame.Items.UnlocksConfig>("Configs/UnlocksConfig");
            if (config == null)
                Debug.LogError("[ShellGameSceneInstaller] UnlocksConfig не найден в Resources/Configs/UnlocksConfig — прогресс работать не будет.");
            return config;
        }
    }

    public static class ShellGameZenjectBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoading()
        {
            SceneManager.sceneLoaded += InstallSceneContext;
        }

        private static void InstallSceneContext(Scene scene, LoadSceneMode mode)
        {
            if (GameSessionProgression.Instance == null)
            {
                var progressionObject = new GameObject("GameSessionProgression");
                progressionObject.AddComponent<GameSessionProgression>();
                Object.DontDestroyOnLoad(progressionObject); // чтобы пережить смену сцены
            }

            var contextObject = new GameObject("ShellGame SceneContext");
            contextObject.SetActive(false);
            var context = contextObject.AddComponent<SceneContext>();
            var installer = contextObject.AddComponent<ShellGameSceneInstaller>();
            context.Installers = new List<MonoInstaller> { installer };
            contextObject.SetActive(true);
        }
    }
}