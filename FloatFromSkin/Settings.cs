using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace FloatFromSkin
{
    public class Settings
    {
        public ushort SocketsPort = 8000;

        public ushort AdminServerPort = 8001;

        public List<SteamFloatUser> SteamFloatUsers = new List<SteamFloatUser>();

        public static Settings LoadFromFile()
        {
            try
            {
                string CurrentLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(Path.Combine(CurrentLocation, "settings.json")));
            }
            catch(FileNotFoundException)
            {
                return new Settings();
            }
        }

        public static void SaveToFile()
        {
            string CurrentLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            File.WriteAllText(Path.Combine(CurrentLocation, "settings.json"), JsonConvert.SerializeObject(main.SettingsObj));
        }
    }
}