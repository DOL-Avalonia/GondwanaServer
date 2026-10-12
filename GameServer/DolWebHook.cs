
using DOL.GS.ServerProperties;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DOL.GS
{
    public sealed class DiscordEmbed
    {
        [JsonPropertyName("title")] public string Title { get; set; }
        [JsonPropertyName("description")] public string Description { get; set; }
        [JsonPropertyName("url")] public string Url { get; set; }
        [JsonPropertyName("color")] public int? Color { get; set; }
        [JsonPropertyName("timestamp")] public string Timestamp { get; set; }
        [JsonPropertyName("footer")] public DiscordEmbedFooter Footer { get; set; }
        [JsonPropertyName("author")] public DiscordEmbedAuthor Author { get; set; }
        [JsonPropertyName("thumbnail")] public DiscordEmbedImage Thumbnail { get; set; }
        [JsonPropertyName("image")] public DiscordEmbedImage Image { get; set; }
        [JsonPropertyName("fields")] public List<DiscordEmbedField> Fields { get; set; }

        public DiscordEmbed AddField(string name, string value, bool inline = false)
        {
            (Fields ??= new()).Add(new DiscordEmbedField { Name = Trunc(name, 256), Value = Trunc(value, 1024), Inline = inline });
            return this;
        }
        public DiscordEmbed WithTimestamp() { Timestamp = DateTime.UtcNow.ToString("o"); return this; }
        internal static string Trunc(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..(max - 1)] + "…";
    }
    public sealed class DiscordEmbedFooter { [JsonPropertyName("text")] public string Text { get; set; } [JsonPropertyName("icon_url")] public string IconUrl { get; set; } }
    public sealed class DiscordEmbedAuthor { [JsonPropertyName("name")] public string Name { get; set; } [JsonPropertyName("icon_url")] public string IconUrl { get; set; } [JsonPropertyName("url")] public string Url { get; set; } }
    public sealed class DiscordEmbedImage { [JsonPropertyName("url")] public string Url { get; set; } }
    public sealed class DiscordEmbedField { [JsonPropertyName("name")] public string Name { get; set; } [JsonPropertyName("value")] public string Value { get; set; } [JsonPropertyName("inline")] public bool Inline { get; set; } }

    public sealed class DiscordMessage
    {
        [JsonPropertyName("content")] public string Content { get; set; }
        [JsonPropertyName("username")] public string Username { get; set; }
        [JsonPropertyName("avatar_url")] public string AvatarUrl { get; set; }
        [JsonPropertyName("tts")] public bool Tts { get; set; }
        [JsonPropertyName("embeds")] public List<DiscordEmbed> Embeds { get; set; }
        [JsonPropertyName("allowed_mentions")] public object AllowedMentions { get; set; } = new { parse = Array.Empty<string>() }; // never ping @everyone from game chat
    }

    public static class DiscordColors
    {
        public const int Green = 0x2ECC71, Red = 0xE74C3C, Orange = 0xE67E22, Blue = 0x3498DB, Purple = 0x9B59B6, Gold = 0xF1C40F, Grey = 0x95A5A6;
        public const int Albion = 0xC0392B, Midgard = 0x2980B9, Hibernia = 0x27AE60;
    }

    /// Non-blocking Discord webhook client: queued, rate-limited (Discord: 30 req/min/webhook), retries on 429/5xx.
    public sealed class DolWebHook
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DolWebHook));
        private static readonly JsonSerializerOptions s_json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        private static readonly HttpClient s_http = CreateClient();
        private static readonly Channel<(string url, string json)> s_queue =
            Channel.CreateBounded<(string, string)>(new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropOldest });
        private static readonly Task s_worker = Task.Run(WorkerLoop);
        private static DateTime s_nextAllowedUtc = DateTime.MinValue;

        public string Url { get; }
        public string Name { get; set; } = string.IsNullOrEmpty(Properties.ROG_SERVER_NAME) ? "Dawn of Light" : Properties.ROG_SERVER_NAME;
        public string AvatarUrl { get; set; }

        public DolWebHook(string webhookUrl) => Url = Normalize(webhookUrl);

        public static bool IsConfigured => Properties.DISCORD_ACTIVE && !string.IsNullOrWhiteSpace(Properties.DISCORD_WEBHOOK_ID);

        // ---- legacy API kept: returns true when *queued* (never blocks) ----
        public bool SendMessage(string content) => Enqueue(new DiscordMessage { Content = DiscordEmbed.Trunc(content, 2000), Username = Name, AvatarUrl = AvatarUrl });

        public bool SendEmbed(DiscordEmbed embed, string content = null) =>
            Enqueue(new DiscordMessage { Content = content, Username = Name, AvatarUrl = AvatarUrl, Embeds = new() { embed } });

        public bool Send(DiscordMessage message) => Enqueue(message);

        private bool Enqueue(DiscordMessage message)
        {
            if (string.IsNullOrEmpty(Url)) return false;
            message.Username ??= Name;
            return s_queue.Writer.TryWrite((Url, JsonSerializer.Serialize(message, s_json)));
        }

        private static string Normalize(string idOrUrl)
        {
            if (string.IsNullOrWhiteSpace(idOrUrl)) return null;
            idOrUrl = idOrUrl.Trim();
            if (idOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return idOrUrl;
            log.Warn("discord_webhook_id must be the full webhook URL (https://discord.com/api/webhooks/<id>/<token>).");
            return null;
        }

        private static HttpClient CreateClient()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                ConnectTimeout = TimeSpan.FromSeconds(10),
                AutomaticDecompression = DecompressionMethods.All,
            };
            /*if (Properties.DISCORD_FORCE_IPV4)   // new bool property, default true – fixes 10060 on hosts with half-broken IPv6
            {
                handler.ConnectCallback = async (ctx, ct) =>
                {
                    IPAddress[] addrs = await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, AddressFamily.InterNetwork, ct);
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try { await socket.ConnectAsync(addrs, ctx.DnsEndPoint.Port, ct); return new NetworkStream(socket, ownsSocket: true); }
                    catch { socket.Dispose(); throw; }
                };
            }*/
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GondwanaGameServer/1.0");
            return client;
        }

        private static async Task WorkerLoop()
        {
            await foreach (var (url, json) in s_queue.Reader.ReadAllAsync())
            {
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    var wait = s_nextAllowedUtc - DateTime.UtcNow;
                    if (wait > TimeSpan.Zero) await Task.Delay(wait);

                    try
                    {
                        using var content = new StringContent(json, Encoding.UTF8, "application/json");
                        using var resp = await s_http.PostAsync(url + "?wait=false", content);
                        s_nextAllowedUtc = DateTime.UtcNow.AddMilliseconds(400);        // ≈ safe sustained rate

                        if (resp.IsSuccessStatusCode) break;
                        if ((int)resp.StatusCode == 429 || (int)resp.StatusCode >= 500)
                        {
                            double retry = 2;
                            if (resp.Headers.TryGetValues("Retry-After", out var v) && double.TryParse(v.FirstOrDefault(), out var r)) retry = r;
                            s_nextAllowedUtc = DateTime.UtcNow.AddSeconds(retry + 0.25);
                            continue;
                        }
                        log.Warn($"Discord webhook rejected message: HTTP {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
                        break;
                    }
                    catch (Exception e)
                    {
                        log.Warn($"Discord webhook send failed (attempt {attempt + 1}): {e.Message}");
                        s_nextAllowedUtc = DateTime.UtcNow.AddSeconds(5 * (attempt + 1));
                    }
                }
            }
        }
    }
}

/*
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DOL.GS
{


    public class DolWebHook
    {
        private HttpClient Client;
        private string Url;

        public string Name { get; set; }


        public DolWebHook(string webhookUrl)
        {
            Client = new HttpClient();
            Url = webhookUrl;
        }

        public bool SendMessage(string content)
        {
            try
            {
                MultipartFormDataContent data = new MultipartFormDataContent();


                data.Add(new StringContent(content), "content");



                var resp = Client.PostAsync(Url, data).Result;

                return resp.StatusCode == HttpStatusCode.NoContent;
            }
            catch (AggregateException ex)
            {
                HttpRequestException httpEx = (HttpRequestException)ex.InnerExceptions.FirstOrDefault(e => e is HttpRequestException);
                if (httpEx != null)
                {
                    GameServer.Instance.Logger.Warn("Error while sending Discord webhook message:\n" + httpEx.Message);
                    return false;
                }
                GameServer.Instance.Logger.Error("Error while sending Discord webhook message:\n" + ex);
            }
            catch (Exception ex)
            {
                GameServer.Instance.Logger.Error("Error while sending Discord webhook message:\n" + ex);
            }
            return false;
        }
    }

}*/