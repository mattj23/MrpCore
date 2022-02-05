using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

public class UnitOperation
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int ProductUnitId { get; set; }
    
    [Required]
    public int RouteOperationId { get; set; }
    
    [ForeignKey(nameof(ProductUnitId))]
    public ProductUnit ProductUnit { get; set; }
    
    [ForeignKey(nameof(RouteOperationId))]
    public RouteOperation RouteOperation { get; set; }
    
    
}