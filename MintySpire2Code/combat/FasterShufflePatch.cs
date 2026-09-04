using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MintySpire2.MintySpire2Code.config;

namespace MintySpire2.MintySpire2Code.combat;

/**
 * Credits to Pandemonium, increases the discard to draw pile shuffling speed.
 */
public class FasterShufflePatch
{

    private static double GetMultiplier()
    {
        return Config.ShuffleSpeed;
    }
    
    //Transpiler patch
    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Shuffle), MethodType.Async)]
    public static class CardPileCmdShufflePatch
    {
        private static float MultiplyShuffleSpeed(float normalTime)
        {
            return Convert.ToSingle(normalTime *  GetMultiplier());
        }
        
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);
        
            codeMatcher
                .MatchEndForward(
                    new CodeMatch(OpCodes.Conv_R4),
                    new CodeMatch(OpCodes.Div),
                    CodeMatch.Calls(typeof(Mathf).GetMethod(nameof(Mathf.Min), [typeof(float), typeof(float)])),
                    new CodeMatch(OpCodes.Stfld)
                )
                .ThrowIfInvalid("Didn't find a match for Fast Shuffle Patch")
                .InsertAndAdvance(
                    CodeInstruction.Call<float,float>( time => MultiplyShuffleSpeed(time))
                );
        
            return codeMatcher.Instructions();
        }
    }
}
