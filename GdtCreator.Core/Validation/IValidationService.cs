using GdtCreator.Core.Models;

namespace GdtCreator.Core.Validation;

public interface IValidationService
{
    ValidationResult Validate(GeometricToleranceSpec spec);
}
