using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class ToolClaim
{
    [Key]
    public int Id { get; set; }
    
    public int ToolId { get; set; }
    
    public int ResultId { get; set; }
    
    public int? CapacityTaken { get; set; }
}