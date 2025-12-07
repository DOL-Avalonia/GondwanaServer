using System.Reflection;
using log4net;
using System;
using System.Collections;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.AI.Brain;
using DOL.GS;
using DOL.Events;
using DOL.GS.Geometry;
using System.Collections.Specialized;
using DOL.GS.Profession;
using DOL.Language;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    //http://www.camelotherald.com/masterlevels/ma.php?ml=Sojourner
    //no shared timer
    #region Sojourner-1
    //Gameplayer - MaxEncumbrance
    #endregion

    //ML2 Unending Breath - already handled in another area

    //ML3 Reveal Crystalseed - already handled in another area

    //no shared timer
    #region Sojourner-4
    [SpellHandlerAttribute("UnmakeCrystalseed")]
    public class UnmakeCrystalseedSpellHandler : SpellHandler
    {
        /// <summary>
        /// Execute unmake crystal seed spell
        /// </summary>
        /// <param name="target"></param>
        /// <param name="force"></param>
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            ConsumePower(target);
            base.FinishSpellCast(target, force);
        }

        /// <summary>
        /// execute non duration spell effect on target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="effectiveness"></param>
        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (!base.OnDirectEffect(target, effectiveness))
                return false;
            
            if (target == null || !target.IsAlive)
                return false;

            foreach (GameNPC item in target.GetNPCsInRadius((ushort)m_spell.Radius))
            {
                if (item != null && item is GameMine)
                {
                    (item as GameMine)!.Delete();
                }
            }
            return true;
        }

        public UnmakeCrystalseedSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Sojourner.UnmakeCrystalseed.MainDescription", Spell.Radius);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //no shared timer
    #region Sojourner-5
    [SpellHandlerAttribute("AncientTransmuter")]
    public class AncientTransmuterSpellHandler : SpellHandler
    {
        private GameMerchant merchant;
        /// <summary>
        /// Execute Acient Transmuter summon spell
        /// </summary>
        /// <param name="target"></param>
        /// <param name="force"></param>
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            ConsumePower(target);
            base.FinishSpellCast(target, force);
        }
        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            if (effect.Owner == null || !effect.Owner.IsAlive)
                return;

            merchant.AddToWorld();
        }
        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            if (merchant != null) merchant.Delete();
            return base.OnEffectExpires(effect, noMessages);
        }
        public AncientTransmuterSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            if (caster is GamePlayer)
            {
                GamePlayer casterPlayer = caster as GamePlayer;
                merchant = new GameMerchant();
                //Fill the object variables
                merchant.Position = casterPlayer!.Position.TurnedAround() 
                    + Vector.Create(Util.Random(-20, 20), Util.Random(-20, 20));
                merchant.Level = 1;
                merchant.Realm = casterPlayer.Realm;
                merchant.Name = "Ancient Transmuter";
                merchant.Model = 993;
                merchant.MaxSpeedBase = 0;
                merchant.GuildName = "";
                merchant.Size = 50;
                merchant.Flags |= GameNPC.eFlags.PEACE;
                merchant.Catalog = MerchantCatalog.LoadFromDatabase("ML_transmuteritems");
            }
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Sojourner.AncientTransmuter.MainDescription");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //no shared timer
    #region Sojourner-6
    [SpellHandlerAttribute("Port")]
    public class Port : MasterlevelHandling
    {
        // constructor
        public Port(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            base.FinishSpellCast(target, force);
        }

        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target is not GamePlayer { IsAlive: true, ObjectState: GameObject.eObjectState.Active, InCombat: false, IsInRvR: false } player)
                return false;

            if (GameRelic.IsPlayerCarryingRelic(player))
                return false;

            SendEffectAnimation(player, 0, false, 1);
            player.MoveToBind();
            return true;
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;
            string spellTarget = LanguageMgr.GetTargetOfType(language, m_spell.Target.ToString());

            string mainDesc;
            if (Spell.Radius > 0)
            {
                mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Sojourner.Port.MainDescription1", spellTarget, Spell.Radius);
            }
            else
            {
                mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Sojourner.Port.MainDescription2");
            }

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //no shared timer
    #region Sojourner-7
    [SpellHandlerAttribute("EssenceResist")]
    public class EssenceResistHandler : AbstractResistBuff
    {
        public override eBuffBonusCategory BonusCategory1 { get { return eBuffBonusCategory.BaseBuff; } }
        public override eProperty Property1 { get { return eProperty.Resist_Natural; } }
        public EssenceResistHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Sojourner.EssenceResist.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion Sojourner-7

    //no shared timer
    #region Sojourner-8
    [SpellHandlerAttribute("Zephyr")]
    public class FZSpellHandler : MasterlevelHandling
    {
        public class ZephyrNPC : GameNPC
        {
            public FZSpellHandler SpellHandler { get; init; }

            public ZephyrNPC(FZSpellHandler spellHandler) : base()
            {
                SpellHandler = spellHandler;
            }
            
            /// <inheritdoc />
            public override void FollowTargetInRange()
            {
                SpellHandler.OnArriveAtTarget();
            }
        }
        
        protected RegionTimer m_expireTimer;
        protected GameNPC m_npc;
        protected GamePlayer m_target;
        protected Coordinate m_loc;

        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target is not GamePlayer { IsAlive: true, ObjectState: GameObject.eObjectState.Active } player)
                return false;
            
            Zephyr(player);
            return true;
        }

        public override bool CheckBeginCast(GameLiving target, bool quiet)
        {
            if (target == null)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MustSelectTarget"), eChatType.CT_SpellResisted);
                return false;
            }

            if (target is GameNPC == true)
                return false;

            if (!GameServer.ServerRules.IsAllowedToAttack(Caster, target, true))
                return false;

            return base.CheckBeginCast(target, quiet);
        }

        private void Zephyr(GamePlayer target)
        {
            if (!target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active) return;
            GameNPC npc = new ZephyrNPC(this);

            m_npc = npc;

            npc.Realm = Caster.Realm;
            npc.Heading = Caster.Heading;
            npc.Model = 1269;
            npc.Position = Caster.Position;
            npc.Name = "Forceful Zephyr";
            npc.MaxSpeedBase = 400;
            npc.Level = 55;
            npc.CurrentRegion = Caster.CurrentRegion;
            npc.Flags |= GameNPC.eFlags.PEACE;
            npc.Flags |= GameNPC.eFlags.DONTSHOWNAME;
            npc.Flags |= GameNPC.eFlags.CANTTARGET;
            BlankBrain brain = new BlankBrain();
            npc.SetOwnBrain(brain);
            npc.AddToWorld();
            npc.TempProperties.setProperty("target", target);
            npc.Follow(target, 10, 1500);

            m_target = target;

            StartTimer();
        }

        protected virtual void StartTimer()
        {
            StopTimer();
            m_expireTimer = new RegionTimer(m_npc, new RegionTimerCallback(ExpiredCallback), 10000);
        }

        protected virtual int ExpiredCallback(RegionTimer callingTimer)
        {
            m_target.IsStunned = false;
            m_target.DismountSteed(true);
            m_target.DebuffCategory[(int)eProperty.SpellFumbleChance] -= 100;
            GameEventMgr.RemoveHandler(m_target, GamePlayerEvent.AttackedByEnemy, new DOLEventHandler(OnAttack));
            m_npc.StopMoving();
            m_npc.RemoveFromWorld();
            //sometimes player can't move after zephyr :
            m_target.Out.SendUpdateMaxSpeed();
            return 0;
        }

        protected virtual void StopTimer()
        {

            if (m_expireTimer != null)
            {
                m_expireTimer.Stop();
                m_expireTimer = null;
            }

        }

        private void OnAttack(DOLEvent e, object sender, EventArgs arguments)
        {
            GameLiving living = sender as GameLiving;
            if (living == null) return;
            AttackedByEnemyEventArgs attackedByEnemy = arguments as AttackedByEnemyEventArgs;
            AttackData ad = null;
            if (attackedByEnemy != null)
                ad = attackedByEnemy.AttackData;

            double absorbPercent = 100;
            int damageAbsorbed = (int)(0.01 * absorbPercent * (ad!.Damage + ad.CriticalDamage));
            int spellAbsorbed = (int)(0.01 * absorbPercent * Spell.Damage);

            ad.Damage -= damageAbsorbed;
            ad.Damage -= spellAbsorbed;

            MessageToLiving(ad.Target, LanguageMgr.GetTranslation((ad.Target as GamePlayer)?.Client, "SpellHandler.Zephyr.NoAttackInZephyr"), eChatType.CT_Spell);
            MessageToLiving(ad.Attacker, LanguageMgr.GetTranslation((ad.Attacker as GamePlayer)?.Client, "SpellHandler.Zephyr.TargetNoAttackInZephyr"), eChatType.CT_Spell);
        }

        private void OnArriveAtTarget()
        {
            GameNPC npc = m_npc;
            
            if (npc == null) return;

            if (npc.ObjectState != GameObject.eObjectState.Active)
            {
                m_npc = null;
                return;
            }
            
            GamePlayer target = npc.TempProperties.getProperty<object>("target", null) as GamePlayer;

            if (target == null || !target.IsAlive) return;

            GamePlayer player = target as GamePlayer;
            if (player == null) return;
            if (!player.IsAlive) return;

            player.IsStunned = true;
            //player.IsSilenced = true;
            player.DebuffCategory[(int)eProperty.SpellFumbleChance] += 100;
            player.StopAttack();
            player.StopCurrentSpellcast();
            player.MountSteed(npc, true);
            
            GameEventMgr.AddHandler(player, GamePlayerEvent.AttackedByEnemy, new DOLEventHandler(OnAttack));

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "SpellHandler.Zephyr.PickedUpByZephyr"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            npc.StopFollowing();

            if (Caster is GamePlayer)
            {
                //Calculate random target
                m_loc = m_npc.Coordinate + Vector.Create(x: Util.Random(-1500, 1500), y: Util.Random(-1500, 1500));;
                (Caster as GamePlayer)!.Out.SendCheckLOS((Caster as GamePlayer), m_npc, new CheckLOSResponse(ZephyrCheckLOS));
            }
        }
        public void ZephyrCheckLOS(GamePlayer player, ushort response, ushort targetOID)
        {
            if ((response & 0x100) == 0x100) m_npc.WalkTo(m_loc, 100);
        }

        public virtual Coordinate GetTargetLoc()
        {
            return m_npc.Coordinate + Vector.Create(Util.Random(-1500, 1500), Util.Random(-1500, 1500), 0);
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }

        public FZSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            double recastSeconds = Spell.RecastDelay / 60000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Sojourner.Zephyr.MainDescription");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Sojourner.Zephyr.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //no shared timer
    #region Sojourner-9
    [SpellHandlerAttribute("Phaseshift")]
    public class PhaseshiftHandler : MasterlevelHandling
    {
        private int endurance;

        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            endurance = (Caster.MaxEndurance * 50) / 100;

            if (Caster.Endurance < endurance)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Phaseshift.NotEnoughEndurance"), eChatType.CT_System);
                return false;
            }

            return base.CheckBeginCast(selectedTarget, quiet);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            GameEventMgr.AddHandler(Caster, GamePlayerEvent.AttackedByEnemy, new DOLEventHandler(OnAttack));
            Caster.Endurance -= endurance;
        }

        private void OnAttack(DOLEvent e, object sender, EventArgs arguments)
        {
            GameLiving living = sender as GameLiving;
            if (living == null) return;
            AttackedByEnemyEventArgs attackedByEnemy = arguments as AttackedByEnemyEventArgs;
            AttackData ad = null;
            if (attackedByEnemy != null)
                ad = attackedByEnemy.AttackData;

            if (ad!.Attacker is GamePlayer)
            {
                ad.Damage = 0;
                ad.CriticalDamage = 0;
                GamePlayer player = ad.Attacker as GamePlayer;
                player!.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Skill.Ability.TargetIsPhaseshifted", player.GetPersonalizedName(living)), eChatType.CT_Missed, eChatLoc.CL_SystemWindow);
            }
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            GameEventMgr.RemoveHandler(Caster, GamePlayerEvent.AttackedByEnemy, new DOLEventHandler(OnAttack));
            return base.OnEffectExpires(effect, noMessages);
        }

        public override bool HasPositiveEffect
        {
            get
            {
                return false;
            }
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }

        // constructor
        public PhaseshiftHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Sojourner.Phaseshift.MainDescription", Spell.Duration);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //no shared timer
    #region Sojourner-10
    [SpellHandlerAttribute("Groupport")]
    public class Groupport : MasterlevelHandling
    {
        public Groupport(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            if (Caster is GamePlayer && Caster.CurrentRegionID == 51 && ((GamePlayer)Caster).BindPosition.RegionID == 51)
            {
                if (Caster.CurrentRegionID == 51)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Groupport.CannotUseAbilityHere"), eChatType.CT_SpellResisted);
                    return false;
                }
                else
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Groupport.BindInAnotherRegion"), eChatType.CT_SpellResisted);
                    return false;
                }
            }
            return base.CheckBeginCast(selectedTarget, quiet);
        }

        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            base.FinishSpellCast(target, force);
        }

        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target is not GamePlayer { IsAlive: true, ObjectState: GameObject.eObjectState.Active } player) return false;

            if (player.Group == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "SpellHandler.Groupport.NotInGroup"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (player.Group.IsGroupInCombat())
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "SpellHandler.Groupport.GroupInCombat"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            foreach (GamePlayer pl in player.Group.GetPlayersInTheGroup())
            {
                if (pl != null)
                {
                    SendEffectAnimation(pl, 0, false, 1);
                    pl.MoveTo(player.BindPosition);
                }
            }
            return true;
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Sojourner.Groupport.MainDescription");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion
}
