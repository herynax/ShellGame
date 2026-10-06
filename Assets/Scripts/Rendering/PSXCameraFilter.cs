using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShellGame.Rendering
{
    /// <summary>
    /// Единый гейт для всех PSX-эффектов: рендерить только в play mode
    /// и только на игровых камерах.
    ///
    /// Зачем: PSX-фичи — это полноэкранные блиты поверх кадра. На Scene View
    /// (CameraType.SceneView) и на превью материалов/префабов (CameraType.Preview)
    /// они дают мусор, красный шум дизеринга и «непонятную картинку», при этом
    /// тумблер «Post Processing» в настройках камеры вью их не гасит —
    /// URP вызывает AddRenderPasses для всех камер, и проверять тип камеры
    /// обязан сам пользовательский код.
    /// </summary>
    public static class PSXCameraFilter
    {
        /// <summary>
        /// Камеры, на которых PSX-эффекты допустимы: только CameraType.Game
        /// в play mode. Всё остальное (Scene View, Preview, Reflection, VR)
        /// отсекается автоматически, потому что cameraType != Game.
        /// </summary>
        public static bool IsEffectCamera(Camera camera)
        {
            if (camera == null) return false;
            return IsEffectCamera(camera.cameraType);
        }

        public static bool IsEffectCamera(CameraType cameraType)
        {
            if (!Application.isPlaying) return false;
            return cameraType == CameraType.Game;
        }

        /// <summary>
        /// Гейт для AddRenderPasses: пасс ставится в очередь только если камера
        /// игровая и мы в play mode.
        /// </summary>
        public static bool ShouldEnqueue(ref CameraData cameraData)
        {
            if (!IsEffectCamera(cameraData.cameraType)) return false;
            if (cameraData.isSceneViewCamera) return false;
            return true;
        }

        /// <summary>
        /// Гейт для RecordRenderGraph: дополнительно требует включённый
        /// post-processing, чтобы тумблер «Post Processing» в настройках камеры
        /// работал так же, как для встроенных эффектов URP.
        /// </summary>
        public static bool ShouldRender(UniversalCameraData cameraData)
        {
            if (!IsEffectCamera(cameraData.cameraType)) return false;
            if (cameraData.isSceneViewCamera) return false;
            if (!cameraData.postProcessEnabled) return false;
            return true;
        }
    }
}
