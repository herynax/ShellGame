using System.Collections.Generic;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Health
{
    public sealed class HealthController : MonoBehaviour
    {
        private const string DoseCounterParameterName = "Dose Counter";
        private const int DoseCounterMax = 5;

        private readonly Dictionary<TurnSide, int> _current = new Dictionary<TurnSide, int>();
        private readonly Dictionary<TurnSide, int> _max = new Dictionary<TurnSide, int>();
        private readonly HashSet<TurnSide> _dead = new HashSet<TurnSide>();

        // Инстанс звука смерти, чтобы отслеживать, когда он закончится
        private FMOD.Studio.EventInstance _deathSoundInstance;

        public void Initialize(int playerMaxHealth, int enemyMaxHealth)
        {
            _max[TurnSide.Player] = playerMaxHealth;
            _max[TurnSide.Enemy] = enemyMaxHealth;
            _current[TurnSide.Player] = 0;
            _current[TurnSide.Enemy] = 0;
            _dead.Clear();

            GameEvents.RaiseHealthChanged(TurnSide.Player, 0, playerMaxHealth);
            GameEvents.RaiseHealthChanged(TurnSide.Enemy, 0, enemyMaxHealth);

            UpdateDoseCounterParameter(TurnSide.Player);
        }

        public int GetHealth(TurnSide side) => _current.TryGetValue(side, out var v) ? v : 0;
        public int GetMaxHealth(TurnSide side) => _max.TryGetValue(side, out var v) ? v : 0;
        public bool IsDead(TurnSide side) => _dead.Contains(side);

        public float GetDoseFraction(TurnSide side)
        {
            int max = GetMaxHealth(side);
            return max > 0 ? Mathf.Clamp01((float)GetHealth(side) / max) : 0f;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ApplyDamage(TurnSide.Player, 1);
            }
        }

        public bool ApplyDamage(TurnSide side, int amount)
        {
            if (_dead.Contains(side) || amount <= 0)
                return false;

            int max = GetMaxHealth(side);
            int rawDose = GetHealth(side) + amount;
            bool overdosed = rawDose >= max;
            int clampedDose = Mathf.Min(max, rawDose);
            _current[side] = clampedDose;

            // --- ВОСПРОИЗВЕДЕНИЕ ЗВУКОВ ЧЕРЕЗ ПРОВАЙДЕР ---
            if (HealthSoundProvider.Instance != null)
            {
                var provider = HealthSoundProvider.Instance;
                Vector3 soundPosition = GetSidePosition(side);

                // 1. Звук укола
                if (!provider.injectionSound.IsNull)
                {
                    FMODUnity.RuntimeManager.PlayOneShot(provider.injectionSound);
                }

                // 2. Звук урона
                var damageSound = side == TurnSide.Player ? provider.playerDamageSound : provider.enemyDamageSound;
                if (!damageSound.IsNull)
                {
                    FMODUnity.RuntimeManager.PlayOneShot(damageSound, soundPosition);
                }
            }

            GameEvents.RaiseHealthChanged(side, clampedDose, max);
            GameEvents.RaiseDamageTaken(side, amount, clampedDose, max, overdosed);
            UpdateDoseCounterParameter(side);

            if (overdosed)
            {
                _dead.Add(side);

                // 3. Звук смерти (сохраняем инстанс для SceneLoader)
                if (HealthSoundProvider.Instance != null)
                {
                    var provider = HealthSoundProvider.Instance;
                    var deathSound = side == TurnSide.Player ? provider.playerDeathSound : provider.enemyDeathSound;
                    Vector3 soundPosition = GetSidePosition(side);

                    if (!deathSound.IsNull)
                    {
                        _deathSoundInstance = FMODUnity.RuntimeManager.CreateInstance(deathSound);
                        _deathSoundInstance.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(soundPosition));
                        _deathSoundInstance.start();
                        _deathSoundInstance.release(); // FMOD сам удалит объект из памяти после завершения
                    }
                }

                GameEvents.RaiseSideDied(side);
            }

            return overdosed;
        }

        public void Heal(TurnSide side, int amount)
        {
            if (_dead.Contains(side) || amount <= 0) return;
            int newDose = Mathf.Max(0, GetHealth(side) - amount);
            _current[side] = newDose;
            GameEvents.RaiseHealthChanged(side, newDose, GetMaxHealth(side));
            UpdateDoseCounterParameter(side);
        }

        public void ResetDose(TurnSide side)
        {
            if (_dead.Contains(side)) return;
            _current[side] = 0;
            GameEvents.RaiseHealthChanged(side, 0, GetMaxHealth(side));
            UpdateDoseCounterParameter(side);
        }

        private Vector3 GetSidePosition(TurnSide side)
        {
            if (HealthSoundProvider.Instance != null)
            {
                var provider = HealthSoundProvider.Instance;
                if (side == TurnSide.Player && provider.playerTransform != null) 
                    return provider.playerTransform.position;
                if (side == TurnSide.Enemy && provider.enemyTransform != null) 
                    return provider.enemyTransform.position;
            }
            return transform.position;
        }

        private void UpdateDoseCounterParameter(TurnSide side)
        {
            if (side != TurnSide.Player) return;

            int dose = GetHealth(side);

            // Теперь просто передаем текущее значение напрямую.
            // Клампаем до 5 (DoseCounterMax) на всякий случай, если здоровье превысит 5, 
            // чтобы FMOD не получил значение, выходящее за рамки его шкалы.
            int value = Mathf.Clamp(dose, 0, DoseCounterMax);
            
            FMODUnity.RuntimeManager.StudioSystem.setParameterByName(DoseCounterParameterName, value);
        }

        // --- МЕТОД ДЛЯ SCENE LOADER ---
        public bool IsDeathSoundPlaying()
        {
            if (!_deathSoundInstance.isValid()) return false;
            
            _deathSoundInstance.getPlaybackState(out var state);
            return state != FMOD.Studio.PLAYBACK_STATE.STOPPED;
        }

        private void OnDisable()
        {
            if (_deathSoundInstance.isValid())
            {
                _deathSoundInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            }
        }
    }
}