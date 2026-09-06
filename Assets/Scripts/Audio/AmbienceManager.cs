using System;
using System.Collections.Generic;
using DG.Tweening;
using FMOD.Studio;
using FMODUnity;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Audio
{
    /// <summary>
    /// Персистентный менеджер атмосферного звука природы (Singleton, DontDestroyOnLoad).
    ///
    /// В отличие от MusicManager, тут НЕ один FMOD Event с Multi Instrument внутри —
    /// каждый звук окружения (ветер, ночной лес, дождь и т.д.) это ОТДЕЛЬНЫЙ простой
    /// FMOD Event с обычным Loop Region (без Shuffle/Multi Instrument — там нечего
    /// переигрывать, поэтому и не будет глюка с "новый звук на каждом цикле").
    /// Рандомный выбор между ними живёт здесь, в списке _ambienceSounds — добавляешь
    /// в инспекторе новые записи через "+" бесконечно, без единой правки кода.
    /// </summary>
    public sealed class AmbienceManager : MonoBehaviour
    {
        public static AmbienceManager Instance { get; private set; }

        [Serializable]
        public struct AmbienceSoundEntry
        {
            [Tooltip("Только для удобства в инспекторе, в логике не участвует")]
            public string DisplayName;

            [Tooltip("FMOD Event с ОДНИМ инструментом и обычным Loop Region, например event:/Ambience/Rain")]
            public EventReference SoundEvent;

            [Min(0.01f)]
            [Tooltip("Относительный вес выпадения при случайном выборе (по умолчанию 1 — все звуки равновероятны)")]
            public float Weight;
        }

        [Header("Звуки окружения (добавляй сколько нужно)")]
        [SerializeField] private List<AmbienceSoundEntry> _ambienceSounds = new List<AmbienceSoundEntry>();

        [Header("Громкость")]
        [Range(0f, 1f)]
        [Tooltip("Целевая громкость атмосферы в обычном состоянии (0..1), к которой она фейдится при ScreenRevealing")]
        public float baseVolume = 1f;

        [Header("Сброс при смерти")]
        [Tooltip("true — новый случайный звук окружения выбирается только при смерти ИГРОКА. false — при смерти любой стороны.")]
        public bool resetOnlyOnPlayerDeath = true;

        private EventInstance _ambienceInstance;
        private Tween _volumeTween;
        private float _currentVolume;
        private bool _stoppedByDeath;
        private int _lastPlayedIndex = -1;

        private void Awake()
        {
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
            StartAmbienceIfNeeded();
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
            ReleaseInstance();
        }

        /// <summary>Выбирает случайный звук из списка (с учётом весов, не повторяя предыдущий) и запускает его.</summary>
        private void StartAmbienceIfNeeded(float initialVolume = -1f)
        {
            if (_ambienceInstance.isValid())
                return;

            if (_ambienceSounds == null || _ambienceSounds.Count == 0)
            {
                Debug.LogWarning("[AmbienceManager] Список звуков окружения пуст — добавь хотя бы один AmbienceSoundEntry в инспекторе.");
                return;
            }

            int index = PickRandomIndex();
            var entry = _ambienceSounds[index];

            if (string.IsNullOrEmpty(entry.SoundEvent.Path))
            {
                Debug.LogWarning($"[AmbienceManager] У записи '{entry.DisplayName}' не назначен SoundEvent.");
                return;
            }

            _lastPlayedIndex = index;
            _ambienceInstance = RuntimeManager.CreateInstance(entry.SoundEvent);
            _currentVolume = initialVolume >= 0f ? initialVolume : baseVolume;
            _ambienceInstance.setVolume(_currentVolume);
            _ambienceInstance.start();
        }

        /// <summary>Взвешенный случайный выбор индекса, исключая последний сыгранный (если вариантов больше одного).</summary>
        private int PickRandomIndex()
        {
            if (_ambienceSounds.Count == 1)
                return 0;

            int candidateIndex;
            int guardCounter = 0;
            do
            {
                candidateIndex = WeightedRandomIndex();
                guardCounter++;
            }
            while (candidateIndex == _lastPlayedIndex && guardCounter < 8);

            return candidateIndex;
        }

        private int WeightedRandomIndex()
        {
            float totalWeight = 0f;
            for (int i = 0; i < _ambienceSounds.Count; i++)
                totalWeight += Mathf.Max(0.01f, _ambienceSounds[i].Weight <= 0f ? 1f : _ambienceSounds[i].Weight);

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < _ambienceSounds.Count; i++)
            {
                float weight = Mathf.Max(0.01f, _ambienceSounds[i].Weight <= 0f ? 1f : _ambienceSounds[i].Weight);
                cumulative += weight;
                if (roll <= cumulative)
                    return i;
            }

            return _ambienceSounds.Count - 1;
        }

        private void ReleaseInstance()
        {
            if (!_ambienceInstance.isValid())
                return;

            _ambienceInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _ambienceInstance.release();
            _ambienceInstance.clearHandle();
        }

        private void HandleScreenGoingBlack(float duration)
        {
            FadeAmbienceTo(0f, duration);
        }

        private void HandleScreenRevealing(float duration)
        {
            if (_stoppedByDeath)
            {
                _stoppedByDeath = false;
                StartAmbienceIfNeeded(0f); // выберет новый случайный звук, отличный от предыдущего
            }

            FadeAmbienceTo(baseVolume, duration);
        }

        private void HandleSideDied(TurnSide side)
        {
            if (resetOnlyOnPlayerDeath && side != TurnSide.Player)
                return;

            _volumeTween?.Kill();
            _volumeTween = null;
            _stoppedByDeath = true;

            ReleaseInstance();
            _currentVolume = 0f;
        }

        /// <summary>Плавно уводит громкость атмосферы в 0 за duration секунд (для мест вне SceneLoader, например MainMenu).</summary>
        public void FadeOutAmbience(float duration) => FadeAmbienceTo(0f, duration);

        /// <summary>Плавно возвращает громкость атмосферы к baseVolume за duration секунд.</summary>
        public void FadeInAmbience(float duration) => FadeAmbienceTo(baseVolume, duration);

        private void FadeAmbienceTo(float target, float duration)
        {
            if (!_ambienceInstance.isValid())
                return;

            _volumeTween?.Kill();

            if (duration <= 0.001f)
            {
                _currentVolume = target;
                _ambienceInstance.setVolume(_currentVolume);
                return;
            }

            _volumeTween = DOTween.To(() => _currentVolume, v =>
                {
                    _currentVolume = v;
                    _ambienceInstance.setVolume(v);
                }, target, duration)
                .SetUpdate(true);
        }
    }
}