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
    /// </summary>
    public static class ExternalTranslator
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ExternalTranslator));

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
        /// Main public entry. Synchronous, as chat send is synchronous.
        /// Returns original text if:
        ///  - feature disabled,
        ///  - provider not 'google',
        ///  - missing/invalid API key,
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

            string source = NormalizeLanguageCode(fromLanguage);
            string target = NormalizeLanguageCode(toLanguage);

            // If codes are identical after normalization, no need to call API.
            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                return text;

            try
            {
                return GoogleTranslateSync(endpoint, apiKey, text, source, target) ?? text;
            }
            catch (Exception ex)
            {
                if (log.IsWarnEnabled)
                    log.Warn($"ExternalTranslator: GoogleTranslateSync failed ({source}->{target}): {ex.Message}", ex);

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

                return null;
            }
            catch (Exception ex)
            {
                if (log.IsWarnEnabled)
                    log.Warn($"ExternalTranslator: Exception ({source}->{target}): {ex.Message}", ex);

                return null;
            }
        }
    }
}