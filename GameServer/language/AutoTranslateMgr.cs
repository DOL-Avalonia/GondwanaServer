using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using log4net;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS
{
    public static class AutoTranslateManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AutoTranslateManager));

        // Cache: Key -> Translated Text
        private static readonly ConcurrentDictionary<string, string> _cache =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        // Pending Tasks: Key -> Running Task. Prevents duplicate API calls for identical requests.
        private static readonly ConcurrentDictionary<string, Task<string>> _pendingTranslations = 
            new ConcurrentDictionary<string, Task<string>>(StringComparer.Ordinal);

        private const int MaxTextLength = 2000;
        private const int MaxCacheEntries = 20000;

        /// <summary>
        /// Retrieves translation asynchronously. 
        /// Uses Cache -> PendingTasks -> Google API.
        /// </summary>
        public static async Task<string> TranslateAsync(GamePlayer sender, GamePlayer receiver, string originalText)
        {
            if (receiver == null || !receiver.AutoTranslateEnabled || string.IsNullOrWhiteSpace(originalText))
                return originalText;

            if (!Properties.AUTOTRANSLATE_ENABLE)
                return originalText;

            var toLang = receiver.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;
            var fromLang = sender?.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;

            return await TranslateCoreAsync(fromLang, toLang, originalText);
        }

        /// <summary>
        /// Core logic. Call this directly if you have language codes but no Player objects.
        /// </summary>
        public static async Task<string> TranslateCoreAsync(string fromLang, string toLang, string originalText)
        {
            if (string.IsNullOrWhiteSpace(originalText)) return originalText;

            // Normalize
            fromLang = NormalizeLang(fromLang);
            toLang = NormalizeLang(toLang);

            if (fromLang == toLang) return originalText;

            // 1. Build Key
            string key = BuildKey(fromLang, toLang, originalText);

            // 2. Check Cache
            if (_cache.TryGetValue(key, out var cached)) return cached;

            // 3. Check/Create Pending Task (The magic deduplication logic)
            // GetOrAdd ensures we only create ONE task for this key, even if called 100 times concurrently
            var task = _pendingTranslations.GetOrAdd(key, (k) => 
                ExternalTranslator.TranslateAsync(originalText, fromLang, toLang)
            );

            try
            {
                // Wait for the task to finish (non-blocking wait)
                string translated = await task;

                // 4. Update Cache if successful
                if (!string.IsNullOrWhiteSpace(translated) && translated != originalText)
                {
                    if (_cache.Count > MaxCacheEntries) _cache.Clear();
                    _cache[key] = translated;
                }
                
                return translated;
            }
            finally
            {
                // Always remove from pending list when done
                _pendingTranslations.TryRemove(key, out _);
            }
        }

        private static string BuildKey(string from, string to, string text)
        {
            if (text.Length > MaxTextLength) text = text.Substring(0, MaxTextLength);
            return $"{from}|{to}|{text}";
        }

        private static string NormalizeLang(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return "EN";
            int sep = lang.IndexOfAny(new[] { '-', '_' });
            if (sep > 0) lang = lang.Substring(0, sep);
            return lang.ToUpperInvariant();
        }
    }
}