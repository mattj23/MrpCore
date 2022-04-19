using Microsoft.EntityFrameworkCore;
using MrpCore.Models;

namespace MrpCore;

/// <summary>
///     Manufacturing Execution System database context
/// </summary>
/// <typeparam name="TUnitOperation"></typeparam>
/// <typeparam name="TProductUnit"></typeparam>
/// <typeparam name="TRouteOperation"></typeparam>
/// <typeparam name="TProductType"></typeparam>
/// <typeparam name="TUnitState"></typeparam>
/// <typeparam name="TOperationResult"></typeparam>
/// <typeparam name="TToolType"></typeparam>
/// <typeparam name="TTool"></typeparam>
/// <typeparam name="TToolClaim"></typeparam>
/// <typeparam name="TToolRequirement"></typeparam>
public class MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult,
    TToolType, TTool, TToolClaim, TToolRequirement> : DbContext
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperationBase<TProductType>
    where TUnitOperation : UnitOperationBase<TProductType, TProductUnit, TRouteOperation>
    where TOperationResult :
    OperationResultBase<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation>
    where TToolType : ToolTypeBase
    where TToolRequirement : ToolRequirementBase
    where TTool : ToolBase<TToolType>
    where TToolClaim : ToolClaimBase<TToolType, TTool>
{
    public DbSet<TUnitState> States { get; set; } = null!;
    public DbSet<TProductType> Types { get; set; } = null!;
    public DbSet<TProductUnit> Units { get; set; } = null!;
    public DbSet<TRouteOperation> RouteOperations { get; set; } = null!;
    public DbSet<TUnitOperation> UnitOperations { get; set; } = null!;
    public DbSet<TOperationResult> OperationResults { get; set; } = null!;
    public DbSet<StateRoute<TProductType, TUnitState, TRouteOperation>> StatesToRoutes { get; set; } = null!;

    public DbSet<Namespace> Namespaces { get; set; } = null!;
    public DbSet<TToolType> ToolTypes { get; set; } = null!;
    public DbSet<TTool> Tools { get; set; } = null!;
    public DbSet<TToolRequirement> ToolRequirements { get; set; } = null!;
    public DbSet<TToolClaim> ToolClaims { get; set; } = null!;

    public DbSet<MaterialRequirement> MaterialRequirements { get; set; } = null!;
    public DbSet<MaterialRequirementOption> MaterialRequirementOptions { get; set; } = null!;
    public DbSet<MaterialClaim> MaterialClaims { get; set; } = null!;

    public DbSet<StockItem> StockItems { get; set; } = null!;

    public MesContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StateRoute<TProductType, TUnitState, TRouteOperation>>()
            .HasOne(i => i.RouteOp);

        modelBuilder.Entity<StateRoute<TProductType, TUnitState, TRouteOperation>>()
            .HasOne(i => i.State);
    }
}