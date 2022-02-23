using MrpCore.Models;

namespace MrpCore.Helpers;

public class OperationResultData
{
    public OperationResultData(IReadOnlyCollection<ToolClaim> toolClaims, IReadOnlyCollection<MaterialClaim> materialClaims)
    {
        ToolClaims = toolClaims;
        MaterialClaims = materialClaims;
    }

    public IReadOnlyCollection<ToolClaim> ToolClaims { get; }
    public IReadOnlyCollection<MaterialClaim> MaterialClaims { get; }
}