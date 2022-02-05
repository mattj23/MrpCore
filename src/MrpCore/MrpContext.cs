using Microsoft.EntityFrameworkCore;
using MrpCore.Models;

namespace MrpCore;

public class MrpContext<TUnitOperation, TProductUnit, TRouteOperation, TProductType, TUnitState, TOperationResult> : DbContext
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperationBase<TProductType, TUnitState>
    where TUnitOperation : UnitOperationBase<TProductUnit, TRouteOperation, TProductType, TUnitState>
    where TOperationResult : OperationResultBase<TUnitOperation, TProductUnit, TRouteOperation, TProductType, TUnitState>
{
    public DbSet<TUnitState> States { get; set; } = null!;
    public DbSet<TProductType> Types { get; set; } = null!;
    public DbSet<TProductUnit> Units { get; set; } = null!;
    public DbSet<TRouteOperation> RouteOperations { get; set; } = null!;
    public DbSet<TUnitOperation> UnitOperations { get; set; } = null!;
    public DbSet<TOperationResult> OperationResults { get; set; } = null!;

    public MrpContext(DbContextOptions options) : base(options)
    {
        
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TRouteOperation>().HasMany(e => e.Adds);
        modelBuilder.Entity<TRouteOperation>().HasMany(e => e.Removes);
    }
}