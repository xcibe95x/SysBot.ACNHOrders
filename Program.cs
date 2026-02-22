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
        private const string DefaultGitHubPath = "github.json";

        private static async Task Main(string[] args)
        {
            string configPath = DefaultConfigPath;
            string twitchPath = DefaultTwitchPath;
            string socketServerPath = DefaultSocketServerAPIPath;
            string extraConfigPath = DefaultExtraConfigPath;
            string githubPath = DefaultGitHubPath;

			Console.WriteLine("Starting up...");
            if (args.Length > 5)
            {
                Console.WriteLine("Too many arguments supplied. Expected up to 5: config, twitch, server, extraconfig, github. Extra arguments will be ignored.");
            }

            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
                configPath = args[0];
            if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
                twitchPath = args[1];
            if (args.Length > 2 && !string.IsNullOrWhiteSpace(args[2]))
                socketServerPath = args[2];
            if (args.Length > 3 && !string.IsNullOrWhiteSpace(args[3]))
                extraConfigPath = args[3];
            if (args.Length > 4 && !string.IsNullOrWhiteSpace(args[4]))
                githubPath = args[4];

            if (!File.Exists(configPath))
            {
                CreateConfigQuit(configPath);
                return;
            }

            if (!File.Exists(twitchPath))
                SaveConfig(new TwitchConfig(), twitchPath);

			if (!File.Exists(socketServerPath))
				SaveConfig(new SocketAPI.SocketAPIServerConfig(), socketServerPath);

            if (!File.Exists(extraConfigPath))
                SaveConfig(new ExtraConfig(), extraConfigPath);

            if (!File.Exists(githubPath))
                SaveConfig(new GitHubConfig(), githubPath);

			var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<CrossBotConfig>(json);
            if (config == null)
            {
                Console.WriteLine("Failed to deserialize configuration file.");
                WaitKeyExit();
                return;
            }

            json = File.ReadAllText(twitchPath);
            var twitchConfig = JsonSerializer.Deserialize<TwitchConfig>(json);
            if (twitchConfig == null)
            {
                Console.WriteLine("Failed to deserialize twitch configuration file.");
                WaitKeyExit();
                return;
            }

			json = File.ReadAllText(socketServerPath);
			var serverConfig = JsonSerializer.Deserialize<SocketAPI.SocketAPIServerConfig>(json);
            if (serverConfig == null)
            {
				Console.WriteLine("Failed to deserialize Socket API Server configuration file.");
				WaitKeyExit();
				return;
            }

            json = File.ReadAllText(extraConfigPath);
            var extraConfig = JsonSerializer.Deserialize<ExtraConfig>(json);
            if (extraConfig == null)
            {
                Console.WriteLine("Failed to deserialize extra configuration file.");
                WaitKeyExit();
                return;
            }

            json = File.ReadAllText(githubPath);
            var githubConfig = JsonSerializer.Deserialize<GitHubConfig>(json);
            if (githubConfig == null)
            {
                Console.WriteLine("Failed to deserialize github configuration file.");
                WaitKeyExit();
                return;
            }

            config.GitHubConfig = githubConfig;
            config.AnchorAutomationConfig = extraConfig.AnchorAutomationConfig ?? new AnchorAutomationConfig();

			SaveConfig(config, configPath);
            SaveConfig(twitchConfig, twitchPath);
			SaveConfig(serverConfig, socketServerPath);
            SaveConfig(extraConfig, extraConfigPath);
            SaveConfig(githubConfig, githubPath);
            
			SocketAPI.SocketAPIServer server = SocketAPI.SocketAPIServer.shared;
			_ = server.Start(serverConfig);

			await BotRunner.RunFrom(config, CancellationToken.None, twitchConfig).ConfigureAwait(false);

			WaitKeyExit();
        }

        private static void SaveConfig<T>(T config, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

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
