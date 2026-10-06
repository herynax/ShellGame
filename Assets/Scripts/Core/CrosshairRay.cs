using UnityEngine;
using UnityEngine.InputSystem;

namespace ShellGame.Core
{
    /// <summary>
    /// Единственный источник луча взаимодействия для напертков, предметов и монет.
    ///
    /// В бою и магазине курсор залочен, поэтому взаимодействие идёт по центру экрана —
    /// так же, как прицел. Когда курсор свободен (пауза, карта), луч идёт от мыши.
    /// Раньше это правило было продублировано в RoundInputSystem и CoinPileController,
    /// из-за чего монеты кликались только в паузе, а напертки — всегда.
    /// </summary>
    public static class CrosshairRay
    {
        /// <summary>Луч из центра экрана. Используется, пока курсор захвачен игрой.</summary>
        public static Ray FromScreenCenter(Camera camera)
        {
            return camera != null
                ? camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : default;
        }

        /// <summary>
        /// Луч взаимодействия: центр экрана при захваченном курсоре, иначе луч от мыши.
        /// camera может быть null — тогда возвращается default и вызывающий обязан это учесть.
        /// </summary>
        public static Ray Build(Camera camera)
        {
            if (camera == null)
                return default;

            if (IsCursorCaptured())
                return FromScreenCenter(camera);

            var mouse = Mouse.current;
            if (mouse == null)
                return FromScreenCenter(camera);

            return camera.ScreenPointToRay(mouse.position.ReadValue());
        }

        /// <summary>Курсор захвачен игрой: наводиться мышью по экрану нельзя.</summary>
        public static bool IsCursorCaptured()
        {
            return Cursor.lockState == CursorLockMode.Locked || !Cursor.visible;
        }
    }
}