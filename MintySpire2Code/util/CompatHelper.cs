using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace MintySpire2.MintySpire2Code.util;

internal static class CompatHelper
{
    // [COMPAT] For v0.111: Look up the v0.113 Dominion power by ID.
    private static readonly ModelId InsatiableDominionPowerId = new("POWER", "INSATIABLE_DOMINION_POWER");

    public static PowerModel? GetInsatiableDominionPower(Creature creature) => creature.GetPower(InsatiableDominionPowerId);

    // [COMPAT] For v0.111: Blitz is absent from the GameMode enum.
    public static bool IsBlitzMode(IRunState run) => run.GameMode.ToString() == "Blitz";
}
