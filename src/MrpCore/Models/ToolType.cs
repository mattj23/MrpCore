using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class ToolType
{
    [Key]
    public int Id { get; set; }

    [MaxLength(32)] public string Name { get; set; } = null!;

    [MaxLength(128)] public string? Description { get; set; }
    
    public int? NamespaceId { get; set; }
}