using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

public class ToolClaimBase<TToolType, TTool>
    where TToolType : ToolTypeBase
    where TTool : ToolBase<TToolType>
{
    [Key]
    public int Id { get; set; }
    
    public int ToolId { get; set; }
    
    [ForeignKey(nameof(ToolId))]
    public TTool? Tool { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the operation result which created this claim
    /// </summary>
    public int ResultId { get; set; }
    
    public int CapacityTaken { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the operation result which released this claim, if one exists
    /// </summary>
    public int? ReleaseId { get; set; }
    
    /// <summary>
    /// Gets or sets a flag which indicates if this claim has been released
    /// </summary>
    public bool Released { get; set; }
}