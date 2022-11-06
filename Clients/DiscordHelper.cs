using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using DiscordRPC;

using BetterLiveScreen.Rooms;

namespace BetterLiveScreen.Clients
{
    public class DiscordHelper
    {
        public const string DISCORD_APPLICATION_ID = "909043000053760012";
        public static DiscordRpcClient Client { get; set; } = new DiscordRpcClient(DISCORD_APPLICATION_ID);

        public static void Initialize()
        {
            Client.Initialize();
            Client.SetPresence(new RichPresence()
            {
                Assets = new Assets()
                {
                    LargeImageKey = "icon"
                },
                Timestamps = new Timestamps(DateTime.UtcNow),
            });
            Client.RegisterUriScheme(null, null);
            Client.OnError += (s, e) =>
            {
                MessageBox.Show(e.Message);
            };
            Client.OnJoin += (s, e) =>
            {

            };
        }

        public static void SetPresenceIfJoined()
        {
            Client.UpdateSecrets(new Secrets()
            {
                JoinSecret = RoomManager.GetInviteSecret()
            });
            Client.UpdateParty(new Party()
            {
                ID = RoomManager.CurrentRoomId.ToString(),
                Size = RoomManager.CurrentRoom.CurrentUserCount,
                Max = RoomManager.MAX_USER_COUNT,
                Privacy = Party.PrivacySetting.Private
            });
            Client.SetSubscription(EventType.Join);
            Client.Subscribe(EventType.Join);
        }
    }
}
