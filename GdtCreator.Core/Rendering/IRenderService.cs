using GdtCreator.Core.Models;

namespace GdtCreator.Core.Rendering;

public interface IRenderService
{
    ToleranceRenderModel Render(GeometricToleranceSpec spec);
}
