using MrpCore.Models;

namespace MrpCore.Helpers;

public class StateRelations<TUnitState> where TUnitState : UnitStateBase
{
    public StateRelations(IEnumerable<TUnitState> add,
        IEnumerable<TUnitState> remove,
        IEnumerable<TUnitState> needs,
        IEnumerable<TUnitState> blockedBy)
    {
        Adds = add.ToArray();
        Removes = remove.ToArray();
        Needs = needs.ToArray();
        BlockedBy = blockedBy.ToArray();
    }

    public IReadOnlyCollection<TUnitState> Adds { get; }
    public IReadOnlyCollection<TUnitState> Removes { get; }
    public IReadOnlyCollection<TUnitState> Needs { get; }
    public IReadOnlyCollection<TUnitState> BlockedBy { get; }
}