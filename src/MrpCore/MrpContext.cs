using Microsoft.EntityFrameworkCore;
using MrpCore.Models;

namespace MrpCore;

public class MrpContext<TUnitOperation, TProductUnit, TRouteOperation, TProductType, TUnitState, TOperationResult> : DbContext
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperation<TProductType, TUnitState>
    where TUnitOperation : UnitOperationBase<TProductUnit, TRouteOperation, TProductType, TUnitState>
    where TOperationResult : OperationResultBase<TUnitOperation, TProductUnit, TRouteOperation, TProductType, TUnitState>
{
    public DbSet<TUnitState> States { get; set; }
    public DbSet<TProductType> Types { get; set; }
    public DbSet<TProductUnit> Units { get; set; }
    public DbSet<TRouteOperation> RouteOperations { get; set; }
    public DbSet<TUnitOperation> UnitOperations { get; set; }
    public DbSet<TOperationResult> OperationResults { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TRouteOperation>().HasMany(e => e.Adds);
        modelBuilder.Entity<TRouteOperation>().HasMany(e => e.Removes);
    }
}