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
using System.Threading;

namespace DOL.GS
{
    public class AutoTranslator
    {
        private readonly IDictionary<string, Task<string>> m_translations;

        public string OriginalText
        {
            get;
            init;
        }

        public string OriginalLanguage
        {
            get;
            init;
        }

        public AutoTranslator(string lang, string text, bool threadSafe = true)
        {
            OriginalText = text;
            OriginalLanguage = LanguageMgr.NormalizeLang(lang);
            var initialValue = new KeyValuePair<string, Task<string>>(OriginalLanguage, Task.FromResult(OriginalText));
            m_translations = threadSafe ? new ConcurrentDictionary<string, Task<string>>([initialValue]) : new Dictionary<string, Task<string>>([initialValue]);
        }

        public AutoTranslator(string text, bool threadSafe = true) : this(lang: LanguageMgr.DefaultLanguage, text: text, threadSafe)
        {
        }

        public AutoTranslator([NotNull] GamePlayer sender, string text, bool threadSafe = true) : this(lang: sender.Client?.Account?.Language, text, threadSafe)
        {
        }

        private async Task<string> TranslateImpl(string langTo)
        {
            langTo = LanguageMgr.NormalizeLang(langTo);
            if (!m_translations.TryGetValue(langTo, out Task<string> task))
            {
                task = AutoTranslateManager.Translate(OriginalLanguage, langTo, OriginalText);
                m_translations[langTo] = task;
            }
            return await task.ConfigureAwait(false);
        }

        public async Task<string> Translate(string langTo)
        {
            if (!Properties.AUTOTRANSLATE_ENABLE)
                return OriginalText;

            if (string.IsNullOrWhiteSpace(OriginalText))
                return string.Empty;

            return await TranslateImpl(langTo).ConfigureAwait(false);
        }

        public async Task<string> Translate([NotNull] GamePlayer receiver)
        {
            if (!Properties.AUTOTRANSLATE_ENABLE)
                return OriginalText;

            if (string.IsNullOrWhiteSpace(OriginalText))
                return string.Empty;

            if (!receiver.AutoTranslateEnabled)
                return OriginalText;

            var lang = receiver.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;
            return await TranslateImpl(lang).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves auto-translation asynchronously. 
        /// Uses Cache -> PendingTasks -> Google API.
        /// </summary>
        public IEnumerable<Task<KeyValuePair<GamePlayer, string>>> Translate(IEnumerable<GamePlayer> receivers)
        {
            if (!Properties.AUTOTRANSLATE_ENABLE)
                return receivers.Select((p) => Task.FromResult(new KeyValuePair<GamePlayer, string>(p, OriginalText)));

            if (string.IsNullOrWhiteSpace(OriginalText))
                return receivers.Select((p) => Task.FromResult(new KeyValuePair<GamePlayer, string>(p, string.Empty)));

            return receivers.Select(async p =>
            {
                string ret = OriginalText;
                if (p.AutoTranslateEnabled)
                {
                    var toLang = LanguageMgr.NormalizeLang(p.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage);
                    ret = await TranslateImpl(toLang);
                }
                return new KeyValuePair<GamePlayer, string>(p, ret);
            });
        }
    }

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
        public static async Task<string> Translate(GamePlayer sender, [NotNull] GamePlayer receiver, string originalText)
        {
            if (!Properties.AUTOTRANSLATE_ENABLE || string.IsNullOrWhiteSpace(originalText))
                return originalText;

            if (sender is null || !receiver.AutoTranslateEnabled)
                return originalText;

            var toLang = receiver.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;
            var fromLang = sender?.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;

            return await TranslateCoreAsync(fromLang, toLang, originalText);
        }

        /// <summary>
        /// Retrieves auto-translation asynchronously. 
        /// Uses Cache -> PendingTasks -> Google API.
        /// </summary>
        public static async Task<string> Translate(string fromLang, string toLang, string originalText)
        {
            if (!Properties.AUTOTRANSLATE_ENABLE || string.IsNullOrWhiteSpace(originalText))
                return originalText;
            
            return await TranslateCoreAsync(fromLang, toLang, originalText);
        }

        /// <summary>
        /// Retrieves auto-translation asynchronously. 
        /// Uses Cache -> PendingTasks -> Google API.
        /// </summary>
        public static async Task<string> Translate([NotNull] GamePlayer receiver, string originalText)
        {
            if (!Properties.AUTOTRANSLATE_ENABLE || string.IsNullOrWhiteSpace(originalText))
                return originalText;

            if (!receiver.AutoTranslateEnabled)
                return originalText;

            var toLang = receiver.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;
            var fromLang = LanguageMgr.DefaultLanguage;

            return await TranslateCoreAsync(fromLang, toLang, originalText);
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
            if (string.IsNullOrWhiteSpace(originalText)) return string.Empty;

            // Normalize
            fromLang = LanguageMgr.NormalizeLang(fromLang);
            toLang = LanguageMgr.NormalizeLang(toLang);

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
    }
}