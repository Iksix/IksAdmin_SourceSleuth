using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Entities;
using Dapper;
using IksAdminApi;
using MySqlConnector;

namespace IksAdmin_SourceSleuth;

public class Main : AdminModule, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "IksAdmin_SourceSleuth";
    public override string ModuleVersion => "1.0.1";
    public override string ModuleAuthor => "iks__";
    public PluginConfig Config { get; set; }
    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
    }

    [GameEventHandler]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || player.IsBot || !player.IsValid || player.AuthorizedSteamID == null) return HookResult.Continue;

        var playerInfo = new PlayerInfo(player);
        if (playerInfo.Ip!.Contains(Api.Config.ServerIp))     
            return HookResult.Continue;   
        Task.Run(async () =>
        {
            var oldBan = await GetLastPlayerIpBan(playerInfo);
            if (oldBan == null)
                return;
            if (Config.BanNewPlayerAccount)
            {
                oldBan.Reason += $" [SS ({oldBan.SteamId})]";
                oldBan.SteamId = playerInfo.SteamId;
                oldBan.Name = playerInfo.PlayerName;
                oldBan.AdminId = Api.ConsoleAdmin.Id;
                await Api.AddBan(oldBan);
                return;
            }
            Server.NextFrame(() =>
            {
                var message = Localizer["NotifyMessage"].Value
                        .Replace("{name}", playerInfo.PlayerName)
                        .Replace("{steamId}", playerInfo.SteamId)
                        .Replace("{ip}", playerInfo.Ip)
                        .Replace("{bannedId}", oldBan.SteamId)
                        .Replace("{bannedName}", oldBan.Name)
                        .Replace("{banReason}", oldBan.Reason)
                    ;
                if (Config.NotifyAllAboutBan)
                    AdminUtils.PrintToServer(message);
                else
                {
                    var onlineAdmins = Utilities.GetPlayers().Where(x => x.Admin() != null);
                    foreach (var admin in onlineAdmins)
                    {
                        admin.Print(message);
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
            await using var conn = new MySqlConnection(Api.DbConnectionString);
            await conn.OpenAsync();
            var ban = await conn.QueryFirstOrDefaultAsync<PlayerBan>(@"
            select
            id as id,
            steam_id as steamId,
            ip as ip,
            name as name,
            duration as duration,
            reason as reason,
            ban_type as banType,
            server_id as serverId,
            admin_id as adminId,
            unbanned_by as unbannedBy,
            unban_reason as unbanReason,
            created_at as createdAt,
            end_at as endAt,
            updated_at as updatedAt,
            deleted_at as deletedAt
            from iks_bans
            where deleted_at is null
            and ip = @ip
            and unbanned_by is null
            and (end_at > unix_timestamp() or end_at = 0)
            and (server_id is null or server_id = @serverId)
            ", new {ip = player.Ip, timeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), server_id = Api.Config.ServerId});
            return ban;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }
    

}
