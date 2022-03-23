using MrpCore.Models;

namespace MrpCore.Helpers;

public class OperationResultData<TToolType, TTool, TToolClaim>
    where TToolType : ToolTypeBase
    where TTool : ToolBase<TToolType>
    where TToolClaim : ToolClaimBase<TToolType, TTool>
{
    public OperationResultData(IReadOnlyCollection<TToolClaim> toolClaims, IReadOnlyCollection<MaterialClaim> materialClaims)
    {
        ToolClaims = toolClaims;
        MaterialClaims = materialClaims;
    }

    public IReadOnlyCollection<TToolClaim> ToolClaims { get; }
    public IReadOnlyCollection<MaterialClaim> MaterialClaims { get; }
}