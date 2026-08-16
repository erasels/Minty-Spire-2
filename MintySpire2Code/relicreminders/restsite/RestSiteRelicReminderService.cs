using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace MintySpire2.MintySpire2Code.relicreminders.restsite;

/// <summary>
///     Mapping service for associating relics with <see cref="RestSiteOption"/> ids (like "HEAL")
///     which are then displayed under <see cref="NRestSiteButton"/> if conditions match.
/// </summary>
public static class RestSiteRelicReminderService
{
    private record Registration(Type RelicType, Func<RelicModel, Player, bool>? ShowWhen = null);

    private static readonly Dictionary<string, List<Registration>> _byOption = new();

    static RestSiteRelicReminderService()
    {
        Register<TinyMailbox>("HEAL");
        Register<RegalPillow>("HEAL");
        Register<DreamCatcher>("HEAL");
        Register<StoneHumidifier>("HEAL");
        // Pantograph shows before the boss since healing can be made redundant.
        Register<Pantograph>("HEAL", (_, player) => IsNextNodeBoss(player));
    }

    /// <summary>
    ///     Register (or replace) a relic reminder for a given rest site option id.
    /// </summary>
    /// <param name="optionId">The <see cref="RestSiteOption.OptionId"/>, e.g. "HEAL".</param>
    /// <param name="showWhen">Optional predicate, when null the relic is always shown if owned.</param>
    public static void Register<T>(string optionId, Func<T, Player, bool>? showWhen = null) where T : RelicModel
    {
        Func<RelicModel, Player, bool>? wrapped = showWhen == null
            ? null
            : (relic, player) => showWhen((T)relic, player);

        Register(optionId, typeof(T), wrapped);
    }
    
    public static void Register(string optionId, Type relicType, Func<RelicModel, Player, bool>? showWhen = null)
    {
        if (!_byOption.TryGetValue(optionId, out var list))
        {
            list = [];
            _byOption[optionId] = list;
        }

        list.RemoveAll(r => r.RelicType == relicType);
        list.Add(new Registration(relicType, showWhen));
    }
    
    public static bool Unregister<T>(string optionId) where T : RelicModel
    {
        return Unregister(optionId, typeof(T));
    }
    
    public static bool Unregister(string optionId, Type relicType)
    {
        return _byOption.TryGetValue(optionId, out var list) && list.RemoveAll(r => r.RelicType == relicType) > 0;
    }
    
    public static IReadOnlyList<RelicModel> GetReminders(string optionId, Player player)
    {
        if (!Config.OutOfCombatReminders) return [];
        if (!_byOption.TryGetValue(optionId, out var list) || list.Count == 0) return [];

        var reminders = new List<RelicModel>();
        foreach (var relic in player.Relics)
        {
            if (relic.IsMelted) continue;
            foreach (var reg in list)
            {
                if (reg.RelicType == relic.GetType() && (reg.ShowWhen is null || reg.ShowWhen(relic, player)))
                {
                    reminders.Add(relic);
                    break;
                }
            }
        }

        return reminders;
    }

    /// <summary>
    ///     Check if the next node is the boss, copied from <see cref="Pantograph"/>.
    /// </summary>
    private static bool IsNextNodeBoss(Player player)
    {
        var current = player.RunState.CurrentMapPoint;
        return current != null && player.RunState.Map.BossMapPoint.parents.Contains(current);
    }
}
