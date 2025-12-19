using DOL.GS.ServerProperties;
using DOL.Language;
using ICSharpCode.SharpZipLib.Zip;
using log4net;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;

namespace DOL.GS
{
    public static class ExternalTranslator
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ExternalTranslator));
        
        // Reusable HttpClient (Best Practice)
        private static readonly HttpClient _httpClient = new HttpClient();

        private static DateTime _disabledUntilUtc = DateTime.MinValue;
        private const int OfflineBackoffMs = 30_000; // 30 seconds backoff on error

        #region Google DTOs
        [DataContract]
        private class GoogleTranslateResponse
        {
            [DataMember(Name = "data")]
            public GoogleTranslateData Data { get; set; }
        }

        [DataContract]
        private class GoogleTranslateData
        {
            [DataMember(Name = "translations")]
            public List<GoogleTranslation> Translations { get; set; }
        }

        [DataContract]
        private class GoogleTranslation
        {
            [DataMember(Name = "translatedText")]
            public string TranslatedText { get; set; }
        }
        #endregion
        private static HttpRequestMessage? CreateRequest(string text, string fromLanguage, string toLanguage)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Global off switch.
            if (!Properties.AUTOTRANSLATE_ENABLE)
                return null;

            var provider = (Properties.AUTOTRANSLATE_PROVIDER ?? "google").Trim().ToLowerInvariant();
            if (provider != "google")
                return null;

            var apiKey = Properties.AUTOTRANSLATE_GOOGLE_API_KEY;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                if (log.IsDebugEnabled)
                    log.Debug("ExternalTranslator: Google API key is empty, returning original text.");
                return null;
            }

            // If we are currently in "offline" backoff mode, don't even try to talk to Google.
            var now = DateTime.UtcNow;
            if (now < _disabledUntilUtc)
            {
                if (log.IsDebugEnabled)
                    log.Debug($"ExternalTranslator: temporarily disabled until {_disabledUntilUtc:o}, returning original text.");
                return null;
            }

            string source = NormalizeLanguageCode(fromLanguage);
            string target = NormalizeLanguageCode(toLanguage);

            // If codes are identical after normalization, no need to call API.
            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                return null;

            var endpoint = Properties.AUTOTRANSLATE_GOOGLE_ENDPOINT;
            if (string.IsNullOrWhiteSpace(endpoint))
                endpoint = "https://translation.googleapis.com/language/translate/v2";

            // Prepare URL and content
            string url = $"{endpoint}?key={Uri.EscapeDataString(apiKey)}";
            List<KeyValuePair<string, string>> contentValues =
            [
                new("q", text),
                new("source", source),
                new("target", target),
                new("format", "text"),
            ];

            var req = new HttpRequestMessage(HttpMethod.Post, url);
            var content = new FormUrlEncodedContent(contentValues);
            req.Content = content;
            return req;
        }

        /// <summary>
        /// Asynchronous translation. Returns original text on failure/disabled.
        /// </summary>
        public static async Task<string> TranslateAsync(string text, string fromLanguage, string toLanguage)
        {
            try
            {
                using var request = CreateRequest(text, fromLanguage, toLanguage);
                if (request is null)
                    return null;

                // This is the magic line. It awaits the network call without blocking the Server Thread.
                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    TripOfflineBreaker($"HTTP {response.StatusCode}");
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var serializer = new DataContractJsonSerializer(typeof(GoogleTranslateResponse));
                var resultObj = serializer.ReadObject(stream) as GoogleTranslateResponse;

                if (resultObj?.Data?.Translations is { Count: > 0 })
                {
                    string result = resultObj.Data.Translations[0].TranslatedText;
                    return WebUtility.HtmlDecode(result);
                }
            }
            catch (Exception ex)
            {
                log.Warn($"ExternalTranslator Error: {ex.Message}");
                TripOfflineBreaker(ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Asynchronous translation. Returns original text on failure/disabled.
        /// </summary>
        public static string TranslateSync(string text, string fromLanguage, string toLanguage)
        {
            try
            {
                using var request = CreateRequest(text, fromLanguage, toLanguage);
                if (request is null)
                    return null;

                // This is the magic line. It awaits the network call without blocking the Server Thread.
                var response = _httpClient.Send(request);

                if (!response.IsSuccessStatusCode)
                {
                    TripOfflineBreaker($"HTTP {response.StatusCode}");
                    return null;
                }

                using var stream = response.Content.ReadAsStream();
                var serializer = new DataContractJsonSerializer(typeof(GoogleTranslateResponse));
                var resultObj = serializer.ReadObject(stream) as GoogleTranslateResponse;

                if (resultObj?.Data?.Translations is { Count: > 0 })
                {
                    string result = resultObj.Data.Translations[0].TranslatedText;
                    return WebUtility.HtmlDecode(result);
                }
            }
            catch (Exception ex)
            {
                log.Warn($"ExternalTranslator Error: {ex.Message}");
                TripOfflineBreaker(ex.Message);
            }

            return null;
        }

        private static string NormalizeLanguageCode(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                lang = LanguageMgr.DefaultLanguage;
                if (string.IsNullOrEmpty(lang))
                    return "en";
            }
            lang = lang.Trim().ToLowerInvariant();
            int sepIndex = lang.IndexOfAny(['-', '_']);
            if (sepIndex > 0) lang = lang.Substring(0, sepIndex);

            if (lang == "eng") return "en";
            if (lang is "fre" or "fra") return "fr";
            if (lang is "ger" or "deu") return "de";

            return lang;
        }

        private static void TripOfflineBreaker(string reason)
        {
            _disabledUntilUtc = DateTime.UtcNow.AddMilliseconds(OfflineBackoffMs);
            if (log.IsWarnEnabled) 
                log.Warn($"ExternalTranslator disabled for {OfflineBackoffMs}ms. Reason: {reason}");
        }
    }
}