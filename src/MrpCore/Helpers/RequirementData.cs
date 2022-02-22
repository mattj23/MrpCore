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
    public RequirementData(int referenceId, string text, ReqType type, IReadOnlyCollection<Option> options)
    {
        ReferenceId = referenceId;
        Text = text;
        Type = type;
        Options = options;
    }

    public int ReferenceId { get; }
    public string Text { get; }
    public ReqType Type {get; }
    public IReadOnlyCollection<Option> Options { get; }

    public record Option(int Id, string Text, object Object);
    
    public enum ReqType
    {
        Material,
        Tool
    }
}

public record RequirementSelect(RequirementData.ReqType Type, int ReqId, int SelectedId);