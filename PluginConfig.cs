using CounterStrikeSharp.API.Core;

namespace IksAdmin_SourceSleuth;

public class PluginConfig : BasePluginConfig
{
    public bool BanNewPlayerAccount { get; set; } = false;
    public bool NotifyAllAboutBan { get; set; } = true; // Если включенно то сообщение о том что у игрока есть бан будет отсылатся всем игрокам, инчае - админам
}