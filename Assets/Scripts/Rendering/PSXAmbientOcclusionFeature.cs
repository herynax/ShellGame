using UnityEngine.Rendering.Universal;

namespace ShellGame.Rendering
{
    /// <summary>
    /// Screen Space Ambient Occlusion под тем же гейтом, что и остальные
    /// полноэкранные эффекты: только play mode и только игровые камеры.
    ///
    /// Зачем: у встроенного ScreenSpaceAmbientOcclusion в URP нет никакой
    /// привязки к камере — он считается для всех камер, включая Scene View.
    /// Сэмплирование идёт по blue-noise текстуре с покадровым сдвигом, поэтому
    /// в редакторе он даёт яркую сетку шума, и она видна только по геометрии
    /// (считается по depth-буферу). В play mode шум перекрывался PSX-эффектами,
    /// из-за чего казалось, что источник в них.
    ///
    /// Наследуем штатную фичу URP, а не дублируем её: Create/Dispose, создание
    /// пасса, ресурсы и все настройки (интенсивность, радиус, метод сэмплирования)
    /// остаются у URP. RecordRenderGraph у ScreenSpaceAmbientOcclusion
    /// отсутствует — весь рендер идёт через AddRenderPasses, поэтому гейта
    /// в этом методе достаточно.
    /// </summary>
    public class PSXAmbientOcclusionFeature : ScreenSpaceAmbientOcclusion
    {
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!PSXCameraFilter.ShouldEnqueue(ref renderingData.cameraData)) return;

            base.AddRenderPasses(renderer, ref renderingData);
        }
    }
}