
using System;
using System.Linq;
using System.Text;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Finance
{
    public class Money
    {
        public long Amount { get; }
        public virtual Currency Currency { get; }

        protected Money(long amount, Currency currency)
        {
            if (amount < 0) throw new ArgumentException("You cannot mint negative Money.");
            Amount = amount;
            Currency = currency;
        }

        internal static Money Mint(long amount, Currency currency)
            => new Money(amount, currency);

        internal static Money XpToCopper(long xp) => Mint((long)Math.Round(xp * Properties.XP_TO_COPPER_RATE), Currency.Copper);

        public string ToText(string language = null)
        {
            language ??= Properties.SERV_LANGUAGE;

            if (Currency.Equals(Currency.Copper))
            {
                if (Amount == 0) return LanguageMgr.GetCurrencyString(language, "zero");

                var copperPart = $"{Amount % 100} {LanguageMgr.GetCurrencyString(language, "copper")}";
                var silverPart = $"{(Amount / 100) % 100} {LanguageMgr.GetCurrencyString(language, "silver")}";
                var goldPart = $"{(Amount / 100 / 100) % 1000} {LanguageMgr.GetCurrencyString(language, "gold")}";
                var platinumPart = $"{(Amount / 100 / 100 / 1000)} {LanguageMgr.GetCurrencyString(language, "platinum")}";
                var moneyParts = new[] { platinumPart, goldPart, silverPart, copperPart }
                    .Where(p => !p.StartsWith("0")).ToArray();

                if (moneyParts.Length == 1) return moneyParts[0];
                else return string.Join(" and ", new[] { string.Join(", ", moneyParts.Take(moneyParts.Length - 1)), moneyParts.Last() });
            }

            return $"{Amount} {Currency.ToText()}";
        }

        public string ToAbbreviatedText(string language = null)
        {
            if (Currency.Equals(Currency.Copper))
            {
                if (Amount == 0) return $"0 c";
                var result = new StringBuilder();
                var copperPart = $"{Amount % 100} c";
                var silverPart = $"{(Amount / 100) % 100} s";
                var goldPart = $"{(Amount / 100 / 100) % 1000} g";
                var platinumPart = $"{(Amount / 100 / 100 / 1000)} p";
                var moneyParts = new[] { platinumPart, goldPart, silverPart, copperPart }
                    .Where(p => !p.StartsWith("0")).ToArray();
                return string.Join(" ", moneyParts);
            }
            return ToText(language);
        }

        public override bool Equals(object obj)
        {
            if (obj is Money otherMoney)
            {
                var areOfSameValue = otherMoney.Amount == this.Amount;
                var areOfSameType = otherMoney.Currency.Equals(Currency);
                return areOfSameType && areOfSameValue;
            }
            return false;
        }

        public override int GetHashCode() => base.GetHashCode();
        public override string ToString() => ToText();
    }
}