using System;
using log4net;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS
{
    /// <summary>
    /// Central place to handle auto-translation of player chat.
    /// </summary>
    public static class AutoTranslateManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AutoTranslateManager));

        /// <summary>
        /// Returns translated text for receiver if their auto-translate is enabled.
        /// If disabled or translation fails, originalText is returned.
        /// </summary>
        public static string MaybeTranslate(GamePlayer sender, GamePlayer receiver, string originalText)
        {
            if (receiver == null || string.IsNullOrWhiteSpace(originalText))
                return originalText;

            // Player has not enabled auto-translate => do nothing.
            if (!receiver.AutoTranslateEnabled)
                return originalText;

            if (!Properties.AUTOTRANSLATE_ENABLE)
                return originalText;

            if (sender == null || sender.Client == null || receiver.Client == null)
                return originalText;

            var fromLang = sender.Client.Account.Language ?? "EN";
            var toLang = receiver.Client.Account.Language ?? "EN";

            if (string.Equals(fromLang, toLang, StringComparison.OrdinalIgnoreCase))
                return originalText;

            try
            {
                var translated = ExternalTranslator.Translate(originalText, fromLang, toLang);

                if (string.IsNullOrWhiteSpace(translated))
                    return originalText;

                return translated;
            }
            catch (Exception e)
            {
                if (log.IsWarnEnabled)
                    log.Warn($"AutoTranslate failed ({fromLang}->{toLang}): {e.Message}", e);

                return originalText;
            }
        }

        /// <summary>
        /// Translate a server-originated text (e.g. NPC dialog) from the server language
        /// into the player's account language, honoring AutoTranslateEnabled and global switch.
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
            var fromLang = LanguageMgr.DefaultLanguage; // your server base language (FR)

            if (string.Equals(fromLang, toLang, StringComparison.OrdinalIgnoreCase))
                return originalText;

            try
            {
                var translated = ExternalTranslator.Translate(originalText, fromLang, toLang);
                return string.IsNullOrWhiteSpace(translated) ? originalText : translated;
            }
            catch
            {
                return originalText;
            }
        }
    }
}