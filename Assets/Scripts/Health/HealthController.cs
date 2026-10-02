using System.Collections;
using System.Collections.Generic;
using ShellGame.Core;
using ShellGame.Gameplay;
using ShellGame.Feedback;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Zenject;

namespace ShellGame.Health
{
    public sealed class HealthController : MonoBehaviour
    {
        private const string DoseCounterParameterName = "Dose Counter";
        private const int DoseCounterMax = 5;

        private readonly Dictionary<TurnSide, int> _current = new Dictionary<TurnSide, int>();
        private readonly Dictionary<TurnSide, int> _max = new Dictionary<TurnSide, int>();
        private readonly HashSet<TurnSide> _dead = new HashSet<TurnSide>();

        private readonly HashSet<TurnSide> _shielded = new HashSet<TurnSide>();

        private PlayerDamageFeedback _playerDamageFeedback;
        private EnemyDamageFeedback _enemyDamageFeedback;

        // Отложенный урон укола иглой: ждём, пока его спишет событие анимации
        // на игле (NeedleMetalSqueak.ApplyDamage).
        private bool _hasPendingDamage;
        private TurnSide _pendingDamageSide;
        private int _pendingDamageAmount;
        private bool _needleInjectionActive;

        // Инстанс звука смерти, чтобы отслеживать, когда он закончится
        private FMOD.Studio.EventInstance _deathSoundInstance;
        public float DeathSoundDuration { get; private set; }


        [Inject]
        private void InjectDependencies(PlayerDamageFeedback playerDamageFeedback)
        {
            _playerDamageFeedback = playerDamageFeedback;
        }

        public void BindEnemyFeedback(EnemyDamageFeedback enemyDamageFeedback)
        {
            _enemyDamageFeedback = enemyDamageFeedback;
        }

        public void Initialize(int playerMaxHealth, int enemyMaxHealth)
        {
            _max[TurnSide.Player] = playerMaxHealth;
            _max[TurnSide.Enemy] = enemyMaxHealth;
            _current[TurnSide.Player] = 0;
            _current[TurnSide.Enemy] = 0;
            _dead.Clear();
            _shielded.Clear(); // Очищаем щиты при рестарте

            GameEvents.RaiseHealthChanged(TurnSide.Player, 0, playerMaxHealth);
            GameEvents.RaiseHealthChanged(TurnSide.Enemy, 0, enemyMaxHealth);

            UpdateDoseCounterParameter(TurnSide.Player);
        }

        public bool HasShield(TurnSide side) => _shielded.Contains(side);
        public void AddShield(TurnSide side) => _shielded.Add(side);

        public int GetHealth(TurnSide side) => _current.TryGetValue(side, out var v) ? v : 0;
        public int GetMaxHealth(TurnSide side) => _max.TryGetValue(side, out var v) ? v : 0;
        public bool IsDead(TurnSide side) => _dead.Contains(side);

        /// <summary>Ждёт ли кто-то отложенный урон укола иглой (см. ApplyPendingDamage).</summary>
        public bool HasPendingDamage => _hasPendingDamage;

        public float GetDoseFraction(TurnSide side)
        {
            int max = GetMaxHealth(side);
            return max > 0 ? Mathf.Clamp01((float)GetHealth(side) / max) : 0f;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // Отладочная дозировка цифрами: 1 — по игроку, 2 — по врагу.
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame)
                ApplyDamage(TurnSide.Player, 1, true);

            if (keyboard.digit2Key.wasPressedThisFrame)
                ApplyDamage(TurnSide.Enemy, 1, true);
#else
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ApplyDamage(TurnSide.Player, 1, true);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ApplyDamage(TurnSide.Enemy, 1, true);
            }
#endif
        }

        /// <summary>
        /// Нанесение урона. Нужно ли перед уроном проигрывать укол иглой:
        ///
        /// false — урон списывается сразу (нож, молоток, отладка цифрами).
        /// true  — урон дозой: сначала запускается анимация укола иглой, а сам
        ///         урон откладывается и списывается ПО СОБЫТИЮ АНИМАЦИИ на
        ///         объекте иглы (NeedleMetalSqueak.ApplyDamage — момент входа
        ///         иглы в тело). Если событие в клипе потеряется, урон спишется
        ///         по страховочному таймеру самой иглы
        ///         (NeedleMetalSqueak.ArmDamage).
        ///
        /// Возвращает true, если нанесённый СЕЙЧАС урон убил сторону. При
        /// needNeedleAnim: true урон отложен, поэтому возвращаемое значение
        /// ничего не значит: вызывающий код должен дождаться укола
        /// (WaitForNeedleInjection) и читать смерть через IsDead(side) либо
        /// слушать GameEvents.SideDied.
        /// </summary>
        public bool ApplyDamage(TurnSide side, int amount, bool needNeedleAnim = false)
        {
            if (!needNeedleAnim)
                return ApplyDamageNow(side, amount);

            // Укол уже идёт: второй раз анимацию не запускаем (она сломалась бы),
            // просто списываем урон сразу, чтобы попадание не потерялось.
            if (_needleInjectionActive)
            {
                Debug.LogWarning($"[HealthController] Укол иглой ещё идёт — урон {side} списывается сразу, без анимации.", this);
                return ApplyDamageNow(side, amount);
            }

            _pendingDamageSide = side;
            _pendingDamageAmount = amount;
            _hasPendingDamage = true;

            _needleInjectionActive = true;
            StartCoroutine(NeedleInjectionRoutine(side));

            return false;
        }

        /// <summary>
        /// Списывает отложенный урон укола иглой. Вызывается из анимации —
        /// Animation Event на объекте иглы (NeedleMetalSqueak.ApplyDamage).
        /// Из обычного кода вызывать не нужно: там достаточно ApplyDamage.
        /// </summary>
        public void ApplyPendingDamage()
        {
            if (!_hasPendingDamage)
            {
                Debug.Log($"[HealthController] Событие анимации укола пришло без отложенного урона — списывать нечего ({name}).", this);
                return;
            }

            TurnSide side = _pendingDamageSide;
            int amount = _pendingDamageAmount;
            _hasPendingDamage = false;

            ApplyDamageNow(side, amount);
        }

        /// <summary>
        /// Ждёт окончания укола иглой: пока идёт анимация, урон может быть ещё
        /// не списан. Возвращает управление, когда укол доигран и отложенный
        /// урон списан — после этого можно читать IsDead(side).
        /// </summary>
        public IEnumerator WaitForNeedleInjection()
        {
            while (_needleInjectionActive || _hasPendingDamage)
                yield return null;
        }

        private IEnumerator NeedleInjectionRoutine(TurnSide side)
        {
            try
            {
                var needle = GetNeedleInjection(side);

                if (needle == null || !needle.CanPlayNeedleInjection)
                {
                    Debug.LogWarning($"[HealthController] Игла для стороны {side} не найдена в сцене — урон списывается без анимации укола.", this);
                    ApplyPendingDamage();
                    yield break;
                }

                yield return needle.PlayNeedleInjection();
            }
            finally
            {
                _needleInjectionActive = false;
            }
        }

        private DamageFeedbackBase GetNeedleInjection(TurnSide side) =>
            side == TurnSide.Player
                ? _playerDamageFeedback
                : (DamageFeedbackBase)_enemyDamageFeedback;

        /// <summary>Непосредственное списание урона — без анимаций и ожиданий.</summary>
        private bool ApplyDamageNow(TurnSide side, int amount)
        {
            if (_dead.Contains(side) || amount <= 0)
                return false;

            // ---  ЛОГИКА ЩИТА ---
            if (_shielded.Contains(side))
            {
                _shielded.Remove(side);
                GameEvents.RaiseShieldBroken(side);
                return false; // Урон заблокирован, дальше ничего не происходит
            }
            // -------------------------

            int max = GetMaxHealth(side);
            int rawDose = GetHealth(side) + amount;
            bool overdosed = rawDose >= max;
            int clampedDose = Mathf.Min(max, rawDose);
            _current[side] = clampedDose;

            // При смертельном попадании обычные звуки урона не запускаем:
            // death-звук должен быть единственным звуком этого попадания.
            if (!overdosed && side == TurnSide.Player)
            {
                PlayDamageSounds(side);
            }

            GameEvents.RaiseHealthChanged(side, clampedDose, max);
            GameEvents.RaiseDamageTaken(side, amount, clampedDose, max, overdosed);
            UpdateDoseCounterParameter(side, overdosed);

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
                        if (_deathSoundInstance.getDescription(out var deathDescription) == FMOD.RESULT.OK
                            && deathDescription.getLength(out int deathLengthMilliseconds) == FMOD.RESULT.OK)
                        {
                            DeathSoundDuration = deathLengthMilliseconds / 1000f;
                        }
                        else
                        {
                            DeathSoundDuration = 0f;
                        }

                        _deathSoundInstance.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(soundPosition));
                        _deathSoundInstance.start();
                        _deathSoundInstance.release(); // FMOD сам удалит объект из памяти после завершения
                    }
                }

                GameEvents.RaiseSideDied(side);
            }

            return overdosed;
        }

        /// <summary>
        /// Восстанавливает HP/дозу из сохранённого чекпоинта — в отличие от
        /// Initialize() не обнуляет, а выставляет конкретные значения напрямую.
        /// Чекпоинт по построению сохраняется только пока никто ещё не умер в этом
        /// раунде, но на всякий случай явно снимаем "мёртв", если он почему-то
        /// был выставлен раньше.
        /// </summary>
        public void RestoreState(TurnSide side, int current, int max)
        {
            _max[side] = max;
            _current[side] = Mathf.Clamp(current, 0, max);
            _dead.Remove(side);
            GameEvents.RaiseHealthChanged(side, _current[side], max);
            UpdateDoseCounterParameter(side);
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

        private void PlayDamageSounds(TurnSide side)
        {
            if (HealthSoundProvider.Instance == null)
                return;

            var provider = HealthSoundProvider.Instance;
            Vector3 soundPosition = GetSidePosition(side);

            if (!provider.injectionSound.IsNull)
                FMODUnity.RuntimeManager.PlayOneShot(provider.injectionSound);

            var damageSound = side == TurnSide.Player ? provider.playerDamageSound : provider.enemyDamageSound;
            if (!damageSound.IsNull)
                FMODUnity.RuntimeManager.PlayOneShot(damageSound, soundPosition);
        }

        private void UpdateDoseCounterParameter(TurnSide side, bool ignoreSeekSpeed = false)
        {
            if (side != TurnSide.Player) return;

            int dose = GetHealth(side);

            // Теперь просто передаем текущее значение напрямую.
            // Клампаем до 5 (DoseCounterMax) на всякий случай, если здоровье превысит 5, 
            // чтобы FMOD не получил значение, выходящее за рамки его шкалы.
            int value = Mathf.Clamp(dose, 0, DoseCounterMax);
            
            FMODUnity.RuntimeManager.StudioSystem.setParameterByName(DoseCounterParameterName, value, ignoreSeekSpeed);
        }

        private void OnDisable()
        {
            // Отложенный урон укола списывать уже некому и незачем: сцену
            // сворачивают, а списать его значило бы выстрелить событиями урона
            // и смерти прямо во время перехода. Просто гасим ожидание.
            _hasPendingDamage = false;
            _needleInjectionActive = false;

            if (_deathSoundInstance.isValid())
            {
                _deathSoundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }
        }
    }
}