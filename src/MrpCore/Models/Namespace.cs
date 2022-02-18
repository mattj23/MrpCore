using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class Namespace
{
    [Key]
    public int Id { get; set; }
    
    [MaxLength(16)] public string Key { get; set; } = null!;

    [MaxLength(128)] public string? Description { get; set; }
}