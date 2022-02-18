using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class ProductTypeBase
{
    [Key]
    public int Id { get; set; }
    
    [MaxLength(128)]
    [Required]
    public string Name { get; set; } = null!;
    
    [MaxLength(256)]
    public string? Description { get; set; }

    public int OutputQuantity { get; set; }
    
    public int? NamespaceId { get; set; }
}