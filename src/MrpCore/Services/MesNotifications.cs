using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.EntityFrameworkCore;

namespace MrpCore.Services;

public enum ChangeType
{
    Created,
    Updated,
    Deleted
}

public record RouteUpdate(ChangeType Type, int ProductTypeId);

public record UnitStateUpdate(ChangeType Type, int UnitStateId);

public record UnitUpdate(ChangeType Type, int UnitId);

public record ResultUpdate(bool Pass, int ResultId);

public record ToolTypeUpdate(ChangeType Type, int ToolTypeId);
public record ToolUpdate(ChangeType Type, int ToolId);
public record ToolClaimUpdate(bool Released, int ToolId);

public interface IMesUpdater
{
    void UpdateRoute(ChangeType type, int productTypeId);
    void UpdateStates(ChangeType type, int unitStateId);
    void UpdateUnit(ChangeType type, int unitId);
    void UpdateResult(bool pass, int resultId);
    
    void UpdateToolType(ChangeType type, int toolTypeId);
    void UpdateTool(ChangeType type, int toolId);
    void UpdateToolClaim(bool released, int toolId);
}

public interface IMesUpdates
{
    IObservable<RouteUpdate> RouteUpdates { get; }
    IObservable<UnitStateUpdate> UnitStateUpdates { get; }
    IObservable<UnitUpdate> UnitUpdates { get; }
    IObservable<ResultUpdate> ResultUpdates { get; }
    
    IObservable<ToolTypeUpdate> ToolTypeUpdates { get; }
    IObservable<ToolUpdate> ToolUpdates { get; }
    IObservable<ToolClaimUpdate> ToolClaimUpdates { get; }
}

public class MesNotifications : IMesUpdater, IMesUpdates
{
    private readonly Subject<RouteUpdate> _routeUpdates;
    private readonly Subject<UnitStateUpdate> _unitStateUpdates;
    private readonly Subject<UnitUpdate> _unitUpdates;
    private readonly Subject<ResultUpdate> _resultUpdates;
    
    private readonly Subject<ToolTypeUpdate> _toolTypeUpdates;
    private readonly Subject<ToolUpdate> _toolUpdates;
    private readonly Subject<ToolClaimUpdate> _toolClaimUpdates;

    public MesNotifications()
    {
        _routeUpdates = new Subject<RouteUpdate>();
        _unitUpdates = new Subject<UnitUpdate>();
        _unitStateUpdates = new Subject<UnitStateUpdate>();
        _resultUpdates = new Subject<ResultUpdate>();
        _toolTypeUpdates = new Subject<ToolTypeUpdate>();
        _toolUpdates = new Subject<ToolUpdate>();
        _toolClaimUpdates = new Subject<ToolClaimUpdate>();
    }

    public IObservable<RouteUpdate> RouteUpdates => _routeUpdates.AsObservable();
    public IObservable<UnitStateUpdate> UnitStateUpdates => _unitStateUpdates.AsObservable();
    public IObservable<UnitUpdate> UnitUpdates => _unitUpdates.AsObservable();
    public IObservable<ResultUpdate> ResultUpdates => _resultUpdates.AsObservable();
    
    public IObservable<ToolTypeUpdate> ToolTypeUpdates => _toolTypeUpdates.AsObservable();
    public IObservable<ToolUpdate> ToolUpdates => _toolUpdates.AsObservable();
    public IObservable<ToolClaimUpdate> ToolClaimUpdates => _toolClaimUpdates.AsObservable();

    public void UpdateRoute(ChangeType type, int productTypeId)
    {
        _routeUpdates.OnNext(new RouteUpdate(type, productTypeId));
    }
    
    public void UpdateStates(ChangeType type, int unitStateId)
    {
        _unitStateUpdates.OnNext(new UnitStateUpdate(type, unitStateId));
    }
    
    public void UpdateUnit(ChangeType type, int unitId)
    {
        _unitUpdates.OnNext(new UnitUpdate(type, unitId));
    }

    public void UpdateResult(bool pass, int resultId)
    {
        _resultUpdates.OnNext(new ResultUpdate(pass, resultId));
    }

    public void UpdateToolType(ChangeType type, int toolTypeId)
    {
        _toolTypeUpdates.OnNext(new ToolTypeUpdate(type, toolTypeId));
    }

    public void UpdateTool(ChangeType type, int toolId)
    {
        _toolUpdates.OnNext(new ToolUpdate(type, toolId));
    }

    public void UpdateToolClaim(bool released, int toolId)
    {
        _toolClaimUpdates.OnNext(new ToolClaimUpdate(released, toolId));
    }
}

public class EmptyMesUpdater : IMesUpdater
{
    public void UpdateRoute(ChangeType type, int productTypeId)
    {
    }

    public void UpdateStates(ChangeType type, int unitStateId)
    {
    }

    public void UpdateUnit(ChangeType type, int unitId)
    {
    }

    public void UpdateResult(bool pass, int resultId)
    {
    }

    public void UpdateToolType(ChangeType type, int toolTypeId)
    {
    }

    public void UpdateTool(ChangeType type, int toolId)
    {
    }

    public void UpdateToolClaim(bool released, int toolId)
    {
    }
}