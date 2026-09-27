// START OF FILE DialogueView.cs
using System.Collections;
using ShellGame.Audio;
using ShellGame.Core;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ShellGame.Tutorial
{
    public sealed class DialogueView : MonoBehaviour, IDialogueService
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TextMeshProUGUI _textLabel;

        public static event System.Action<bool> OnDialogueActive;

        private IAudioService _audio;

        private void Awake()
        {
            ServiceLocator.Register<IDialogueService>(this);
            Hide();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<IDialogueService>();
        }

        public IEnumerator ShowLine(DialogueLine line)
        {
            if (line == null)
                yield break;

            Show(line);

            // Клик, которым игрок закрыл ПРЕДЫДУЩУЮ реплику, не должен закрывать
            // и эту: без защиты новая реплика получала clicked = true в том же
            // кадре, когда только появилась, и тут же уходила по своему таймеру
            // (выглядело как «автоскип» сразу после скипа игрока). Ввод в кадре
            // показа игнорируем — таймер реплики стартует заново, и чтобы её
            // закрыть, нужен свой клик.
            int shownFrame = Time.frameCount;

            float timer = 0f;
            bool clicked = false;

            while (true)
            {
                timer += Time.deltaTime;

                bool inputTriggered = false;

#if ENABLE_INPUT_SYSTEM
                // �������� ������ ����� ������ ���� (���)
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                    inputTriggered = true;

                // �������� ������ ������ � Enter
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
                    inputTriggered = true;

                // ��� �� ������ (�������������� � ��� ��� �������/���������)
                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                    inputTriggered = true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
                // ������ ������� �����: ������ ���(0), ������ � Enter/Return
                if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                    inputTriggered = true;
#endif

                if (inputTriggered && Time.frameCount > shownFrame)
                    clicked = true;

                bool minTimePassed = timer >= line.MinDisplayDuration;

                // Автоскип: реплика реакции врага уходит сама, даже если
                // игрок не кликнул — иначе игра встаёт намертво, пока он
                // читает. Игрок может закрыть её раньше, клик по-прежнему
                // работает. 0 = ждать клика до конца.
                bool autoSkipped = line.AutoSkipSeconds > 0f && timer >= line.AutoSkipSeconds;

                bool finished = autoSkipped || (line.WaitForClick ? (minTimePassed && clicked) : minTimePassed);

                if (finished)
                    break;

                yield return null;
            }

            Hide();
        }

        private void Show(DialogueLine line)
        {
            if (_root != null)
                _root.SetActive(true);

            if (_textLabel != null)
            {
                _textLabel.text = line.Text;
                _textLabel.color = line.TextColor;

                if (line.FontAsset != null)
                    _textLabel.font = line.FontAsset;

                if (line.FontMaterial != null)
                    _textLabel.fontMaterial = line.FontMaterial;
            }

            if (_audio == null)
            {
                ServiceLocator.TryGet(out _audio);
            }

            if (!line.VoiceEvent.IsNull)
            {
                if (_audio != null)
                    _audio.PlayOneShot(line.VoiceEvent);
            }

            OnDialogueActive?.Invoke(true);
        }

        private void Hide()
        {
            if (_root != null)
                _root.SetActive(false);

            OnDialogueActive?.Invoke(false);
        }
    }
}
// END OF FILE