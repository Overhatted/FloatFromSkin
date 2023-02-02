using NHttp;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FloatFromSkin
{
    public static class HTTPAdminServer
    {
        private const int KeyNumberOfBytes = 16;

        public static string Key;

        public static void Start()
        {
            byte[] KeyBytes = new byte[KeyNumberOfBytes];
            RNGCryptoServiceProvider RandomBytesGenerator = new RNGCryptoServiceProvider();
            RandomBytesGenerator.GetBytes(KeyBytes);

            Key = Convert.ToBase64String(KeyBytes);
            Console.WriteLine("HTTPAdminServer Key: " + Key);

            using (HttpServer Server = new HttpServer())
            {
                Server.RequestReceived += (s, e) =>
                {
                    if (e.Request.Path == "/")
                    {
                        bool IsAuthenticated = false;
                        if (e.Request.Cookies.Get("key").Value == Key)
                        {
                            IsAuthenticated = true;
                        }
                        else if (e.Request.Form.Get("key") == Key)
                        {
                            IsAuthenticated = true;
                            e.Response.Cookies.Add(new HttpCookie("key", Key));
                        }

                        if (IsAuthenticated)
                        {
                            if (e.Request.HttpMethod == "POST")
                            {
                                string Username = e.Request.Form.Get("username");
                                if(Username != null && Username != "")
                                {
                                    string Password = e.Request.Form.Get("password");
                                    if (Password == "")
                                    {
                                        Password = null;
                                    }

                                    string EmailAuthCode = e.Request.Form.Get("emailauthcode");
                                    if (EmailAuthCode == "")
                                    {
                                        EmailAuthCode = null;
                                    }

                                    string TwoFactorAuthCode = e.Request.Form.Get("twofactorauthcode");
                                    if (TwoFactorAuthCode == "")
                                    {
                                        TwoFactorAuthCode = null;
                                    }

                                    SteamFloatClient TargetedSteamClient = SocketsServer.GetSteamFloatClient(Username);
                                    if (TargetedSteamClient == null)
                                    {
                                        SocketsServer.AddSteamFloatClient(Username, Password, EmailAuthCode, TwoFactorAuthCode);

                                        HTTPAdminServer.SaveUsernames();
                                    }
                                    else
                                    {
                                        bool EnabledCheckbox = (e.Request.Form.Get("enabled") == "on") ? true : false;

                                        if (TargetedSteamClient.CurrentState == SteamFloatClient.SteamClientState.TryingToConnect ||
                                        TargetedSteamClient.CurrentState == SteamFloatClient.SteamClientState.ErrorWhileConnecting)
                                        {
                                            bool SomethingChanged = false;
                                            if (Password != null)
                                            {
                                                TargetedSteamClient.Password = Password;
                                                SomethingChanged = true;
                                            }

                                            if (EmailAuthCode != null)
                                            {
                                                TargetedSteamClient.EmailAuthCode = EmailAuthCode;
                                                SomethingChanged = true;
                                            }

                                            if (TwoFactorAuthCode != null)
                                            {
                                                TargetedSteamClient.TwoFactorAuthCode = TwoFactorAuthCode;
                                                SomethingChanged = true;
                                            }

                                            if (SomethingChanged)
                                            {
                                                Task.Run(() => {
                                                    TargetedSteamClient.SteamConnect();
                                                });
                                            }

                                        }
                                        else if(TargetedSteamClient.CurrentState == SteamFloatClient.SteamClientState.ConnectedButNotPlaying)
                                        {
                                            if(EnabledCheckbox)
                                            {
                                                TargetedSteamClient.SetReadyToReceiveRequests();
                                            }
                                        }
                                        else
                                        {
                                            if (!EnabledCheckbox)
                                            {
                                                TargetedSteamClient.SetNotReadyToReceiveRequests();
                                            }
                                        }
                                    }
                                }
                            }

                            using (StreamWriter Writer = new StreamWriter(e.Response.OutputStream))
                            {
                                Writer.Write("<a href=/>Refresh</a><br>");

                                foreach (SteamFloatClient CurrentSteamFloatClient in SocketsServer.SteamClients)
                                {
                                    Writer.Write(GetFloatClientForm(CurrentSteamFloatClient.UserInfo.Username, CurrentSteamFloatClient.CurrentState));
                                }

                                Writer.Write(GetFloatClientForm("", SteamFloatClient.SteamClientState.TryingToConnect));
                            }
                        }
                        else
                        {
                            using (StreamWriter Writer = new StreamWriter(e.Response.OutputStream))
                            {
                                Writer.Write("<form method=POST action=/><label for=key>Key:</label><input name=key></form>");
                            }
                        }
                    }
                };

                Server.EndPoint = new IPEndPoint(IPAddress.Any, main.SettingsObj.AdminServerPort);

                Server.Start();

                Console.WriteLine("HTTPAdminServer started on port " + main.SettingsObj.AdminServerPort);

                Thread.Sleep(Timeout.Infinite);
            }
        }

        private static string GetFloatClientForm(string Username, SteamFloatClient.SteamClientState ClientState)
        {
            string FormString = "<form method=POST action=/><label for=username>Username:</label><input name=username value=" + Username + ">";

            if(ClientState == SteamFloatClient.SteamClientState.TryingToConnect || ClientState == SteamFloatClient.SteamClientState.ErrorWhileConnecting)
            {
                FormString += "<label for=password>Password:</label><input name=password><label for=emailauthcode>EmailAuthCode:</label><input name=emailauthcode><label for=twofactorauthcode>TwoFactorAuthCode:</label><input name=twofactorauthcode>";
            }
            else
            {
                string CheckboxEnabledString = (ClientState == SteamFloatClient.SteamClientState.ConnectedButNotPlaying) ? "" : " checked";
                FormString += "<label for=enabled>Enabled:</label><input name=enabled type=checkbox" + CheckboxEnabledString + ">";
            }

            FormString += "<input type=submit></form>";

            return FormString;
        }

        private static void SaveUsernames()
        {
            string CurrentLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string UsernamesPath = Path.Combine(CurrentLocation, "usernames.txt");
            using (StreamWriter UsernamesFile = new StreamWriter(UsernamesPath))
            {
                foreach (SteamFloatClient CurrentSteamFloatClient in SocketsServer.SteamClients)
                {
                    UsernamesFile.WriteLine(CurrentSteamFloatClient.UserInfo.Username);
                }
            }
        }
    }
}
