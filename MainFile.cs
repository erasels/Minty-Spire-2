using BaseLib.Config;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MintySpire2.MintySpire2Code;

namespace MintySpire2;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "MintySpire2"; //At the moment, this is used only for the Logger and harmony names.

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        ModConfigRegistry.Register(ModId, new Config());
        
        Harmony harmony = new(ModId);

        harmony.PatchAll();
    }
}
