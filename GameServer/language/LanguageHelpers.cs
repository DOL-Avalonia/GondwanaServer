using DOL.GS.ServerProperties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOL.GS
{
    public static class LanguageHelpers
    {
        public static IEnumerable<Task<T>> Translate<T>(this IEnumerable<GamePlayer> players, string translationKey, Func<GamePlayer, string, T> selector, bool threadSafe = true)
        {
            var translator = new KeyTranslator(translationKey, threadSafe);
            return players.Select(async p => selector(p, await translator.Translate(p)));
        }

        public static IEnumerable<Task<KeyValuePair<GamePlayer, string>>> Translate(this IEnumerable<GamePlayer> players, string translationKey, bool threadSafe = true)
        {
            return Translate(players, translationKey, (p, str) => new KeyValuePair<GamePlayer, string>(p, str), threadSafe);
        }

        public static IEnumerable<Task<T>> AutoTranslate<T>(this IEnumerable<GamePlayer> players, string input, string inputLang, Func<GamePlayer, string, T> selector, bool threadSafe = true)
        {
            if (!Properties.AUTOTRANSLATE_ENABLE || string.IsNullOrWhiteSpace(input))
                return players.Select(p => Task.FromResult(selector(p, input)));

            var translator = new AutoTranslator(lang: inputLang, text: input, threadSafe);
            return players.Select(async p =>
            {
                string translated = input;
                if (p.AutoTranslateEnabled)
                {
                    translated = await translator.Translate(p);
                }
                return selector(p, translated);
            });
        }

        public static IEnumerable<Task<T>> AutoTranslate<T>(this IEnumerable<GamePlayer> players, string input, GamePlayer source, Func<GamePlayer, string, T> selector, bool threadSafe = true)
        {
            return AutoTranslate(players, input, source.Client?.Account?.Language, selector, threadSafe);
        }

        public static IEnumerable<Task<KeyValuePair<GamePlayer, string>>> AutoTranslate(this IEnumerable<GamePlayer> players, string input, string inputLang, bool threadSafe = true)
        {
            return AutoTranslate(players, input, inputLang, (p, str) => new KeyValuePair<GamePlayer, string>(p, str), threadSafe);
        }

        public static IEnumerable<Task<KeyValuePair<GamePlayer, string>>> AutoTranslate(this IEnumerable<GamePlayer> players, string input, GamePlayer source, bool threadSafe = true)
        {
            return AutoTranslate(players, input, source.Client?.Account?.Language, (p, str) => new KeyValuePair<GamePlayer, string>(p, str), threadSafe);
        }
    }
    
}
