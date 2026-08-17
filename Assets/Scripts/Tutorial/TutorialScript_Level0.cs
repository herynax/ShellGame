using Unity.Cinemachine;
using FMODUnity;
using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Gameplay;
using UnityEngine;

namespace ShellGame.Tutorial
{
    public sealed class TutorialScript_Level0 : MonoBehaviour
    {
        [Header("Игровые системы")]
        [SerializeField] private GameManager _gameManager;

        [Header("Камеры")]
        [SerializeField] private CinemachineCamera _gameplayCamera;
        [SerializeField] private CinemachineCamera _narratorCamera;
        [SerializeField] private CinemachineCamera _buttonCamera;
        [SerializeField] private CinemachineCamera _tableCenterCamera;
        [SerializeField] private CinemachineCamera _healthBarCamera;

        [SerializeField] private int _focusPriority = 20;
        [SerializeField] private EventReference _zoomWhooshSfx;

        [Header("UI здоровья")]
        [SerializeField] private GameObject _enemyHealthBarRoot;
        [SerializeField] private GameObject _playerHealthBarRoot;
        [SerializeField] private float _pauseBetweenLines = 1.0f;

        [Header("Реплики по сценам")]
        [SerializeField] private DialogueLine[] _scene0_WakeUp;
        [SerializeField] private DialogueLine[] _scene1_BeforeClick;
        [SerializeField] private DialogueLine _scene1_AfterClick;

        [Header("Сцена 2 (Показ метки)")]
        [SerializeField] private DialogueLine _scene2_See;
        [SerializeField] private DialogueLine _scene2_ThereItIs;
        [SerializeField] private DialogueLine _scene2_Remember;

        [Header("Сцена 3 (Перемешивание)")]
        [SerializeField] private DialogueLine _scene3_DontBlink;
        [SerializeField] private DialogueLine _scene3_WatchIt;
        [SerializeField] private DialogueLine _scene3_NotTheCups;
        [SerializeField] private DialogueLine _scene3_OnIt;

        [Header("Сцена 4 (После перемешивания)")]
        [SerializeField] private DialogueLine _scene4_Well;
        [SerializeField] private DialogueLine _scene4_WhereIsIt;

        [Header("Сцена 5 (Угадал)")]
        [SerializeField] private DialogueLine _scene5_Lucky;
        [SerializeField] private DialogueLine _scene5_Good;
        [SerializeField] private DialogueLine _scene5_Watch;

        [Header("Сцена 6+ (Здоровье и ход врага)")]
        [SerializeField] private DialogueLine[] _scene6_Lines;
        [SerializeField] private DialogueLine _scene7_MyTurn;
        [SerializeField] private DialogueLine _scene8_Sorry;
        [SerializeField] private DialogueLine[] _scene9_Lines;

        [Header("Сцена 10 (Конец)")]
        [SerializeField] private DialogueLine _scene10_YourTurn;
        [SerializeField] private DialogueLine _scene10_NoHints;

        [SerializeField] private TutorialSequencer _sequencer;
        private ShuffleSystem _shuffleSystem;

        private void Awake()
        {
            if (_enemyHealthBarRoot != null) _enemyHealthBarRoot.SetActive(false);
            if (_playerHealthBarRoot != null) _playerHealthBarRoot.SetActive(false);
            if (_sequencer == null) _sequencer = gameObject.AddComponent<TutorialSequencer>();

            // ВАЖНО: ShuffleSystem.TutorialStepMode больше НЕ выставляем здесь.
            // Порядок Awake() между разными компонентами сцены не гарантирован
            // одинаковым в билде (особенно IL2CPP) так же строго, как в
            // редакторе (Mono) — там могло годами "случайно" работать за счёт
            // стабильного порядка объектов в иерархии. В билде FindObjectOfType
            // здесь либо не находил ShuffleSystem (объект ещё не активен/не
            // заспавнен), либо этот Awake() отрабатывал позже, чем
            // GameManager успевал дойти до первого StartShuffling — из-за
            // этого TutorialStepMode оставался false, и первое перемешивание
            // шло одним сплошным непрерываемым циклом, как в обычной игре,
            // вместо пошагового режима для сцены 3.
            //
            // Теперь TutorialStepMode выставляется прямо в Play() — в сцене 2,
            // с большим запасом по времени (пара реплик + ожидание State ==
            // Shuffle) до того, как GameManager реально вызовет
            // ShuffleSystem.StartShuffling(). Это убирает гонку полностью,
            // независимо от порядка Awake() в конкретной сборке.
        }

        private void Start()
        {
            Play();
        }

        public void Play()
        {
            var builder = TutorialBuilder.Create();
            int p = _focusPriority;

            // Сцена 0: Пробуждение
            builder.Wait(new CameraFocus(_narratorCamera, p, 0f)).WaitSeconds(_pauseBetweenLines);
            SayEach(builder, _scene0_WakeUp);

            // Сцена 1: Кнопка начала
            p++;
            builder.Do(() =>
            {
                var btn = FindFirstObjectByType<RoundStartButton>(FindObjectsInactive.Include);
                if (btn != null) btn.SetInteractable(false);
            });

            builder.Wait(new CameraFocus(_buttonCamera, p, 0.8f));

            if (_scene1_BeforeClick != null && _scene1_BeforeClick.Length > 0)
            {
                for (int i = 0; i < _scene1_BeforeClick.Length; i++)
                {
                    builder.Say(_scene1_BeforeClick[i]);
                    if (i < _scene1_BeforeClick.Length - 1) builder.WaitSeconds(_pauseBetweenLines);
                }
            }

            builder.Do(() =>
            {
                var btn = FindFirstObjectByType<RoundStartButton>(FindObjectsInactive.Include);
                if (btn != null) btn.SetInteractable(true);
            });

            builder.Wait(new WaitForEvent(
                    h => GameEvents.RoundStartConfirmed += h,
                    h => GameEvents.RoundStartConfirmed -= h))
                .Say(_scene1_AfterClick);

            // Сцена 2: Показ метки
            p++;
            builder.Wait(new CameraFocus(_tableCenterCamera, p, 0.8f));
            builder.Say(_scene2_See);

            builder.Do(() =>
            {
                if (_gameManager != null)
                {
                    _gameManager.LockTutorialPlayerChoice();
                    _gameManager.ContinueTutorialReveal();
                }

                // Включаем пошаговый режим шафла ЗАРАНЕЕ, с запасом — раунд
                // дойдёт до RoundState.Shuffle только через Reveal (пауза
                // спавна + показ + удержание меток), так что времени на
                // поиск ShuffleSystem более чем достаточно в любой сборке.
                _shuffleSystem = FindFirstObjectByType<ShuffleSystem>();
                if (_shuffleSystem != null)
                    _shuffleSystem.TutorialStepMode = true;
                else
                    Debug.LogError("[TutorialScript_Level0] ShuffleSystem не найден на сцене в сцене 2 — сцена 3 пойдёт непрерывным шафлом вместо пошагового.");
            });

            builder.WaitSeconds(0.3f);
            builder.Say(_scene2_ThereItIs);

            builder.WaitUntil(() => _gameManager != null && _gameManager.State == RoundState.Shuffle);

            // Страховка: если ShuffleSystem почему-то не нашёлся выше (сцена
            // ещё не успела его создать) — пробуем найти ещё раз прямо
            // перед тем, как он реально понадобится в сцене 3.
            builder.Do(() =>
            {
                if (_shuffleSystem == null)
                {
                    _shuffleSystem = FindFirstObjectByType<ShuffleSystem>();
                    if (_shuffleSystem != null)
                        _shuffleSystem.TutorialStepMode = true;
                    else
                        Debug.LogError("[TutorialScript_Level0] ShuffleSystem всё ещё не найден перед сценой 3.");
                }
            });

            builder.Say(_scene2_Remember);

            // Сцена 3: 4 контролируемых шага перемешивания
            builder.Do(() => { if (_shuffleSystem != null) _shuffleSystem.TriggerNextStep(); });
            builder.WaitSeconds(0.1f);
            builder.WaitUntil(() => _shuffleSystem != null && _shuffleSystem.IsWaitingForStep);
            builder.Say(_scene3_DontBlink);

            builder.Do(() => { if (_shuffleSystem != null) _shuffleSystem.TriggerNextStep(); });
            builder.WaitSeconds(0.1f);
            builder.WaitUntil(() => _shuffleSystem != null && _shuffleSystem.IsWaitingForStep);
            builder.Say(_scene3_WatchIt);

            builder.Do(() => { if (_shuffleSystem != null) _shuffleSystem.TriggerNextStep(); });
            builder.WaitSeconds(0.1f);
            builder.WaitUntil(() => _shuffleSystem != null && _shuffleSystem.IsWaitingForStep);
            builder.Say(_scene3_NotTheCups);

            builder.Do(() => { if (_shuffleSystem != null) _shuffleSystem.TriggerNextStep(); });
            builder.WaitSeconds(0.1f);
            builder.WaitUntil(() => _shuffleSystem != null && _shuffleSystem.IsWaitingForStep);
            builder.Say(_scene3_OnIt);

            builder.Do(() =>
            {
                if (_shuffleSystem != null)
                {
                    _shuffleSystem.TriggerNextStep();
                    _shuffleSystem.TutorialStepMode = false;
                }
            });

            builder.WaitUntil(() => _gameManager != null && _gameManager.State == RoundState.PlayerTurn);

            // Сцена 4: Разрешаем выбор игроку
            builder
                .WaitSeconds(_pauseBetweenLines).Say(_scene4_Well)
                .WaitSeconds(_pauseBetweenLines).Say(_scene4_WhereIsIt);

            builder.Do(() => { if (_gameManager != null) _gameManager.UnlockTutorialPlayerChoice(); });
            builder.Do(() => { if (_gameManager != null) _gameManager.PauseTutorialBeforeDamage(); });

            builder.Wait(new WaitForShellSelected());
            builder.WaitSeconds(1.5f);

            // Сцена 5: Игрок угадал
            p++;
            builder.Wait(new CameraFocus(_narratorCamera, p, 0.8f));
            builder.Say(_scene5_Lucky).WaitSeconds(_pauseBetweenLines).Say(_scene5_Good);
            if (_scene5_Watch != null)
                builder.WaitSeconds(_pauseBetweenLines).Say(_scene5_Watch);

            // Наносим урон врагу
            builder.Do(() => { if (_gameManager != null) _gameManager.PauseTutorialAfterDamage(); });
            builder.Do(() => { if (_gameManager != null) _gameManager.ResumeTutorialBeforeDamage(); });
            builder.WaitSeconds(0.5f);

            // Сцена 6: Показ здоровья
            p++;
            builder.Do(() =>
            {
                if (_enemyHealthBarRoot != null) _enemyHealthBarRoot.SetActive(true);
                if (_playerHealthBarRoot != null) _playerHealthBarRoot.SetActive(true);
            });

            builder.Parallel(
                b => b.Wait(new PlaySfx(_zoomWhooshSfx)).Wait(new CameraFocus(_healthBarCamera, p, 0.8f)),
                b =>
                {
                    if (_scene6_Lines != null && _scene6_Lines.Length > 0)
                        b.Say(_scene6_Lines[0]);
                    return b;
                }
            );

            p++;
            builder.Wait(new CameraFocus(_narratorCamera, p, 0.8f));

            if (_scene6_Lines != null && _scene6_Lines.Length > 1)
            {
                for (int i = 1; i < _scene6_Lines.Length; i++)
                {
                    builder.Say(_scene6_Lines[i]);
                    if (i < _scene6_Lines.Length - 1) builder.WaitSeconds(_pauseBetweenLines);
                }
            }

            // Сцена 7: "Теперь я." и фокус на стол
            builder.Say(_scene7_MyTurn);

            p++;
            builder.Wait(new CameraFocus(_tableCenterCamera, p, 0.8f));

            // Блокируем урон ДО передачи хода врагу
            builder.Do(() => { if (_gameManager != null) _gameManager.PauseTutorialBeforeDamage(); });

            // Передаем ход врагу
            builder.Do(() => { if (_gameManager != null) _gameManager.ResumeTutorialAfterDamage(); });

            // Ждем выбора наперстка врагом
            builder.Wait(new WaitForShellSelected());
            builder.WaitSeconds(1.0f); // Даем наперстку подняться

            // Сцена 8: Камера на врага и реплика "Прости"
            p++;
            builder.Wait(new CameraFocus(_narratorCamera, p, 0.8f));
            builder.Say(_scene8_Sorry);

            // Блокируем смену хода после удара (чтобы игра не пошла дальше во время реплик 9 и 10)
            builder.Do(() => { if (_gameManager != null) _gameManager.PauseTutorialAfterDamage(); });

            // Наносим урон игроку
            builder.Do(() => { if (_gameManager != null) _gameManager.ResumeTutorialBeforeDamage(); });
            builder.WaitSeconds(0.6f);

            // Сцена 9: Реплики про боль
            SayEach(builder, _scene9_Lines);

            // Сцена 10: "Теперь твоя очередь" / "Без подсказок"
            builder.Say(_scene10_YourTurn).WaitSeconds(_pauseBetweenLines).Say(_scene10_NoHints);

            // Возвращаем камеру на основную и делаем паузу 2 секунды
            p++;
            builder.Wait(new CameraFocus(_gameplayCamera, p, 1.0f));
            builder.WaitSeconds(2.0f);

            // И ТОЛЬКО ТЕПЕРЬ отпускаем игру в стандартный боевой цикл
            builder.Do(() => { if (_gameManager != null) _gameManager.ResumeTutorialAfterDamage(); });

            _sequencer.Play(builder.Build());
        }

        private TutorialBuilder SayEach(TutorialBuilder builder, DialogueLine[] lines)
        {
            if (lines == null) return builder;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == null) continue;
                builder.Say(lines[i]);
                bool hasMoreAfter = false;
                for (int j = i + 1; j < lines.Length; j++)
                    if (lines[j] != null) { hasMoreAfter = true; break; }
                if (hasMoreAfter) builder.WaitSeconds(_pauseBetweenLines);
            }
            return builder;
        }
    }
}