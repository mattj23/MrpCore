using MrpCore.Models;

namespace MrpCore.Helpers;

public class RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TRouteOperation : RouteOperationBase<TProductType>
    where TToolRequirement : ToolRequirementBase
{
    public RouteOpAndData(TRouteOperation operation, StateRelations<TUnitState> states, TToolRequirement[] toolRequirements, MaterialRequirement[] materialRequirements)
    {
        Op = operation;
        States = states;
        ToolRequirements = toolRequirements;
        MaterialRequirements = materialRequirements;
    }

    public TRouteOperation Op { get; }
    public StateRelations<TUnitState> States { get; }
    
    public TToolRequirement[] ToolRequirements { get; }
    
    public MaterialRequirement[] MaterialRequirements { get; }
}