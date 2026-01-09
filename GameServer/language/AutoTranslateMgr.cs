using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using log4net;
using DOL.GS.ServerProperties;
using DOL.Language;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DOL.GS
{
    public static class AutoTranslateManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AutoTranslateManager));

        private class CacheKey(string From, string To, string Text)
        {
            public string From { get; } = From;
            
            public string To { get; } = To;
            
            public string Text { get; } = Text;

            /// <inheritdoc />
            public override bool Equals(object obj)
            {
                if (obj is CacheKey other)
                {
                    if (!string.Equals(From, other.From, StringComparison.OrdinalIgnoreCase))
                        return false;

                    if (!string.Equals(To, other.To, StringComparison.OrdinalIgnoreCase))
                        return false;

                    if (!string.Equals(Text, other.Text, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                return base.Equals(obj);
            }

            /// <inheritdoc />
            public override int GetHashCode()
            {
                var a = string.GetHashCode(From, StringComparison.OrdinalIgnoreCase);
                var b = string.GetHashCode(To, StringComparison.OrdinalIgnoreCase);
                var c = string.GetHashCode(Text, StringComparison.OrdinalIgnoreCase);
                return HashCode.Combine(a, b, c);
            }
        }

        // Cache: Key -> Translated Text
        private static readonly ConcurrentDictionary<CacheKey, string> _cache = new();

        // Pending Tasks: Key -> Running Task. Prevents duplicate API calls for identical requests.
        private static readonly ConcurrentDictionary<CacheKey, Task<string>> _pendingTranslations = new();

        private const int MaxTextLength = 2000;
        private const int MaxCacheEntries = 20000;

        /// <summary>
        /// Retrieves auto-translation asynchronously. 
        /// Uses Cache -> PendingTasks -> Google API.
        /// </summary>
        public static async Task<string> Translate(GamePlayer? sender, [NotNull] GamePlayer receiver, string originalText)
        {
            if (!Properties.AUTOTRANSLATE_ENABLE || string.IsNullOrWhiteSpace(originalText))
                return originalText;

            if (!receiver.AutoTranslateEnabled)
                return originalText;

            var toLang = receiver.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;
            var fromLang = sender?.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;

            return await TranslateCoreAsync(fromLang, toLang, originalText);
        }

        /// <summary>
        /// Retrieves auto-translation asynchronously. 
        /// Uses Cache -> PendingTasks -> Google API.
        /// </summary>
        public static async Task<IEnumerable<KeyValuePair<GamePlayer, string>>> Translate(string fromLang, IEnumerable<GamePlayer> receivers, string originalText)
        {
            if (!Properties.AUTOTRANSLATE_ENABLE || string.IsNullOrWhiteSpace(originalText))
                return receivers.Select(p => new KeyValuePair<GamePlayer, string>(p, originalText));

            var translations = new Dictionary<string, Task<string>>();
            async Task<KeyValuePair<GamePlayer, string>> Each(GamePlayer receiver)
            {
                string ret = originalText;
                if (receiver.AutoTranslateEnabled)
                {
                    var toLang = NormalizeLang(receiver.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage);
                    if (!translations.TryGetValue(toLang, out var task))
                    {
                        task = TranslateCoreAsync(fromLang, toLang, originalText);
                        translations[toLang] = task;
                    }
                    ret = await task.ConfigureAwait(false);
                }
                return new(receiver, ret);
            }

            return await Task.WhenAll(receivers.Select(Each)).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves auto-translation asynchronously. 
        /// Uses Cache -> PendingTasks -> Google API.
        /// </summary>
        public static async Task<IEnumerable<KeyValuePair<GamePlayer, string>>> Translate(GamePlayer sender, IEnumerable<GamePlayer> receivers, string originalText)
        {
            var fromLang = sender?.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;
            return await Translate(fromLang, receivers, originalText).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves auto-translation asynchronously. 
        /// Uses Cache -> PendingTasks -> Google API.
        /// </summary>
        public static async Task<IEnumerable<KeyValuePair<GamePlayer, string>>> Translate(IEnumerable<GamePlayer> receivers, string originalText)
        {
            var fromLang = LanguageMgr.DefaultLanguage;
            return await Translate(fromLang, receivers, originalText).ConfigureAwait(false);
        }

        public static string MaybeTranslate(GamePlayer sender, GamePlayer receiver, string msg)
        {
            return Translate(sender, receiver, msg).Result;
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
            var key = new CacheKey(fromLang, toLang, originalText);

            // 2. Check Cache
            if (_cache.TryGetValue(key, out var cached))
                return cached;

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
                if (!string.IsNullOrWhiteSpace(translated))
                {
                    if (_cache.Count > MaxCacheEntries)
                        _cache.Clear(); // TODO: How does this even make sense?
                    _cache[key] = translated;
                }
                else if (translated == null)
                {
                    translated = originalText;
                }

                return translated;
            }
            finally
            {
                // Always remove from pending list when done
                _pendingTranslations.TryRemove(key, out _);
            }
        }

        private static string NormalizeLang(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                lang = LanguageMgr.DefaultLanguage;
                if (string.IsNullOrEmpty(lang))
                    return "EN";
            }
            int sep = lang.IndexOfAny(new[] { '-', '_' });
            if (sep > 0)
                lang = lang.Substring(0, sep);
            return lang.ToUpperInvariant();
        }
    }
}