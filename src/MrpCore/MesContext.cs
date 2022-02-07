using Microsoft.EntityFrameworkCore;
using MrpCore.Models;

namespace MrpCore;

/// <summary>
/// Manufacturing Execution System database context
/// </summary>
/// <typeparam name="TUnitOperation"></typeparam>
/// <typeparam name="TProductUnit"></typeparam>
/// <typeparam name="TRouteOperation"></typeparam>
/// <typeparam name="TProductType"></typeparam>
/// <typeparam name="TUnitState"></typeparam>
/// <typeparam name="TOperationResult"></typeparam>
public class MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult> : DbContext
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperationBase<TProductType, TUnitState>
    where TUnitOperation : UnitOperationBase<TProductType, TUnitState, TProductUnit, TRouteOperation>
    where TOperationResult : OperationResultBase<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation>
{
    public DbSet<TUnitState> States { get; set; } = null!;
    public DbSet<TProductType> Types { get; set; } = null!;
    public DbSet<TProductUnit> Units { get; set; } = null!;
    public DbSet<TRouteOperation> RouteOperations { get; set; } = null!;
    public DbSet<TUnitOperation> UnitOperations { get; set; } = null!;
    public DbSet<TOperationResult> OperationResults { get; set; } = null!;

    public MesContext(DbContextOptions options) : base(options)
    {
        
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TRouteOperation>().HasMany(e => e.Adds);
        modelBuilder.Entity<TRouteOperation>().HasMany(e => e.Removes);
    }
}