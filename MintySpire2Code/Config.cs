using BaseLib.Config;

namespace MintySpire2.MintySpire2Code;

public class Config: SimpleModConfig
{
    [ConfigSection("combat")]
    [ConfigSlider(0.1, 1.0, 0.1, Format = "{0:0.0}x")]
    public static double ShuffleSpeed { get; set; } = 0.5;
    
    public static bool ShowIncomingDamage { get; set; } = true;
    
    [ConfigSection("misc")]
    public static bool EnableJokes { get; set; } = true;
    public static bool ChangeRewardOrder { get; set; } = true;
}