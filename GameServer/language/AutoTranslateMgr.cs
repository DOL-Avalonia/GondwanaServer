using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using log4net;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS
{
    /// <summary>
    /// Central place to handle auto-translation of player chat and server texts (quests, NPC dialog, etc.).
    ///
    /// IMPORTANT:
    /// - This is often called from the Region TimeManager thread (RegionTime1), e.g. when opening quest windows.
    /// - Any slow work here will block the whole region.
    /// - To avoid that, we cache translations in memory so that each unique text+language pair
    ///   is translated at most once, subsequent calls are basically free.
    /// </summary>
    public static class AutoTranslateManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AutoTranslateManager));

        /// <summary>
        /// In-memory cache for translations.
        /// Key = fromLang + "|" + toLang + "|" + originalText
        /// Value = translated text
        /// </summary>
        private static readonly ConcurrentDictionary<string, string> _cache =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        private const int MaxTextLength = 2000;
        private const int MaxCacheEntries = 20000;

        /// <summary>
        /// Returns translated text for receiver if their auto-translate is enabled.
        /// If disabled or translation fails, originalText is returned.
        ///
        /// This version is meant for PLAYER chat (sender != null).
        /// For NPC / quest / server texts, use MaybeTranslateServerText.
        /// </summary>
        public static string MaybeTranslate(GamePlayer sender, GamePlayer receiver, string originalText)
        {
            if (receiver == null || string.IsNullOrWhiteSpace(originalText))
                return originalText;

            if (!receiver.AutoTranslateEnabled)
                return originalText;

            if (!Properties.AUTOTRANSLATE_ENABLE)
                return originalText;

            var toLang = receiver.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;
            var fromLang = sender?.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;

            return TranslateWithCache(fromLang, toLang, originalText);
        }

        /// <summary>
        /// Translate a server-originated text (e.g. NPC dialog, quest story/summary/steps)
        /// from the server base language into the player's account language,
        /// honoring AutoTranslateEnabled and the global AUTOTRANSLATE_ENABLE switch.
        /// </summary>
        public static string MaybeTranslateServerText(GamePlayer receiver, string originalText)
        {
            if (receiver == null || string.IsNullOrWhiteSpace(originalText))
                return originalText;

            if (!receiver.AutoTranslateEnabled)
                return originalText;

            if (!Properties.AUTOTRANSLATE_ENABLE)
                return originalText;

            var toLang = receiver.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;
            var fromLang = LanguageMgr.DefaultLanguage; // your base language (FR in your case)

            return TranslateWithCache(fromLang, toLang, originalText);
        }

        /// <summary>
        /// Shared core logic: normalize languages, use cache, and only call the slow
        /// ExternalTranslator when absolutely necessary.
        /// </summary>
        private static string TranslateWithCache(string fromLang, string toLang, string originalText)
        {
            if (string.IsNullOrWhiteSpace(originalText))
                return originalText;

            fromLang = NormalizeLang(fromLang);
            toLang = NormalizeLang(toLang);

            if (string.Equals(fromLang, toLang, StringComparison.OrdinalIgnoreCase))
                return originalText;

            string text = originalText;
            if (text.Length > MaxTextLength)
            {
                if (log.IsWarnEnabled)
                    log.Warn($"AutoTranslate: text too long ({text.Length}), truncating to {MaxTextLength} chars.");

                text = text.Substring(0, MaxTextLength);
            }

            string key = $"{fromLang}|{toLang}|{text}";

            if (_cache.TryGetValue(key, out var cached))
                return cached;

            string translated = originalText;

            try
            {
                var sw = Stopwatch.StartNew();

                translated = ExternalTranslator.Translate(text, fromLang, toLang);

                sw.Stop();

                if (sw.ElapsedMilliseconds > 500 && log.IsWarnEnabled)
                {
                    log.Warn($"AutoTranslate: ExternalTranslator.Translate took {sw.ElapsedMilliseconds}ms " +
                             $"({fromLang}->{toLang}, {text.Length} chars).");
                }
            }
            catch (Exception ex)
            {
                if (log.IsWarnEnabled)
                    log.Warn($"AutoTranslate: Exception during translation ({fromLang}->{toLang}): {ex.Message}", ex);

                return originalText;
            }

            if (string.IsNullOrWhiteSpace(translated))
                return originalText;

            if (_cache.Count > MaxCacheEntries)
            {
                if (log.IsWarnEnabled)
                    log.Warn($"AutoTranslate: cache size {_cache.Count} exceeded {MaxCacheEntries}, clearing cache.");
                _cache.Clear();
            }

            _cache[key] = translated;
            return translated;
        }

        /// <summary>
        /// Very small normalization to avoid cache misses like "EN" vs "en-US".
        /// This does NOT replace the proper normalization done inside ExternalTranslator.
        /// It just keeps keys more consistent.
        /// </summary>
        private static string NormalizeLang(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
                return LanguageMgr.DefaultLanguage ?? "EN";

            lang = lang.Trim();

            int sepIndex = lang.IndexOfAny(new[] { '-', '_' });
            if (sepIndex > 0)
                lang = lang.Substring(0, sepIndex);

            return lang.ToUpperInvariant();
        }
    }
}
