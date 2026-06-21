using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MintySpire2.MintySpire2Code.util;

public static class CardTurnEndInspector
{
    private static readonly Dictionary<Type, bool> CardCache = new();
    private static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
    private static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];

    static CardTurnEndInspector()
    {
        foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (f.GetValue(null) is not OpCode op) continue;

            ushort v = unchecked((ushort)op.Value);
            if (v < 0x100)
                OneByteOpCodes[v] = op;
            else if ((v & 0xff00) == 0xfe00)
                TwoByteOpCodes[v & 0xff] = op;
        }
    }

    public static bool DoesTurnEndInHandCallDamage(CardModel card)
    {
        var cardType = card.GetType();
        
        if(CardCache.TryGetValue(cardType, out var result)) return result;

        // Fast fail if the card does not even claim to have a hand end effect.
        if (!card.HasTurnEndInHandEffect)
            return false;

        var onTurnEnd = cardType.GetMethod(
            "OnTurnEndInHand",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(PlayerChoiceContext)],
            modifiers: null);

        if (onTurnEnd == null)
            return false;

        // For async methods, inspect the state machine MoveNext().
        var methodToInspect = GetMethodToInspect(onTurnEnd);
        if (methodToInspect == null)
            return false;
        
        var doesDamage = CallsTargetMethod(methodToInspect, IsCreatureCmdDamage);
        
        CardCache.Add(cardType, doesDamage);
        return doesDamage;
    }

    private static MethodInfo? GetMethodToInspect(MethodInfo originalMethod)
    {
        var asyncAttr = originalMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        if (asyncAttr == null) return originalMethod;
        var moveNext = asyncAttr.StateMachineType.GetMethod(
            "MoveNext",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return moveNext;

    }

    private static bool IsCreatureCmdDamage(MethodBase method)
    {
        if (method is not MethodInfo mi)
            return false;

        if (mi.Name != "Damage")
            return false;

        if (mi.DeclaringType?.Name != nameof(CreatureCmd))
            return false;

        var parameters = mi.GetParameters();
        if (parameters.Length < 2)
            return false;

        return parameters[0].ParameterType == typeof(PlayerChoiceContext)
               && parameters[1].ParameterType == typeof(Creature);
    }

    private static bool CallsTargetMethod(MethodInfo method, Func<MethodBase, bool> predicate)
    {
        var body = method.GetMethodBody();

        var il = body?.GetILAsByteArray();
        if (il == null || il.Length == 0)
            return false;

        int i = 0;
        var module = method.Module;

        while (i < il.Length)
        {
            OpCode op;
            byte code = il[i++];

            if (code != 0xFE)
            {
                op = OneByteOpCodes[code];
            }
            else
            {
                byte code2 = il[i++];
                op = TwoByteOpCodes[code2];
            }

            switch (op.OperandType)
            {
                case OperandType.InlineMethod:
                {
                    int token = ReadInt32(il, ref i);
                    MethodBase? called;
                    try
                    {
                        called = module.ResolveMethod(token, method.DeclaringType?.GetGenericArguments(), method.GetGenericArguments());
                    }
                    catch
                    {
                        called = null;
                    }

                    if ((op == OpCodes.Call || op == OpCodes.Callvirt) && called != null && predicate(called))
                    {
                        return true;
                    }

                    break;
                }

                case OperandType.InlineNone:
                    break;

                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    i += 1;
                    break;

                case OperandType.InlineVar:
                    i += 2;
                    break;

                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    i += 4;
                    break;

                case OperandType.InlineI8:
                case OperandType.InlineR:
                    i += 8;
                    break;

                case OperandType.ShortInlineR:
                    i += 4;
                    break;

                case OperandType.InlineSwitch:
                {
                    int count = ReadInt32(il, ref i);
                    i += count * 4;
                    break;
                }

                default:
                    throw new NotSupportedException($"Unhandled operand type: {op.OperandType}");
            }
        }

        return false;
    }

    private static int ReadInt32(byte[] il, ref int index)
    {
        int value = BitConverter.ToInt32(il, index);
        index += 4;
        return value;
    }
}