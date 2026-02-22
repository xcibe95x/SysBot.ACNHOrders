using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Threading;

namespace SysBot.ACNHOrders
{
    internal static class Program
    {
        private const string DefaultConfigPath = "config.json";
        private const string DefaultTwitchPath = "twitch.json";
		private const string DefaultSocketServerAPIPath = "server.json";
        private const string DefaultExtraConfigPath = "extraconfig.json";
        private const string LegacyGitHubPath = "github.json";

        private static async Task Main(string[] args)
        {
            string configPath;

			Console.WriteLine("Starting up...");
            if (args.Length > 0) 
            {
                if (args.Length > 1) 
                {
                    Console.WriteLine("Too many arguments supplied and will be ignored.");
                    configPath = DefaultConfigPath;
                }
                else {
                    configPath = args[0];
                }
            }
            else {
                configPath = DefaultConfigPath;
            }

            if (!File.Exists(configPath))
            {
                CreateConfigQuit(configPath);
                return;
            }

            if (!File.Exists(DefaultTwitchPath))
                SaveConfig(new TwitchConfig(), DefaultTwitchPath);

			if (!File.Exists(DefaultSocketServerAPIPath))
				SaveConfig(new SocketAPI.SocketAPIServerConfig(), DefaultSocketServerAPIPath);

            if (!File.Exists(DefaultExtraConfigPath))
            {
                if (File.Exists(LegacyGitHubPath))
                {
                    var legacyJson = File.ReadAllText(LegacyGitHubPath);
                    var legacyGitHubConfig = JsonSerializer.Deserialize<GitHubConfig>(legacyJson) ?? new GitHubConfig();
                    SaveConfig(new ExtraConfig { GitHubConfig = legacyGitHubConfig }, DefaultExtraConfigPath);
                }
                else
                {
                    SaveConfig(new ExtraConfig(), DefaultExtraConfigPath);
                }
            }

			var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<CrossBotConfig>(json);
            if (config == null)
            {
                Console.WriteLine("Failed to deserialize configuration file.");
                WaitKeyExit();
                return;
            }

            json = File.ReadAllText(DefaultTwitchPath);
            var twitchConfig = JsonSerializer.Deserialize<TwitchConfig>(json);
            if (twitchConfig == null)
            {
                Console.WriteLine("Failed to deserialize twitch configuration file.");
                WaitKeyExit();
                return;
            }

			json = File.ReadAllText(DefaultSocketServerAPIPath);
			var serverConfig = JsonSerializer.Deserialize<SocketAPI.SocketAPIServerConfig>(json);
            if (serverConfig == null)
            {
				Console.WriteLine("Failed to deserialize Socket API Server configuration file.");
				WaitKeyExit();
				return;
            }

            json = File.ReadAllText(DefaultExtraConfigPath);
            var extraConfig = JsonSerializer.Deserialize<ExtraConfig>(json);
            if (extraConfig == null)
            {
                Console.WriteLine("Failed to deserialize extra configuration file.");
                WaitKeyExit();
                return;
            }

            config.GitHubConfig = extraConfig.GitHubConfig ?? new GitHubConfig();

			SaveConfig(config, configPath);
            SaveConfig(twitchConfig, DefaultTwitchPath);
			SaveConfig(serverConfig, DefaultSocketServerAPIPath);
            SaveConfig(extraConfig, DefaultExtraConfigPath);
            
			SocketAPI.SocketAPIServer server = SocketAPI.SocketAPIServer.shared;
			_ = server.Start(serverConfig);

			await BotRunner.RunFrom(config, CancellationToken.None, twitchConfig).ConfigureAwait(false);

			WaitKeyExit();
        }

        private static void SaveConfig<T>(T config, string path)
        {
            var options = new JsonSerializerOptions {WriteIndented = true};
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(path, json);
        }

        private static void CreateConfigQuit(string configPath)
        {
            SaveConfig(new CrossBotConfig {IP = "192.168.0.1", Port = 6000}, configPath);
            Console.WriteLine("Created blank config file. Please configure it and restart the program.");
            WaitKeyExit();
        }

        private static void WaitKeyExit()
        {
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
    }
}
