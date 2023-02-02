using System;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.CSGO.Internal;
using System.IO;
using SteamKit2.Internal;
using System.Security.Cryptography;
using System.Threading;
using SteamKit2.Discovery;

namespace FloatFromSkin
{
    public class SteamFloatClient
    {
        private static bool ServersLoaded = false;

        [Flags]
        public enum SteamClientState
        {
            TryingToConnect = 0,
            ErrorWhileConnecting = 1,
            ConnectedButNotPlaying = 2,
            ReadyToReceiveRequests = 3,
            WaitingForCallback = 4,
            WaitingForRequestCooldown = 5
        }

        public SteamClientState CurrentState;

        private Thread UsedThread;

        public SteamFloatUser UserInfo;

        public string Password;
        public string EmailAuthCode;
        public string TwoFactorAuthCode;

        private SteamClient steamClient;
        private CallbackManager callbackManager;
        private SteamGameCoordinator steamGameCoordinator;
        private SteamUser steamUser;
        private SteamFriends steamFriends;

        private const int TimeBetweenFloatRequests = 1400;
        private const int TimeoutSeconds = 10;
        private const uint APPID = 730;

        public SteamFloatClient(SteamFloatUser UserInfo)
        {
            this.UserInfo = UserInfo;
        }

        public void SteamConnect()
        {
            this.CurrentState = SteamClientState.TryingToConnect;
            // create our steamclient instance
            this.steamClient = new SteamClient();
            
            // create the callback manager which will route callbacks to function calls
            this.callbackManager = new CallbackManager(steamClient);

            // register a few callbacks we're interested in
            // these are registered upon creation to a callback manager, which will then route the callbacks
            // to the functions specified
            this.callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            this.callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

            // get the steamuser handler, which is used for logging on after successfully connecting
            this.steamUser = steamClient.GetHandler<SteamUser>();
            this.callbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            this.callbackManager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
            this.callbackManager.Subscribe<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth);
            this.callbackManager.Subscribe<SteamUser.LoginKeyCallback>(OnLoginKey);

            this.steamFriends = steamClient.GetHandler<SteamFriends>();
            this.callbackManager.Subscribe<SteamUser.AccountInfoCallback>(
                async (cb) => await this.steamFriends.SetPersonaState(EPersonaState.Online)
            );

            this.steamGameCoordinator = steamClient.GetHandler<SteamGameCoordinator>();
            this.callbackManager.Subscribe<SteamGameCoordinator.MessageCallback>(OnMessage);

            this.PrintToConsole("Connecting to Steam...");

            if (!ServersLoaded)
            {
                StartServers(this.UserInfo.CellID);
            }

            // initiate the connection
            SteamDirectory.Initialize().Wait();
            this.steamClient.Connect();

            this.WaitForLoginCallbacks();
        }

        private void WaitForLoginCallbacks()
        {
            while (this.CurrentState == SteamClientState.TryingToConnect)
            {
                Thread.Sleep(500);
                this.callbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(TimeoutSeconds));
            }
        }

        public static void StartServers(uint CellID)
        {
            SteamClient.Servers.CellID = CellID;
            string CurrentLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            SteamClient.Servers.ServerListProvider = new FileStorageServerListProvider(Path.Combine(CurrentLocation, "Servers.bin"));

            ServersLoaded = true;
        }

        private void OnConnected(SteamClient.ConnectedCallback callback)
        {
            if (callback.Result == EResult.OK)
            {
                this.PrintToConsole("Connected to Steam");

                this.Login();
            }
            else {
                this.PrintToConsole("Unable to connect to Steam: " + callback.Result);

                this.CurrentState = SteamClientState.ErrorWhileConnecting;
            }
        }

        private void Login()
        {
            if(this.UserInfo.LoginKey == null && this.Password == null)
            {
                this.PrintToConsole("Please send your password");
                return;
            }

            byte[] sentryHash = null;
            if (this.UserInfo.SentryBase64 != null)
            {
                // if we have a saved sentry file, read and sha-1 hash it
                byte[] sentryFile = Convert.FromBase64String(this.UserInfo.SentryBase64);
                sentryHash = CryptoHelper.SHAHash(sentryFile);
            }

            if (this.UserInfo.LoginID == 0)
            {
                //Generate new LoginID (it is stored if the login is successful)
                this.UserInfo.LoginID = (uint)new Random().Next(1, int.MaxValue);
            }

            if (this.UserInfo.LoginKey == null)
            {
                //Login with password
                try
                {
                    this.steamUser.LogOn(new SteamUser.LogOnDetails
                    {
                        LoginID = this.UserInfo.LoginID,
                        Username = this.UserInfo.Username,
                        Password = this.Password,
                        AuthCode = this.EmailAuthCode,
                        TwoFactorCode = this.TwoFactorAuthCode,

                        ShouldRememberPassword = true,

                        SentryFileHash = sentryHash
                    });
                }
                catch (ArgumentException e)
                {
                    this.PrintToConsole("Invalid login credentials");
                    this.PrintToConsole(e.Message);
                }
            }
            else
            {
                //Passwordless login
                try
                {
                    this.steamUser.LogOn(new SteamUser.LogOnDetails
                    {
                        LoginID = this.UserInfo.LoginID,
                        Username = this.UserInfo.Username,

                        LoginKey = this.UserInfo.LoginKey,
                        ShouldRememberPassword = true,

                        SentryFileHash = sentryHash
                    });
                }
                catch (ArgumentException e)
                {
                    this.PrintToConsole("Passwordless login failed");
                    this.PrintToConsole(e.Message);
                }
            }
        }

        private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            bool isSteamGuard = callback.Result == EResult.AccountLogonDenied;
            bool is2FA = callback.Result == EResult.AccountLoginDeniedNeedTwoFactor;

            if (isSteamGuard || is2FA)
            {
                if (is2FA)
                {
                    this.PrintToConsole("Please send your 2 factor auth code from your authenticator app");
                }
                else
                {
                    this.PrintToConsole("Please send the auth code sent to the email at " + callback.EmailDomain);
                }
                return;
            }

            if (callback.Result != EResult.OK)
            {
                if(callback.Result == EResult.InvalidPassword)
                {
                    this.PrintToConsole("Invalid Password, please send it again:");
                    this.UserInfo.LoginKey = null;
                    this.Password = null;
                }
                else
                {
                    this.PrintToConsole("Unable to logon to Steam: " + callback.Result + " / " + callback.ExtendedResult);
                    this.CurrentState = SteamClientState.ErrorWhileConnecting;
                }
                return;
            }

            this.UserInfo.CellID = callback.CellID;

            Settings.SaveToFile();

            this.SetReadyToReceiveRequests();
        }

        private void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
        {
            // write out our sentry file
            byte[] sentryHash;

            using (SHA1Managed sha = new SHA1Managed())
            {
                sentryHash = sha.ComputeHash(callback.Data);
            }

            this.UserInfo.SentryBase64 = Convert.ToBase64String(callback.Data);

            // inform the steam servers that we're accepting this sentry file
            steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,

                FileName = callback.FileName,

                BytesWritten = callback.BytesToWrite,
                FileSize = callback.BytesToWrite,
                Offset = callback.Offset,

                Result = EResult.OK,
                LastError = 0,

                OneTimePassword = callback.OneTimePassword,

                SentryFileHash = sentryHash
            });

            Settings.SaveToFile();

            this.PrintToConsole("Sentry file received");
        }

        private void OnLoginKey(SteamUser.LoginKeyCallback callback)
        {
            this.UserInfo.LoginKey = callback.LoginKey;

            this.steamUser.AcceptNewLoginKey(callback);

            Settings.SaveToFile();

            this.PrintToConsole("Login Key received");
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            UsedThread.Abort();

            this.PrintToConsole("Logged off of Steam: " + callback.Result);

            this.CurrentState = SteamClientState.ErrorWhileConnecting;
        }

        private void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            UsedThread.Abort();

            this.PrintToConsole("Disconnected from Steam");

            this.CurrentState = SteamClientState.ErrorWhileConnecting;

            this.PrintToConsole(this.CurrentState.ToString());
        }

        public void SetReadyToReceiveRequests()
        {
            var StartPlayingGameMessage = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);

            StartPlayingGameMessage.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
            {
                game_id = new GameID(APPID),
            });

            this.steamClient.Send(StartPlayingGameMessage);

            var ClientHello = new ClientGCMsgProtobuf<CMsgClientHello>((uint)EGCBaseClientMsg.k_EMsgGCClientHello);
            this.steamGameCoordinator.Send(ClientHello, APPID);

            this.CurrentState = SteamClientState.ReadyToReceiveRequests;

            UsedThread = new Thread(WaitForFloats);
            UsedThread.Start();

            this.PrintToConsole("Ready to receive requests");
        }

        public void SetNotReadyToReceiveRequests()
        {
            UsedThread.Abort();

            this.CurrentState = SteamClientState.ConnectedButNotPlaying;

            var StopPlayingGameMessage = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);

            StopPlayingGameMessage.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
            {
                game_id = new GameID(0),
            });

            this.steamClient.Send(StopPlayingGameMessage);

            this.PrintToConsole("NOT ready to receive requests");
        }

        public void WaitForFloats()
        {
            while (true)
            {
                SocketsServer.QueueGetsAnElementEvent.WaitOne();
                while (this.TryRequestNextFloat())
                {
                    
                }
            }
        }

        public bool TryRequestNextFloat()
        {
            ulong AssetIDOfSkinToRequest;
            Skin SkinToRequest;
            if (SocketsServer.SkinsQueue.TryDequeue(out AssetIDOfSkinToRequest) && SocketsServer.SkinsDatabase.TryGetValue(AssetIDOfSkinToRequest, out SkinToRequest))
            {
                this.CurrentState = SteamClientState.WaitingForCallback;

                var SkinDataRequest = new ClientGCMsgProtobuf<CMsgGCCStrike15_v2_Client2GCEconPreviewDataBlockRequest>((uint)ECsgoGCMsg.k_EMsgGCCStrike15_v2_Client2GCEconPreviewDataBlockRequest);

                SkinDataRequest.Body.param_s = SkinToRequest.param_s;
                SkinDataRequest.Body.param_a = SkinToRequest.param_a;
                SkinDataRequest.Body.param_d = SkinToRequest.param_d;
                SkinDataRequest.Body.param_m = SkinToRequest.param_m;

                int NumberOfAttemptsRemaining = 2;
                DateTime RequestDateTime = DateTime.UtcNow;

                while (this.CurrentState == SteamClientState.WaitingForCallback && NumberOfAttemptsRemaining != 0)
                {

                    if (NumberOfAttemptsRemaining == 0)
                    {
                        SocketsServer.SendSkinErrorMessage(SkinToRequest);

                        this.CurrentState = SteamClientState.ReadyToReceiveRequests;
                    }
                    else
                    {
                        steamGameCoordinator.Send(SkinDataRequest, APPID);

                        while(this.CurrentState == SteamClientState.WaitingForCallback && DateTime.UtcNow - RequestDateTime < TimeSpan.FromSeconds(TimeoutSeconds))
                        {
                            this.callbackManager.RunWaitAllCallbacks(TimeSpan.FromSeconds(TimeoutSeconds) - (DateTime.UtcNow - RequestDateTime));
                        }

                        NumberOfAttemptsRemaining--;
                    }
                }

                return true;
            }
            else
            {
                this.CurrentState = SteamClientState.ReadyToReceiveRequests;

                return false;
            }
        }

        private void OnMessage(SteamGameCoordinator.MessageCallback callback)
        {
            if (callback.EMsg == (uint)ECsgoGCMsg.k_EMsgGCCStrike15_v2_Client2GCEconPreviewDataBlockResponse)
            {
                var ReceivedMessage = new ClientGCMsgProtobuf<CMsgGCCStrike15_v2_Client2GCEconPreviewDataBlockResponse>(callback.Message);

                SocketsServer.SendSkinResponses(ReceivedMessage.Body.iteminfo.itemid, BitConverter.ToSingle(BitConverter.GetBytes(ReceivedMessage.Body.iteminfo.paintwear), 0));

                this.CurrentState = SteamClientState.WaitingForRequestCooldown;

                Thread.Sleep(TimeBetweenFloatRequests);
            }
        }

        public void PrintToConsole(string Message)
        {
            if(Message == null)
            {
                Message = "null";
            }
            Console.WriteLine(this.UserInfo.Username + " - " + Message);
        }
    }
}
