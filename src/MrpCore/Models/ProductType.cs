using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class ProductType
{
    [Key]
    public int Id { get; set; }
    
    [MaxLength(128)]
    [Required]
    public string Name { get; set; } = null!;
    
    [MaxLength(256)]
    public string? Description { get; set; }
    
    public ICollection<RouteOperation> RouteOperations { get; set; }

}