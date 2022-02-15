using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

public class StateRoute<TProductType, TUnitState, TRouteOperation> : IEquatable<StateRoute<TProductType, TUnitState, TRouteOperation>>
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

    public OpRelation Relation { get; set; }

    public bool Equals(StateRoute<TProductType, TUnitState, TRouteOperation>? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return RouteOperationId == other.RouteOperationId && UnitStateId == other.UnitStateId && Relation == other.Relation;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((StateRoute<TProductType, TUnitState, TRouteOperation>)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(RouteOperationId, UnitStateId, (int)Relation);
    }
}
    
public enum OpRelation
{
    Add,
    Remove,
    Needs,
    BlockedBy
}
