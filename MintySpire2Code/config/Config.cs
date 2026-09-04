using BaseLib.Config;
using Godot;

namespace MintySpire2.MintySpire2Code.config;

public class Config : SimpleModConfig
{
    
    [ConfigHideInUI]
    [ConfigSlider(0.1, 1.0, 0.1, Format = "{0:0.0}x")]
    public static double ShuffleSpeed { get; set; } = 0.5;
    
    [ConfigSection("combat")]
    public static bool ShowIncomingDamage { get; set; } = true;
    [ConfigSlider(8, 30)]
    public static int IncomingDamageSize { get; set; } = 14;
    
    [ConfigSection("reminders")]
    public static bool CardOverlayReminders { get; set; } = true;
    [ConfigVisibleIf(nameof(CardOverlayReminders), true)]
    public static bool RelicCardGlow { get; set; } = true;
    public static bool EndTurnButtonReminders { get; set; } = true;
    public static bool OutOfCombatReminders { get; set; } = true;
    
    [ConfigSection("misc")]
    public static bool HideCommonKeywordTooltips { get; set; } = false;
    public static bool ChangeRewardOrder { get; set; } = true;
    public static bool StickyMapLegendHighlights { get; set; } = true;
    public static bool AscHoverTooltip { get; set; } = true;
    public static bool EnableJokes { get; set; } = true;
    public static bool HighlightEnchants { get; set; } = true;
    [ConfigVisibleIf(nameof(HighlightEnchants))]
    [ConfigColorPicker(EditAlpha = false)]
    public static string HighlightEnchantsColor { get; set; } = "#EE82EE"; // purple
    [ConfigVisibleIf(nameof(HighlightEnchants))]
    [ConfigColorPicker(EditAlpha = false)]
    public static string HighlightEnchantsOutlineColor { get; set; } = "#6F1F6F"; // dark pink
    
    public override void SetupConfigUI(Control optionContainer)
    {
        MintyInit.Logger.Info("Setting up SimpleModConfig " + GetType().FullName);
        GenerateOptionsForAllProperties(optionContainer);
        optionContainer.AddChild(new config.EnchantedCardPreview());
        AddRestoreDefaultsButton(optionContainer);
        SetupFocusNeighbors(optionContainer);
    }
}
