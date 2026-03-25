using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;

namespace MintySpire2.relicreminders.endturnbutton;

/// <summary>
/// Event-driven reminder service: computes relevant end-turn relic reminders only when notified by EndTurnRelicReminderTriggers.
/// </summary>
public static class EndTurnRelicReminderService
{
    private static readonly Dictionary<Type, Func<RelicModel, Player, bool>> ReminderRules = new()
    {
        { typeof(PaelsEye), ShouldShowStatusActive },
        { typeof(Orichalcum), (_, me) => ShouldShowOrichalcum(me) },
        { typeof(FakeOrichalcum), (_, me) => ShouldShowOrichalcum(me) },
        { typeof(RippleBasin), ShouldShowStatusActive },
        { typeof(Pocketwatch), ShouldShowStatusActive },
        { typeof(ArtOfWar), ShouldShowStatusActive },
        { typeof(PaelsTears), (relic, me) => ShouldShowPaelsTears((PaelsTears)relic, me) },
        { typeof(DiamondDiadem), ShouldShowStatusActive },
    };

    public static event Action? RemindersChanged;

    public static IReadOnlyList<RelicModel> GetCurrentReminders()
    {
        var me = LocalContext.GetMe(RunManager.Instance.State);
        if (me == null)
            return [];

        var combatState = me.Creature?.CombatState;
        if (combatState == null || combatState.CurrentSide != CombatSide.Player || !CombatManager.Instance.IsInProgress)
            return [];

        var reminders = new List<RelicModel>(2);
        foreach (var relic in me.Relics)
        {
            if (ReminderRules.TryGetValue(relic.GetType(), out var rule) && rule(relic, me))
                reminders.Add(relic);
        }

        return reminders;
    }

    public static void NotifyRemindersMayHaveChanged() => RemindersChanged?.Invoke();

    // Generic method that works for all relics that "pulse" when they can still trigger
    private static bool ShouldShowStatusActive(RelicModel relic, Player me)
    {
        return relic.Owner == me && me.Creature.IsAlive && relic.Status == RelicStatus.Active;
    }
    
    private static bool ShouldShowOrichalcum(Player me)
    {
        return me.Creature is { IsAlive: true, Block: <= 0 };
    }
    
    private static bool ShouldShowPaelsTears(PaelsTears paelsTears, Player me)
    {
        if (paelsTears.Owner != me || !me.Creature.IsAlive)
            return false;

        return me.PlayerCombatState!.Energy > 0;
    }
}
