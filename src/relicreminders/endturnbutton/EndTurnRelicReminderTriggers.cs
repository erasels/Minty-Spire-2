using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace MintySpire2.relicreminders.endturnbutton;

public class EndTurnRelicReminderTriggers
{
    [HarmonyPatch]
    static class BasicReminderNotificationPatches
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> MethodsToTriggerReminders()
        {
            return
            [
                typeof(NEndTurnButton).Method(nameof(NEndTurnButton.OnTurnStarted)),
                typeof(NEndTurnButton).Method(nameof(NEndTurnButton.AfterPlayerEndedTurn)),
            ];
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
        }
    }

    [HarmonyPatch]
    public static class RelicSpecificNotificationPatches
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> MethodsToTriggerReminders()
        {
            return
            [
                // PaelsEye
                typeof(PaelsEye).Method(nameof(PaelsEye.BeforeCardPlayed)),
                typeof(PaelsEye).Method(nameof(PaelsEye.AfterSideTurnStart)),
                typeof(PaelsEye).Method(nameof(PaelsEye.AfterTakingExtraTurn)),


                // Orichalcum
                typeof(Orichalcum).Method(nameof(Orichalcum.BeforeTurnEndVeryEarly)),
                typeof(Orichalcum).Method(nameof(Orichalcum.BeforeTurnEnd)),
                typeof(Orichalcum).Method(nameof(Orichalcum.BeforeSideTurnStart)),


                // FakeOrichalcum
                typeof(FakeOrichalcum).Method(nameof(FakeOrichalcum.BeforeTurnEndVeryEarly)),
                typeof(FakeOrichalcum).Method(nameof(FakeOrichalcum.BeforeTurnEnd)),
                typeof(FakeOrichalcum).Method(nameof(FakeOrichalcum.BeforeSideTurnStart)),


                // RippleBasin
                typeof(RippleBasin).Method(nameof(RippleBasin.BeforeTurnEnd)),
                typeof(RippleBasin).Method(nameof(RippleBasin.AfterCardPlayed)),
                typeof(RippleBasin).Method(nameof(RippleBasin.BeforeSideTurnStart)),


                // Pocketwatch
                typeof(Pocketwatch).Method(nameof(Pocketwatch.AfterCardPlayed)),
                typeof(Pocketwatch).Method(nameof(Pocketwatch.BeforeSideTurnStart)),
                typeof(Pocketwatch).Method(nameof(Pocketwatch.AfterSideTurnStart)),


                // ArtOfWar
                typeof(ArtOfWar).Method(nameof(ArtOfWar.AfterCardPlayed)),
                typeof(ArtOfWar).Method(nameof(ArtOfWar.AfterTurnEnd)),
                typeof(ArtOfWar).Method(nameof(ArtOfWar.AfterEnergyReset)),


                // PaelsTears
                typeof(PaelsTears).Method(nameof(PaelsTears.BeforeTurnEnd)),
                typeof(PaelsTears).Method(nameof(PaelsTears.AfterSideTurnStart)),


                // DiamondDiadem
                typeof(DiamondDiadem).Method(nameof(DiamondDiadem.AfterCardPlayed)),
                typeof(DiamondDiadem).Method(nameof(DiamondDiadem.BeforeTurnEnd)),
                typeof(DiamondDiadem).Method(nameof(DiamondDiadem.AfterSideTurnStart)),
            ];
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
        }
    }
}