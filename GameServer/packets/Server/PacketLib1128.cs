using DOL.Database;
using log4net;
using System.Reflection;

namespace DOL.GS.PacketHandler
{
    [PacketLib(1128, GameClient.eClientVersion.Version1128)]
    public class PacketLib1128 : PacketLib1127
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Constructs a new PacketLib for Client Version 1.128
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib1128(GameClient client)
            : base(client)
        {
        }
    }
}