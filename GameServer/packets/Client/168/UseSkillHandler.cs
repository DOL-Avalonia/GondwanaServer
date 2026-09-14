using DOL.GS.Geometry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using DOL.GS.Styles;
using log4net;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.UseSkill, "Handles Player Use Skill Request.", eClientStatus.PlayerInGame)]
    public class UseSkillHandler : IPacketHandler
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public void HandlePacket(GameClient client, GSPacketIn packet)
        {
            var player = client.Player;
            if (client.Version >= GameClient.eClientVersion.Version1124)
            {
                var x = (int)packet.ReadFloatLowEndian();
                var y = (int)packet.ReadFloatLowEndian();
                var z = (int)packet.ReadFloatLowEndian();
                player.CurrentSpeed = (short)packet.ReadFloatLowEndian();
                var heading = packet.ReadShort();
                player.Position = Position.Create(player.Position.RegionID, x, y, z, heading);
            }
            int flagSpeedData = packet.ReadShort();
            int index = packet.ReadByte();
            int type = packet.ReadByte();

            new UseSkillAction(player, flagSpeedData, index, type).Start(1);
        }

        /// <summary>
        /// Handles player use skill actions
        /// </summary>
        protected class UseSkillAction : RegionAction
        {
            /// <summary>
            /// The speed and flags data
            /// </summary>
            protected readonly int m_flagSpeedData;

            /// <summary>
            /// The skill index
            /// </summary>
            protected readonly int m_index;

            /// <summary>
            /// The skill type
            /// </summary>
            protected readonly int m_type;

            /// <summary>
            /// Constructs a new UseSkillAction
            /// </summary>
            /// <param name="actionSource">The action source</param>
            /// <param name="flagSpeedData">The skill type</param>
            /// <param name="index">The skill index</param>
            /// <param name="type">The skill type</param>
            public UseSkillAction(GamePlayer actionSource, int flagSpeedData, int index, int type)
                : base(actionSource)
            {
                m_flagSpeedData = flagSpeedData;
                m_index = index;
                m_type = type;
            }

            /// <summary>
            /// Called on every timer tick
            /// </summary>
            public override void OnTick()
            {
                GamePlayer player = (GamePlayer)m_actionSource;
                if (player == null)
                    return;

                if ((m_flagSpeedData & 0x200) != 0)
                {
                    player.SetCurrentSpeed((short)-(m_flagSpeedData & 0x1ff)); // backward movement
                }
                else
                {
                    player.SetCurrentSpeed((short)(m_flagSpeedData & 0x1ff)); // forwardmovement
                }

                player.IsStrafing = (m_flagSpeedData & 0x4000) != 0;
                player.TargetInView = (m_flagSpeedData & 0xa000) != 0; // why 2 bits? that has to be figured out
                player.GroundTargetInView = ((m_flagSpeedData & 0x1000) != 0);

                List<Tuple<Skill, Skill>> snap = player.GetAllUsableSkills();

                Skill sk = null;
                Skill sksib = null;

                // we're not using a spec !
                if (m_type > 0)
                {

                    // find the first non-specialization index.
                    int begin = Math.Max(0, snap.FindIndex(it => (it.Item1 is Specialization) == false));

                    // are we in list ?
                    if (m_index + begin < snap.Count)
                    {
                        sk = snap[m_index + begin].Item1;
                        sksib = snap[m_index + begin].Item2;
                    }

                }
                else
                {
                    // mostly a spec !
                    if (m_index < snap.Count)
                    {
                        sk = snap[m_index].Item1;
                        sksib = snap[m_index].Item2;
                    }
                }

                // we really got a skill !
                if (sk != null)
                {
                    // Test if we can use it !
                    int reuseTime = player.GetSkillDisabledDuration(sk);
                    if (reuseTime > 60000)
                    {
                        player.Out.SendMessage(
                            string.Format("You must wait {0} minutes {1} seconds to use this ability!", reuseTime / 60000, reuseTime % 60000 / 1000),
                            eChatType.CT_System, eChatLoc.CL_SystemWindow);

                        if (player.Client.Account.PrivLevel < 2)
                            return;
                    }
                    else if (reuseTime > 0)
                    {
                        player.Out.SendMessage(string.Format("You must wait {0} seconds to use this ability!", reuseTime / 1000 + 1),
                                               eChatType.CT_System, eChatLoc.CL_SystemWindow);

                        if (player.Client.Account.PrivLevel < 2)
                            return;
                    }

                    // See what we should do depending on skill type !


                    if (sk is Specialization)
                    {
                        Specialization spec = (Specialization)sk;
                        ISpecActionHandler handler = SkillBase.GetSpecActionHandler(spec.KeyName);
                        if (handler != null)
                        {
                            handler.Execute(spec, player);
                        }
                    }
                    else if (sk is Ability)
                    {
                        Ability ab = (Ability)sk;
                        IAbilityActionHandler handler = SkillBase.GetAbilityActionHandler(ab.KeyName);
                        if (handler != null)
                        {
                            handler.Execute(ab, player);
                            return;
                        }

                        ab.Execute(player);
                    }
                    else if (sk is Spell)
                    {
                        if (sksib != null && sksib is SpellLine)
                            player.castingComponent.RequestCastSpell((Spell)sk, (SpellLine)sksib);
                    }
                    else if (sk is Style)
                    {
                        player.ExecuteWeaponStyle((Style)sk);
                    }

                }

                if (sk == null)
                {
                    player.Out.SendMessage("Skill is not implemented.", eChatType.CT_Advise, eChatLoc.CL_SystemWindow);
                }
            }
        }
    }
}