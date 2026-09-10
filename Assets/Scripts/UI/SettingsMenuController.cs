// START OF FILE SettingsMenuController.cs
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
    /// Контроллер панели настроек в духе "Мор" — вкладки сверху (Дисплей / Графика / Аудио /
    /// Другое / Управление), под ними сменяется контентная панель выбранной категории.
    ///
    /// ВАЖНО: сама панель настроек (тот GameObject, на котором висит этот скрипт) — это обычный
    /// элемент стека меню MainMenuController/PauseController и открывается через их OpenSubmenu(...),
    /// как любое другое подменю. А вот переключение вкладок ВНУТРИ настроек — уже локальная логика
    /// этого скрипта и общий стек меню никак не трогает (Esc всё так же закрывает весь блок настроек
    /// целиком, а не отдельную вкладку).
    /// </summary>
    public class SettingsMenuController : MonoBehaviour
    {
        [Serializable]
        public class SettingsTab
        {
            [Tooltip("Название вкладки — только для читаемости в инспекторе/логах")]
            public string tabName;

            [Tooltip("Кнопка вкладки сверху (Дисплей / Графика / Аудио / ...)")]
            public Button tabButton;

            [Tooltip("Текст на кнопке вкладки — перекрашивается при выборе. Если используете " +
                     "обычный UI.Text, а не TMP — поменяйте тип поля ниже на Text.")]
            public TMP_Text tabLabel;

            [Tooltip("Панель с контентом этой вкладки")]
            public CanvasGroup panel;
        }

        [Header("Вкладки (порядок = порядок кнопок сверху)")]
        [SerializeField] private List<SettingsTab> tabs = new List<SettingsTab>();

        [Header("Цвет текста вкладки")]
        [SerializeField] private Color activeTabColor = new Color(0.82f, 0.24f, 0.14f);   // как "ГРАФИКА" на скрине
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
                if (tab.tabButton == null) continue;

                var capturedTab = tab; // локальная копия — иначе все кнопки схватят последний tab из цикла
                tab.tabButton.onClick.AddListener(() => SelectTab(capturedTab));
            }
        }

        private void OnEnable()
        {
            // Каждый раз, когда панель настроек снова становится активной (открыли через
            // OpenSubmenu из главного меню или из паузы), возвращаемся на дефолтную вкладку.
            // Если нужно, наоборот, помнить последнюю открытую вкладку между заходами —
            // просто уберите вызов SelectTabImmediate отсюда.
            if (tabs.Count == 0) return;

            int index = Mathf.Clamp(defaultTabIndex, 0, tabs.Count - 1);
            SelectTabImmediate(tabs[index]);
        }

        /// <summary>
        /// Переключение вкладки с фейдом. Уже подписано на кнопки в Awake, но метод публичный —
        /// можно дёргать и вручную (например, стрелками с геймпада, см. пример ниже).
        /// </summary>
        public void SelectTab(SettingsTab tab)
        {
            if (tab == null || tab == _currentTab) return;

            var previous = _currentTab;
            _currentTab = tab;

            UpdateTabVisuals();

            if (previous?.panel != null)
                FadeOutPanel(previous.panel);

            if (tab.panel != null)
                FadeInPanel(tab.panel);
        }

        /// <summary>
        /// То же самое, но без анимации — нужно для мгновенной инициализации при каждом
        /// открытии панели настроек (чтобы не было "вспышки" всех вкладок разом).
        /// </summary>
        private void SelectTabImmediate(SettingsTab tab)
        {
            if (tab == null) return;

            _currentTab = tab;

            foreach (var t in tabs)
            {
                if (t.panel == null) continue;

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
            panel.interactable = false;
            panel.blocksRaycasts = false;

            if (panel.TryGetComponent<PanelAnimator_Free>(out var animator))
            {
                animator.Hide();
            }
            else
            {
                panel.DOKill();
                panel.DOFade(0f, fadeDuration).SetUpdate(true)
                    .OnComplete(() => panel.gameObject.SetActive(false));
            }
        }

        private void FadeInPanel(CanvasGroup panel)
        {
            panel.gameObject.SetActive(true);
            panel.interactable = true;
            panel.blocksRaycasts = true;

            if (panel.TryGetComponent<PanelAnimator_Free>(out var animator))
            {
                animator.Show();
            }
            else
            {
                panel.alpha = 0f;
                panel.DOKill();
                panel.DOFade(1f, fadeDuration).SetUpdate(true);
            }
        }

        private void UpdateTabVisuals()
        {
            foreach (var t in tabs)
            {
                if (t.tabLabel == null) continue;
                t.tabLabel.color = (t == _currentTab) ? activeTabColor : inactiveTabColor;
            }
        }

        // ==========================================
        // Опционально: переключение вкладок бамперами геймпада / Q-E на клавиатуре.
        // Если не нужно — просто не вызывайте эти методы и не подписывайте инпут.
        // ==========================================

        public void SelectNextTab()
        {
            if (tabs.Count == 0) return;
            int currentIndex = tabs.IndexOf(_currentTab);
            int nextIndex = (currentIndex + 1) % tabs.Count;
            SelectTab(tabs[nextIndex]);
        }

        public void SelectPreviousTab()
        {
            if (tabs.Count == 0) return;
            int currentIndex = tabs.IndexOf(_currentTab);
            int prevIndex = (currentIndex - 1 + tabs.Count) % tabs.Count;
            SelectTab(tabs[prevIndex]);
        }
    }
}
// END OF FILE