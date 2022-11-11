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
using System.Diagnostics;

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
            Client.OnJoinRequested += OnJoinRequested;

            Client.SetSubscription(EventType.Join);
            Client.Subscribe(EventType.Join);
        }

        private static async void OnJoin(object sender, JoinMessage e)
        {
            string[] splitted = e.Secret.Split(';');

            if (splitted.Length == 0) return;
            string address = splitted[0];
            string password = splitted.Length >= 2 ? splitted[1] : string.Empty;

            if (await MainWindow.Me.ShowRoomInfo(address))
            {
                await MainWindow.Me.ConnectRoom(password);
            }
        }

        private static void OnJoinRequested(object sender, JoinRequestMessage e)
        {
            bool accept = true;

            Debug.WriteLine($"[Info] User {e.User.Username} requested the join message.");

            if (RoomManager.CurrentRoom.PasswordRequired)
            {
                accept = true; //need to make popup feature
            }

            DiscordRpcClient client = (DiscordRpcClient)sender;
            client.Respond(e, accept);

            Debug.WriteLine($"[Info] User {e.User.Username} is {(accept ? "accepted" : "rejected")}.");
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

            if (RoomManager.IsHost || !RoomManager.CurrentRoom.PasswordRequired)
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

            if (RoomManager.CurrentRoom.PasswordRequired)
            {
                Client.SetSubscription(EventType.Join);
                Client.Subscribe(EventType.Join);
            }
            else
            {
                Client.SetSubscription(EventType.Join | EventType.JoinRequest);
                Client.Subscribe(EventType.Join | EventType.JoinRequest);
            }
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
