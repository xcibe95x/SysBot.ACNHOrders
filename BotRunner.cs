using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using SysBot.ACNHOrders.Twitch;
using SysBot.ACNHOrders.Signalr;

namespace SysBot.ACNHOrders
{
    public static class BotRunner
    {
        private const int MaxSwitchConnectionRetries = 3;

        public static async Task RunFrom(CrossBotConfig config, CancellationToken cancel, TwitchConfig? tConfig = null)
        {
            // Set up logging for Console Window
            LogUtil.Forwarders.Add(Logger);
            static void Logger(string msg, string identity) => Console.WriteLine(GetMessage(msg, identity));
            static string GetMessage(string msg, string identity) => $"> [{DateTime.Now:hh:mm:ss}] - {identity}: {msg}";

            var bot = new CrossBot(config);
            var sys = new SysCord(bot);

            Globals.Self = sys;
            Globals.Bot = bot;
            Globals.Hub = QueueHub.CurrentInstance;
            GlobalBan.UpdateConfiguration(config);

            if (config.EnableDiscord)
                bot.Log("Starting Discord.");
            else
                bot.Log("Discord is disabled in config.");
#pragma warning disable 4014
            if (config.EnableDiscord)
                Task.Run(() => sys.MainAsync(config.Token, cancel), cancel);
#pragma warning restore 4014

            if (tConfig != null && !string.IsNullOrWhiteSpace(tConfig.Token))
            {
                bot.Log("Starting Twitch.");
                var _ = new TwitchCrossBot(tConfig, bot);
            }

            if (!string.IsNullOrWhiteSpace(config.SignalrConfig.URIEndpoint))
            {
                bot.Log("Starting Web.");
                var _ = new SignalrCrossBot(config.SignalrConfig, bot);
            }

            if (config.SkipConsoleBotCreation)
            {
                await Task.Delay(-1, cancel).ConfigureAwait(false);
                return;
            }

            int connectionFailures = 0;
            while (!cancel.IsCancellationRequested)
            {
                bot.Log("Starting bot loop.");

                bool attemptReconnect = true;
                try
                {
                    await bot.RunAsync(cancel).ConfigureAwait(false);
                    connectionFailures = 0;
                    bot.Log("Bot has terminated. Restarting bot process.");
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    bot.Log("Bot has terminated due to an error:");
                    foreach (var inner in FlattenExceptions(ex))
                    {
                        bot.Log(inner.Message);
                        if (!string.IsNullOrWhiteSpace(inner.StackTrace))
                            bot.Log(inner.StackTrace);
                    }

                    if (IsSwitchConnectionFailure(ex))
                    {
                        connectionFailures++;
                        bot.Log($"Switch connection retry {connectionFailures}/{MaxSwitchConnectionRetries}.");
                        if (connectionFailures >= MaxSwitchConnectionRetries)
                        {
                            bot.Log("Failed to connect to the Switch after 3 retries. Exiting.");
                            attemptReconnect = false;
                        }
                    }
                    else
                    {
                        connectionFailures = 0;
                    }
                }

                if (!attemptReconnect || cancel.IsCancellationRequested)
                    break;

                await Task.Delay(10_000, cancel).ConfigureAwait(false);
                bot.Log("Bot is attempting a restart...");
                bot = new CrossBot(config);
                Globals.Bot = bot;

                if (config.EnableDiscord)
                    await sys.Disconnect();
                sys = new SysCord(bot);
                Globals.Self = sys;
                if (config.EnableDiscord)
                    bot.Log("Restarting Discord.");
#pragma warning disable 4014
                if (config.EnableDiscord)
                    Task.Run(() => sys.MainAsync(config.Token, cancel), cancel);
#pragma warning restore 4014
            }
        }

        private static IEnumerable<Exception> FlattenExceptions(Exception ex)
        {
            if (ex is AggregateException ae)
            {
                foreach (var inner in ae.Flatten().InnerExceptions)
                {
                    foreach (var flattened in FlattenExceptions(inner))
                        yield return flattened;
                }
                yield break;
            }

            yield return ex;
            if (ex.InnerException != null)
            {
                foreach (var flattened in FlattenExceptions(ex.InnerException))
                    yield return flattened;
            }
        }

        private static bool IsSwitchConnectionFailure(Exception ex)
        {
            foreach (var inner in FlattenExceptions(ex))
            {
                if (inner is SocketException || inner is IOException || inner is TimeoutException)
                    return true;

                var msg = inner.Message;
                if (string.IsNullOrWhiteSpace(msg))
                    continue;

                if (msg.Contains("switch", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("refused", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("unable to read data", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
