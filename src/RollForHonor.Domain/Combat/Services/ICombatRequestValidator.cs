using RollForHonor.Domain.Combat.Resolution;

namespace RollForHonor.Domain.Combat.Services;

public interface ICombatRequestValidator
{
    CombatRejection? Validate(CombatRequest request);
}
