using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

public class Tool
{
    [Key]
    public int Id { get; set; }

    [MaxLength(32)] public string Name { get; set; } = null!;
    
    [MaxLength(128)] public string? Description { get; set; }
    
    public int? Capacity { get; set; }
    
    public int TypeId { get; set; }
    
    [ForeignKey(nameof(TypeId))]
    public ToolType? Type { get; set; }
    
    public bool Retired { get; set; }
}