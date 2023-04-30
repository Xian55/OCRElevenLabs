using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

using Protonox.Labs.Api;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;
using System;
using Protonox.Web;
using Protonox.CommandLine;
using Microsoft.Extensions.Logging;

namespace Protonox
{
    public sealed class ElevenLabs
    {
        private const string BASE_URL = "https://api.elevenlabs.io";
        private const string VERSION = "v1";
        public const string EXT = ".mp3";

        private readonly string output = "output";

        public string Output => Path.Join(Directory.GetCurrentDirectory(), output);

        private readonly ILogger logger;
        private readonly HttpClient client;

        public Dictionary<string, Voices> Voices { private set; get; } = new();

        public ElevenLabs(ILogger logger, ElevenLabsCLIArgs args)
        {
            this.logger = logger;

            int keyHash = args.ApiKey.DetHashGen();

            Uri proxy = null;
            if (args.Proxy != null)
            {
                proxy = args.Proxy == "rand"
                    ? Proxys.Get(keyHash)
                    : new Uri(args.Proxy);

                logger.LogInformation($"[{nameof(ElevenLabs)}] Proxy: {proxy}");
            }

            var webProxy = new WebProxy
            {
                Address = proxy,
                BypassProxyOnLocal = false,
                UseDefaultCredentials = false,
            };

            var httpClientHandler = new HttpClientHandler
            {
                Proxy = webProxy,
            };

            client = new(handler: httpClientHandler, disposeHandler: true);
            client.DefaultRequestHeaders.Add("xi-api-key", args.ApiKey);
            client.DefaultRequestHeaders.Add("accept", "application/json");
            client.DefaultRequestHeaders.Add("accept", "audio/mpeg");

            if (args.UserAgent != null)
            {
                if (args.UserAgent == "rand")
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgents.Get(keyHash));
                else
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(args.UserAgent);

                logger.LogInformation($"[{nameof(ElevenLabs)}] UserAgent: {client.DefaultRequestHeaders.UserAgent}");
            }

            if (!Directory.Exists(Output))
            {
                logger.LogInformation($"[{nameof(ElevenLabs)}]: {Output} folder created!");
                Directory.CreateDirectory(Output);
            }
        }

        public async Task<Voices[]> GetInitVoices()
        {
            Voices.Clear();

            try
            {
                Voices[] array = await GetVoices();

                foreach (Voices item in array)
                    Voices.Add(item.name, item);

                logger.LogInformation($"[{nameof(ElevenLabs)}] {nameof(GetInitVoices)}: {string.Join(',', Voices.Keys)}");

                return array;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public async Task<Voices[]> GetVoices()
        {
            try
            {
                using var response = await client.GetAsync($"{BASE_URL}/{VERSION}/voices",
                HttpCompletionOption.ResponseContentRead);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<VoicesArray>(json).voices;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public async Task<Voices> GetVoice(string voice_id)
        {
            try
            {
                using var response = await client.GetAsync($"{BASE_URL}/{VERSION}/voices/{voice_id}",
                HttpCompletionOption.ResponseContentRead);

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                return JsonSerializer.Deserialize<Voices>(json);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public async Task<bool> PostTTS(string voice_id, string text, VoiceSettings settings, int hash)
        {
            var body = new TTS_Body
            {
                text = text,
                voice_settings = settings
            };

            string json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync($"{BASE_URL}/{VERSION}/text-to-speech/{voice_id}", content);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(response.ReasonPhrase);
                return false;
            }

            string absPath = Path.Combine(Output, hash.ToString() + EXT);
            using Stream stream = File.Open(absPath, FileMode.Create);
            await response.Content.ReadAsStream().CopyToAsync(stream);

            logger.LogInformation($"[{nameof(ElevenLabs)}] Saved `{text}` as `{absPath}`");

            return true;
        }
    }
}
