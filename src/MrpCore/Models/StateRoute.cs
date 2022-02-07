using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

public class StateRoute<TProductType, TUnitState, TRouteOperation>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TRouteOperation : RouteOperationBase<TProductType>
{
    [Key]
    public int Id { get; set; }
    
    public int RouteOperationId { get; set; }
    
    public int UnitStateId { get; set; }

    [ForeignKey(nameof(RouteOperationId))]
    public TRouteOperation RouteOp { get; set; } = null!;

    [ForeignKey(nameof(UnitStateId))]
    public TUnitState State { get; set; } = null!;
    
    public bool IsAdd { get; set; }
}