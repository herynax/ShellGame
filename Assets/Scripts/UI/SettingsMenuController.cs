using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using SpankyBoy.JuiceUI.Free;

namespace ShellGame.Meta
{
    /// <summary>
    /// Контроллер панели настроек.
    /// </summary>
    public class SettingsMenuController : MonoBehaviour
    {
        [Serializable]
        public class SettingsTab
        {
            [Tooltip("Название вкладки — только для читаемости в инспекторе/логах")]
            public string tabName;

            [Tooltip("Кнопка вкладки сверху")]
            public Button tabButton;

            [Tooltip("Текст на кнопке вкладки")]
            public TMP_Text tabLabel;

            [Tooltip("Панель с контентом этой вкладки")]
            public CanvasGroup panel;
        }

        [Header("Вкладки (порядок = порядок кнопок сверху)")]
        [SerializeField] private List<SettingsTab> tabs = new List<SettingsTab>();

        [Header("Цвет текста вкладки")]
        [SerializeField] private Color activeTabColor = new Color(0.82f, 0.24f, 0.14f);
        [SerializeField] private Color inactiveTabColor = new Color(0.80f, 0.70f, 0.55f);

        [Header("Анимация смены контента")]
        [SerializeField] private float fadeDuration = 0.15f;

        [Header("Вкладка, которая открывается первой")]
        [SerializeField] private int defaultTabIndex = 0;

        private SettingsTab _currentTab;

        private void Awake()
        {
            foreach (var tab in tabs)
            {
                if (tab.tabButton == null)
                    continue;

                var capturedTab = tab;

                tab.tabButton.onClick.AddListener(() => SelectTab(capturedTab));
            }
        }

        private void OnEnable()
        {
            if (tabs.Count == 0)
                return;

            int index = Mathf.Clamp(defaultTabIndex, 0, tabs.Count - 1);
            SelectTabImmediate(tabs[index]);
        }

        public void SelectTab(SettingsTab tab)
        {
            if (tab == null || tab == _currentTab)
                return;

            var previous = _currentTab;
            _currentTab = tab;

            UpdateTabVisuals();

            // --------------------------------------------------
            // СНАЧАЛА ПРИНУДИТЕЛЬНО ЗАВЕРШАЕМ ВСЕ СТАРЫЕ TWEEN'Ы
            // --------------------------------------------------

            CompleteAllPanelTweens();

            // --------------------------------------------------
            // Теперь состояние всех панелей гарантированно чистое.
            // Старые OnComplete больше не смогут неожиданно
            // выключить панель после нашего переключения.
            // --------------------------------------------------

            if (previous?.panel != null)
            {
                FadeOutPanel(previous.panel);
            }

            if (tab.panel != null)
            {
                FadeInPanel(tab.panel);
            }
        }

        /// <summary>
        /// Принудительно завершает все текущие анимации панелей.
        /// Это критично при быстром переключении вкладок.
        /// </summary>
        private void CompleteAllPanelTweens()
        {
            foreach (var tab in tabs)
            {
                if (tab.panel == null)
                    continue;

                var panel = tab.panel;

                // Сначала завершаем tween.
                // DOComplete вызывает OnComplete старого tween'а.
                panel.DOComplete();

                // После завершения гарантированно убиваем tween,
                // чтобы новый tween не конфликтовал со старым.
                panel.DOKill();
            }
        }

        private void SelectTabImmediate(SettingsTab tab)
        {
            if (tab == null)
                return;

            // При повторном открытии меню старые tween'ы не должны жить.
            CompleteAllPanelTweens();

            _currentTab = tab;

            foreach (var t in tabs)
            {
                if (t.panel == null)
                    continue;

                bool isActive = t == tab;

                t.panel.DOKill();

                t.panel.gameObject.SetActive(isActive);
                t.panel.alpha = isActive ? 1f : 0f;
                t.panel.interactable = isActive;
                t.panel.blocksRaycasts = isActive;
            }

            UpdateTabVisuals();
        }

        private void FadeOutPanel(CanvasGroup panel)
        {
            if (panel == null)
                return;

            panel.interactable = false;
            panel.blocksRaycasts = false;

            if (panel.TryGetComponent<PanelAnimator_Free>(out var animator))
            {
                animator.Hide();
                return;
            }

            panel.DOKill();

            panel.DOFade(0f, fadeDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    // Дополнительная защита:
                    // к моменту выполнения callback панель могла
                    // уже снова стать активной.
                    if (panel.alpha <= 0.001f)
                    {
                        panel.gameObject.SetActive(false);
                    }
                });
        }

        private void FadeInPanel(CanvasGroup panel)
        {
            if (panel == null)
                return;

            panel.DOKill();

            panel.gameObject.SetActive(true);
            panel.interactable = true;
            panel.blocksRaycasts = true;

            if (panel.TryGetComponent<PanelAnimator_Free>(out var animator))
            {
                animator.Show();
                return;
            }

            panel.alpha = 0f;

            panel.DOFade(1f, fadeDuration)
                .SetUpdate(true);
        }

        private void UpdateTabVisuals()
        {
            foreach (var t in tabs)
            {
                if (t.tabLabel == null)
                    continue;

                t.tabLabel.color =
                    (t == _currentTab)
                        ? activeTabColor
                        : inactiveTabColor;
            }
        }

        public void SelectNextTab()
        {
            if (tabs.Count == 0)
                return;

            int currentIndex = tabs.IndexOf(_currentTab);
            int nextIndex = (currentIndex + 1) % tabs.Count;

            SelectTab(tabs[nextIndex]);
        }

        public void SelectPreviousTab()
        {
            if (tabs.Count == 0)
                return;

            int currentIndex = tabs.IndexOf(_currentTab);
            int prevIndex = (currentIndex - 1 + tabs.Count) % tabs.Count;

            SelectTab(tabs[prevIndex]);
        }
    }
}