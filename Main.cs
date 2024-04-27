using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using Dapper;
using IksAdminApi;
using MySqlConnector;

namespace IksAdmin_SourceSleuth;

public class Main : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "IksAdmin_SourceSleuth";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "iks__";
    
    private readonly PluginCapability<IIksAdminApi> _adminPluginCapability = new("iksadmin:core");
    public static IIksAdminApi? AdminApi;
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        AdminApi = _adminPluginCapability.Get();
    }
    
    public PluginConfig Config { get; set; }
    
    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
    }

    [GameEventHandler]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player.IsBot || !player.IsValid || player.AuthorizedSteamID == null) return HookResult.Continue;

        var playerInfo = player.GetInfo();
        
        Task.Run(async () =>
        {
            var oldBan = await GetLastPlayerIpBan(playerInfo);
            if (oldBan == null)
                return;
            if (Config.BanNewPlayerAccount)
            {
                oldBan.Reason += $" [SS ({oldBan.Sid})]";
                oldBan.Sid = playerInfo.SteamId.SteamId64.ToString();
                oldBan.Name = playerInfo.PlayerName;
                await AdminApi!.AddBan("CONSOLE", oldBan);
                return;
            }
            Server.NextFrame(() =>
            {
                var message = Localizer["NotifyMessage"].Value
                        .Replace("{name}", playerInfo.PlayerName)
                        .Replace("{steamId}", playerInfo.SteamId.SteamId64.ToString())
                        .Replace("{ip}", playerInfo.IpAddress)
                        .Replace("{bannedId}", oldBan.Sid)
                        .Replace("{bannedName}", oldBan.Name)
                        .Replace("{banReason}", oldBan.Reason)
                    ;
                if (Config.NotifyAllAboutBan)
                    AdminApi!.SendMessageToAll(message);
                else
                {
                    var onlineAdmins = Utilities.GetPlayers().Where(x => AdminApi!.GetAdmin(x) != null);
                    foreach (var admin in onlineAdmins)
                    {
                        AdminApi!.SendMessageToPlayer(admin, message);
                    }
                }
            });
        });
        
        return HookResult.Continue;
    }

    public async Task<PlayerBan?> GetLastPlayerIpBan(PlayerInfo player)
    {
        try
        {
            await using var conn = new MySqlConnection(AdminApi!.DbConnectionString);
            await conn.OpenAsync();
            var ban = await conn.QueryFirstOrDefaultAsync<PlayerBan>(@"
            select 
            name as name,
            sid as sid,
            ip as ip,
            adminsid as adminSid,
            adminName as adminName,
            created as created,
            time as time,
            end as end,
            reason as reason,
            server_id as serverId,
            BanType as banType,
            Unbanned as unbanned,
            UnbannedBy as unbannedBy,
            id as id
            from iks_bans
            where ip LIKE @ip and (end > @timeNow or time = 0) and Unbanned = 0 and (server_id = @server_id or server_id = '')
            ", new {ip = $"%{player.IpAddress.Split(":")[0]}%", timeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), server_id = AdminApi.Config.ServerId});
            
            if (ban != null)
            {
                if (ban.ServerId.Trim() != "" && ban.ServerId != AdminApi.Config.ServerId)
                {
                    return null;
                }
            }
            
            return ban;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }
    

}
