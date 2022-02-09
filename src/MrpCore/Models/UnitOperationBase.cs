using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

public class UnitOperationBase<TProductType, TProductUnit, TRouteOperation>
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperationBase<TProductType>
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int ProductUnitId { get; set; }
    
    [Required]
    public int RouteOperationId { get; set; }
    
    [ForeignKey(nameof(ProductUnitId))]
    public TProductUnit? Product { get; set; }
    
    [ForeignKey(nameof(RouteOperationId))]
    public TRouteOperation? RouteOperation { get; set; }
    
}