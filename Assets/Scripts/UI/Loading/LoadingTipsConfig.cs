using UnityEngine;

namespace ShellGame.UI
{
    [CreateAssetMenu(fileName = "LoadingTipsConfig", menuName = "ShellGame/UI/Loading Tips Config")]
    public class LoadingTipsConfig : ScriptableObject
    {
        [Tooltip("Фразы для экрана загрузки. Держим их расплывчатыми, без прямого раскрытия сюжета.")]
        [TextArea(2, 4)]
        public string[] tips;
    }
}