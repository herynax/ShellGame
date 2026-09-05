using System.Collections;
using DG.Tweening;
using FMOD.Studio;
using FMODUnity;
using ShellGame.Core;
using ShellGame.Health;
using UnityEngine;

namespace ShellGame.Audio
{
    /// <summary>
    /// Персистентный менеджер фоновой музыки (Singleton, DontDestroyOnLoad).
    /// Один FMOD EventInstance создаётся один раз при первом запуске игры и
    /// живёт через все смены сцен — поэтому музыка никогда не обрывается и
    /// не перезапускается с нуля при рестарте сцены или загрузке следующей.
    ///
    /// Плавность обеспечивается подпиской на статические события SceneLoader:
    ///  - ScreenGoingBlack(duration)  — экран уходит в чёрное  -> громкость музыки
    ///    плавно идёт к 0 за то же duration (в т.ч. duration = 0 при мгновенном
    ///    почернении экрана при смерти игрока — тогда музыка обрывается сразу).
    ///  - ScreenFullyBlack()          — экран уже полностью чёрный, новая сцена
    ///    загружена -> сбрасываем дозу игрока (HealthController.ResetDose),
    ///    пока игрок этого не видит.
    ///  - ScreenRevealing(duration)   — экран открывается обратно -> громкость
    ///    музыки плавно возвращается к baseVolume за то же duration.
    ///
    /// Никакой связи с самим SceneLoader через прямые ссылки не требуется —
    /// достаточно того, что оба объекта существуют в сцене (DontDestroyOnLoad).
    /// </summary>
    public sealed class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance { get; private set; }

        [Header("Трек")]
        [Tooltip("FMOD Event фоновой музыки, например event:/Music/MainTheme")]
        [EventRef]
        public string musicEvent;

        [Header("Громкость")]
        [Range(0f, 1f)]
        [Tooltip("Целевая громкость музыки в обычном состоянии (0..1), к которой она фейдится при ScreenRevealing")]
        public float baseVolume = 1f;

        [Header("Сброс дозы при переходе")]
        [Tooltip("Сбрасывать дозу при загрузке новой сцены. HealthController спавнится в рантайме (его нет в сцене по умолчанию), поэтому ссылку в инспекторе не назначить — MusicManager сам ищет его на только что загруженной сцене через FindObjectOfType.")]
        public bool resetDoseOnSceneLoad = true;
        [Tooltip("Сбрасывать также дозу врага (не только игрока) при переходе между сценами")]
        public bool resetEnemyDoseToo = false;

        private EventInstance _musicInstance;
        private Tween _volumeTween;
        private float _currentVolume;
        private bool _stoppedByDeath;

        private void Awake()
        {
            // Singleton, переживающий смену сцен
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            StartMusicIfNeeded();
        }

        private void OnEnable()
        {
            SceneLoader.ScreenGoingBlack += HandleScreenGoingBlack;
            SceneLoader.ScreenRevealing += HandleScreenRevealing;
            GameEvents.SideDied += HandleSideDied;
        }

        private void OnDisable()
        {
            SceneLoader.ScreenGoingBlack -= HandleScreenGoingBlack;
            SceneLoader.ScreenRevealing -= HandleScreenRevealing;
            GameEvents.SideDied -= HandleSideDied;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            _volumeTween?.Kill();
            if (_musicInstance.isValid())
            {
                _musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                _musicInstance.release();
            }
        }

        /// <summary>Создаёт и запускает музыку один раз за всю жизнь приложения.</summary>
        private void StartMusicIfNeeded(float initialVolume = -1f)
        {
            if (_musicInstance.isValid())
                return;

            if (string.IsNullOrEmpty(musicEvent))
            {
                Debug.LogWarning("[MusicManager] musicEvent не назначен — фоновая музыка не будет играть.");
                return;
            }

            _musicInstance = RuntimeManager.CreateInstance(musicEvent);
            _currentVolume = initialVolume >= 0f ? initialVolume : baseVolume;
            _musicInstance.setVolume(_currentVolume);
            _musicInstance.start();
        }

        private void HandleScreenGoingBlack(float duration)
        {
            FadeMusicTo(0f, duration);
            FMODUnity.RuntimeManager.StudioSystem.setParameterByName("Dose Counter", 0f, true);
        }

        private void HandleScreenRevealing(float duration)
        {
            if (_stoppedByDeath)
            {
                _stoppedByDeath = false;
                StartMusicIfNeeded(0f);
            }

            FadeMusicTo(baseVolume, duration);
        }

        private void HandleSideDied(TurnSide side)
        {
            _volumeTween?.Kill();
            _volumeTween = null;
            _stoppedByDeath = true;

            // ALLOWFADEOUT запускает AHDSR-релиз FMOD-события.
            if (_musicInstance.isValid())
            {
                _musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                _musicInstance.release();
                _musicInstance.clearHandle();
            }

            _currentVolume = 0f;
            FMODUnity.RuntimeManager.StudioSystem.setParameterByName("Dose Counter", 0f, true);
        }

        /// <summary>
        /// Плавно уводит громкость музыки в 0 за duration секунд. Публичный
        /// вход для мест, которые не проходят через события SceneLoader
        /// (например, фейд перед выходом из игры в MainMenu).
        /// </summary>
        public void FadeOutMusic(float duration)
        {
            FadeMusicTo(0f, duration);
        }

        /// <summary>
        /// Плавно возвращает громкость музыки к baseVolume за duration секунд.
        /// </summary>
        public void FadeInMusic(float duration)
        {
            FadeMusicTo(baseVolume, duration);
        }

        /// <summary>
        /// Плавно (или мгновенно, если duration ~ 0) переводит громкость музыки
        /// к target. SetUpdate(true) — чтобы фейд не замирал, если где-то стоит
        /// Time.timeScale = 0 во время перехода между сценами.
        /// </summary>
        private void FadeMusicTo(float target, float duration)
        {
            if (!_musicInstance.isValid())
                return;

            _volumeTween?.Kill();

            if (duration <= 0.001f)
            {
                _currentVolume = target;
                _musicInstance.setVolume(_currentVolume);
                return;
            }

            _volumeTween = DOTween.To(() => _currentVolume, v =>
                {
                    _currentVolume = v;
                    _musicInstance.setVolume(v);
                }, target, duration)
                .SetUpdate(true);
        }
    }
}