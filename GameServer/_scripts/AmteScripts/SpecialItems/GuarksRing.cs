using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using AmteScripts.Managers;
using DOL.Events;
using DOL.GS.Commands;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;

namespace DOL.GS.Scripts
{
    public class GuarksRing
    {
        public static Lieu[] Lieux = new[] {
            new Lieu("Dysfonctionnement de l'anneau", 333521, 394153, 16913, 0, 51, false), // Obligatoirement en 1er
			new Lieu("Araich", 496327, 524071, 3128, 858, 51, false),
            new Lieu("Eronig", 411902, 382014, 4977, 1028, 51, false),
            new Lieu("Aimital", 351909, 449663, 3194, 0, 51, false),
            new Lieu("Eskoth", 371046, 488096, 3176, 0, 51, false),
            new Lieu("Dogmak", 283289, 458794, 3515, 0, 51, false),
            new Lieu("Xogob", 350753, 391265, 3745, 0, 51, false),
            new Lieu("Angeruak", 422978, 440262, 5952, 2004, 181, true)
        };

        private const string RING_IS_AMULETTE = "GUARKS_RING_IS_AMULETTE";
        private const string RING_TARGET = "GUARKS_RING_TARGET";
        private const string RING_TIMER = "GUARKS_RING_TIMER";
        private static List<string> _playerIDs = new List<string>();

        [GameServerStartedEvent]
        public static void Init(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.AddHandler(PlayerInventoryEvent.ItemEquipped, new DOLEventHandler(ItemEquipped));
            GameEventMgr.AddHandler(PlayerInventoryEvent.ItemUnequipped, new DOLEventHandler(ItemUnequipped));
            GameEventMgr.AddHandler(GameLivingEvent.Say, new DOLEventHandler(Say));
        }

        public static void Say(DOLEvent e, object sender, EventArgs args)
        {
            var player = sender as GamePlayer;
            if (player == null)
                return;
            if (!_playerIDs.Contains(player.InternalID))
                return;
            SayEventArgs arg = (SayEventArgs)args;

            bool amulette = player.TempProperties.getProperty(RING_IS_AMULETTE, false);
            foreach (Lieu lieu in Lieux)
            {
                if (lieu.Amulette && !amulette) continue;

                if (lieu != Lieux[0] && arg.Text.ToLower().IndexOf(lieu.Name.ToLower()) != -1)
                {
                    player.TempProperties.setProperty(RING_TARGET, lieu);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Items.Specialitems.GuarkRingListened"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    return;
                }
            }
        }

        #region Timer, Item
        public static void ItemEquipped(DOLEvent e, object sender, EventArgs args)
        {
            ItemEquippedArgs arg = (ItemEquippedArgs)args;
            if (sender == null || (arg.Item.Id_nb != "guarks_anneau" && arg.Item.Id_nb != "guarks_amulette"))
                return;

            GamePlayer player = ((GamePlayerInventory)sender).Player;
            if (player.Client.ClientState != GameClient.eClientState.Playing)
                return;
            if (!CheckUse(player))
                return;

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingUsed"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);

            player.MaxSpeedBase = 1;
            player.Out.SendUpdateMaxSpeed();

            if (player.HealthPercent <= 75)
            {
                if (arg.Item.Id_nb == "anneau_guarks")
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingNoPower"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                else
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkAmuletNoPower"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return;
            }

            int harm = player.MaxHealth / 10; // 10%
            if (harm < player.Health)
                player.Health -= harm;
            else
            {
                player.Health = 0;
                player.Die(player);
                return;
            }

            _playerIDs.Add(player.InternalID);

            // Capture initial position and pass state via lambda
            RingState state = new RingState(player.Position, 0);
            ECSGameTimer timer = new ECSGameTimer(player, t => TimerTicks(t, player, state), 1000);

            player.TempProperties.setProperty(RING_IS_AMULETTE, (arg.Item.Id_nb == "guarks_amulette"));
            player.TempProperties.setProperty(RING_TIMER, timer);

            foreach (GamePlayer plr in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                plr.Out.SendSpellEffectAnimation(player, player, 2661, 0, false, 1);
        }

        public static void ItemUnequipped(DOLEvent e, object sender, EventArgs args)
        {
            ItemUnequippedArgs arg = (ItemUnequippedArgs)args;
            if (sender == null || (arg.Item.Id_nb != "guarks_anneau" && arg.Item.Id_nb != "guarks_amulette"))
                return;
            GamePlayer player = ((GamePlayerInventory)sender).Player;
            if (player.Client.ClientState != GameClient.eClientState.Playing)
                return;

            if (player.MaxSpeedBase == 1)
            {
                player.MaxSpeedBase = 191;
                player.Out.SendUpdateMaxSpeed();
            }

            _playerIDs.Remove(player.InternalID);
            player.TempProperties.getProperty<ECSGameTimer>(RING_TIMER, null)?.Stop();
            player.TempProperties.removeProperty(RING_IS_AMULETTE);
            player.TempProperties.removeProperty(RING_TIMER);
            player.TempProperties.removeProperty(RING_TARGET);
        }

        private static int TimerTicks(ECSGameTimer timer, GamePlayer player, RingState state)
        {
            if (player == null)
            {
                timer.Stop();
                return 0;
            }

            // Verification of movement, combat, and cancellation
            bool stop = !_playerIDs.Contains(player.InternalID);
            if (!stop)
                stop = !CheckUse(player);

            // Check position easily against the state's StartPosition
            if (stop || player.InCombat || player.Position != state.StartPosition)
            {
                timer.Stop();
                player.TempProperties.removeProperty(RING_TARGET);
                if (stop)
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingTPCancelMoved"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                else if (!_playerIDs.Contains(player.InternalID) && !player.TempProperties.getProperty(RING_IS_AMULETTE, false))
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingTPCancelRemoved"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                else if (!_playerIDs.Contains(player.InternalID))
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkAmuletTPCancelRemoved"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                _playerIDs.Remove(player.InternalID);
                return 0;
            }

            state.Ticks += 1000;
            int ticks = state.Ticks;

            int harm = player.MaxHealth / 10; // 10%
            switch (ticks)
            {
                case 1000:
                case 3000:
                case 5000:
                    if (harm < player.Health)
                        player.Health -= harm;
                    else
                    {
                        player.Health = 0;
                        player.Die(player);
                        timer.Stop();
                        _playerIDs.Remove(player.InternalID);
                        player.TempProperties.removeProperty(RING_TARGET);
                        return 0;
                    }
                    break;

                case 7000:
                    harm /= 2; // 5%
                    if (harm < player.Health)
                        player.Health -= harm;
                    else
                    {
                        player.Health = 0;
                        player.Die(player);
                        timer.Stop();
                        _playerIDs.Remove(player.InternalID);
                        player.TempProperties.removeProperty(RING_TARGET);
                        return 0;
                    }
                    break;

                case 2000: //2s
                    if (harm < player.Health)
                        player.Health -= harm;
                    else
                    {
                        player.Health = 0;
                        player.Die(player);
                        timer.Stop();
                        _playerIDs.Remove(player.InternalID);
                        player.TempProperties.removeProperty(RING_TARGET);
                        return 0;
                    }

                    foreach (GamePlayer plr in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        plr.Out.SendSpellEffectAnimation(player, player, 2661, 0, false, 1);
                    break;

                case 4000: //4s
                    if (harm < player.Health)
                        player.Health -= harm;
                    else
                    {
                        player.Health = 0;
                        player.Die(player);
                        timer.Stop();
                        _playerIDs.Remove(player.InternalID);
                        player.TempProperties.removeProperty(RING_TARGET);
                        return 0;
                    }

                    foreach (GamePlayer plr in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        plr.Out.SendSpellEffectAnimation(player, player, 2661, 0, false, 1);
                        plr.Out.SendSpellEffectAnimation(player, player, 1677, 0, false, 1);
                    }
                    break;

                case 6000: //6s
                    if (harm < player.Health)
                        player.Health -= harm;
                    else
                    {
                        player.Health = 0;
                        player.Die(player);
                        timer.Stop();
                        _playerIDs.Remove(player.InternalID);
                        player.TempProperties.removeProperty(RING_TARGET);
                        return 0;
                    }

                    foreach (GamePlayer plr in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        plr.Out.SendSpellEffectAnimation(player, player, 82, 0, false, 1);
                        plr.Out.SendSpellEffectAnimation(player, player, 276, 0, false, 1);
                    }
                    break;

                case 8000: //8s
                    foreach (GamePlayer plr in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        plr.Out.SendSpellEffectAnimation(player, player, 2569, 0, false, 1);
                    break;

                case 9000: //9s
                    Lieu lieu = player.TempProperties.getProperty<Lieu>(RING_TARGET, null);
                    if (Util.Chance(5))
                    {
                        lieu = Lieux[0];
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingTPFailed"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (lieu == null)
                    {
                        lieu = GetRandomLieu(player.TempProperties.getProperty(RING_IS_AMULETTE, false));
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingTPNone"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }

                    ushort oldRegion = player.CurrentRegionID;
                    var dest = Position.Create(regionID: lieu.Region, x: lieu.X, y: lieu.Y, z: lieu.Z, heading: lieu.Heading);
                    player.MoveTo(dest);

                    if (dest.RegionID == oldRegion)
                        new ECSGameTimer(player, t => EffectCallback(t, player), 500); // Updated to ECSGameTimer
                    else
                        GameEventMgr.AddHandler(player, GamePlayerEvent.RegionChanged, EnterWorld);

                    timer.Stop();
                    _playerIDs.Remove(player.InternalID);
                    player.TempProperties.removeProperty(RING_TARGET);
                    return 0;
            }

            return 1000;
        }

        private sealed class RingState
        {
            public readonly Position StartPosition;
            public int Ticks;

            public RingState(Position startPos, int ticks)
            {
                StartPosition = startPos;
                Ticks = ticks;
            }
        }
        #endregion

        #region Methodes
        private static bool CheckUse(GamePlayer player)
        {
            if (player.InCombat)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Items.Specialitems.GuarkRingUsageCombat"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (RvrManager.Instance.IsInRvr(player) || player.IsInPvP || player.CurrentRegion.IsDungeon)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Items.Specialitems.GuarkRingUsageHere"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (player.CurrentRegionID == ServerRules.AmtenaelRules.HousingRegionID)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingCannotUseHere"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (JailMgr.IsPrisoner(player))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Items.Specialitems.GuarkRingUsageJailed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (player.IsRiding || player.IsOnHorse)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Items.Specialitems.GuarkRingUsageMounted"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (player.IsStealthed)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingUsageStealthed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (SpellHandler.FindEffectOnTarget(player, "Petrify") != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingUsagePetrified"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (player.IsDamned)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingUsageDamned"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (player.IsCrafting)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingUsageCrafting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (player.IsClimbing)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingUsageClimbing"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (player.DuelTarget != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingUsageDuel"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (player.TempProperties.getProperty<bool>("ArenaParticipant", false))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingUsageArena"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (player.TempProperties.getProperty<object>(StealCommandHandlerBase.PLAYER_VOL_TIMER, null) != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingUsageStealing"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (player.PlayerAfkMessage != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Items.Specialitems.GuarkRingUsageAFK"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (player.HasTerritoryRelic())
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingUsageRelic"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (SpellHandler.FindEffectOnTarget(player, "SummonMonster") != null || SpellHandler.FindEffectOnTarget(player, "CallOfShadows") != null || SpellHandler.FindEffectOnTarget(player, "BringerOfDeath") != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingUsageCantThisForm"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            var wsd = SpellHandler.FindEffectOnTarget(player, "WarlockSpeedDecrease");
            if (wsd != null)
            {
                int rm = wsd.Spell?.ResurrectMana ?? 0;
                string appearancetype = LanguageMgr.GetWarlockMorphAppearance(player.Client.Account.Language, rm);

                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.GuarkRingUsageMorphed", appearancetype), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                return false;
            }
            return true;
        }

        private static void EnterWorld(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.RemoveHandler(sender, GamePlayerEvent.RegionChanged, EnterWorld);
            if (sender is GamePlayer player)
            {
                new ECSGameTimer(player, t => EffectCallback(t, player), 500); // Updated to ECSGameTimer
            }
        }

        private static int EffectCallback(ECSGameTimer timer, GamePlayer player)
        {
            if (player != null)
                foreach (GamePlayer plr in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    plr.Out.SendSpellEffectAnimation(player, player, 276, 0, false, 1);
            timer.Stop();
            return 0;
        }
        #endregion

        #region RandomLieu/Lieu
        private static Lieu GetRandomLieu(bool amulette)
        {
            if (amulette)
                return Lieux[Util.Random(0, Lieux.Length - 1)];

            int i = 0;
            foreach (Lieu lieu in Lieux)
                if (!lieu.Amulette)
                    i++;
            int random = Util.Random(1, i);
            i = 0;
            foreach (Lieu lieu in Lieux)
                if (!lieu.Amulette)
                {
                    i++;
                    if (i == random)
                        return lieu;
                }
            return null;
        }

        public class Lieu
        {
            public string Name;
            public int X;
            public int Y;
            public int Z;
            public ushort Heading;
            public ushort Region;
            public bool Amulette;

            public Lieu(string name, int x, int y, int z, ushort heading, ushort region, bool amulette)
            {
                Name = name;
                X = x;
                Y = y;
                Z = z;
                Heading = heading;
                Region = region;
                Amulette = amulette;
            }
        }
        #endregion
    }
}