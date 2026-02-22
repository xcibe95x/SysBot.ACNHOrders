using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;

namespace SysBot.ACNHOrders
{
    internal static class GitHubDodoPusher
    {
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SysBot.ACNHOrders");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }

        public static async Task<bool> TryPushAsync(GitHubConfig cfg, string content, string ip, CancellationToken token)
        {
            try
            {
                if (!TryParseRepo(cfg.GitHubDodoRepo, out var owner, out var repo))
                {
                    LogUtil.LogError("GitHub Dodo push failed: invalid repo format. Use \"owner/repo\" or a GitHub URL.", ip);
                    return false;
                }

                var branch = string.IsNullOrWhiteSpace(cfg.GitHubDodoBranch) ? "main" : cfg.GitHubDodoBranch.Trim();
                var path = string.IsNullOrWhiteSpace(cfg.GitHubDodoPath) ? "Dodo.txt" : cfg.GitHubDodoPath.Trim().TrimStart('/');
                var urlPath = string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
                var baseUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{urlPath}";

                string? sha = null;
                var getUrl = $"{baseUrl}?ref={Uri.EscapeDataString(branch)}";
                using (var getRequest = new HttpRequestMessage(HttpMethod.Get, getUrl))
                {
                    AddAuthHeader(getRequest, cfg.GitHubToken);
                    using var getResponse = await Client.SendAsync(getRequest, token).ConfigureAwait(false);
                    if (getResponse.IsSuccessStatusCode)
                    {
                        var json = await getResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("sha", out var shaProp))
                            sha = shaProp.GetString();
                    }
                    else if (getResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
                    {
                        var body = await getResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                        LogUtil.LogError($"GitHub Dodo push failed: unable to read current file (HTTP {(int)getResponse.StatusCode}). {body}", ip);
                        return false;
                    }
                }

                var payload = new Dictionary<string, object?>
                {
                    ["message"] = string.IsNullOrWhiteSpace(cfg.GitHubDodoCommitMessage) ? "Update Dodo.txt" : cfg.GitHubDodoCommitMessage,
                    ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
                    ["branch"] = branch
                };
                if (!string.IsNullOrWhiteSpace(sha))
                    payload["sha"] = sha;

                var jsonPayload = JsonSerializer.Serialize(payload);
                using var putRequest = new HttpRequestMessage(HttpMethod.Put, baseUrl)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                AddAuthHeader(putRequest, cfg.GitHubToken);
                using var putResponse = await Client.SendAsync(putRequest, token).ConfigureAwait(false);
                if (!putResponse.IsSuccessStatusCode)
                {
                    var body = await putResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    LogUtil.LogError($"GitHub Dodo push failed: HTTP {(int)putResponse.StatusCode}. {body}", ip);
                    return false;
                }

                return true;
            }
            catch (TaskCanceledException)
            {
                LogUtil.LogError("GitHub Dodo push failed: request timed out.", ip);
                return false;
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"GitHub Dodo push failed: {ex.Message}", ip);
                return false;
            }
        }

        private static void AddAuthHeader(HttpRequestMessage request, string token)
        {
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private static bool TryParseRepo(string input, out string owner, out string repo)
        {
            owner = string.Empty;
            repo = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            var trimmed = input.Trim();
            if (trimmed.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                    return false;
                var parts = uri.AbsolutePath.Trim('/').Split('/');
                if (parts.Length < 2)
                    return false;
                owner = parts[0];
                repo = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
                return !string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repo);
            }

            var split = trimmed.Split('/');
            if (split.Length != 2)
                return false;
            owner = split[0];
            repo = split[1];
            return !string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repo);
        }
    }
}
