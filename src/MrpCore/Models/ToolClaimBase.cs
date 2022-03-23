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
    
    public int ResultId { get; set; }
    
    public int CapacityTaken { get; set; }
    
    public bool Released { get; set; }
}