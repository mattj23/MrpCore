using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class ToolRequirement
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the RootId of the route operation which has this requirement
    /// </summary>
    public int RouteOpRootId { get; set; }
    
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