using UnityEngine;

namespace ShellGame.UI
{
    [CreateAssetMenu(fileName = "LoadingTipsConfig", menuName = "ShellGame/UI/Loading Tips Config")]
    public class LoadingTipsConfig : ScriptableObject
    {
        [Header("Текстовые подсказки")]
        [Tooltip("Фразы для экрана загрузки. Держим их расплывчатыми, без прямого раскрытия сюжета.")]
        [TextArea(2, 4)]
        public string[] tips;

        [Header("Картинки")]
        [Tooltip("Изображения (арты, скетчи), которые будут случайно показываться на экране загрузки.")]
        public Sprite[] images;
    }
}