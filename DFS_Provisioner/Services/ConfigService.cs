using System.IO;
using DFS_Provisioner.Models;
using Newtonsoft.Json;

namespace DFS_Provisioner.Services
{
    /// <summary>Handles loading and saving of the JSON configuration file.</summary>
    public static class ConfigService
    {
        /// <summary>Loads a <see cref="DefaultConfig"/> from the specified path.</summary>
        public static DefaultConfig LoadConfig(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Configuration file not found.", path);

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<DefaultConfig>(json);
        }

        /// <summary>Saves a <see cref="DefaultConfig"/> to the specified path.</summary>
        public static void SaveConfig(string path, DefaultConfig config)
        {
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }
}