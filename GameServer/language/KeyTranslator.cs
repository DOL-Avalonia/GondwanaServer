using DOL.Language;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DOL.GS
{
    public class KeyTranslator
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        private readonly IDictionary<string, string> m_translations;
        private readonly IDictionary<string, Task<string>> m_autoTranslations;

        public string TranslationKey
        {
            get;
            init;
        }

        public string OriginalLanguage
        {
            get;
            init;
        }

        public string OriginalText
        {
            get;
            init;
        }

        public KeyTranslator(string translationKey, bool threadSafe = true)
        {
            TranslationKey = translationKey;
            OriginalLanguage = LanguageMgr.DefaultLanguage;
            OriginalText = LanguageMgr.GetTranslation(OriginalLanguage, translationKey);
            m_translations = threadSafe ? new ConcurrentDictionary<string, string>() : new Dictionary<string, string>();
            m_autoTranslations = threadSafe ? new ConcurrentDictionary<string, Task<string>>() : new Dictionary<string, Task<string>>();
            m_translations[OriginalLanguage] = OriginalText;
        }

        private async Task<string> TranslateImpl(string langTo, bool autoTranslate)
        {
            langTo = LanguageMgr.NormalizeLang(langTo);
            if (!m_translations.TryGetValue(langTo, out string translation))
            {
                if (LanguageMgr.TryGetTranslation(out string staticTranslation, langTo, TranslationKey))
                {
                    translation = staticTranslation;
                    m_translations[langTo] = staticTranslation;
                }
                else if (autoTranslate)
                {
                    if (!m_autoTranslations.TryGetValue(langTo, out Task<string> autoTranslation))
                    {
                        // TODO: Race condition here, does it matter?
                        autoTranslation = AutoTranslateManager.Translate(OriginalLanguage, langTo, OriginalText);
                        m_autoTranslations[langTo] = autoTranslation;
                    }
                    var result = await autoTranslation;
                    translation = !string.IsNullOrEmpty(result) ? result : OriginalText;
                }
            }
            return translation;
        }

        public async Task<string> Translate(string langTo, params object[] args)
        {
            var str = await TranslateImpl(langTo, true);

            if (string.IsNullOrEmpty(str))
                return str;
                
            if (args is { Length: > 0 })
            {
                try
                {
                    str = string.Format(str, args);
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to translate {TranslationKey} to {langTo} with args [{string.Join(", ", args)}]: {ex}\n\tText: {str}");
                }
            }
            return str;
        }

        /// <summary>
        /// Bulk translate a key with a player message. Useful for example for "Player says: {0}"
        /// </summary>
        /// <param name="receivers">Players to translate for</param>
        /// <param name="inputLang">Language of the original message</param>
        /// <param name="inputText">Original message</param>
        /// <param name="formatArgsSupplier">Function to format with</param>
        /// <returns></returns>
        public async Task<KeyValuePair<GamePlayer, string>> TranslatePlayerInput(GamePlayer receiver, string inputLang, string inputText, Func<GamePlayer, string, object[]> formatArgsSupplier)
        {
            AutoTranslator msgTranslator = new(lang: inputLang, text: inputText);
            var results = await Task.WhenAll(Translate(receiver), msgTranslator.Translate(receiver)).ConfigureAwait(false);
            return new KeyValuePair<GamePlayer, string>(receiver, string.Format(results[0], formatArgsSupplier(receiver, results[1])));
        }

        /// <summary>
        /// Bulk translate a key with a player message. Useful for example for "Player says: {0}"
        /// </summary>
        /// <param name="receivers">Players to translate for</param>
        /// <param name="inputLang">Language of the original message</param>
        /// <param name="inputText">Original message</param>
        /// <param name="formatArgsSupplier">Function to format with</param>
        /// <returns></returns>
        public IEnumerable<Task<KeyValuePair<GamePlayer, string>>> TranslatePlayerInput(IEnumerable<GamePlayer> receivers, string inputLang, string inputText, Func<GamePlayer, string, object[]> formatArgsSupplier)
        {
            return receivers.Select(p => TranslatePlayerInput(p, inputLang, inputText, formatArgsSupplier));
        }

        /// <summary>
        /// Bulk translate a key with a player message. Useful for example for "Player says: hablo español"
        /// </summary>
        /// <param name="receivers">Players to translate for</param>
        /// <param name="inputLang">Language of the original message</param>
        /// <param name="inputText">Original message</param>
        /// <returns></returns>
        public IEnumerable<Task<KeyValuePair<GamePlayer, string>>> TranslatePlayerInput(IEnumerable<GamePlayer> receivers, string inputLang, string inputText)
        {
            return TranslatePlayerInput(receivers, inputLang, inputText, (_, msg) => [msg]);
        }

        public async Task<string> Translate([NotNull] GamePlayer receiver, params object[] args)
        {
            var langTo = receiver.Client?.Account?.Language;
            var str = await TranslateImpl(langTo, receiver.AutoTranslateEnabled);

            if (string.IsNullOrEmpty(str))
                return str;
                
            if (args is { Length: > 0 })
            {
                try
                {
                    str = string.Format(str, args);
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to translate {TranslationKey} to {langTo} with args [{string.Join(", ", args)}]: {ex}\n\tText: {str}");
                }
            }
            return str;
        }

        public IEnumerable<Task<KeyValuePair<GamePlayer, string>>> Translate(IEnumerable<GamePlayer> receivers, params object[] args)
        {
            if (args is { Length: > 0 })
            {
                return receivers.Select(async p =>
                {
                    var str = await Translate(p);
                    return new KeyValuePair<GamePlayer, string>(p, string.Format(str, args));
                });
            }
            else
            {
                return receivers.Select(async p =>
                {
                    var str = await Translate(p);
                    return new KeyValuePair<GamePlayer, string>(p, str);
                });
            }
        }
    }
}
