using DOL.GS.ServerProperties;
using DOL.Language;
using log4net;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DOL.GS
{
    public static partial class AutoTranslateManager
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

                    return true;
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

            var toLang = receiver.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;
            var fromLang = sender?.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;

            return await TranslateCoreAsync(fromLang, toLang, originalText);
        }

        public static async Task<string> MaybeTranslate(GamePlayer? sender, [NotNull] GamePlayer receiver, string originalText)
        {
            if (!receiver.AutoTranslateEnabled)
                return originalText;
            
            return await Translate(sender, receiver, originalText);
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

            var toLang = receiver.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;
            var fromLang = LanguageMgr.DefaultLanguage;

            return await TranslateCoreAsync(fromLang, toLang, originalText);
        }

        public static async Task<string> MaybeTranslate([NotNull] GamePlayer receiver, string originalText)
        {
            if (!receiver.AutoTranslateEnabled)
                return originalText;
            
            return await Translate(receiver, originalText);
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

        [GeneratedRegex(@"\[(.+?)\]")]
        [return: NotNull]
        public static partial Regex BracketsRegex();

        [GeneratedRegex(@"\{(.+?)\}")]
        [return: NotNull]
        public static partial Regex BracesRegex();

        public static async Task<(string translatedText, IDictionary<string, string>? mappings)> TranslatePlaceholderText(GamePlayer player, string originalText, bool translatePlaceholders = true, Regex regex = null)
        {
            if (player == null || string.IsNullOrWhiteSpace(originalText))
                return (translatedText: originalText, null);

            if (!player.AutoTranslateEnabled || !GS.ServerProperties.Properties.AUTOTRANSLATE_ENABLE)
                return (translatedText: originalText, null);

            string serverLang = LanguageMgr.DefaultLanguage; // FR on your server
            string playerLang = player.Client?.Account?.Language ?? serverLang;

            if (string.Equals(serverLang, playerLang, StringComparison.OrdinalIgnoreCase))
                return (translatedText: originalText, null);
            
            regex = regex ?? BracketsRegex();

            Dictionary<string, string>? keyMap = null;
            if (translatePlaceholders)
            {
                keyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            int index = 0;

            // 1) Replace [key] with placeholders and build mapping originalKey -> translatedKey.
            const string placeholderPrefix = "§§";
            const string placeholderSuffix = "§§";

            List<KeyValuePair<string, string>> originalResponses = null;
            
            if (translatePlaceholders)
                originalResponses = new List<KeyValuePair<string, string>>();
            string toTranslate = regex.Replace(originalText, match =>
            {
                string originalKey = match.Groups[1].Value.Trim();

                // §§0§§, §§1§§, etc.
                string placeholder = $"{placeholderPrefix}{index++}{placeholderSuffix}";

                if (translatePlaceholders)
                    originalResponses!.Add(new(placeholder, originalKey));
                return placeholder;
            });
            
            Task<string> translateText = AutoTranslateManager.Translate(serverLang, playerLang, toTranslate);
            Task<KeyValuePair<string, string>[]> translateResponses = null;
            if (translatePlaceholders)
            {
                translateResponses = Task.WhenAll(originalResponses.Select(async kv =>
                {
                    var (placeholder, original) = kv;
                    return new KeyValuePair<string, string>(placeholder, await AutoTranslateManager.Translate(player, original));
                }));
            }

            // 2) Translate the full text with placeholders so Google sees full context
            string translatedFull;
            try
            {
                translatedFull = await translateText;
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Error while translating npc text for player {0} from {1} to {2}: {4}\nText: {3}", player.Name, serverLang, playerLang, ex, toTranslate);
                return (originalText, null);
            }

            if (string.IsNullOrWhiteSpace(translatedFull))
                return (originalText, null);

            // 3) Replace placeholders with the final [translatedKey] texts
            var translatedResponses = await translateResponses;
            foreach (var (kv, i) in translatedResponses.Select((kv, i) => (kv, i)))
            {
                var (placeholder, translated) = kv;
                translatedFull = translatedFull.Replace(placeholder, '[' + translated + ']');
                if (translatePlaceholders) // keyMap is not null
                    keyMap![translated] = originalResponses[i].Value;
            }
            
            if (translatedFull.Contains(placeholderPrefix))
            {
                log.WarnFormat("Placeholder still found after translating npc text for player {0} from {1} to {2}\nText: {3}", player.Name, serverLang, playerLang, toTranslate);
                return (originalText, null);
            }
            return (originalText, keyMap);
        }
    }
}