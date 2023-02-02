using Fleck2;
using Fleck2.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.Caching;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

namespace FloatFromSkin
{
    public static class SocketsServer
    {
        public static AutoResetEvent QueueGetsAnElementEvent = new AutoResetEvent(false);
        public static ConcurrentQueue<ulong> SkinsQueue = new ConcurrentQueue<ulong>();
        private const ushort MaximumSizeOfSkinsQueue = 1000;

        public static List<SteamFloatClient> SteamClients = new List<SteamFloatClient>();

        public static ConcurrentDictionary<ulong, Skin> SkinsDatabase = new ConcurrentDictionary<ulong, Skin>();

        private static ConcurrentDictionary<Guid, IWebSocketConnection> SocketsDict = new ConcurrentDictionary<Guid, IWebSocketConnection>();

        public static void Start()
        {
            try
            {
                WebSocketServer Server = new WebSocketServer("ws://localhost:" + main.SettingsObj.SocketsPort);

                Server.Start(socket =>
                {
                    socket.OnOpen = () =>
                    {
                        SocketsDict.TryAdd(socket.ConnectionInfo.Id, socket);
                    };

                    socket.OnClose = () => {
                        IWebSocketConnection SocketConnection;
                        SocketsDict.TryRemove(socket.ConnectionInfo.Id, out SocketConnection);
                    };

                    socket.OnMessage = message =>
                    {
                        ProcessIncomingMessage(socket, message);
                    };
                });

                Console.WriteLine("Sockets server successfully started on port " + main.SettingsObj.SocketsPort.ToString());
            }
            catch (System.Net.Sockets.SocketException)
            {
                Console.WriteLine("Port " + main.SettingsObj.SocketsPort.ToString() + " is busy, please choose a different one.");
                return;
            }

            //Start Steam Float Clients
            List<string> Usernames = new List<string>();

            string CurrentLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string UsernamesPath = Path.Combine(CurrentLocation, "usernames.txt");
            if (File.Exists(UsernamesPath))
            {
                foreach (string Line in File.ReadLines(UsernamesPath))
                {
                    if (Line.Length != 0)
                    {
                        Usernames.Add(Line);
                    }
                }
            }

            foreach (string Username in Usernames)
            {
                SocketsServer.AddSteamFloatClient(Username);
            }
        }

        public static SteamFloatClient GetSteamFloatClient(string Username)
        {
            foreach (SteamFloatClient CurrentSteamClient in SteamClients)
            {
                if (Username == CurrentSteamClient.UserInfo.Username)
                {
                    return CurrentSteamClient;
                }
            }

            return null;
        }

        public static void AddSteamFloatClient(string Username, string Password = null, string EmailAuthCode = null, string TwoFactorAuthCode = null)
        {
            SteamFloatUser SteamFloatUserToAdd = new SteamFloatUser();
            SteamFloatUserToAdd.Username = Username;

            foreach (SteamFloatUser UserToCheck in main.SettingsObj.SteamFloatUsers)
            {
                if (UserToCheck.Username == Username)
                {
                    SteamFloatUserToAdd = UserToCheck;
                    break;
                }
            }

            SteamFloatClient SteamFloatClientAdded = new SteamFloatClient(SteamFloatUserToAdd);
            SteamFloatClientAdded.Password = Password;
            SteamFloatClientAdded.EmailAuthCode = EmailAuthCode;
            SteamFloatClientAdded.TwoFactorAuthCode = TwoFactorAuthCode;

            SteamClients.Add(SteamFloatClientAdded);

            Task.Run(() => {
                SteamFloatClientAdded.SteamConnect();
            });
        }

        public static void ProcessIncomingMessage(IWebSocketConnection Socket, string Message)
        {
            string[] ParamsArray = Message.Split('S', 'M', 'A', 'D');

            if (ParamsArray.Length != 4)
            {
                Socket.Send("Invalid Skin String");
                return;
            }

            ulong FirstNumber = 0;
            ulong param_a = 0;
            ulong param_d = 0;
            try
            {
                FirstNumber = ulong.Parse(ParamsArray[1]);
                param_a = ulong.Parse(ParamsArray[2]);
                param_d = ulong.Parse(ParamsArray[3]);
            }
            catch (FormatException)
            {
                Socket.Send("Invalid Skin String");
                return;
            }

            if (MemoryCache.Default.Contains(param_a.ToString()))
            {
                SendSkinCachedResponse(param_a, Socket);
            }
            else
            {
                Skin SkinRequested;
                if (SkinsDatabase.TryGetValue(param_a, out SkinRequested))
                {
                    if (!SkinRequested.ConnectionGuids.Contains(Socket.ConnectionInfo.Id))
                    {
                        SkinRequested.ConnectionGuids.Add(Socket.ConnectionInfo.Id);
                    }
                }
                else
                {
                    SkinRequested = new Skin();

                    if (Message[0].ToString() == "S")
                    {
                        SkinRequested.param_s = FirstNumber;
                        SkinRequested.param_m = 0;
                    }
                    else
                    {
                        SkinRequested.param_s = 0;
                        SkinRequested.param_m = FirstNumber;
                    }

                    SkinRequested.param_a = param_a;
                    SkinRequested.param_d = param_d;

                    SkinRequested.ConnectionGuids.Add(Socket.ConnectionInfo.Id);

                    if (SkinsDatabase.TryAdd(SkinRequested.param_a, SkinRequested))
                    {
                        SkinsQueue.Enqueue(SkinRequested.param_a);

                        QueueGetsAnElementEvent.Set();
                    }
                    else
                    {
                        Socket.Send("F" + SkinRequested.param_a.ToString() + ":Error");
                    }
                }
            }
        }

        public static void SendSkinCachedResponse(ulong AssedID, IWebSocketConnection Socket)
        {
            string FloatValueString = (string) MemoryCache.Default.Get(AssedID.ToString());

            Socket.Send("F" + AssedID.ToString() + ":" + FloatValueString);
        }

        public static void SendSkinResponses(ulong AssedID, float FloatValue)
        {
            Skin SkinToRespond;
            if(SkinsDatabase.TryRemove(AssedID, out SkinToRespond))
            {
                NumberFormatInfo FloatFormat = new NumberFormatInfo();
                FloatFormat.NumberDecimalSeparator = ".";
                FloatFormat.NumberDecimalDigits = 9;
                string FloatValueString = FloatValue.ToString(FloatFormat);

                MemoryCache.Default.Add(new CacheItem(AssedID.ToString(), FloatValueString), new CacheItemPolicy());

                foreach (Guid ConnectionGuid in SkinToRespond.ConnectionGuids)
                {
                    IWebSocketConnection SocketsConnectionsConnection;
                    if (SocketsDict.TryGetValue(ConnectionGuid, out SocketsConnectionsConnection))
                    {
                        SocketsConnectionsConnection.Send("F" + SkinToRespond.param_a.ToString() + ":" + FloatValueString);
                    }
                }
            }
        }

        public static void SendSkinErrorMessage(Skin SkinRequested)
        {
            SkinsDatabase.TryRemove(SkinRequested.param_a, out SkinRequested);

            foreach (Guid ConnectionGuid in SkinRequested.ConnectionGuids)
            {
                IWebSocketConnection SocketsConnectionsConnection;
                if (SocketsDict.TryGetValue(ConnectionGuid, out SocketsConnectionsConnection))
                {
                    SocketsConnectionsConnection.Send("F" + SkinRequested.param_a.ToString() + ":Error");
                }
            }
        }
    }
}
