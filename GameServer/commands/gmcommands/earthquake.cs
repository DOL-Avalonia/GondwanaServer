using System;
using DOL.GS.PacketHandler;
using DOL.GS.Commands;
using DOL.GS.Geometry;
using DOL.Language;

namespace DOL.GS.Scripts
{
    [CmdAttribute(
   "&earthquake",
   ePrivLevel.GM,
   "Commands.GM.EarthQuake.Description",
   "Commands.GM.EarthQuake.Usage")]
    public class EarthQuakeCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client == null || client.Player == null || client.ClientState != GameClient.eClientState.Playing) return;

            uint unk1 = 0;
            float radius, intensity, duration, delay = 0;
            radius = 1200.0f;
            intensity = 50.0f;
            duration = 1000.0f;
            int x, y, z = 0;
            if (client.Player.GroundTargetPosition == Position.Nowhere)
            {
                x = (int)client.Player.Position.X;
                y = (int)client.Player.Position.Y;
                //            z = client.Player.Z;
            }
            else
            {
                var tempGroundTarget = client.Player.GroundTargetPosition;// as System.Numerics.Vector3;
                x = (int)tempGroundTarget.X;
                y = (int)tempGroundTarget.Y;
                z = (int)tempGroundTarget.Z;
            }
            if (args.Length > 1)
            {
                try
                {
                    unk1 = (uint)Convert.ToSingle(args[1]);
                }
                catch { }
            }
            if (args.Length > 2)
            {
                try
                {
                    radius = (float)Convert.ToSingle(args[2]);
                }
                catch { }
            }
            if (args.Length > 3)
            {
                try
                {
                    intensity = (float)Convert.ToSingle(args[3]);
                }
                catch { }
            }
            if (args.Length > 4)
            {
                try
                {
                    duration = (float)Convert.ToSingle(args[4]);
                }
                catch { }
            }
            if (args.Length > 5)
            {
                try
                {
                    delay = (float)Convert.ToSingle(args[5]);
                }
                catch { }
            }

            using (var pak = PooledObjectFactory.GetForTick<GSTCPPacketOut>().Init(0x47))
            {
                pak.WriteIntLowEndian(unk1);
                pak.WriteIntLowEndian((uint)x);
                pak.WriteIntLowEndian((uint)y);
                pak.WriteIntLowEndian((uint)z);
                pak.Write(BitConverter.GetBytes(radius), 0, sizeof(Single));
                pak.Write(BitConverter.GetBytes(intensity), 0, sizeof(Single));
                pak.Write(BitConverter.GetBytes(duration), 0, sizeof(Single));
                pak.Write(BitConverter.GetBytes(delay), 0, sizeof(Single));
                client.Out.SendTCP(pak);
            }

            foreach (GamePlayer player in client.Player.GetPlayersInRadius((ushort)radius))
            {
                if (player == client.Player)
                    continue;

                using (var pakBis = PooledObjectFactory.GetForTick<GSTCPPacketOut>().Init(0x47))
                {
                    pakBis.WriteIntLowEndian(unk1);
                    pakBis.WriteIntLowEndian((uint)x);
                    pakBis.WriteIntLowEndian((uint)y);
                    pakBis.WriteIntLowEndian((uint)z);
                    pakBis.Write(BitConverter.GetBytes(radius), 0, sizeof(Single));
                    int distance = (int)player.Coordinate.DistanceTo(client.Player.Position);
                    float newIntensity = intensity * (1 - distance / radius);
                    pakBis.Write(BitConverter.GetBytes(newIntensity), 0, sizeof(Single));
                    pakBis.Write(BitConverter.GetBytes(duration), 0, sizeof(Single));
                    pakBis.Write(BitConverter.GetBytes(delay), 0, sizeof(Single));
                    player.Out.SendTCP(pakBis);
                }
            }

            return;
        }
    }
}