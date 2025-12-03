using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using DOL.GS.ServerProperties;
using log4net;

namespace DOL.GS
{
    /// <summary>
    /// Place where you plug your real external translation provider (Google, DeepL, etc.).
    /// This implementation uses Google Cloud Translation API v2.
    /// 
    /// IMPORTANT:
    /// - This is often called on the RegionTime thread (RegionTime1) via AutoTranslateManager.
    /// - Any slow network I/O here will freeze the region.
    /// - We therefore use:
    ///     * short HTTP timeouts
    ///     * a circuit-breaker (temporary disable) when Google is unreachable
    /// </summary>
    public static class ExternalTranslator
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ExternalTranslator));

        /// <summary>
        /// If true, we temporarily skip all calls to Google and just return original text.
        /// </summary>
        private static volatile bool _temporarilyDisabled = false;

        /// <summary>
        /// When _temporarilyDisabled is true, we skip Google calls until this UTC time.
        /// </summary>
        private static DateTime _disabledUntilUtc = DateTime.MinValue;

        /// <summary>
        /// How long we stay in "offline/backoff" mode after a failure, in milliseconds.
        /// </summary>
        private const int OfflineBackoffMs = 15_000;

        /// <summary>
        /// Max time we allow for an HTTP request (connect + send + receive), in milliseconds.
        /// Keep it small so RegionTime doesn't freeze.
        /// </summary>
        private const int HttpTimeoutMs = 2_000;

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

            [DataMember(Name = "detectedSourceLanguage")]
            public string DetectedSourceLanguage { get; set; }
        }

        #endregion

        /// <summary>
        /// Main public entry. Synchronous.
        /// Returns original text if:
        ///  - feature disabled,
        ///  - provider not 'google',
        ///  - missing/invalid API key,
        ///  - circuit breaker currently in offline mode,
        ///  - any error during HTTP or JSON.
        /// </summary>
        public static string Translate(string text, string fromLanguage, string toLanguage)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Global off switch.
            if (!Properties.AUTOTRANSLATE_ENABLE)
                return text;

            var provider = (Properties.AUTOTRANSLATE_PROVIDER ?? "google").Trim().ToLowerInvariant();
            if (provider != "google")
                return text;

            var apiKey = Properties.AUTOTRANSLATE_GOOGLE_API_KEY;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                if (log.IsDebugEnabled)
                    log.Debug("ExternalTranslator: Google API key is empty, returning original text.");
                return text;
            }

            var endpoint = Properties.AUTOTRANSLATE_GOOGLE_ENDPOINT;
            if (string.IsNullOrWhiteSpace(endpoint))
                endpoint = "https://translation.googleapis.com/language/translate/v2";

            // If we are currently in "offline" backoff mode, don't even try to talk to Google.
            var now = DateTime.UtcNow;
            if (_temporarilyDisabled && now < _disabledUntilUtc)
            {
                if (log.IsDebugEnabled)
                    log.Debug($"ExternalTranslator: temporarily disabled until {_disabledUntilUtc:o}, returning original text.");
                return text;
            }

            string source = NormalizeLanguageCode(fromLanguage);
            string target = NormalizeLanguageCode(toLanguage);

            // If codes are identical after normalization, no need to call API.
            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                return text;

            try
            {
                var result = GoogleTranslateSync(endpoint, apiKey, text, source, target);

                // If null → we had some problem, keep original text.
                if (string.IsNullOrWhiteSpace(result))
                    return text;

                // Successful call: re-enable if we were previously disabled.
                _temporarilyDisabled = false;
                _disabledUntilUtc = DateTime.MinValue;

                return result;
            }
            catch (Exception ex)
            {
                TripOfflineBreaker(ex.Message);

                if (log.IsWarnEnabled)
                    log.Warn($"ExternalTranslator: Exception in Translate ({source}->{target}): {ex.Message}", ex);

                return text;
            }
        }

        /// <summary>
        /// Turn DB / account language codes ('EN', 'FR', 'DE', 'en-US', etc.)
        /// into Google-style 'en', 'fr', 'de', ...
        /// </summary>
        private static string NormalizeLanguageCode(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
                return "en";

            lang = lang.Trim();

            int separatorIndex = lang.IndexOfAny(new[] { '-', '_' });
            if (separatorIndex > 0)
                lang = lang.Substring(0, separatorIndex);

            // Google expects lowercase ISO 639-1 codes.
            lang = lang.ToLowerInvariant();

            if (lang == "eng")
                lang = "en";
            if (lang == "fre" || lang == "fra")
                lang = "fr";
            if (lang == "ger" || lang == "deu")
                lang = "de";

            return lang;
        }

        /// <summary>
        /// Synchronous call to Google Translate v2 REST API.
        /// </summary>
        private static string GoogleTranslateSync(string endpoint, string apiKey, string text, string source, string target)
        {
            string url = endpoint;
            if (!url.Contains("?"))
                url += "?key=" + Uri.EscapeDataString(apiKey);
            else
                url += "&key=" + Uri.EscapeDataString(apiKey);

            string postData =
                "q=" + Uri.EscapeDataString(text) +
                "&source=" + Uri.EscapeDataString(source) +
                "&target=" + Uri.EscapeDataString(target) +
                "&format=text";

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded; charset=utf-8";

                // SHORT TIMEOUTS so RegionTime1 doesn't freeze forever.
                request.Timeout = HttpTimeoutMs;
                request.ReadWriteTimeout = HttpTimeoutMs;

                byte[] dataBytes = Encoding.UTF8.GetBytes(postData);
                request.ContentLength = dataBytes.Length;

                using (var requestStream = request.GetRequestStream())
                {
                    requestStream.Write(dataBytes, 0, dataBytes.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        if (log.IsWarnEnabled)
                            log.Warn($"ExternalTranslator: Google returned status {response.StatusCode}");
                        return null;
                    }

                    using (var respStream = response.GetResponseStream())
                    {
                        if (respStream == null)
                            return null;

                        var serializer = new DataContractJsonSerializer(typeof(GoogleTranslateResponse));
                        var resultObj = serializer.ReadObject(respStream) as GoogleTranslateResponse;

                        string result = null;

                        if (resultObj?.Data?.Translations != null &&
                            resultObj.Data.Translations.Count > 0)
                        {
                            result = resultObj.Data.Translations[0].TranslatedText;
                        }

                        if (!string.IsNullOrEmpty(result))
                            result = WebUtility.HtmlDecode(result);

                        return result;
                    }
                }
            }
            catch (WebException webEx)
            {
                string body = null;

                if (webEx.Response != null)
                {
                    using (var stream = webEx.Response.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        body = reader.ReadToEnd();
                    }
                }

                if (log.IsWarnEnabled)
                    log.Warn($"ExternalTranslator: WebException ({source}->{target}): {webEx.Message}. Body: {body}");

                TripOfflineBreaker(webEx.Message);

                return null;
            }
            catch (Exception ex)
            {
                if (log.IsWarnEnabled)
                    log.Warn($"ExternalTranslator: Exception ({source}->{target}): {ex.Message}", ex);

                TripOfflineBreaker(ex.Message);

                return null;
            }
        }

        /// <summary>
        /// Put the translator into "offline/backoff" mode for some time.
        /// During that time, Translate() will immediately return original text,
        /// so NPC JSON quests behave exactly as if AutoTranslate was disabled.
        /// </summary>
        private static void TripOfflineBreaker(string reason)
        {
            _temporarilyDisabled = true;
            _disabledUntilUtc = DateTime.UtcNow.AddMilliseconds(OfflineBackoffMs);

            if (log.IsWarnEnabled)
                log.Warn($"ExternalTranslator: entering offline/backoff mode for {OfflineBackoffMs}ms. Reason: {reason}");
        }
    }
}
