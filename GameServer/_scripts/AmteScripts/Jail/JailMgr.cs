using DOL.Database;
using DOL.events.gameobjects;
using DOL.Events;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DOL.GS.Scripts
{
    /// <summary>
    /// Summary description for JailMgr.
    /// </summary>
    public static class JailMgr
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        public const ushort Radius = 300; //Taille de la prison

        // Réglages prison
        // Bind de prison + tp
        public const int PrisonHRP_X = 32943;
        public const int PrisonHRP_Y = 34440;
        public const int PrisonHRP_Z = 16528;
        public const int PrisonHRP_RegionID = 499;
        public const int PrisonHRP_Heading = 1043;
        // Bind de sortie de prison + tp
        public const int SortieHRP_X = 434493;
        public const int SortieHRP_Y = 492983;
        public const int SortieHRP_Z = 3085;
        public const int SortieHRP_RegionID = 51;
        public const int SortieHRP_Heading = 3560;

        // Bind de prison HRP + tp
        public const int Prison_X = 434518;
        public const int Prison_Y = 494012;
        public const int Prison_Z = 3191;
        public const int Prison_RegionID = 51;
        public const int Prison_Heading = 3115;
        // Bind de sortie de prison HRP + tp
        public const int Sortie_X = 434493;
        public const int Sortie_Y = 492983;
        public const int Sortie_Z = 3085;
        public const int Sortie_RegionID = 51;
        public const int Sortie_Heading = 3560;

        /// <summary>
        /// Prisonniers
        /// </summary>
        public static List<GamePlayer> Prisonniers = new List<GamePlayer>();

        public static Dictionary<GamePlayer, Prisoner> PlayerXPrisoner = new Dictionary<GamePlayer, Prisoner>();

        [GameServerStartedEvent]
        public static void OnScriptsCompiled(DOLEvent e, object sender, EventArgs args)
        {
            Area.Circle prisHRP = new Area.Circle("", PrisonHRP_X, PrisonHRP_Y, PrisonHRP_Z, Radius);
            WorldMgr.GetRegion(PrisonHRP_RegionID).AddArea(prisHRP);

            Area.Circle prisRP = new Area.Circle("", Prison_X, Prison_Y, Prison_Z, Radius);
            WorldMgr.GetRegion(Prison_RegionID).AddArea(prisRP);

            GameEventMgr.AddHandler(GamePlayerEvent.GameEntered, new DOLEventHandler(PlayerEnter));
            GameEventMgr.AddHandler(prisHRP, AreaEvent.PlayerLeave, PlayerEvade);
            GameEventMgr.AddHandler(prisRP, AreaEvent.PlayerLeave, PlayerEvade);
            GameEventMgr.AddHandler(GamePlayerEvent.SendToJail, SendToJail);
        }

        private static void SendToJail(DOLEvent e, object sender, EventArgs arguments)
        {
            var args = arguments as SendToJailEventArgs;
            if (args != null)
            {
                int cost = 0;
                TimeSpan time;
                string reason = null;

                if (args.OriginalReputation == -1)
                {
                    cost = 450;
                    time = TimeSpan.FromHours(16);
                    reason = LanguageMgr.GetTranslation(args.GamePlayer.Client.Account.Language, "GameJail.ReputNeg01");
                }
                else if (args.OriginalReputation == -2)
                {
                    cost = 900;
                    time = TimeSpan.FromHours(32);
                    reason = LanguageMgr.GetTranslation(args.GamePlayer.Client.Account.Language, "GameJail.ReputNeg02");
                }
                else if (args.OriginalReputation == -3)
                {
                    cost = 1800;
                    time = TimeSpan.FromHours(48);
                    reason = LanguageMgr.GetTranslation(args.GamePlayer.Client.Account.Language, "GameJail.ReputNeg03");
                }
                else if (args.OriginalReputation == -4)
                {
                    cost = 3600;
                    time = TimeSpan.FromHours(72);
                    reason = LanguageMgr.GetTranslation(args.GamePlayer.Client.Account.Language, "GameJail.ReputNeg04");
                }
                else if (args.OriginalReputation <= -5)
                {
                    cost = 7200;
                    time = TimeSpan.FromHours(96);
                    reason = LanguageMgr.GetTranslation(args.GamePlayer.Client.Account.Language, "GameJail.ReputNeg05");
                }
                else
                {
                    log.Warn(string.Format("Cannot send Player {0} from Acount {1} to Jail because reputation is not correct. Value: {2}", args.GamePlayer.Name, args.GamePlayer.AccountName, args.GamePlayer.Reputation));
                    return;
                }

                //clear Deathlogs
                var deathLogs = GameServer.Database.SelectObjects<DBDeathLog>(DB.Column("KillerId").IsEqualTo(args.GamePlayer.InternalID).And(DB.Column("WasPunished").IsEqualTo(0)));

                if (deathLogs != null)
                {
                    foreach (DBDeathLog deathLog in deathLogs)
                    {
                        deathLog.WasPunished = true;
                        GameServer.Database.SaveObject(deathLog);
                    }
                }

                EmprisonnerRP(args.GamePlayer, cost, DateTime.Now + time, args.GamePlayer.LastKiller?.Name, reason, true);
            }
        }

        private static void PlayerEnter(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = sender as GamePlayer;
            if (player == null) return;
            var prison = GameServer.Database.SelectObject<Prisoner>(p => p.PlayerId == player.InternalID);
            if (prison == null) return;
            if (prison.Sortie != DateTime.MinValue && prison.Sortie.Ticks <= DateTime.Now.Ticks)
            {
                Relacher(player);
                return;
            }
            Prisonniers.Add(player);
            PlayerXPrisoner.Add(player, prison);
            player.Reputation = 0;
            player.Wanted = false;

            if (prison.RP) player.MoveTo(Position.Create((ushort)Prison_RegionID, Prison_X, Prison_Y, Prison_Z, (ushort)Prison_Heading));
            else player.MoveTo(Position.Create((ushort)PrisonHRP_RegionID, PrisonHRP_X, PrisonHRP_Y, PrisonHRP_Z, (ushort)PrisonHRP_Heading));

            player.MaxSpeedBase = 50;
            player.Out.SendUpdateMaxSpeed();

            player.TempProperties.setProperty("JailMgr", prison);
            GameEventMgr.AddHandler(player, GamePlayerEvent.Revive, PlayerRevive);
            GameEventMgr.AddHandler(player, GamePlayerEvent.Quit, PlayerExit);
            GameEventMgr.AddHandler(player, GamePlayerEvent.RegionChanged, PlayerRevive);

            if (prison.Sortie == DateTime.MinValue) return;

            long time = (prison.Sortie.Ticks - DateTime.Now.Ticks) / 10000;
            if (time < 0)
                time = 1;
            if (time >= 864000000) return;
            RegionTimer SortieTimer = new RegionTimer(player, SortiePrison);
            SortieTimer.Properties.setProperty("player", player);
            SortieTimer.Start((int)time);
        }

        private static void PlayerRevive(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = sender as GamePlayer;
            if (player == null) return;
            Prisoner prison = player.TempProperties.getProperty<Prisoner>("JailMgr", null);
            if (prison == null) return;

            if (prison.RP) player.MoveTo(Position.Create((ushort)Prison_RegionID, Prison_X, Prison_Y, Prison_Z, (ushort)Prison_Heading));
            else player.MoveTo(Position.Create((ushort)PrisonHRP_RegionID, PrisonHRP_X, PrisonHRP_Y, PrisonHRP_Z, (ushort)PrisonHRP_Heading));
            player.Bind(true);

            if (player.MaxSpeed == 50) return;
            player.MaxSpeedBase = 50;
            player.Out.SendUpdateMaxSpeed();
        }

        private static void PlayerEvade(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = ((AreaEventArgs)args).GameObject as GamePlayer;
            if (player == null || !Prisonniers.Contains(player)) return;
            PlayerRevive(null, player, null);
        }

        private static void PlayerExit(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = sender as GamePlayer;
            if (player == null) return;
            var prison = GameServer.Database.SelectObject<Prisoner>(p => p.PlayerId == player.InternalID);
            if (prison == null) return;
            Prisonniers.Remove(player);
            PlayerXPrisoner.Remove(player);
            GameEventMgr.RemoveHandler(player, GamePlayerEvent.Revive, PlayerRevive);
            GameEventMgr.RemoveHandler(player, GamePlayerEvent.Quit, PlayerExit);
            GameEventMgr.RemoveHandler(player, GamePlayerEvent.RegionChanged, PlayerRevive);
        }

        private static int SortiePrison(RegionTimer timer)
        {
            GamePlayer player = timer.Properties.getProperty<GamePlayer>("player", null);
            if (player == null ||
                (player.Client.ClientState != GameClient.eClientState.Playing &&
                player.Client.ClientState != GameClient.eClientState.WorldEnter))
                return 0;
            Relacher(player);
            return 0;
        }

        #region Animation
        private static void Animation(GameLiving player)
        {
            foreach (GamePlayer pl in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                pl.Out.SendSpellEffectAnimation(pl, player, 177, 0, false, 1);
        }
        #endregion

        #region Entrée/Sortie Prison

        /// <summary>
        /// Emprisonner un joueur
        /// </summary>
        /// <param name="player">Prisonnier</param>
        /// <param name="cost">Amende</param>
        /// <param name="sortie">Date de sortie (si null ou valeur minimale alors temps infini)</param>
        /// <param name="GM"></param>
        /// <param name="JailRP"></param>
        /// <param name="raison"></param>
        private static void Emprisonner(GamePlayer player, int cost, DateTime sortie, string jailer, bool JailRP, string raison, bool isOutLaw)
        {
            long time = (sortie.Ticks - DateTime.Now.Ticks) / 10000;
            if (sortie != DateTime.MinValue && time <= 0)
                return;

            bool isNew = false;
            Prisoner prisoner = GetPrisoner(player);

            if (prisoner == null)
                prisoner = GameServer.Database.FindObjectByKey<Prisoner>(player.InternalID);

            if (prisoner != null)
            {
                // Player is already in jail, update existing sentence
                prisoner.Cost += cost;
                if (sortie > prisoner.Sortie) prisoner.Sortie = sortie;
                prisoner.Raison = string.IsNullOrEmpty(prisoner.Raison) ? raison : prisoner.Raison + " - " + raison;
                prisoner.IsOutLaw = isOutLaw;
                prisoner.RP = JailRP;
                GameServer.Database.SaveObject(prisoner);
            }
            else
            {
                // Create new sentence
                isNew = true;
                prisoner = new Prisoner(player)
                {
                    Cost = cost,
                    Sortie = sortie,
                    RP = JailRP,
                    Raison = raison,
                    IsOutLaw = isOutLaw
                };
                GameServer.Database.AddObject(prisoner);
            }

            if (JailRP)
                player.MoveTo(Position.Create((ushort)Prison_RegionID, Prison_X, Prison_Y, Prison_Z, (ushort)Prison_Heading));
            else player.MoveTo(Position.Create((ushort)PrisonHRP_RegionID, PrisonHRP_X, PrisonHRP_Y, PrisonHRP_Z, (ushort)PrisonHRP_Heading));

            player.Bind(true);
            player.MaxSpeedBase = 50;
            player.Out.SendUpdateMaxSpeed();
            player.Reputation = 0;
            player.Wanted = false;
            player.SaveIntoDatabase();

            if (isNew)
            {
                Prisonniers.Add(player);
                PlayerXPrisoner.Add(player, prisoner);

                player.TempProperties.setProperty("JailMgr", prisoner);
                GameEventMgr.AddHandler(player, GamePlayerEvent.Quit, PlayerExit);
                GameEventMgr.AddHandler(player, GamePlayerEvent.Revive, PlayerRevive);
                GameEventMgr.AddHandler(player, GamePlayerEvent.RegionChanged, PlayerRevive);
            }
            else
            {
                if (!Prisonniers.Contains(player)) Prisonniers.Add(player);
                if (!PlayerXPrisoner.ContainsKey(player)) PlayerXPrisoner.Add(player, prisoner);
                else PlayerXPrisoner[player] = prisoner;

                player.TempProperties.setProperty("JailMgr", prisoner);
            }

            Animation(player);

            if (JailRP)
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameJail.JailedRP", jailer, cost), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            else
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameJail.JailedHRP", jailer), eChatType.CT_Important, eChatLoc.CL_SystemWindow);

            string message = "";
            if (!string.IsNullOrEmpty(jailer))
            {
                message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.Jailed", player.Name, jailer);
                NewsMgr.CreateNews("GameObjects.GamePlayer.Jailed", player.Realm, eNewsType.RvRGlobal, false, true, player.Name, jailer);
            }
            else if (string.IsNullOrEmpty(player.LastKiller?.Name))
            {
                message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.Jailed.Unknown", player.Name);
                NewsMgr.CreateNews("GameObjects.GamePlayer.Jailed.Unknown", player.Realm, eNewsType.RvRGlobal, false, true, player.Name);
            }

            if (Properties.DISCORD_ACTIVE)
            {
                DolWebHook hook = new DolWebHook(Properties.DISCORD_WEBHOOK_ID);
                hook.SendMessage(message);
            }

            if (sortie == DateTime.MinValue) return;

            // Stop existing timer if there is one
            RegionTimer oldTimer = player.TempProperties.getProperty<RegionTimer>("JailTimer", null);
            if (oldTimer != null)
            {
                oldTimer.Stop();
                player.TempProperties.removeProperty("JailTimer");
            }

            // Start a new timer for the updated total duration
            time = (prisoner.Sortie.Ticks - DateTime.Now.Ticks) / 10000;
            if (time <= 0 || time >= 864000000) return;

            RegionTimer SortieTimer = new RegionTimer(player, SortiePrison, 1);
            SortieTimer.Properties.setProperty("player", player);
            SortieTimer.Start((int)time);
            player.TempProperties.setProperty("JailTimer", SortieTimer);
        }

        /// <summary>
        /// Emprisonner un joueur non connecté
        /// </summary>
        /// <param name="playerName"></param>
        /// <param name="cost">Amende</param>
        /// <param name="sortie">Date de sortie (si null ou valeur minimale alors temps infini)</param>
        /// <param name="JailRP"></param>
        /// <param name="raison"></param>
        private static bool Emprisonner(string playerName, int cost, DateTime sortie, bool JailRP, string raison)
        {
            long time = (sortie.Ticks - DateTime.Now.Ticks) / 10000;
            if (sortie != DateTime.MinValue && time <= 0)
                return false;

            var perso = GameServer.Database.SelectObject<DOLCharacters>(c => c.Name == playerName);
            if (perso == null || perso.Name.ToLower() != playerName.ToLower())
                return false;


            bool isNew = false;
            Prisoner Prisonnier = GameServer.Database.FindObjectByKey<Prisoner>(perso.ObjectId);

            if (Prisonnier != null)
            {
                Prisonnier.Cost += cost;
                if (sortie > Prisonnier.Sortie) Prisonnier.Sortie = sortie;
                Prisonnier.Raison = string.IsNullOrEmpty(Prisonnier.Raison) ? raison : Prisonnier.Raison + " - " + raison;
                Prisonnier.RP = JailRP;

                GameServer.Database.SaveObject(Prisonnier);
            }
            else
            {
                isNew = true;
                Prisonnier = new Prisoner(perso)
                {
                    Cost = cost,
                    Sortie = sortie,
                    RP = JailRP,
                    Raison = raison
                };
                GameServer.Database.AddObject(Prisonnier);
            }

            if (JailRP)
            {
                perso.Xpos = Prison_X;
                perso.Ypos = Prison_Y;
                perso.Zpos = Prison_Z;
                perso.Region = Prison_RegionID;

                perso.BindXpos = Prison_X;
                perso.BindYpos = Prison_Y;
                perso.BindZpos = Prison_Z;
                perso.BindHeading = Prison_Heading;
                perso.BindRegion = Prison_RegionID;
            }
            else
            {
                perso.Xpos = PrisonHRP_X;
                perso.Ypos = PrisonHRP_Y;
                perso.Zpos = PrisonHRP_Z;
                perso.Region = PrisonHRP_RegionID;

                perso.BindXpos = PrisonHRP_X;
                perso.BindYpos = PrisonHRP_Y;
                perso.BindZpos = PrisonHRP_Z;
                perso.BindHeading = PrisonHRP_Heading;
                perso.BindRegion = PrisonHRP_RegionID;
            }
            perso.Reputation = 0;
            perso.IsWanted = false;
            perso.OutlawTimeStamp = 0;
            perso.MaxSpeed = 50;
            GameServer.Database.SaveObject(perso);

            return true;
        }

        /// <summary>
        /// Emprisonner un joueur connecté dans la prison RP
        /// </summary>
        public static void EmprisonnerRP(GamePlayer player, int cost, DateTime sortie, string jailer, string raison, bool isOutLaw)
        {
            Emprisonner(player, cost, sortie, jailer, true, raison, isOutLaw);
        }
        /// <summary>
        /// Emprisonner un joueur connecté dans la prison HRP
        /// </summary>
        public static void EmprisonnerHRP(GamePlayer player, DateTime sortie, string jailer, string raison)
        {
            Emprisonner(player, 0, sortie, jailer, false, raison, false);
        }

        /// <summary>
        /// Emprisonner un joueur déconnecté dans la prison RP
        /// </summary>
        public static bool EmprisonnerRP(string player, int cost, DateTime sortie, string jailer, string raison)
        {
            return Emprisonner(player, cost, sortie, true, raison);
        }
        /// <summary>
        /// Emprisonner un joueur déconnecté dans la prison HRP
        /// </summary>
        public static bool EmprisonnerHRP(string player, DateTime sortie, string jailer, string raison)
        {
            return Emprisonner(player, 0, sortie, false, raison);
        }

        /// <summary>
        /// Relâche un prisonnier
        /// </summary>
        /// <param name="player">Prisonnier</param>
        /// <returns></returns>
        public static bool Relacher(GamePlayer player, bool quiet = false)
        {
            var Prisonnier = GameServer.Database.SelectObject<Prisoner>(p => p.PlayerId == player.InternalID);
            if (Prisonnier == null)
                return false;
            GameServer.Database.DeleteObject(Prisonnier);

            player.MaxSpeedBase = 191;
            player.Out.SendUpdateMaxSpeed();
            player.Reputation = 0;
            player.Wanted = false;

            if (Prisonnier.RP) player.MoveTo(Position.Create((ushort)Sortie_RegionID, Sortie_X, Sortie_Y, Sortie_Z, (ushort)Sortie_Heading));
            else player.MoveTo(Position.Create((ushort)SortieHRP_RegionID, SortieHRP_X, SortieHRP_Y, SortieHRP_Z, (ushort)SortieHRP_Heading));
            player.Bind(true);
            player.SaveIntoDatabase();

            Prisonniers.Remove(player);
            PlayerXPrisoner.Remove(player);
            player.TempProperties.removeProperty("JailMgr");

            RegionTimer oldTimer = player.TempProperties.getProperty<RegionTimer>("JailTimer", null);
            if (oldTimer != null)
            {
                oldTimer.Stop();
                player.TempProperties.removeProperty("JailTimer");
            }

            GameEventMgr.RemoveHandler(player, GamePlayerEvent.Revive, PlayerRevive);
            GameEventMgr.RemoveHandler(player, GamePlayerEvent.Quit, PlayerExit);
            GameEventMgr.RemoveHandler(player, GamePlayerEvent.RegionChanged, PlayerRevive);
            Animation(player);

            if (!quiet)
            {
                NewsMgr.CreateNews("GameObjects.GamePlayer.Released", player.Realm, eNewsType.RvRGlobal, false, true, player.Name);
                if (Properties.DISCORD_ACTIVE)
                {
                    DolWebHook hook = new DolWebHook(Properties.DISCORD_WEBHOOK_ID);
                    hook.SendMessage(LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "GameObjects.GamePlayer.Released", player.Name));
                }
            }

            return true;
        }

        /// <summary>
        /// Relâche un prisonnier
        /// </summary>
        /// <param name="player">Prisonnier</param>
        /// <returns></returns>
        public static bool Relacher(DOLCharacters player, bool quiet = false)
        {
            if (player == null)
                return false;
            var Prisonnier = GameServer.Database.SelectObject<Prisoner>(p => p.PlayerId == player.ObjectId);
            if (Prisonnier == null)
                return false;
            GameServer.Database.DeleteObject(Prisonnier);

            if (Prisonnier.RP) //if(Sortie_X != 0 && Sortie_Y != 0 && Sortie_RegionID != 0 && Sortie_Heading != 0)
            {
                player.Region = Sortie_RegionID;
                player.Xpos = Sortie_X;
                player.Ypos = Sortie_Y;
                player.Zpos = Sortie_Z;
                player.BindRegion = Sortie_RegionID;
                player.BindXpos = Sortie_X;
                player.BindYpos = Sortie_Y;
                player.BindZpos = Sortie_Z;
                player.MaxSpeed = 191;
                GameServer.Database.SaveObject(player);
            }
            else
            {
                player.Region = SortieHRP_RegionID;
                player.Xpos = SortieHRP_X;
                player.Ypos = SortieHRP_Y;
                player.Zpos = SortieHRP_Z;
                player.BindRegion = SortieHRP_RegionID;
                player.BindXpos = SortieHRP_X;
                player.BindYpos = SortieHRP_Y;
                player.BindZpos = SortieHRP_Z;
                player.MaxSpeed = 191;
                GameServer.Database.SaveObject(player);
            }

            if (!quiet)
            {
                NewsMgr.CreateNews("GameObjects.GamePlayer.Released", (eRealm)player.Realm, eNewsType.RvRGlobal, false, true, player.Name);
                if (Properties.DISCORD_ACTIVE)
                {
                    DolWebHook hook = new DolWebHook(Properties.DISCORD_WEBHOOK_ID);
                    hook.SendMessage(LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "GameObjects.GamePlayer.Released", player.Name));
                }
            }
            return true;
        }

        /// <summary>
        /// Relâche un prisonnier
        /// </summary>
        /// <param name="name">Nom du prisonnier</param>
        /// <returns></returns>
        public static bool Relacher(string name, bool quiet = false)
        {
            GameClient client = null;
            if (WorldMgr.GetAllPlayingClientsCount() > 0)
                client = WorldMgr.GetClientByPlayerName(name, true, true);
            if (client != null && client.Player != null)
                return Relacher(client.Player, quiet);
            return Relacher(GameServer.Database.SelectObject<DOLCharacters>(c => c.Name == name), quiet);
        }
        #endregion

        /// <summary>
        /// Donne l'entrée dans la base du prisonnier par son nom
        /// </summary>
        /// <param name="name">Nom du prisonnier</param>
        /// <returns>Null si non trouvable</returns>
        public static Prisoner GetPrisoner(string name)
        {
            return GameServer.Database.SelectObject<Prisoner>(p => p.Name == name);
        }

        /// <summary>
        /// Retourne true si le joueur est un prisonnier.
        /// </summary>
        public static bool IsPrisoner(string name)
        {
            return GetPrisoner(name) != null;
        }

        /// <summary>
        /// Donne l'entrée dans la base du prisonnier par son nom
        /// </summary>
        /// <returns>Null si non trouvable</returns>
        public static Prisoner GetPrisoner(GamePlayer player)
        {
            return player.TempProperties.getProperty<Prisoner>("JailMgr", null);
        }

        /// <summary>
        /// Retourne true si le joueur est un prisonnier.
        /// </summary>
        public static bool IsPrisoner(GamePlayer player)
        {
            return player.TempProperties.getProperty<Prisoner>("JailMgr", null) != null;
        }


    }
}
