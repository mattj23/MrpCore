using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class ToolRequirementBase
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the id of the route operation which has this requirement
    /// </summary>
    public int RouteOperationId { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the tool type that is used by this requirement
    /// </summary>
    public int ToolTypeId { get; set; }
    
    public ToolRequirementType Type { get; set; }
    
    public int? CapacityTaken { get; set; }
}


public enum ToolRequirementType
{
    UsedOnly,
    Occupied,
    Released,
}