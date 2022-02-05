using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class UnitStateBase
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(32)]
    public string Name { get; set; } = null!;
    
    [MaxLength(128)]
    public string? Description { get; set; }
    
    public bool BlocksCompletion { get; set; }
    
}