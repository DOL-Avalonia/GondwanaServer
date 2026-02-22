using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DOL.Database;

namespace DOL.GS
{
    public static class BookUtils
    {
        private static readonly Regex LeaderRegex = new(@"#GuildLeader_([A-Za-z0-9_\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex MemberRegex = new(@"#GuildMember(\d{2})_([A-Za-z0-9_\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex WordRegex = new(@"\b[\p{L}\p{N}']+\b", RegexOptions.Compiled);
        private static readonly Regex LetterRegex = new(@"\p{L}", RegexOptions.Compiled);

        // Vowels (latin + diacritics), y included
        private static readonly Regex VowelRegex = new(@"[aeiouyàâäáãåæèéêëìíîïòóôöõøœùúûüýÿ]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Long consonant run (unpronounceable chunks)
        private static readonly Regex LongConsonantRunRegex = new(@"(?i)(?:[^aeiouyàâäáãåæèéêëìíîïòóôöõøœùúûüýÿ'\W\d]{4,})", RegexOptions.Compiled);

        // Within a word: aaa, ooo, mmm...
        private static readonly Regex InWordRepeat3Regex = new(@"(?i)([a-zàâäáãåæèéêëìíîïòóôöõøœùúûüýÿ])\1\1", RegexOptions.Compiled);

        // Small FR/EN stopword set (weak signal only)
        private static readonly HashSet<string> CommonWords = new(StringComparer.OrdinalIgnoreCase)
        {
            // EN
            "the","and","or","but","to","of","in","on","for","with","as","is","are","was","were","be","been",
            "a","an","this","that","these","those","it","its","i","you","he","she","we","they","them","his","her","our","your",
            // FR
            "le","la","les","un","une","des","de","du","dans","sur","pour","avec","sans","et","ou","mais","est","sont","été","être",
            "je","tu","il","elle","nous","vous","ils","elles","mon","ma","mes","ton","ta","tes","son","sa","ses","notre","votre","leur",
            "ce","cet","cette","ces","ça","qui","que","quoi","dont","au","aux","pas","ne","n","d","l",
            // DE (basic)
            "der","die","das","ein","eine","einer","einem","eines","einen","und","oder","aber","zu","von","im","in","am","an","auf","für","mit","ohne",
            "ist","sind","war","waren","sein","bin","bist","seid","nicht","kein","keine","keiner","keinem","keinen",
            "ich","du","er","sie","es","wir","ihr","sie","mich","dich","ihn","uns","euch","ihnen",
            "mein","meine","meiner","meinem","meinen","dein","deine","deiner","deinem","deinen","sein","seine","seiner","seinem","seinen",
            "ihr","ihre","ihrer","ihrem","ihren","unser","unsere","unserer","unserem","unseren","euer","eure","eurer","eurem","euren",
            "dass","weil","wenn","wie","was","wer","wen","wem","wo","woher","wohin","hier","dort","diese","dieser","dieses","jener","jenes","jeden",
            "auch","noch","nur","schon","sehr","mehr","weniger","man"
        };

        // ====== Prohibited terms (InvalidNamesManager) ======
        /// <summary>
        /// Returns true if the text contains prohibited terms using the centralized InvalidNamesManager.
        /// It checks both the raw text and the normalized text for maximum restrictiveness.
        /// </summary>
        public static bool ContainsProhibitedTerms(string text, out string matched)
        {
            matched = string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var invalidNamesMgr = GameServer.Instance?.PlayerManager?.InvalidNames;

            if (invalidNamesMgr == null)
            {
                return false;
            }

            string normalized = NormalizeForFilter(text);
            if (invalidNamesMgr[normalized])
            {
                matched = "Restricted content found (Normalized Check)";
                return true;
            }

            if (invalidNamesMgr[text])
            {
                matched = "Restricted content found (Raw Check)";
                return true;
            }

            return false;
        }

        /// <summary>
        /// Normalize for filtering:
        /// - lower
        /// - remove diacritics (é -> e)
        /// - replace punctuation by spaces
        /// - collapse spaces
        /// </summary>
        public static string NormalizeForFilter(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            string s = input.ToLowerInvariant().Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(c);

                if (uc == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else
                    sb.Append(' ');
            }

            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        public static int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return WordRegex.Matches(text).Count;
        }

        public static bool LooksLikeGibberish(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;

            string normalized = Regex.Replace(text, @"\s+", " ").Trim();
            if (normalized.Length < 60) return true;

            int letters = LetterRegex.Matches(normalized).Count;
            if (letters < Math.Max(30, normalized.Length / 6)) return true;

            if (HasLongRepeatRun(normalized, 10)) return true;

            var words = WordRegex.Matches(normalized).Select(m => m.Value).ToArray();
            if (words.Length < 15) return true;

            var lower = words.Select(w => w.ToLowerInvariant()).ToArray();

            int unique = lower.Distinct().Count();
            double uniqueRatio = (double)unique / words.Length;

            int shortWords = words.Count(w => w.Length <= 2);
            double shortRatio = (double)shortWords / words.Length;

            int len3Words = words.Count(w => w.Length == 3);
            double len3Ratio = (double)len3Words / words.Length;

            int vowelLessWords = words.Count(w => !VowelRegex.IsMatch(w));
            double vowelLessRatio = (double)vowelLessWords / words.Length;

            int consonantRunWords = words.Count(w => LongConsonantRunRegex.IsMatch(w));
            double consonantRunRatio = (double)consonantRunWords / words.Length;

            double avgWordLen = words.Average(w => (double)w.Length);

            var freq = lower.GroupBy(w => w).Select(g => new { Word = g.Key, Count = g.Count() })
                            .OrderByDescending(x => x.Count).ToArray();

            int top1 = freq[0].Count;
            int top2 = freq.Length > 1 ? freq[1].Count : 0;
            int top3 = freq.Length > 2 ? freq[2].Count : 0;

            double top1Ratio = (double)top1 / words.Length;
            double top3Ratio = (double)(top1 + top2 + top3) / words.Length;

            int consecutiveRepeats = 0;
            for (int i = 1; i < lower.Length; i++)
                if (lower[i] == lower[i - 1]) consecutiveRepeats++;

            double consecutiveRepeatRatio = (double)consecutiveRepeats / Math.Max(1, words.Length - 1);

            int noiseShort = words.Count(w => w.Length <= 3 && !CommonWords.Contains(w));
            double noiseShortRatio = (double)noiseShort / words.Length;

            int inWordRepeat3 = words.Count(w => InWordRepeat3Regex.IsMatch(w));
            double inWordRepeatRatio = (double)inWordRepeat3 / words.Length;

            int commonCount = lower.Count(w => CommonWords.Contains(w));
            double commonRatio = (double)commonCount / words.Length;

            int punct = normalized.Count(c => c == ',' || c == ';' || c == ':' || c == '!' || c == '?');
            double punctPer100 = punct * 100.0 / Math.Max(1, normalized.Length);

            int score = 0;

            if (top1Ratio >= 0.14) score += 3;
            if (top3Ratio >= 0.30) score += 2;
            if (top3Ratio >= 0.40) score += 2;

            if (consecutiveRepeatRatio >= 0.10) score += 2;
            if (consecutiveRepeatRatio >= 0.18) score += 2;

            if (noiseShortRatio >= 0.20) score += 2;
            if (noiseShortRatio >= 0.30) score += 2;

            if (shortRatio >= 0.20) score += 2;
            if (len3Ratio >= 0.25) score += 1;

            if (vowelLessRatio >= 0.25) score += 2;
            if (consonantRunRatio >= 0.12) score += 2;

            if (inWordRepeatRatio >= 0.08) score += 2;
            if (inWordRepeatRatio >= 0.15) score += 2;

            if (avgWordLen < 3.5) score += 2;
            if (uniqueRatio < 0.20) score += 2;
            if (uniqueRatio >= 0.80 && (noiseShortRatio >= 0.20 || shortRatio >= 0.20)) score += 2;
            if (punctPer100 >= 1.2 && (noiseShortRatio >= 0.20 || top3Ratio >= 0.30)) score += 1;
            if (commonRatio < 0.02 && score >= 3) score += 1;

            return score >= 5;
        }

        private static bool HasLongRepeatRun(string s, int runLen)
        {
            int run = 1;
            for (int i = 1; i < s.Length; i++)
            {
                if (s[i] == s[i - 1]) run++;
                else run = 1;
                if (run >= runLen) return true;
            }
            return false;
        }

        public static (string leader, List<string> members) ExtractFounders(string text, int requiredGuildNum)
        {
            string leader = null;
            var leaderMatch = LeaderRegex.Match(text ?? "");
            if (leaderMatch.Success)
                leader = leaderMatch.Groups[1].Value;

            var members = new SortedDictionary<int, string>();
            foreach (Match m in MemberRegex.Matches(text ?? ""))
            {
                if (!int.TryParse(m.Groups[1].Value, out int idx)) continue;
                members[idx] = m.Groups[2].Value;
            }

            var list = new List<string>();
            for (int i = 0; i < requiredGuildNum; i++)
            {
                if (!members.TryGetValue(1 + i, out var n)) break;
                list.Add(n);
            }

            return (leader, list);
        }

        public static DOLCharacters GetCharacter(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return null;
            return DOLDB<DOLCharacters>.SelectObject(DB.Column(nameof(DOLCharacters.Name)).IsEqualTo(playerName));
        }

        public static bool IsGuildless(DOLCharacters ch)
        {
            if (ch == null) return false;
            return string.IsNullOrEmpty(ch.GuildID);
        }

        public static string GetAccountName(DOLCharacters ch)
        {
            return ch?.AccountName ?? string.Empty;
        }

        public static bool AccountsAreUnique(IEnumerable<string> accountNames)
        {
            var list = accountNames.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim().ToLowerInvariant()).ToList();
            return list.Count == list.Distinct().Count();
        }
    }
}
