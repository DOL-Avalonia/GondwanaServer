using DOL.GS.ServerProperties;
using DOL.Language;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public AutoTranslator(GamePlayer? sender, string text, bool threadSafe = true) : this(lang: sender?.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage, text, threadSafe)
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

}
