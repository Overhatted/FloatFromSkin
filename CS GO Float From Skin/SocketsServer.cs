using Fleck2;
using Fleck2.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FloatFromSkin
{
    class SocketsServer
    {
        public static Dictionary<ulong, Skin> Skins_Database = new Dictionary<ulong, Skin>();

        private static Dictionary<Guid, IWebSocketConnection> Sockets_Dict = new Dictionary<Guid, IWebSocketConnection>();
        private static double Float_Requests_Timeout = 10;

        public static void Start_Sockets_Server()
        {
            WebSocketServer server = new WebSocketServer("ws://localhost:"+Properties.Settings.Default.Sockets_Port);

            server.Start(socket =>
            {
                socket.OnOpen = () => Sockets_Dict.Add(socket.ConnectionInfo.Id, socket);
                socket.OnClose = () => Sockets_Dict.Remove(socket.ConnectionInfo.Id);
                socket.OnMessage = message => Process_Incoming_Message(socket, message);
            });
        }

        public static void Process_Incoming_Message(IWebSocketConnection socket, string Message)
        {
            string[] Params_Array = Regex.Split(Message, "[A-Z]", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));

            if(Params_Array.Length != 4)
            {
                socket.Send("Invalid Skin String");
                return;
            }

            ulong First_Number = 0;
            ulong param_a = 0;
            ulong param_d = 0;
            try
            {
                First_Number = ulong.Parse(Params_Array[1]);
                param_a = ulong.Parse(Params_Array[2]);
                param_d = ulong.Parse(Params_Array[3]);
            }
            catch (FormatException)
            {
                socket.Send("Invalid Skin String");
                return;
            }

            if (Skins_Database.ContainsKey(param_a))
            {
                Skin Skin_Requested = Skins_Database[param_a];

                Skin_Requested.Connection_Guids.Add(socket.ConnectionInfo.Id);

                if (Skin_Requested.Has_Float_Value)
                {
                    Send_Skin_Responses(Skin_Requested);
                }
                else if(Skin_Requested.Last_Float_Request_Time.AddSeconds(Float_Requests_Timeout) < DateTime.UtcNow)
                {
                    SteamFloatClient.Request_Float(Skin_Requested);
                }
            }
            else
            {
                Skin Skin_Requested = new Skin();

                if (Message[0].ToString() == "S")
                {
                    Skin_Requested.param_s = First_Number;
                    Skin_Requested.param_m = 0;
                }
                else
                {
                    Skin_Requested.param_s = 0;
                    Skin_Requested.param_m = First_Number;
                }

                Skin_Requested.param_a = param_a;
                Skin_Requested.param_d = param_d;

                Skin_Requested.Connection_Guids.Add(socket.ConnectionInfo.Id);

                Skins_Database.Add(Skin_Requested.param_a, Skin_Requested);

                SteamFloatClient.Request_Float(Skin_Requested);
            }
        }

        public static void Process_Skin_Response(ulong Assed_ID, float Float_Value)
        {
            Skin Skin_Responded = Skins_Database[Assed_ID];

            Skin_Responded.Float_Value = Float_Value;
            Skin_Responded.Has_Float_Value = true;

            Send_Skin_Responses(Skin_Responded);
        }

        public static void Send_Skin_Responses(Skin Skin_To_Respond)
        {
            NumberFormatInfo Float_Format = new NumberFormatInfo();
            Float_Format.NumberDecimalSeparator = ".";
            Float_Format.NumberDecimalDigits = 9;

            foreach (Guid Connection_Guid in Skin_To_Respond.Connection_Guids)
            {
                if (Sockets_Dict.ContainsKey(Connection_Guid))
                {
                    Sockets_Dict[Connection_Guid].Send("F" + Skin_To_Respond.param_a.ToString() + ":" + Skin_To_Respond.Float_Value.ToString(Float_Format));
                }
            }
        }
    }
}
