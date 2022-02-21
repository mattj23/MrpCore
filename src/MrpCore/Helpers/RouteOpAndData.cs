using MrpCore.Models;

namespace MrpCore.Helpers;

public class RouteOpAndData<TProductType, TUnitState, TRouteOperation>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TRouteOperation : RouteOperationBase<TProductType>
{
    public RouteOpAndData(TRouteOperation operation, StateRelations<TUnitState> states, ToolRequirement[] toolRequirements, MaterialRequirement[] materialRequirements)
    {
        Op = operation;
        States = states;
        ToolRequirements = toolRequirements;
        MaterialRequirements = materialRequirements;
    }

    public TRouteOperation Op { get; }
    public StateRelations<TUnitState> States { get; }
    
    public ToolRequirement[] ToolRequirements { get; }
    
    public MaterialRequirement[] MaterialRequirements { get; }
}