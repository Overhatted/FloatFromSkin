using System;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.CSGO.Internal;
using System.IO;
using System.Net;
using SteamKit2.Internal;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace FloatFromSkin
{
    class SteamFloatClient
    {
        private static string Username;
        private static string Password;
        private static string EmailAuthCode;
        private static string TwoFactorAuthCode;

        private static bool IsRunning = false;

        private static SteamClient steamClient;
        private static CallbackManager callbackManager;
        private static SteamGameCoordinator steamGameCoordinator;
        private static SteamUser steamUser;
        private static SteamFriends steamFriends;

        private static double Time_Between_Float_Requests = 1.4;
        private static uint APP_ID = 730;

        private static List<ulong> Skins_Queue = new List<ulong>();
        private static DateTime Last_Float_Request_Time;

        public static void Start()
        {
            Console.Title = "Float From Skin";
            SteamConnect();
        }

        private static void SteamConnect()
        {
            // create our steamclient instance
            steamClient = new SteamClient();
            // create the callback manager which will route callbacks to function calls
            callbackManager = new CallbackManager(steamClient);

            // register a few callbacks we're interested in
            // these are registered upon creation to a callback manager, which will then route the callbacks
            // to the functions specified
            callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

            // get the steamuser handler, which is used for logging on after successfully connecting
            steamUser = steamClient.GetHandler<SteamUser>();
            callbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            callbackManager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
            callbackManager.Subscribe<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth);
            callbackManager.Subscribe<SteamUser.LoginKeyCallback>(OnLoginKey);

            steamFriends = steamClient.GetHandler<SteamFriends>();
            callbackManager.Subscribe<SteamUser.AccountInfoCallback>(
                async (cb) => await steamFriends.SetPersonaState(EPersonaState.Online)
            );

            steamGameCoordinator = steamClient.GetHandler<SteamGameCoordinator>();
            callbackManager.Subscribe<SteamGameCoordinator.MessageCallback>(OnMessage);

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;

                Console.WriteLine("Received {0}, disconnecting...", e.SpecialKey);
                steamUser.LogOff();

                IsRunning = false;
            };

            LoadServers();

            IsRunning = true;

            Console.WriteLine("Connecting to Steam...");

            // initiate the connection
            SteamDirectory.Initialize().Wait();
            steamClient.Connect();

            // create our callback handling loop
            while (IsRunning)
            {
                // in order for the callbacks to get routed, they need to be handled by the manager
                callbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(Time_Between_Float_Requests));
                Proccess_Next_Float_Request();
            }

            SaveServers();
        }

        private static void LoadServers()
        {
            if (Properties.Settings.Default.Servers_base_64 != "")
            {
                // last time we connected to Steam, we got a list of servers. that list is persisted below.
                // load that list of servers into the server list.
                // this is a very simplistic serialization, you're free to serialize the server list however
                // you like (json, xml, whatever).

                byte[] Servers = Convert.FromBase64String(Properties.Settings.Default.Servers_base_64);

                using (MemoryStream fs = new MemoryStream(Servers))
                {
                    using (BinaryReader reader = new BinaryReader(fs))
                    {
                        while (fs.Position < fs.Length)
                        {
                            var numAddressBytes = reader.ReadInt32();
                            var addressBytes = reader.ReadBytes(numAddressBytes);
                            var port = reader.ReadInt32();

                            var ipaddress = new IPAddress(addressBytes);
                            var endPoint = new IPEndPoint(ipaddress, port);

                            CMClient.Servers.TryAdd(endPoint);
                        }
                    }
                }

                //Console.WriteLine($"Loaded {CMClient.Servers.GetAllEndPoints().Length} servers from server list cache.");
            }
            else
            {
                // since we don't have a list of servers saved, load the latest list of Steam servers
                // from the Steam Directory.
                var loadServersTask = SteamDirectory.Initialize(Properties.Settings.Default.Cell_id);
                loadServersTask.Wait();

                if (loadServersTask.IsFaulted)
                {
                    Console.WriteLine("Error loading server list from directory: {0}", loadServersTask.Exception.Message);
                    return;
                }
            }
        }

        private static void SaveServers()
        {
            // before we exit, save our current server list to disk.
            // this is a very simplistic serialization, you're free to serialize the server list however
            // you like (json, xml, whatever).
            using (MemoryStream fs = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    foreach (var endPoint in CMClient.Servers.GetAllEndPoints())
                    {
                        var addressBytes = endPoint.Address.GetAddressBytes();
                        writer.Write(addressBytes.Length);
                        writer.Write(addressBytes);
                        writer.Write(endPoint.Port);
                    }
                }

                Properties.Settings.Default.Servers_base_64 = Convert.ToBase64String(fs.ToArray());
            }

            Properties.Settings.Default.Save();
        }

        private static void OnConnected(SteamClient.ConnectedCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to connect to Steam: {0}", callback.Result);
                IsRunning = false;
                return;
            }
            Console.WriteLine("Connected to Steam");

            Login();
        }

        private static void Login()
        {
            if(Username == null)//If it's the first time we are trying to login
            {
                if (Properties.Settings.Default.Username == "")
                {
                    Console.WriteLine("Username:");
                    Username = Console.ReadLine();

                    Console.WriteLine("Password:");
                    Password = Console.ReadLine();
                }
                else
                {
                    Username = Properties.Settings.Default.Username;
                }
            }

            Console.WriteLine("Logging in {0}...", Username);

            string LoginKey = Properties.Settings.Default.Login_key;
            bool ShouldRememberPassword = true;

            byte[] sentryHash = null;
            if (Properties.Settings.Default.Sentry_base_64 != "")
            {
                // if we have a saved sentry file, read and sha-1 hash it
                byte[] sentryFile = Convert.FromBase64String(Properties.Settings.Default.Sentry_base_64);
                sentryHash = CryptoHelper.SHAHash(sentryFile);
            }

            try
            {
                steamUser.LogOn(new SteamUser.LogOnDetails
                {
                    Username = Username,
                    Password = Password,
                    AuthCode = EmailAuthCode,
                    TwoFactorCode = TwoFactorAuthCode,

                    LoginKey = LoginKey,
                    ShouldRememberPassword = ShouldRememberPassword,

                    SentryFileHash = sentryHash,
                });
            }
            catch (ArgumentException)
            {
                Console.WriteLine("Invalid login credentials");
            }
        }

        private static void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            Console.WriteLine("Disconnected from Steam");
        }

        private static void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            bool isSteamGuard = callback.Result == EResult.AccountLogonDenied;
            bool is2FA = callback.Result == EResult.AccountLoginDeniedNeedTwoFactor;

            if (isSteamGuard || is2FA)
            {
                Console.WriteLine("This account is SteamGuard protected!");

                if (is2FA)
                {
                    Console.Write("Please enter your 2 factor auth code from your authenticator app: ");
                    TwoFactorAuthCode = Console.ReadLine();
                }
                else
                {
                    Console.Write("Please enter the auth code sent to the email at {0}: ", callback.EmailDomain);
                    EmailAuthCode = Console.ReadLine();
                }

                steamClient.Connect();
                return;
            }

            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult);

                IsRunning = false;
                return;
            }

            Console.Title = Username + " - Float From Skin";

            // save the current cellid somewhere. if we lose our saved server list, we can use this when retrieving
            // servers from the Steam Directory.
            Properties.Settings.Default.Cell_id = callback.CellID;

            Properties.Settings.Default.Username = Username;

            Properties.Settings.Default.Save();

            Console.WriteLine("Successfully logged on! Press Ctrl+C to log off...");

            OnSuccessLoggedOn();
        }

        private static void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine("Logged off of Steam: {0}", callback.Result);
        }

        private static void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
        {
            // write out our sentry file
            byte[] sentryHash;

            using (SHA1CryptoServiceProvider sha = new SHA1CryptoServiceProvider())
            {
                sentryHash = sha.ComputeHash(callback.Data);
            }

            Properties.Settings.Default.Sentry_base_64 = Convert.ToBase64String(callback.Data);

            Properties.Settings.Default.Save();

            // inform the steam servers that we're accepting this sentry file
            steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,

                FileName = callback.FileName,

                BytesWritten = callback.BytesToWrite,
                FileSize = callback.Data.Length,
                Offset = callback.Offset,

                Result = EResult.OK,
                LastError = 0,

                OneTimePassword = callback.OneTimePassword,

                SentryFileHash = sentryHash,
            });

            //Console.WriteLine("Updated sentry file");
        }

        private static void OnLoginKey(SteamUser.LoginKeyCallback callback)
        {
            Properties.Settings.Default.Login_key = callback.LoginKey;
            Properties.Settings.Default.Save();

            //Console.WriteLine("Updated loginkey");
        }

        private static void OnSuccessLoggedOn()
        {
            var playGame = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);

            playGame.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
            {
                game_id = new GameID(APP_ID),
            });

            steamClient.Send(playGame);

            var clientHello = new ClientGCMsgProtobuf<CMsgClientHello>((uint)EGCBaseClientMsg.k_EMsgGCClientHello);
            steamGameCoordinator.Send(clientHello, APP_ID);

            SocketsServer.Start_Sockets_Server();
            Last_Float_Request_Time = DateTime.UtcNow;
        }

        public static void Request_Float(Skin Skin_Requested)
        {
            if (!Skins_Queue.Contains(Skin_Requested.param_a))
            {
                Skins_Queue.Add(Skin_Requested.param_a);
                Proccess_Next_Float_Request();
            }
        }

        private static void Proccess_Next_Float_Request()
        {
            if (Last_Float_Request_Time.AddSeconds(Time_Between_Float_Requests) < DateTime.UtcNow && Skins_Queue.Count != 0)
            {
                Last_Float_Request_Time = DateTime.UtcNow;

                Skin Skin_To_Request = SocketsServer.Skins_Database[Skins_Queue[0]];
                Skins_Queue.RemoveAt(0);

                Skin_To_Request.Last_Float_Request_Time = DateTime.UtcNow;

                var request = new ClientGCMsgProtobuf<CMsgGCCStrike15_v2_Client2GCEconPreviewDataBlockRequest>((uint)ECsgoGCMsg.k_EMsgGCCStrike15_v2_Client2GCEconPreviewDataBlockRequest);

                request.Body.param_s = Skin_To_Request.param_s;
                request.Body.param_a = Skin_To_Request.param_a;
                request.Body.param_d = Skin_To_Request.param_d;
                request.Body.param_m = Skin_To_Request.param_m;

                steamGameCoordinator.Send(request, APP_ID);
            }
        }

        private static void OnMessage(SteamGameCoordinator.MessageCallback callback)
        {
            switch (callback.EMsg)
            {
                case (uint)ECsgoGCMsg.k_EMsgGCCStrike15_v2_Client2GCEconPreviewDataBlockResponse:
                    var msg = new ClientGCMsgProtobuf<CMsgGCCStrike15_v2_Client2GCEconPreviewDataBlockResponse>(callback.Message);

                    SocketsServer.Process_Skin_Response(msg.Body.iteminfo.itemid, BitConverter.ToSingle(BitConverter.GetBytes(msg.Body.iteminfo.paintwear), 0));

                    break;
            }
        }
    }
}
