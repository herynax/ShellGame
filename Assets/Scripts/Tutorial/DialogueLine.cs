using FMODUnity;
using TMPro;
using UnityEngine;

namespace ShellGame.Tutorial
{
    /// <summary>
    /// Одна реплика обучения — данные, редактируемые в инспекторе.
    /// Создаётся как ассет: правый клик в Project → Create → ShellGame →
    /// Tutorial → Dialogue Line. Один ассет = одна реплика, удобно
    /// переиспользовать между сценариями и переводить на другие языки.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueLine", menuName = "ShellGame/Tutorial/Dialogue Line")]
    public sealed class DialogueLine : ScriptableObject
    {
        [SerializeField, TextArea(2, 5)] private string _text;

        [Header("Оформление текста (TMP)")]
        [Tooltip("Необязательно — если не задано, используется шрифт, стоящий на текстовом объекте DialogueView по умолчанию.")]
        [SerializeField] private TMP_FontAsset _fontAsset;
        [Tooltip("Материал TMP (пресет шрифта — обводка, глоу и т.п.). Необязательно.")]
        [SerializeField] private Material _fontMaterial;
        [SerializeField] private Color _textColor = Color.white;

        [Header("Звук (FMOD, необязательно)")]
        [Tooltip("Озвучка реплики — проигрывается одновременно с показом текста. Это ИМЕННО голос, а не SFX — для отдельных звуков (вжух камеры и т.п.) используйте шаг PlaySfx.")]
        [SerializeField] private EventReference _voiceEvent;

        [Header("Тайминг")]
        [Tooltip("Минимальное время показа реплики, даже если игрок сразу кликнул.")]
        [SerializeField] private float _minDisplayDuration = 0.6f;
        [Tooltip("Если включено — реплика ждёт клик/пробел, чтобы закрыться. Если выключено — закроется сама через minDisplayDuration.")]
        [SerializeField] private bool _waitForClick = true;

        public string Text => _text;
        public TMP_FontAsset FontAsset => _fontAsset;
        public Material FontMaterial => _fontMaterial;
        public Color TextColor => _textColor;
        public EventReference VoiceEvent => _voiceEvent;
        public float MinDisplayDuration => _minDisplayDuration;
        public bool WaitForClick => _waitForClick;
    }
}
