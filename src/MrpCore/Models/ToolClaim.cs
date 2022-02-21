using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

public class ToolClaim
{
    [Key]
    public int Id { get; set; }
    
    public int ToolId { get; set; }
    
    [ForeignKey(nameof(ToolId))]
    public Tool? Tool { get; set; }
    
    public int ResultId { get; set; }
    
    public int? CapacityTaken { get; set; }
    
    public bool Released { get; set; }
}