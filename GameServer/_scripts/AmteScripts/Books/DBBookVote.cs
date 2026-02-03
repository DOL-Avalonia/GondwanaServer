using System;
using DOL.Database.Attributes;

namespace DOL.Database
{
    [DataTable(TableName = "BookVote")]
    public class DBBookVote : DataObject
    {
        [PrimaryKey(AutoIncrement = true)]
        public long ID { get; set; }

        [DataElement(AllowDbNull = false, Index = true)]
        public long BookID { get; set; }

        [DataElement(AllowDbNull = false, Index = true)]
        public string VoterPlayerID { get; set; }

        [DataElement(AllowDbNull = false)]
        public int VoteValue { get; set; } // +1 or -1

        [DataElement(AllowDbNull = false)]
        public DateTime VoteDate { get; set; } = DateTime.UtcNow;
    }
}
