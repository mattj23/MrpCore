using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

public class ToolBase<TToolType> where TToolType : ToolTypeBase
{
    [Key]
    public int Id { get; set; }

    [MaxLength(32)] public string Name { get; set; } = null!;
    
    [MaxLength(128)] public string? Description { get; set; }
    
    public int? Capacity { get; set; }
    
    public int TypeId { get; set; }
    
    [ForeignKey(nameof(TypeId))]
    public TToolType? Type { get; set; }
    
    public bool Retired { get; set; }

    [NotMapped] public string? TypeName => Type?.Name;

    public override string ToString()
    {
        return Name;
    }
}