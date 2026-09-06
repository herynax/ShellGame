using System;
using System.Collections;
using DG.Tweening;
using FMODUnity;
using ShellGame.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShellGame.UI
{
    /// <summary>
    /// Экран статистики забега (в духе Kaycee's Mod из Inscryption).
    /// Показывается после того как экран стал полностью чёрным (SceneLoader),
    /// последовательно "зачисляет" каждую строку статистики со звуком,
    /// и ждёт клика по кнопке продолжения перед тем как отдать управление обратно.
    /// </summary>
    public sealed class RunStatsScreenController : MonoBehaviour
    {
        [Header("Панель")]
        [SerializeField] private CanvasGroup _panelGroup;
        [SerializeField] private RectTransform _panelRoot;
        [SerializeField] private Button _continueButton;

        [Header("Строки статистики (порядок = порядок появления)")]
        [SerializeField] private StatRow _timeRow;
        [SerializeField] private StatRow _movesRow;
        [SerializeField] private StatRow _mistakesRow;
        [SerializeField] private StatRow _enemiesRow;
        [SerializeField] private StatRow _streakRow;
        [SerializeField] private StatRow _accuracyRow;
        [SerializeField] private TextMeshProUGUI _rankLabel;

        [Header("Тайминги")]
        [SerializeField] private float _panelFadeInDuration = 0.4f;
        [SerializeField] private float _panelFadeOutDuration = 0.15f;
        [SerializeField] private float _counterDuration = 0.8f;
        [SerializeField] private float _rowRevealInterval = 0.35f;
        [SerializeField] private float _tickMinInterval = 0.04f;

        [Header("FMOD")]
        [SerializeField] private EventReference _tickSound;
        [SerializeField] private EventReference _rowRevealStinger;
        [SerializeField] private EventReference _finalRankStinger;

        private bool _continueRequested;
        private CursorLockMode _previousCursorLockState;
        private bool _previousCursorVisible;

        private void Awake()
        {
            if (_panelGroup != null)
            {
                _panelGroup.alpha = 0f;
                _panelGroup.blocksRaycasts = false;
            }

            if (_continueButton != null)
            {
                _continueButton.interactable = false;
                _continueButton.gameObject.SetActive(false);
                _continueButton.onClick.AddListener(() => _continueRequested = true);
            }

            gameObject.SetActive(false);
        }

        public IEnumerator ShowAndWaitForContinue(RunStatsSnapshot snapshot)
        {
            _previousCursorLockState = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            gameObject.SetActive(true);
            _continueRequested = false;
            if (_continueButton != null)
            {
                _continueButton.interactable = false;
                _continueButton.gameObject.SetActive(false);
            }

            foreach (var row in new[] { _timeRow, _movesRow, _mistakesRow, _enemiesRow, _streakRow, _accuracyRow })
                row?.Root?.gameObject.SetActive(false);
            if (_rankLabel != null) _rankLabel.gameObject.SetActive(false);

            if (_panelGroup != null)
            {
                _panelGroup.blocksRaycasts = true;
                yield return _panelGroup.DOFade(1f, _panelFadeInDuration).SetUpdate(true).WaitForCompletion();
            }

            if (_panelRoot != null)
            {
                _panelRoot.localScale = Vector3.one * 0.9f;
                yield return _panelRoot.DOScale(1f, _panelFadeInDuration).SetEase(Ease.OutBack).SetUpdate(true).WaitForCompletion();
            }

            int totalSeconds = Mathf.RoundToInt(snapshot.ElapsedSeconds);

            yield return RevealRow(_timeRow, totalSeconds, RowFormat.Time);
            yield return RevealRow(_movesRow, snapshot.TotalMoves, RowFormat.Plain);
            yield return RevealRow(_mistakesRow, snapshot.Mistakes, RowFormat.Plain);
            yield return RevealRow(_enemiesRow, snapshot.EnemiesDefeated, RowFormat.Plain);
            yield return RevealRow(_streakRow, snapshot.BestStreak, RowFormat.Plain);
            yield return RevealRow(_accuracyRow, Mathf.RoundToInt(snapshot.Accuracy * 100f), RowFormat.Percent);

            if (_rankLabel != null)
            {
                _rankLabel.gameObject.SetActive(true);
                _rankLabel.text = ResolveRank(snapshot);
                _rankLabel.transform.localScale = Vector3.zero;
                if (!_finalRankStinger.IsNull) RuntimeManager.PlayOneShot(_finalRankStinger);
                yield return _rankLabel.transform
                    .DOScale(1f, 0.5f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .WaitForCompletion();
            }

            if (_continueButton != null)
            {
                _continueButton.gameObject.SetActive(true);
                _continueButton.interactable = true;
            }

            while (!_continueRequested) yield return null;

            if (_panelGroup != null)
            {
                yield return _panelGroup.DOFade(0f, _panelFadeOutDuration).SetUpdate(true).WaitForCompletion();
                _panelGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
            Cursor.lockState = _previousCursorLockState;
            Cursor.visible = _previousCursorVisible;
        }

        private enum RowFormat { Plain, Time, Percent }

        private IEnumerator RevealRow(StatRow row, int targetValue, RowFormat format)
        {
            if (row == null || row.ValueLabel == null) yield break;

            row.Root?.gameObject.SetActive(true);
            if (row.Root != null)
            {
                row.Root.localScale = Vector3.one * 0.85f;
                row.Root.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            if (!_rowRevealStinger.IsNull)
                RuntimeManager.PlayOneShot(_rowRevealStinger);

            float lastTickTime = -999f;
            yield return DOTween.To(() => 0f, value =>
                {
                    if (Time.unscaledTime - lastTickTime >= _tickMinInterval)
                    {
                        if (!_tickSound.IsNull) RuntimeManager.PlayOneShot(_tickSound);
                        lastTickTime = Time.unscaledTime;
                    }

                    row.ValueLabel.text = FormatValue(Mathf.RoundToInt(value), format);
                }, targetValue, _counterDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .WaitForCompletion();

            row.ValueLabel.text = FormatValue(targetValue, format);
            yield return new WaitForSecondsRealtime(_rowRevealInterval);
        }

        private static string FormatValue(int value, RowFormat format)
        {
            switch (format)
            {
                case RowFormat.Time:
                    return $"{value / 60:00}:{value % 60:00}";
                case RowFormat.Percent:
                    return $"{value}%";
                default:
                    return value.ToString();
            }
        }

        private static string ResolveRank(RunStatsSnapshot snapshot)
        {
            if (snapshot.Accuracy >= 0.9f && snapshot.EnemiesDefeated >= 3) return "S";
            if (snapshot.Accuracy >= 0.75f && snapshot.EnemiesDefeated >= 2) return "A";
            if (snapshot.Accuracy >= 0.5f) return "B";
            return "C";
        }

        [Serializable]
        public sealed class StatRow
        {
            public RectTransform Root;
            public TextMeshProUGUI ValueLabel;
        }
    }
}