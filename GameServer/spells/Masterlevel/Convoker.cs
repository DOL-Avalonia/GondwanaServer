using System;
using System.Collections;
using System.Reflection;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.SkillHandler;
using log4net;
using DOL.Database;
using DOL.GS.RealmAbilities;
using System.Numerics;
using DOL;
using DOL.GS.Geometry;
using Vector = DOL.GS.Geometry.Vector;
using DOL.Language;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    //http://www.camelotherald.com/masterlevels/ma.php?ml=Convoker
    //no shared timer
    #region Convoker-1
    [SpellHandlerAttribute("SummonWood")]
    public class SummonWoodSpellHandler : SummonItemSpellHandler
    {
        public SummonWoodSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            ItemTemplate template = GameServer.Database.FindObjectByKey<ItemTemplate>("mysticwood_wooden_boards");
            if (template != null)
            {
                items.Add(GameInventoryItem.Create(template));
                foreach (InventoryItem item in items)
                {
                    if (item.IsStackable)
                    {
                        item.Count = 1;
                        item.Weight = item.Count * item.Weight;
                    }
                }
            }
        }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Convoker.SummonWood.MainDescription");

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
    #region Convoker-2
    [SpellHandlerAttribute("PrescienceNode")]
    public class PrescienceNodeSpellHandler : FontSpellHandler
    {
        // constructor
        public PrescienceNodeSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            ApplyOnNPC = false;
            ApplyOnCombat = true;

            //Construct a new font.
            font = new GameFont();
            font.Model = 2584;
            font.Name = spell.Name;
            font.Realm = caster.Realm;
            font.Position = caster.Position;
            font.Owner = (GamePlayer)caster;

            // Construct the font spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7312;
            dbs.ClientEffect = 7312;
            dbs.Damage = spell.Damage;
            dbs.DamageType = (int)spell.DamageType;
            dbs.Target = "enemy";
            dbs.Radius = 0;
            dbs.Type = "Prescience";
            dbs.Value = spell.Value;
            dbs.Duration = spell.ResurrectHealth;
            dbs.Frequency = spell.ResurrectMana;
            dbs.Pulse = 0;
            dbs.PulsePower = 0;
            dbs.LifeDrainReturn = spell.LifeDrainReturn;
            dbs.Power = 0;
            dbs.CastTime = 0;
            dbs.Range = WorldMgr.VISIBILITY_DISTANCE;
            sRadius = 2000;
            s = new Spell(dbs, 50);
            sl = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            heal = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.Convoker.PrescienceNode.MainDescription1");
            string mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.Convoker.PrescienceNode.MainDescription2", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc1 + "\n\n" + mainDesc2 + "\n\n" + secondDesc;
            }

            return mainDesc1 + "\n\n" + mainDesc2;
        }
    }
    
    [SpellHandlerAttribute("Prescience")]
    public class PrescienceSpellHandler : SpellHandler
    {
        public override bool IsOverwritable(GameSpellEffect compare)
        {
            return false;
        }

        public override bool HasPositiveEffect
        {
            get { return false; }
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            return base.OnEffectExpires(effect, noMessages);
        }

        public PrescienceSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.Convoker.Prescience.MainDescription1");
        }
    }
    #endregion

    //no shared timer
    #region Convoker-3
    [SpellHandlerAttribute("PowerTrap")]
    public class PowerTrapSpellHandler : MineSpellHandler
    {
        // constructor
        public PowerTrapSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            //Construct a new mine.
            mine = new GameMine();
            mine.Model = 2590;
            mine.Name = spell.Name;
            mine.Realm = caster.Realm;
            mine.Position = caster.Position;
            mine.Owner = (GamePlayer)caster;

            // Construct the mine spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7313;
            dbs.ClientEffect = 7313;
            dbs.Damage = spell.Damage;
            dbs.DamageType = (int)spell.DamageType;
            dbs.Target = "enemy";
            dbs.Radius = 0;
            dbs.Type = "PowerRend";
            dbs.Value = spell.Value;
            dbs.Duration = spell.ResurrectHealth;
            dbs.Frequency = spell.ResurrectMana;
            dbs.Pulse = 0;
            dbs.PulsePower = 0;
            dbs.LifeDrainReturn = spell.LifeDrainReturn;
            dbs.Power = 0;
            dbs.CastTime = 0;
            dbs.Value = 0.2;
            dbs.Range = WorldMgr.VISIBILITY_DISTANCE;
            sRadius = 350;
            s = new Spell(dbs, 1);
            sl = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            trap = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Convoker.PowerTrap.MainDescription");

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
    #region Convoker-4
    [SpellHandlerAttribute("SpeedWrapWard")]
    public class SpeedWrapWardSpellHandler : FontSpellHandler
    {
        // constructor
        public SpeedWrapWardSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            ApplyOnCombat = true;
            Friendly = false;

            //Construct a new mine.
            font = new GameFont();
            font.Model = 2586;
            font.Name = spell.Name;
            font.Realm = caster.Realm;
            font.Position = caster.Position;
            font.Owner = (GamePlayer)caster;

            // Construct the mine spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7237;
            dbs.ClientEffect = 7237;
            dbs.Damage = spell.Damage;
            dbs.DamageType = (int)spell.DamageType;
            dbs.Target = "enemy";
            dbs.Radius = 0;
            dbs.Type = "SpeedWrap";
            dbs.Value = spell.Value;
            dbs.Duration = spell.ResurrectHealth;
            dbs.Frequency = spell.ResurrectMana;
            dbs.Pulse = 0;
            dbs.PulsePower = 0;
            dbs.LifeDrainReturn = spell.LifeDrainReturn;
            dbs.Power = 0;
            dbs.CastTime = 0;
            dbs.Range = WorldMgr.VISIBILITY_DISTANCE;
            sRadius = 1000;
            dbs.SpellGroup = 9;
            s = new Spell(dbs, 50);
            sl = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            heal = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }
    }
    [SpellHandlerAttribute("SpeedWrap")]
    public class SpeedWrapSpellHandler : SpellHandler
    {
        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }
        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            if (effect.Owner is GamePlayer)
                ((GamePlayer)effect.Owner).Out.SendUpdateMaxSpeed();
        }
        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            if (effect.Owner is GamePlayer)
                ((GamePlayer)effect.Owner).Out.SendUpdateMaxSpeed();
            return base.OnEffectExpires(effect, noMessages);
        }
        public SpeedWrapSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Convoker.SpeedWrap.MainDescription");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //shared timer 1
    #region Convoker-5
    [SpellHandlerAttribute("SummonWarcrystal")]
    public class SummonWarcrystalSpellHandler : SummonItemSpellHandler
    {
        public SummonWarcrystalSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            string ammo = "";
            switch (Util.Random(1, 2))
            {
                case 1:
                    ammo = "mystic_ammo_heat";
                    break;
                case 2:
                    ammo = "mystic_ammo_cold";
                    break;
            }
            ItemTemplate template = GameServer.Database.FindObjectByKey<ItemTemplate>(ammo);
            if (template != null)
            {
                items.Add(GameInventoryItem.Create(template));
                foreach (InventoryItem item in items)
                {
                    if (item.IsStackable)
                    {
                        item.Count = 1;
                        item.Weight = item.Count * item.Weight;
                    }
                }

            }
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Convoker.SummonWarcrystal.MainDescription");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //shared timer 1
    #region Convoker-6
    [SpellHandlerAttribute("Battlewarder")]
    public class BattlewarderSpellHandler : SpellHandler
    {
        private GameNPC warder;
        private GameSpellEffect m_effect;
        /// <summary>
        /// Execute battle warder summon spell
        /// </summary>
        /// <param name="target"></param>
        /// <param name="force"></param>
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= CalculatePowerCost(target);
            base.FinishSpellCast(target, force);
        }
        public override bool IsOverwritable(GameSpellEffect compare)
        {
            return false;
        }
        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            m_effect = effect;
            if (effect.Owner == null || !effect.Owner.IsAlive)
                return;

            if ((effect.Owner is GamePlayer))
            {
                GamePlayer casterPlayer = effect.Owner as GamePlayer;
                if (casterPlayer!.GroundTargetPosition != Position.Nowhere && casterPlayer.GroundTargetInView)
                {
                    GameEventMgr.AddHandler(casterPlayer, GamePlayerEvent.Moving, new DOLEventHandler(PlayerMoves));
                    GameEventMgr.AddHandler(warder, GameLivingEvent.Dying, new DOLEventHandler(BattleWarderDie));
                    GameEventMgr.AddHandler(casterPlayer, GamePlayerEvent.CastStarting, new DOLEventHandler(PlayerMoves));
                    GameEventMgr.AddHandler(casterPlayer, GamePlayerEvent.AttackFinished, new DOLEventHandler(PlayerMoves));
                    warder.Position = casterPlayer.GroundTargetPosition;
                    warder.Owner = effect.Owner;
                    warder.AddBrain(new MLBrain());
                    warder.AddToWorld();
                }
                else
                {
                    MessageToCaster(LanguageMgr.GetTranslation(casterPlayer.Client, "SpellHandler.Convoker.AreaTargetOutOfRange"), eChatType.CT_SpellResisted);
                    effect.Cancel(false);
                }
            }
        }
        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            if (warder != null)
            {
                GameEventMgr.RemoveHandler(warder, GameLivingEvent.Dying, new DOLEventHandler(BattleWarderDie));
                warder.RemoveBrain(warder.Brain);
                warder.Health = 0;
                warder.Delete();
            }
            if ((effect.Owner is GamePlayer))
            {
                GamePlayer casterPlayer = effect.Owner as GamePlayer;
                GameEventMgr.RemoveHandler(casterPlayer, GamePlayerEvent.Moving, new DOLEventHandler(PlayerMoves));
                GameEventMgr.RemoveHandler(casterPlayer, GamePlayerEvent.CastStarting, new DOLEventHandler(PlayerMoves));
                GameEventMgr.RemoveHandler(casterPlayer, GamePlayerEvent.AttackFinished, new DOLEventHandler(PlayerMoves));
            }
            effect.Owner.EffectList.Remove(effect);
            return base.OnEffectExpires(effect, noMessages);
        }

        // Event : player moves, lose focus
        public void PlayerMoves(DOLEvent e, object sender, EventArgs args)
        {
            GameLiving player = sender as GameLiving;
            if (player == null) return;
            if (e == GamePlayerEvent.Moving)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Battlewarder.ConcentrationFades"), eChatType.CT_SpellExpires);
                OnEffectExpires(m_effect, true);
                return;
            }
        }

        // Event : Battle warder has died
        private void BattleWarderDie(DOLEvent e, object sender, EventArgs args)
        {
            GameNPC kWarder = sender as GameNPC;
            if (kWarder == null) return;
            if (e == GameLivingEvent.Dying)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Battlewarder.WarderFallen"), eChatType.CT_SpellExpires);
                OnEffectExpires(m_effect, true);
                return;
            }
        }
        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            if (!base.CheckBeginCast(selectedTarget, quiet)) return false;
            if (!(m_caster.GroundTargetPosition != Position.Nowhere && m_caster.GroundTargetInView))
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.Convoker.AreaTargetOutOfRange"), eChatType.CT_SpellResisted);
                return false;
            }
            return true;

        }
        public BattlewarderSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            warder = new GameNPC();
            //Fill the object variables
            warder.CurrentRegion = caster.CurrentRegion;
            warder.Orientation = caster.Orientation + Angle.Degrees(180);
            warder.Level = 70;
            warder.Realm = caster.Realm;
            warder.Name = "Battle Warder";
            warder.Model = 993;
            warder.MaxSpeedBase = 0;
            warder.GuildName = "";
            warder.Size = 50;
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.Convoker.Battlewarder.MainDescription1");
            string mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.Convoker.Battlewarder.MainDescription2");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc1 + "\n\n" + mainDesc2 + "\n\n" + secondDesc;
            }

            return mainDesc1 + "\n\n" + mainDesc2;
        }
    }
    #endregion

    //no shared timer
    #region Convoker-7
    [SpellHandlerAttribute("DissonanceTrap")]
    public class DissonanceTrapSpellHandler : MineSpellHandler
    {
        // constructor
        public DissonanceTrapSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            //Construct a new mine.
            mine = new GameMine();
            mine.Model = 2588;
            mine.Name = spell.Name;
            mine.Realm = caster.Realm;
            mine.Position = caster.Position;
            mine.Owner = (GamePlayer)caster;

            // Construct the mine spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7255;
            dbs.ClientEffect = 7255;
            dbs.Damage = spell.Damage;
            dbs.DamageType = (int)spell.DamageType;
            dbs.Target = "Enemy";
            dbs.Radius = 0;
            dbs.Type = "DirectDamage";
            dbs.Value = spell.Value;
            dbs.Duration = spell.ResurrectHealth;
            dbs.Frequency = spell.ResurrectMana;
            dbs.Pulse = 0;
            dbs.PulsePower = 0;
            dbs.LifeDrainReturn = spell.LifeDrainReturn;
            dbs.Power = 0;
            dbs.CastTime = 0;
            dbs.Range = WorldMgr.VISIBILITY_DISTANCE;
            sRadius = 350;
            s = new Spell(dbs, 1);
            sl = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            trap = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Convoker.DissonanceTrap.MainDescription", Spell.Damage, LanguageMgr.GetDamageOfType(delveClient, Spell.DamageType));

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
    #region Convoker-8
    [SpellHandler("BrittleGuard")]
    public class BrittleGuardSpellHandler : MasterlevelHandling
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        GameNPC summoned = null;
        GameSpellEffect beffect = null;
        public BrittleGuardSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {

        }

        /// <summary>
        /// called after normal spell cast is completed and effect has to be started
        /// </summary>
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= CalculatePowerCost(target);
            base.FinishSpellCast(target, force);
        }

        /// <summary>
        /// Apply effect on target or do spell action if non duration spell
        /// </summary>
        /// <param name="target">target that gets the effect</param>
        /// <param name="effectiveness">factor from 0..1 (0%-100%)</param>
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            GamePlayer player = Caster as GamePlayer;
            if (player == null)
            {
                return false;
            }

            INpcTemplate template = NpcTemplateMgr.GetTemplate(Spell.LifeDrainReturn);
            if (template == null)
            {
                if (log.IsWarnEnabled)
                    log.WarnFormat("NPC template {0} not found! Spell: {1}", Spell.LifeDrainReturn, Spell.ToString());
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Convoker.TemplateNotFound", Spell.LifeDrainReturn), eChatType.CT_System);
                return false;
            }

            beffect = CreateSpellEffect(target, effectiveness);

            var summonloc = GameMath.GetPointFromHeading(target, 64);

            BrittleBrain controlledBrain = new BrittleBrain(player);
            controlledBrain.IsMainPet = false;
            summoned = new GameNPC(template);
            summoned.SetOwnBrain(controlledBrain);
            summoned.Position = summoned.Position.TurnedAround() + Vector.Create(target.Orientation, length: 64);
            summoned.Realm = target.Realm;
            summoned.Level = 1;
            summoned.Size = 10;
            summoned.AddToWorld();
            controlledBrain.AggressionState = eAggressionState.Passive;
            GameEventMgr.AddHandler(summoned, GameLivingEvent.Dying, new DOLEventHandler(GuardDie));
            beffect.Start(Caster);
            return true;
        }
        private void GuardDie(DOLEvent e, object sender, EventArgs args)
        {
            GameNPC bguard = sender as GameNPC;
            if (bguard == summoned)
            {
                GameEventMgr.RemoveHandler(summoned, GameLivingEvent.Dying, new DOLEventHandler(GuardDie));
                beffect.Cancel(false);
            }
        }
        /// <summary>
        /// When an applied effect expires.
        /// Duration spells only.
        /// </summary>
        /// <param name="effect">The expired effect</param>
        /// <param name="noMessages">true, when no messages should be sent to player and surrounding</param>
        /// <returns>immunity duration in milliseconds</returns>
        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            if (summoned != null)
            {
                summoned.Health = 0; // to send proper remove packet
                summoned.Delete();
            }
            return base.OnEffectExpires(effect, noMessages);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.Convoker.BrittleGuard.MainDescription1");
            string mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.Convoker.BrittleGuard.MainDescription2");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc1 + "\n\n" + mainDesc2 + "\n\n" + secondDesc;
            }

            return mainDesc1 + "\n\n" + mainDesc2;
        }
    }
    #endregion

    //no shared timer
    #region Convoker-9
    [SpellHandlerAttribute("SummonMastery")]
    public class Convoker9Handler : MasterlevelHandling
    //public class Convoker9Handler : MasterlevelBuffHandling
    {
        private GameNPC m_living;
        private GamePlayer m_player;

        //public override eProperty Property1 { get { return eProperty.MeleeDamage; } }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            foreach (JuggernautEffect jg in target.EffectList.GetAllOfType<JuggernautEffect>())
            {
                if (jg != null)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Convoker9.PetAlreadyHasAbility"), eChatType.CT_SpellResisted);
                    return false;
                }
            }

            // Add byNefa 04.02.2011 13:35
            // Check if Necro try to use ML9 Convoker at own Pet
            if (m_player != null && m_player.CharacterClass.ID == (int)eCharacterClass.Necromancer)
            { // Caster is a Necro
                NecromancerPet necroPet = target as NecromancerPet;
                if (necroPet == null || necroPet.Owner == m_player)
                { // Caster is a Nekro and his Target is his Own Pet
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Convoker9.NecroCannotUseOnOwnPet"), eChatType.CT_SpellResisted);
                    return false;
                }
            }
            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            m_living = m_player.ControlledBrain.Body;
            m_living.Level += 20;
            m_living.BaseBuffBonusCategory[(int)eProperty.MeleeDamage] += 275;
            m_living.BaseBuffBonusCategory[(int)eProperty.ArmorAbsorption] += 75;
            m_living.Size += 40;
            base.OnEffectStart(effect);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            m_living.Level -= 20;
            m_living.BaseBuffBonusCategory[(int)eProperty.MeleeDamage] -= 275;
            m_living.BaseBuffBonusCategory[(int)eProperty.ArmorAbsorption] -= 75;
            m_living.Size -= 40;
            return base.OnEffectExpires(effect, noMessages);
        }

        public Convoker9Handler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line)
        {
            m_player = caster as GamePlayer;
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Convoker.SummonMastery.MainDescription");

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
    #region Convoker-10
    [SpellHandler("SummonTitan")]
    public class Convoker10SpellHandler : MasterlevelHandling
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        private Position position = Position.Nowhere;
        GameNPC summoned = null;
        RegionTimer m_growTimer;
        private const int C_GROWTIMER = 2000;

        public Convoker10SpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            if (!CheckCastCoordinate())
                return false;
            return base.CheckBeginCast(selectedTarget, quiet);
        }

        /// <summary>
        /// called after normal spell cast is completed and effect has to be started
        /// </summary>
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= CalculatePowerCost(target);
            base.FinishSpellCast(target, force);
        }

        /// <summary>
        /// Apply effect on target or do spell action if non duration spell
        /// </summary>
        /// <param name="target">target that gets the effect</param>
        /// <param name="effectiveness">factor from 0..1 (0%-100%)</param>
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            GamePlayer player = Caster as GamePlayer;
            if (player == null)
            {
                return false;
            }

            INpcTemplate template = NpcTemplateMgr.GetTemplate(Spell.LifeDrainReturn);
            if (template == null)
            {
                if (log.IsWarnEnabled)
                    log.WarnFormat("NPC template {0} not found! Spell: {1}", Spell.LifeDrainReturn, Spell.ToString());
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Convoker.TemplateNotFound", Spell.LifeDrainReturn), eChatType.CT_System);
                return false;
            }
            GameSpellEffect effect = CreateSpellEffect(target, effectiveness);
            TitanBrain controlledBrain = new TitanBrain(player);
            controlledBrain.IsMainPet = false;
            controlledBrain.WalkState = eWalkState.Stay;
            summoned = new GameNPC(template);
            summoned.SetOwnBrain(controlledBrain);
            //Suncheck:
            //	Is needed, else it can cause error (i.e. /cast-command)
            if (position == Position.Nowhere) CheckCastCoordinate();

            summoned.Position = position.With(orientation: Caster.Orientation + Angle.Degrees(180));
            summoned.Realm = player.Realm;
            summoned.Size = 10;
            summoned.Level = 100;
            summoned.Flags |= GameNPC.eFlags.PEACE;
            summoned.AddToWorld();
            controlledBrain.AggressionState = eAggressionState.Aggressive;
            effect.Start(summoned);
            m_growTimer = new RegionTimer((GameObject)m_caster, new RegionTimerCallback(TitanGrows), C_GROWTIMER);
            return false;
        }

        // Make titan growing, and activate it on completition
        private int TitanGrows(RegionTimer timer)
        {
            if (summoned != null && summoned.Size != 60)
            {
                summoned.Size += 10;
                return C_GROWTIMER;
            }
            else
            {
                summoned!.Flags = 0;
                m_growTimer.Stop();
                m_growTimer = null;
            }
            return 0;
        }

        private bool CheckCastCoordinate()
        {
            position = Caster.Position;
            if (Spell.Target.ToLower() == "area")
            {
                if (Caster.GroundTargetInView && Caster.GroundTargetPosition != Position.Nowhere)
                {
                    position = Caster.GroundTargetPosition;
                }
                else
                {
                    if (Caster.GroundTargetPosition == Position.Nowhere)
                    {
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Convoker.MustSetGroundTarget"), eChatType.CT_SpellResisted);
                        return false;
                    }
                    else
                    {
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Convoker.AreaTargetNotInView"), eChatType.CT_SpellResisted);
                        return false;
                    }
                }
            }
            return true;
        }
        
        /// <summary>
        /// When an applied effect expires.
        /// Duration spells only.
        /// </summary>
        /// <param name="effect">The expired effect</param>
        /// <param name="noMessages">true, when no messages should be sent to player and surrounding</param>
        /// <returns>immunity duration in milliseconds</returns>
        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            effect.Owner.Health = 0; // to send proper remove packet
            effect.Owner.Delete();
            return 0;
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.Convoker.SummonTitan.MainDescription1");
            string mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.Convoker.SummonTitan.MainDescription2");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc1 + "\n\n" + mainDesc2 + "\n\n" + secondDesc;
            }

            return mainDesc1 + "\n\n" + mainDesc2;
        }
    }
    #endregion
}

#region BrittleBrain
namespace DOL.AI.Brain
{
    public class BrittleBrain : ControlledNpcBrain
    {
        public BrittleBrain(GameLiving owner)
            : base(owner)
        {
            if (owner == null)
                throw new ArgumentNullException("owner");
        }

        public override void FollowOwner()
        {
            Body.StopAttack();
            Body.Follow(Owner, MIN_OWNER_FOLLOW_DIST, MAX_OWNER_FOLLOW_DIST);
        }
    }
}
#endregion

#region Titanbrain

namespace DOL.AI.Brain
{
    public class TitanBrain : ControlledNpcBrain
    {
        private GameLiving m_target;

        public TitanBrain(GameLiving owner)
            : base(owner)
        {
        }

        public override int ThinkInterval
        {
            get { return 2000; }
        }

        public GameLiving Target
        {
            get { return m_target; }
            set { m_target = value; }
        }

        #region AI

        public override bool Start()
        {
            if (!base.Start()) return false;
            return true;
        }

        public override bool Stop()
        {
            if (!base.Stop()) return false;
            return true;
        }

        private IList FindTarget()
        {
            ArrayList list = new ArrayList();

            foreach (GamePlayer o in Body.GetPlayersInRadius((ushort)Body.AttackRange))
            {
                GamePlayer p = o as GamePlayer;

                if (GameServer.ServerRules.IsAllowedToAttack(Body, p, true))
                    list.Add(p);
            }
            return list;
        }

        public override void Think()
        {
            if (Body.TargetObject is GameNPC)
                Body.TargetObject = null;

            if (Body.AttackState)
                return;

            IList enemies = new ArrayList();
            if (Target == null)
                enemies = FindTarget();
            else if (!Body.IsWithinRadius(Target, Body.AttackRange))
                enemies = FindTarget();
            else if (!Target.IsAlive)
                enemies = FindTarget();
            if (enemies.Count > 0 && Target == null)
            {
                //pick a random target...
                int targetnum = Util.Random(0, enemies.Count - 1);

                //Choose a random target.
                Target = enemies[targetnum] as GameLiving;
            }
            else if (enemies.Count < 1)
            {
                WalkState = eWalkState.Stay;
                enemies = FindTarget();
            }

            if (Target != null)
            {
                if (!Target.IsAlive)
                {
                    Target = null;
                }
                else if (Body.IsWithinRadius(Target, Body.AttackRange))
                {
                    Body.TargetObject = Target;
                    Goto(Target);
                    Body.StartAttack(Target);
                }
                else
                {
                    Target = null;
                }
            }
        }
        #endregion
    }
}
#endregion

#region MLBrain
public class MLBrain : GuardBrain
{
    public MLBrain() : base()
    {
        AggroRange = 400;
    }
    
    protected override void CheckNPCAggro()
    {
        if (HasAggro) return;

        foreach (GameNPC npc in Body.GetNPCsInRadius((ushort)AggroRange))
        {
            if (m_aggroTable.ContainsKey(npc))
                continue; // add only new npcs
            if (npc.IsFlying)
                continue; // let's not try to attack flying mobs
            if (!GameServer.ServerRules.IsAllowedToAttack(Body, npc, true))
                continue;
            if (!npc.IsWithinRadius(Body, AggroRange))
                continue;

            AddToAggroList(npc, 1);
            return;
        }
    }
}
#endregion
