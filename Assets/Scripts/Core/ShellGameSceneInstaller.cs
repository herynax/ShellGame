using System.Collections.Generic;
using ShellGame.AI;
using ShellGame.Feedback;
using ShellGame.Gameplay;
using ShellGame.Health;
using ShellGame.Items;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Zenject;

namespace ShellGame.Core
{
    public sealed class ShellGameSceneInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            BindSceneComponent<GameManager>();
            Container.Bind<GameSessionProgression>().FromInstance(GameSessionProgression.Instance).AsSingle();
            BindSceneComponent<RoundGenerator>();
            BindSceneComponent<RoundInputSystem>();
            BindSceneComponent<ShuffleSystem>();
            BindSceneComponent<HealthController>();
            BindSceneComponent<EnemyAIController>();
            BindSceneComponent<RoundStartButton>();
            BindSceneComponent<TurnIndicatorController>();
            BindSceneComponent<ItemSpawner>();
            BindSceneComponent<TurnSpotlightController>();

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
            Container.Bind<T>().FromComponentInHierarchy().AsSingle().IfNotBound();
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