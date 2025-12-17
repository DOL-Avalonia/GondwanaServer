using DOL.GS.ServerProperties;
using DOL.Language;
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

        private static volatile bool _temporarilyDisabled = false;
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

        /// <summary>
        /// Asynchronous translation. Returns original text on failure/disabled.
        /// </summary>
        public static async Task<string> TranslateAsync(string text, string fromLanguage, string toLanguage)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            if (!Properties.AUTOTRANSLATE_ENABLE) return text;

            // Circuit Breaker
            if (_temporarilyDisabled)
            {
                if (DateTime.UtcNow < _disabledUntilUtc) return text;
                _temporarilyDisabled = false; // Retry time reached
            }

            var apiKey = Properties.AUTOTRANSLATE_GOOGLE_API_KEY;
            if (string.IsNullOrWhiteSpace(apiKey)) return text;

            string source = NormalizeLanguageCode(fromLanguage);
            string target = NormalizeLanguageCode(toLanguage);

            if (source.Equals(target, StringComparison.OrdinalIgnoreCase)) return text;

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

            try
            {
                using (var content = new FormUrlEncodedContent(contentValues))
                {
                    // This is the magic line. It awaits the network call without blocking the Server Thread.
                    var response = await _httpClient.PostAsync(url, content).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        TripOfflineBreaker($"HTTP {response.StatusCode}");
                        return text;
                    }

                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(GoogleTranslateResponse));
                        var resultObj = serializer.ReadObject(stream) as GoogleTranslateResponse;

                        if (resultObj?.Data?.Translations is { Count: > 0 })
                        {
                            string result = resultObj.Data.Translations[0].TranslatedText;
                            return WebUtility.HtmlDecode(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warn($"ExternalTranslator Error: {ex.Message}");
                TripOfflineBreaker(ex.Message);
            }

            return text;
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
            _temporarilyDisabled = true;
            _disabledUntilUtc = DateTime.UtcNow.AddMilliseconds(OfflineBackoffMs);
            if (log.IsWarnEnabled) 
                log.Warn($"ExternalTranslator disabled for {OfflineBackoffMs}ms. Reason: {reason}");
        }
    }
}