using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using DiscordRPC;
using DiscordRPC.Events;
using DiscordRPC.Message;

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
            Client.RegisterUriScheme();
            Client.OnError += (s, e) =>
            {
                MessageBox.Show(e.Message);
            };
            Client.OnJoin += OnJoin;
        }

        private static void OnJoin(object sender, JoinMessage e)
        {

        }

        public static void SetPresenceIfCreated()
        {
            Client.UpdateState("Streaming Live");

            Client.UpdateSecrets(new Secrets()
            {
                JoinSecret = RoomManager.GetInviteSecret()
            });
            Client.UpdateParty(new Party()
            {
                ID = RoomManager.CurrentRoomId.ToString(),
                Size = RoomManager.CurrentRoom.CurrentUserCount,
                Max = RoomManager.MAX_USER_COUNT,
                Privacy = Party.PrivacySetting.Public
            });

            Client.SetSubscription(EventType.Join | EventType.JoinRequest);
            Client.Subscribe(EventType.Join | EventType.JoinRequest);
        }

        public static void SetPresenceIfUserUpdated()
        {
            if (!Client.IsInitialized || Client.CurrentUser == null) return;

            if (RoomManager.IsHost)
            {
                Client.UpdateSecrets(new Secrets()
                {
                    JoinSecret = RoomManager.GetInviteSecret()
                });
            }
            Client.UpdateParty(new Party()
            {
                ID = RoomManager.CurrentRoomId.ToString(),
                Size = RoomManager.CurrentRoom.CurrentUserCount,
                Max = RoomManager.MAX_USER_COUNT,
                Privacy = Party.PrivacySetting.Public
            });
        }

        public static void SetPresenceIfJoined()
        {
            Client.UpdateState("Streaming Live");

            Client.UpdateParty(new Party()
            {
                ID = RoomManager.CurrentRoomId.ToString(),
                Size = RoomManager.CurrentRoom.CurrentUserCount,
                Max = RoomManager.MAX_USER_COUNT,
                Privacy = Party.PrivacySetting.Public
            });

            Client.SetSubscription(EventType.Join);
            Client.Subscribe(EventType.Join);
        }

        public static void SetPresenceIfLeft()
        {
            Client.UpdateState(string.Empty);

            Client.UpdateSecrets(new Secrets());
            Client.UpdateParty(new Party());

            Client.SetSubscription(EventType.None);
            Client.Subscribe(EventType.None);
        }
    }
}
