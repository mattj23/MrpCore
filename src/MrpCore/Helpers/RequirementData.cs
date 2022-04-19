namespace MrpCore.Helpers;

/// <summary>
/// This is a unified way of communicating requirements for an operation back and forth from the core.
///
/// A client may query the manager for a list of requirements needed to perform a result on an operation, and a list of
/// these objects will be returned. Then, when attempting to apply a result to the operation, the client will need to
/// pass a requirement selection for each requirement they were asked for.
/// </summary>
public class RequirementData
{
    public RequirementData(int referenceId, string text, ReqType type, IReadOnlyCollection<RequirementOption> options)
    {
        ReferenceId = referenceId;
        Text = text;
        Type = type;
        Options = options;
    }

    public int ReferenceId { get; }
    public string Text { get; }
    public ReqType Type {get; }
    public IReadOnlyCollection<RequirementOption> Options { get; }

    
    public enum ReqType
    {
        Material,
        MaterialStockItem,
        Tool
    }
}

public static class ReqTypeExtension
{
    public static bool Matches(this RequirementData.ReqType reqType, RequirementData.ReqType other)
    {
        if (reqType is RequirementData.ReqType.Tool && other is RequirementData.ReqType.Tool) return true;
        if (reqType is RequirementData.ReqType.Material or RequirementData.ReqType.MaterialStockItem &&
            other is RequirementData.ReqType.Material or RequirementData.ReqType.MaterialStockItem) return true;
        return false;
    }
}

/// <summary>
/// A unified type for passing along information about an option to satisfy a requirement.
/// </summary>
/// <param name="Type">The type of requirement the option fulfills. This determines what the ID field means.</param>
/// <param name="Id">An ID associated with the option. This may be a stock item ID, a product unit ID, or a tool ID, based on value of the type field.</param>
/// <param name="Text">Primary display text for the client, typically will be a tool name, product unit serial, or a stock item name.</param>
/// <param name="ExtraText">Secondary display text for the client, typically a product unit type name or a stock item description</param>
/// <param name="Object">The underlying object associated with the option. Typically a tool, a stock item, or a product unit</param>
public record RequirementOption(RequirementData.ReqType Type, int Id, string Text, string ExtraText, object Object);

/// <summary>
/// A unified type for making a selection to satisfy a requirement.
/// </summary>
/// <param name="Type">The type of requirement the selection fulfills. This necessary to distinguish between material stock and product items.</param>
/// <param name="ReqId">The ID of the requirement which this selection is supposed to satisfy.</param>
/// <param name="SelectedId">The ID of the selection, should be the value taken out of the <see cref="RequirementOption"/> record, what it means is determined by the Type field</param>
public record RequirementSelect(RequirementData.ReqType Type, int ReqId, int SelectedId);