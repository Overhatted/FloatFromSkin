using System;

namespace FloatFromSkin
{
    public class main
    {
        public static Settings SettingsObj;

        static void Main(string[] args)
        {
            Console.Title = "Float From Skin";
            SettingsObj = Settings.LoadFromFile();

            SocketsServer.Start();

            //SteamFloatClient.StartServers(SteamClients[0].UserInfo.CellID);
            HTTPAdminServer.Start();
        }
    }
}