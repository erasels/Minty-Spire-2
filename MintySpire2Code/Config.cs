using BaseLib.Config;

namespace MintySpire2.MintySpire2Code;

public class Config: SimpleModConfig
{
    [ConfigHideInUI]
    [ConfigSlider(0.1, 1.0, 0.1, Format = "{0:0.0}x")]
    public static double ShuffleSpeed { get; set; } = 0.5;
    
    [ConfigSection("combat")]
    public static bool ShowIncomingDamage { get; set; } = true;
    [ConfigSlider(8, 30)]
    public static int IncomingDamageSize { get; set; } = 14;
    
    
    [ConfigSection("misc")]
    public static bool EnableJokes { get; set; } = true;
    public static bool ChangeRewardOrder { get; set; } = true;
    public static bool AscHoverTooltip { get; set; } = true;
}
