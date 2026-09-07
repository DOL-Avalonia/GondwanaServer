using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DOL.GS.PacketHandler
{
    public class ClientDelve
    {
        private string delveType;
        private List<(string, string)> delveElements = new List<(string, string)>();
        private int index = 0;

        public ClientDelve(string delveType)
        {
            this.delveType = delveType;
        }

        public int Index
        {
            get => index;
            set
            {
                index = value;
                AddElement("Index", value);
            }
        }

        public string ClientMessage
        {
            get
            {
                ushort delveCharLimit = 2048;
                var elementString = new StringBuilder();
                foreach (var element in delveElements)
                {
                    var nextElementStringLength = element.Item1.Length + element.Item2.ToString().Length + 5;
                    var delveTypeEnclosureLength = delveType.Length + 3;
                    var currentDelveLength = elementString.Length + delveTypeEnclosureLength;
                    if ((currentDelveLength + nextElementStringLength) <= delveCharLimit)
                    {
                        elementString.Append($"({element.Item1} \"{element.Item2}\")");
                    }
                }
                return $"({delveType} {elementString})";
            }
        }

        public int TypeID
        {
            get
            {
                var type = delveType.ToLower();
                switch (type)
                {
                    case "spell": return 24;
                    case "style": return 25;
                    case "song": return 26;
                    case "realmability": return 27;
                    case "ability": return 28;
                    default: throw new System.ArgumentException($"{delveType} has no client packet ID yet.");
                }
            }
        }

        public void AddElement(string name, object value)
        {
            var ignoredValues = new List<string>() { "", "0" };
            if (ignoredValues.Contains(value.ToString())) return;
            delveElements.Add((name, value.ToString()));
        }

        public void AddElement(string name, IEnumerable<object> collection)
        {
            if (collection == null || !collection.Any()) return;

            AddElement(name, string.Join(", ", collection));
        }

        public void AddElement(string name, bool flag) => AddElement(name, flag ? "1" : "0");
    }

}
