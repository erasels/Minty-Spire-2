using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace MintySpire2.MintySpire2Code.util;

public class Wiz
{
    public static Player? p()
    {
        return LocalContext.GetMe(RunManager.Instance?.State);
    }
    
    public static bool IsInHand(Player player, CardModel? card)
    {
        if (card == null) return false;

        return PileType.Hand.GetPile(player).Cards.Contains(card);
    }
    
    public static bool IsInMyHand(CardModel? card)
    {
        var me = p();
        if (me == null) return false;

        return IsInHand(me, card);
    }

    public static bool IsInCombat()
    {
        return CombatManager.Instance.IsInProgress;
    }
}