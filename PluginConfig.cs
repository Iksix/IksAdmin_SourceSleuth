using CounterStrikeSharp.API.Core;

namespace IksAdmin_SourceSleuth;

public class PluginConfig : BasePluginConfig
{
    public bool BanNewPlayerAccount { get; set; } = false;
    public bool NotifyAdminsAboutBans { get; set; } = true;
}