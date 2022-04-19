﻿using MrpCore.Models;

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

    public bool Any() => Adds.Any() || Removes.Any() || Needs.Any() || BlockedBy.Any();

    public IReadOnlyCollection<TUnitState> Adds { get; }
    public IReadOnlyCollection<TUnitState> Removes { get; }
    public IReadOnlyCollection<TUnitState> Needs { get; }
    public IReadOnlyCollection<TUnitState> BlockedBy { get; }

    public static StateRelations<TUnitState> Empty()
    {
        return new StateRelations<TUnitState>(
            Enumerable.Empty<TUnitState>(),
            Enumerable.Empty<TUnitState>(),
            Enumerable.Empty<TUnitState>(),
            Enumerable.Empty<TUnitState>());
    }
    
    public static StateRelations<TUnitState> FromAdds(IEnumerable<TUnitState> adds)
    {
        return new StateRelations<TUnitState>(
            adds,
            Enumerable.Empty<TUnitState>(),
            Enumerable.Empty<TUnitState>(),
            Enumerable.Empty<TUnitState>());
    }
}