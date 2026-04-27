using System.IO;
using DFS_Provisioner.Models;
using Newtonsoft.Json;

namespace DFS_Provisioner.Services
{
    public static class ConfigService
    {
        public static DefaultConfig LoadConfig(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Файл конфигурации не найден.", path);

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<DefaultConfig>(json);
        }

        public static void SaveConfig(string path, DefaultConfig config)
        {
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }
}