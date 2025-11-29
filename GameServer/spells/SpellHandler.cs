/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;

using System.Text;
using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.GS.RealmAbilities;
using DOL.GS.SkillHandler;
using DOL.GS.Utils;
using DOL.Language;

using log4net;
using DOL.gameobjects.CustomNPC;
using System.Numerics;
using static DOL.GS.GameTimer;
using DOL.GS.Scripts;
using DOL.GS.ServerRules;
using DOL.GS.Styles;
using DOL.Territories;
using static Grpc.Core.Metadata;
using System.Collections;
using DOL.GS.PlayerClass;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Default class for spell handler
    /// should be used as a base class for spell handler
    /// </summary>
    public class SpellHandler : ISpellHandler
    {
        public static class OccultistForms
        {
            public const string KEY_SPIRIT = "OCCULTIST_FORM_SPIRIT";
            public const string KEY_DECREPIT = "OCCULTIST_FORM_DECREPIT";
            public const string KEY_CHTONIC = "OCCULTIST_FORM_CHTONIC";
            public const string KEY_COS = "OCCULTIST_FORM_CALL_OF_SHADOWS";

            public const string PET_BASE_TPL = "OCCULT_PET_BASE_TPL";
            public const string PET_SPIRIT_TPL = "OCCULT_PET_SPIRIT_TPL";
            public const string PET_SUMMON_SPELL_ID = "OCCULT_PET_SUMMON_SPELL_ID";

            private static void PlaySwapFx(GamePet pet)
            {
                var list = pet.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE);
                if (list == null) return;

                foreach (var o in list)
                {
                    if (o is GamePlayer pl)
                        pl.Out.SendSpellEffectAnimation(pet, pet, 10184, 0, false, 1);
                }
            }

            public static void ApplyTemplate(GamePet pet, int tplId)
            {
                if (pet == null || tplId <= 0) return;
                var tpl = NpcTemplateMgr.GetTemplate(tplId);
                if (tpl == null) return;

                if (ushort.TryParse(tpl.Model, out var m) && m > 0) pet.Model = m;
                if (!string.IsNullOrWhiteSpace(tpl.Name)) pet.Name = tpl.Name;
                if (byte.TryParse(tpl.Size, out var s) && s > 0) pet.Size = s;

                pet.MeleeDamageType = tpl.MeleeDamageType;
                pet.Race = (short)tpl.Race;
                pet.BodyType = (ushort)tpl.BodyType;
                pet.VisibleActiveWeaponSlots = tpl.VisibleActiveWeaponSlot;
                pet.ParryChance = tpl.ParryChance;
                pet.EvadeChance = tpl.EvadeChance;
                pet.BlockChance = tpl.BlockChance;
                pet.LeftHandSwingChance = tpl.LeftHandSwingChance;
                pet.Flags = (GameNPC.eFlags)tpl.Flags;

                try
                {
                    pet.Styles = tpl.Styles != null ? new List<Style>(tpl.Styles) : new List<Style>();
                    pet.Spells = tpl.Spells != null ? new ArrayList(tpl.Spells) : new ArrayList();
                    ApplyAbilitiesFromTemplate(pet, tpl);

                    if (pet.Brain is ControlledNpcBrain cb)
                        cb.CheckSpells(StandardMobBrain.eCheckSpellType.Defensive);

                    pet.SetPetLevel();
                    pet.AutoSetStats();

                    var owner = pet.GetPlayerOwner();
                    if (owner != null) pet.SortSpells(owner.Level);

                    owner?.Out?.SendObjectUpdate(pet);
                    owner?.Out?.SendLivingEquipmentUpdate(pet);
                    if (owner != null && pet.Brain is IControlledBrain brain)
                        owner.Out.SendPetWindow(pet, ePetWindowAction.Update, brain.AggressionState, brain.WalkState);
                }
                catch { }

                PlaySwapFx(pet);
            }

            static void ApplyAbilitiesFromTemplate(GamePet pet, NpcTemplate tpl)
            {
                try
                {
                    var dict = pet.Abilities as IDictionary;
                    if (dict != null && dict.Count > 0)
                    {
                        var toRemove = new List<string>(dict.Count);
                        foreach (Ability a in dict.Values)
                            if (a != null && !string.IsNullOrEmpty(a.KeyName))
                                toRemove.Add(a.KeyName);

                        foreach (var key in toRemove)
                            pet.RemoveAbility(key);
                    }

                    if (tpl?.Abilities != null)
                    {
                        foreach (Ability abil in tpl.Abilities)
                        {
                            if (abil == null) continue;
                            var resolved = SkillBase.GetAbility(abil.Name, abil.Level);
                            pet.AddAbility(resolved ?? abil);
                        }
                    }
                }
                catch { }
            }

            public static void ForEachControlledPet(GamePlayer owner, Action<GamePet> action)
            {
                if (owner?.ControlledBrain?.Body is GamePet main && main.IsAlive)
                    action(main);

                var mainBody = owner?.ControlledBrain?.Body;
                if (mainBody?.ControlledNpcList != null)
                {
                    lock (mainBody.ControlledNpcList)
                        foreach (var b in mainBody.ControlledNpcList)
                            if (b?.Body is GamePet sub && sub.IsAlive)
                                action(sub);
                }
            }

            public static void ApplyTemplateFromPetMemory(GamePet pet, bool toSpirit)
            {
                if (pet == null) return;

                int baseTpl = pet.TempProperties.getProperty<int>(PET_BASE_TPL, 0);
                int spiritTpl = pet.TempProperties.getProperty<int>(PET_SPIRIT_TPL, 0);
                int which = toSpirit ? spiritTpl : baseTpl;

                if (which <= 0)
                    return;

                ApplyTemplate(pet, which);

                var tpl = NpcTemplateMgr.GetTemplate(which);
                if (tpl != null)
                {
                    if (ushort.TryParse(tpl.Model, out var m) && m > 0) pet.Model = m;
                    if (!string.IsNullOrWhiteSpace(tpl.Name)) pet.Name = tpl.Name;
                    if (byte.TryParse(tpl.Size, out var s) && s > 0) pet.Size = s;
                }

                int summonId = pet.TempProperties.getProperty<int>(PET_SUMMON_SPELL_ID, 0);
                bool isUmbralHulk = (summonId == 25161);

                if (toSpirit)
                {
                    static ushort MulU16(ushort v, double factor) => (ushort)Math.Min(ushort.MaxValue, Math.Round(v * factor));

                    static short MulI16(short v, double factor)
                    {
                        double res = Math.Round(v * factor);
                        if (res < 0) res = 0;
                        if (res > short.MaxValue) res = short.MaxValue;
                        return (short)res;
                    }

                    if (isUmbralHulk)
                    {
                        pet.ArmorAbsorb = MulU16((ushort)pet.ArmorAbsorb, 1.60);
                        pet.ArmorFactor = MulU16((ushort)pet.ArmorFactor, 2.00);
                        pet.Strength = MulI16(pet.Strength, 2.00);
                        pet.Constitution = MulI16(pet.Constitution, 1.25);
                        pet.WeaponDps = (ushort)Math.Max(1, Math.Round(pet.WeaponDps * 2.25));
                    }
                    else
                    {
                        pet.ArmorFactor = MulU16((ushort)pet.ArmorFactor, 1.25);
                        pet.ArmorAbsorb = MulU16((ushort)pet.ArmorAbsorb, 1.20);
                    }
                }
            }

            public static void SetOccultistPetForm(GamePlayer owner, bool toSpirit)
            {
                if (owner == null) return;
                ForEachControlledPet(owner, pet => ApplyTemplateFromPetMemory(pet, toSpirit));
            }

            public static bool IsSpiritLikeActive(GameLiving l)
            {
                if (l == null) return false;
                return l.TempProperties.getProperty<bool>(KEY_SPIRIT, false)
                    || l.TempProperties.getProperty<bool>(KEY_COS, false);
            }
        }

        public static class OccultistCastingRules
        {
            private static readonly HashSet<int> OnlyChtonicSpellIDs = new() { 25136, 25137 }; // Icebrand, Nethersbane
            private static readonly HashSet<int> ForbiddenInChtonicIDs = new() { 25075, 25076, 25077, 25078, 25079, 25259, 25260, 25261, 25264, 25265, 25191, 25192, 25193, 25194, 25195, 25159, 25160, 25161, 25162, 25163 };
            private static readonly HashSet<int> ForbiddenInDecrepitSpiritIDs = new() { 25206, 25207, 25208, 25209, 25210 };

            private static bool IsOccultist(GamePlayer p) => p?.CharacterClass is ClassOccultist;

            private static bool IsChtonic(GameLiving l) => l?.TempProperties.getProperty<bool>(OccultistForms.KEY_CHTONIC, false) == true;
            private static bool IsSpirit(GameLiving l) => l?.TempProperties.getProperty<bool>(OccultistForms.KEY_SPIRIT, false) == true;
            private static bool IsDecrepit(GameLiving l) => l?.TempProperties.getProperty<bool>(OccultistForms.KEY_DECREPIT, false) == true;

            /// <summary>
            /// Centralized “may I cast this?” logic for Occultist + shapeshift.
            /// Return true if the cast MUST be blocked (with a human-readable reason).
            /// </summary>
            public static bool TryBlockCast(GamePlayer player, Spell spell, out string reason)
            {
                reason = null;
                if (player == null || spell == null) return false;
                if (!IsOccultist(player)) return false;

                bool inChtonic = IsChtonic(player);
                bool inDecreptitOrSpirit = IsSpirit(player) || IsDecrepit(player);
                bool isHuman = !IsChtonic(player) && !IsDecrepit(player) && !IsChtonic(player);

                if (OnlyChtonicSpellIDs.Contains(spell.ID))
                {
                    if (!inChtonic)
                    {
                        reason = LanguageMgr.GetTranslation(player, "SpellHandler.Occultist.CastCondition1");
                        return true;
                    }
                    return false;
                }

                if (inChtonic)
                {
                    if (ForbiddenInChtonicIDs.Contains(spell.ID))
                    {
                        reason = LanguageMgr.GetTranslation(player, "SpellHandler.Occultist.CastCondition2");
                        return true;
                    }
                }

                if (inDecreptitOrSpirit || isHuman)
                {
                    if (ForbiddenInDecrepitSpiritIDs.Contains(spell.ID))
                    {
                        reason = LanguageMgr.GetTranslation(player, "SpellHandler.Occultist.CastCondition3");
                        return true;
                    }
                }

                return false;
            }
        }

        private static readonly ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        /// <summary>
        /// Maximum number of sub-spells to get delve info for.
        /// </summary>
        protected static readonly byte MAX_DELVE_RECURSION = 5;

        protected DelayedCastTimer m_castTimer;
        /// <summary>
        /// The spell that we want to handle
        /// </summary>
        protected Spell m_spell;
        /// <summary>
        /// The spell line the spell belongs to
        /// </summary>
        protected SpellLine m_spellLine;
        /// <summary>
        /// The caster of the spell
        /// </summary>
        protected GameLiving m_caster;
        /// <summary>
        /// The target for this spell
        /// </summary>
        protected GameLiving m_spellTarget = null;

        public GameLiving Target { get => m_spellTarget;  }

        public enum eStatus
        {
            Ready,
            Interrupted,
            Casting,
            Failure,
            Success, // Consume items
            Pass // Success, but does not consume items, i.e. cancelling pulse
        }

        public eStatus Status { get; protected set; }

        /// <summary>
        /// Has the spell been interrupted
        /// </summary>
        public bool Interrupted => Status == eStatus.Interrupted;

        /// <summary>
        /// Delayedcast Stage
        /// </summary>
        public int Stage
        {
            get { return m_stage; }
            set { m_stage = value; }
        }
        protected int m_stage = 0;
        /// <summary>
        /// Use to store Time when the delayedcast started
        /// </summary>
        protected long m_started = 0;
        /// <summary>
        /// Shall we start the reuse timer
        /// </summary>
        protected bool m_startReuseTimer = true;

        public bool StartReuseTimer
        {
            get { return m_startReuseTimer; }
        }

        /// <summary>
        /// Can this spell be queued with other spells?
        /// </summary>
        public virtual bool CanQueue
        {
            get { return true; }
        }

        /// <summary>
        /// Does this spell break stealth on start of cast?
        /// </summary>
        public virtual bool UnstealthCasterOnStart
        {
            get { return true; }
        }

        /// <summary>
        /// Does this spell break stealth on Finish of cast?
        /// </summary>
        public virtual bool UnstealthCasterOnFinish
        {
            get { return true; }
        }

        protected InventoryItem m_spellItem = null;

        public InventoryItem Item
        {
            get => m_spellItem;
        }

        /// <summary>
        /// Ability that casts a spell
        /// </summary>
        protected ISpellCastingAbilityHandler m_ability = null;

        /// <summary>
        /// Stores the current delve info depth
        /// </summary>
        private byte m_delveInfoDepth;

        /// <summary>
        /// AttackData result for this spell, if any
        /// </summary>
        protected AttackData m_lastAttackData = null;
        /// <summary>
        /// AttackData result for this spell, if any
        /// </summary>
        public AttackData LastAttackData
        {
            get { return m_lastAttackData; }
        }

        /// <summary>
        /// The property key for the interrupt timeout
        /// </summary>
        public const string INTERRUPT_TIMEOUT_PROPERTY = "CAST_INTERRUPT_TIMEOUT";
        /// <summary>
        /// The property key for focus spells
        /// </summary>
        protected const string FOCUS_SPELL = "FOCUSING_A_SPELL";

        protected bool m_ignoreDamageCap = false;

        /// <summary>
        /// Does this spell ignore any damage cap?
        /// </summary>
        public bool IgnoreDamageCap
        {
            get { return m_ignoreDamageCap; }
            set { m_ignoreDamageCap = value; }
        }

        protected bool m_useMinVariance = false;

        /// <summary>
        /// Should this spell use the minimum variance for the type?
        /// Followup style effects, for example, always use the minimum variance
        /// </summary>
        public bool UseMinVariance
        {
            get { return m_useMinVariance; }
            set { m_useMinVariance = value; }
        }

        /// <summary>
        /// Can this SpellHandler Coexist with other Overwritable Spell Effect
        /// </summary>
        public virtual bool AllowCoexisting
        {
            get { return Spell.AllowCoexisting; }
        }

        public bool CastSubSpellsWithSpell { get; protected set; } = true;

        /// <summary>
        /// The CastingCompleteEvent
        /// </summary>
        public event CastingCompleteCallback CastingCompleteEvent;
        //public event SpellEndsCallback SpellEndsEvent;

        /// <summary>
        /// spell handler constructor
        /// <param name="caster">living that is casting that spell</param>
        /// <param name="spell">the spell to cast</param>
        /// <param name="spellLine">the spell line that spell belongs to</param>
        /// </summary>
        public SpellHandler(GameLiving caster, Spell spell, SpellLine spellLine)
        {
            m_caster = caster;
            m_spell = spell;
            m_spellLine = spellLine;
        }

        /// <summary>
        /// Returns the string representation of the SpellHandler
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return new StringBuilder(128)
                .Append("Caster=").Append(Caster == null ? "(null)" : Caster.Name)
                .Append(", IsCasting=").Append(IsCasting)
                .Append(", Interrupted=").Append(Interrupted)
                .Append("\nSpell: ").Append(Spell == null ? "(null)" : Spell.ToString())
                .Append("\nSpellLine: ").Append(SpellLine == null ? "(null)" : SpellLine.ToString())
                .ToString();
        }

        #region Pulsing Spells

        /// <summary>
        /// When spell pulses
        /// </summary>
        public virtual void OnSpellPulse(PulsingSpellEffect effect)
        {
            if (Caster.IsMoving && Spell.IsFocus)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.PulsingSpellCancelled"), eChatType.CT_SpellExpires);
                effect.Cancel(false);
                return;
            }
            if (Caster.IsAlive == false)
            {
                effect.Cancel(false);
                return;
            }
            if (Caster.ObjectState != GameObject.eObjectState.Active)
                return;
            if (Caster.IsStunned || Caster.IsMezzed)
                return;

            // no instrument anymore = stop the song
            if (m_spell.InstrumentRequirement != 0 && !CheckInstrument())
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.StopPlayingSong"), eChatType.CT_Spell);
                effect.Cancel(false);
                return;
            }

            if (Caster.Mana >= Spell.PulsePower)
            {
                Caster.Mana -= Spell.PulsePower;
                if (Spell.InstrumentRequirement != 0 || !HasPositiveEffect)
                {
                    SendEffectAnimation(Caster, 0, true, 1); // pulsing auras or songs
                }

                StartSpell(m_spellTarget);
            }
            else
            {
                if (Spell.IsFocus)
                {
                    FocusSpellAction(null, Caster, null);
                }
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.PulsingSpellNoMana"), eChatType.CT_SpellExpires);
                effect.Cancel(false);
            }
        }

        /// <summary>
        /// Checks if caster holds the right instrument for this spell
        /// </summary>
        /// <returns>true if right instrument</returns>
        protected bool CheckInstrument()
        {
            InventoryItem instrument = Caster.AttackWeapon;
            // From patch 1.97:  Flutes, Lutes, and Drums will now be able to play any song type, and will no longer be limited to specific songs.
            if (instrument == null || instrument.Object_Type != (int)eObjectType.Instrument) // || (instrument.DPS_AF != 4 && instrument.DPS_AF != m_spell.InstrumentRequirement))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Cancels first pulsing spell of type
        /// </summary>
        /// <param name="living">owner of pulsing spell</param>
        /// <param name="spellType">type of spell to cancel</param>
        /// <returns>true if any spells were canceled</returns>
        public virtual PulsingSpellEffect? CancelPulsingSpell(GameLiving living, string spellType)
        {
            lock (living.ConcentrationEffects)
            {
                for (int i = 0; i < living.ConcentrationEffects.Count; i++)
                {
                    PulsingSpellEffect effect = living.ConcentrationEffects[i] as PulsingSpellEffect;
                    if (effect == null)
                        continue;

                    if (effect.SpellHandler.Spell.SpellType == spellType)
                    {
                        if (Caster is GamePlayer player)
                            player.PulseSpell = null;
                        effect.Cancel(false);
                        return effect;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Cancels all pulsing spells
        /// </summary>
        /// <param name="living"></param>
        public static void CancelAllPulsingSpells(GameLiving living)
        {
            List<IConcentrationEffect> pulsingSpells = new List<IConcentrationEffect>();

            GamePlayer player = living as GamePlayer;

            lock (living.ConcentrationEffects)
            {
                for (int i = 0; i < living.ConcentrationEffects.Count; i++)
                {
                    PulsingSpellEffect effect = living.ConcentrationEffects[i] as PulsingSpellEffect;
                    if (effect == null)
                        continue;

                    if (player != null && player.CharacterClass.MaxPulsingSpells > 1)
                        pulsingSpells.Add(effect);
                    else
                        effect.Cancel(false);
                }
            }

            // Non-concentration spells are grouped at the end of GameLiving.ConcentrationEffects.
            // The first one is added at the very end; successive additions are inserted just before the last element
            // which results in the following ordering:
            // Assume pulsing spells A, B, C, and D were added in that order; X, Y, and Z represent other spells
            // ConcentrationEffects = { X, Y, Z, ..., B, C, D, A }
            // If there are only ever 2 or less pulsing spells active, then the oldest one will always be at the end.
            // However, if an update or modification allows more than 2 to be active, the goofy ordering of the spells
            // will prevent us from knowing which spell is the oldest and should be canceled - we can go ahead and simply
            // cancel the last spell in the list (which will result in inconsistent behavior) or change the code that adds
            // spells to ConcentrationEffects so that it enforces predictable ordering.
            if (pulsingSpells.Count > 1 && (player == null || pulsingSpells.Count >= player.CharacterClass.MaxPulsingSpells))
            {
                pulsingSpells[pulsingSpells.Count - 1].Cancel(false);
            }
        }

        #endregion

        /// <summary>
        /// Sets the target of the spell to the caster for beneficial effects when not selecting a valid target
        ///		ie. You're in the middle of a fight with a mob and want to heal yourself.  Rather than having to switch
        ///		targets to yourself to healm and then back to the target, you can just heal yourself
        /// </summary>
        /// <param name="target">The current target of the spell, changed to the player if appropriate</param>
        protected virtual void AutoSelectCaster(ref GameLiving target)
        {
            GameNPC npc = target as GameNPC;
            if (Spell.Target.ToUpper() == "REALM" && Caster is GamePlayer &&
                (npc == null || npc.Realm != Caster.Realm || npc.IsPeaceful))
                target = Caster;
        }

        /// <summary>
        /// Cast a spell by using an item
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public virtual bool CastSpell(InventoryItem item)
        {
            m_spellItem = item;
            return CastSpell(Caster.TargetObject as GameLiving);
        }

        /// <summary>
        /// Cast a spell by using an Item
        /// </summary>
        /// <param name="targetObject"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public virtual bool CastSpell(GameLiving targetObject, InventoryItem item)
        {
            m_spellItem = item;
            return CastSpell(targetObject);
        }

        /// <summary>
        /// called whenever the player clicks on a spell icon
        /// or a GameLiving wants to cast a spell
        /// </summary>
        public virtual bool CastSpell()
        {
            return CastSpell(Caster.TargetObject as GameLiving);
        }

        public virtual bool CastSpell(GameLiving targetObject)
        {
            //Disactivate AFK
            if (Caster is GamePlayer pl && pl.PlayerAfkMessage != null)
            {
                pl.ResetAFK(false);
            }

            bool success = true;

            if (Properties.AUTOSELECT_CASTER)
                AutoSelectCaster(ref targetObject);

            m_spellTarget = targetObject;

            //[Stryve]: Do not break stealth if spell can be cast without breaking stealth.
            if (UnstealthCasterOnStart)
                Caster.Stealth(false);

            if (Caster.IsEngaging)
            {
                EngageEffect effect = Caster.EffectList.GetOfType<EngageEffect>();

                if (effect != null)
                    effect.Cancel(false);
            }

            Status = eStatus.Ready;

            if (Spell.Target.ToLower() == "pet")
            {
                // Pet is the target, check if the caster is the pet.

                if (Caster is GameNPC && (Caster as GameNPC)!.Brain is IControlledBrain)
                    m_spellTarget = Caster;

                if (Caster is GamePlayer && Caster.ControlledBrain != null && Caster.ControlledBrain.Body != null)
                {
                    if (m_spellTarget == null || !Caster.IsControlledNPC(m_spellTarget as GameNPC))
                    {
                        m_spellTarget = Caster.ControlledBrain.Body;
                    }
                }
            }
            else if (Spell.Target.ToLower() == "controlled")
            {
                // Can only be issued by the owner of a pet and the target
                // is always the pet then.

                if (Caster is GamePlayer && Caster.ControlledBrain != null)
                    m_spellTarget = Caster.ControlledBrain.Body;
                else
                    m_spellTarget = null;
            }

            success = DoCastSpell(targetObject);

            // This is critical to restore the casters state and allow them to cast another spell
            if (!IsCasting)
                OnAfterSpellCastSequence();

            return success;
        }

        private bool DoCastSpell(GameLiving targetObject)
        {
            if (Spell.Pulse != 0 && !Spell.IsFocus)
            {
                var pulsing = CancelPulsingSpell(Caster, Spell.SpellType);
                if (pulsing != null)
                {
                    if (Spell.InstrumentRequirement == 0)
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.CancelEffect"), eChatType.CT_Spell);
                    else
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.StopPlayingSong"), eChatType.CT_Spell);
                    Status = eStatus.Pass;
                    return true;
                }
            }

            if (!GameServer.ServerRules.IsAllowedToCastSpell(Caster, m_spellTarget, Spell, m_spellLine))
            {
                Status = eStatus.Failure;
                return false;
            }
            
            if (!CheckBeginCast(m_spellTarget))
            {
                Status = eStatus.Failure;
                return false;
            }

            if (m_caster is GamePlayer casterPlayer)
            {
                if (casterPlayer.IsOnHorse && !HasPositiveEffect)
                {
                    casterPlayer.IsOnHorse = false;
                }

                if (casterPlayer.IsSummoningMount)
                {
                    casterPlayer.SendTranslatedMessage("GameObjects.GamePlayer.UseSlot.CantMountSpell");
                    casterPlayer.IsOnHorse = false;
                }
            }

            if (Spell.Pulse != 0 && m_caster is GamePlayer player)
            {
                player.PulseSpell = this;
            }

            if (!Spell.IsInstantCast)
            {
                StartCastTimer(m_spellTarget);

                if ((Caster is GamePlayer && (Caster as GamePlayer)!.IsStrafing) || Caster.IsMoving)
                    CasterMoves();
            }
            else
            {
                if (Caster.ControlledBrain == null || Caster.ControlledBrain.Body == null || !(Caster.ControlledBrain.Body is NecromancerPet))
                {
                    SendCastAnimation(0);
                }

                Caster.Notify(GameLivingEvent.CastStarting, m_caster, new CastingEventArgs(this));

                FinishSpellCast(m_spellTarget);
            }
            return true;
        }


        public virtual void StartCastTimer(GameLiving target)
        {
            Status = eStatus.Casting;
            SendSpellMessages();

            int time = CalculateCastingTime();

            int step1 = time / 3;
            int step3 = step1;

            step1 = step1.Clamp(1, ServerProperties.Properties.SPELL_INTERRUPT_MAXSTAGELENGTH);
            step3 = step3.Clamp(1, ServerProperties.Properties.SPELL_INTERRUPT_MAXSTAGELENGTH);

            int step2 = time - step1 - step3;
            byte step2_substeps = 0;
            if (step2 > ServerProperties.Properties.SPELL_INTERRUPT_MAX_INTERMEDIATE_STAGELENGTH)
            {
                step2_substeps = (byte)((step2 / ServerProperties.Properties.SPELL_INTERRUPT_MAX_INTERMEDIATE_STAGELENGTH) + 1);
                step2 /= step2_substeps;
            }
            if (step2 < 1)
                step2 = 1;

            if (Caster is GamePlayer casterPlayer && ServerProperties.Properties.ENABLE_DEBUG)
            {
                casterPlayer.Out.SendMessage($"[DEBUG] spell time = {time}, step1 = {step1}, step2 = {step2}, step3 = {step3}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }

            m_castTimer = new DelayedCastTimer(Caster, this, target, step2, step3, step2_substeps);
            m_castTimer.Start(step1);
            m_started = Caster.CurrentRegion.Time;
            SendCastAnimation();

            Caster.Notify(GameLivingEvent.CastStarting, m_caster, new CastingEventArgs(this));

            if (m_caster.IsMoving || m_caster.IsStrafing)
            {
                CasterMoves();
            }
        }

        /// <summary>
        /// Is called when the caster moves
        /// </summary>
        public virtual void CasterMoves()
        {
            if (Spell.InstrumentRequirement != 0)
                return;

            if (Caster.TempProperties.getProperty<bool>("BOD_CAN_MOVECAST", false))
                return;

            if (Spell.MoveCast)
                return;

            InterruptCasting();
            if (Caster is GamePlayer)
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.CasterMove"), eChatType.CT_Important);
        }

        /// <summary>
        /// This sends the spell messages to the player/target.
        ///</summary>
        public virtual void SendSpellMessages()
        {
            if (Spell.InstrumentRequirement == 0)
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.BeginCasting", Spell.Name), eChatType.CT_Spell);
            else
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.BeginPlaying", Spell.Name), eChatType.CT_Spell);
        }

        /// <summary>
        /// casting sequence has a chance for interrupt through attack from enemy
        /// the final decision and the interrupt is done here
        /// TODO: con level dependend
        /// </summary>
        /// <param name="attacker">attacker that interrupts the cast sequence</param>
        /// <returns>true if casting was interrupted</returns>
        public virtual bool CasterIsAttacked(GameLiving attacker)
        {
            //[StephenxPimentel] Check if the necro has MoC effect before interrupting.
            if (Caster is NecromancerPet)
            {
                if ((Caster as NecromancerPet)!.Owner.EffectList.GetOfType<MasteryofConcentrationEffect>() != null)
                {
                    return false;
                }
            }
            if (Spell.Uninterruptible)
                return false;
            if (Caster.EffectList.CountOfType(typeof(QuickCastEffect), typeof(MasteryofConcentrationEffect), typeof(FacilitatePainworkingEffect)) > 0)
                return false;
            if (IsCasting && Stage < 2)
            {
                if (Caster.ChanceSpellInterrupt(attacker))
                {
                    string interruptMessage = LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.SpellInterrupted", attacker.GetName(0, true));
                    Caster.LastInterruptMessage = interruptMessage;
                    MessageToLiving(Caster, interruptMessage, eChatType.CT_SpellResisted);
                    InterruptCasting(); // always interrupt at the moment
                    return true;
                }
            }
            return false;
        }

        #region begin & end cast check

        public bool CheckBeginCast(GameLiving selectedTarget)
        {
            return CheckBeginCast(selectedTarget, false);
        }

        /// <summary>
        /// All checks before any casting begins
        /// </summary>
        /// <param name="selectedTarget"></param>
        /// <returns></returns>
        public virtual bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            if (m_caster.ObjectState != GameLiving.eObjectState.Active)
            {
                return false;
            }

            if (!m_caster.IsAlive)
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.DeadCantCast"), eChatType.CT_System);
                return false;
            }


            if (m_caster is GamePlayer)
            {
                long nextSpellAvailTime = m_caster.TempProperties.getProperty<long>(GamePlayer.NEXT_SPELL_AVAIL_TIME_BECAUSE_USE_POTION);

                if (nextSpellAvailTime > m_caster.CurrentRegion.Time)
                {
                    ((GamePlayer)m_caster).Out.SendMessage(LanguageMgr.GetTranslation(((GamePlayer)m_caster).Client, "GameObjects.GamePlayer.CastSpell.MustWaitBeforeCast", (nextSpellAvailTime - m_caster.CurrentRegion.Time) / 1000), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
                if (((GamePlayer)m_caster).Steed != null && ((GamePlayer)m_caster).Steed is GameSiegeRam)
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.CantCastInSiegeram"), eChatType.CT_System);
                    return false;
                }
                GameSpellEffect naturesWomb = FindEffectOnTarget(Caster, e => e is NaturesWombEffect);
                if (naturesWomb != null)
                {
                    //[StephenxPimentel]
                    //Get Correct Message for 1.108 update.
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.SilencedCantCast"), eChatType.CT_SpellResisted);
                    return false;
                }
            }

            GameSpellEffect Phaseshift = FindEffectOnTarget(Caster, "Phaseshift");
            if (Phaseshift != null && (Spell.InstrumentRequirement == 0 || Spell.SpellType == "Mesmerize"))
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.PhaseshiftedCantCast"), eChatType.CT_System);
                return false;
            }

            // Apply Mentalist RA5L
            if (Spell.Range > 0)
            {
                SelectiveBlindnessEffect SelectiveBlindness = Caster.EffectList.GetOfType<SelectiveBlindnessEffect>();
                if (SelectiveBlindness != null)
                {
                    GameLiving EffectOwner = SelectiveBlindness.EffectSource;
                    if (EffectOwner == selectedTarget)
                    {
                        if (m_caster is GamePlayer player && !quiet)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameLiving.AttackData.InvisibleToYou", selectedTarget.GetName(0, true)), eChatType.CT_Missed, eChatLoc.CL_SystemWindow);
                        }

                        return false;
                    }
                }
            }

            if (selectedTarget != null && selectedTarget.HasAbility("DamageImmunity") && Spell.SpellType == "DirectDamage" && Spell.Radius == 0)
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.DamageImmunity", m_caster.GetPersonalizedName(selectedTarget)), eChatType.CT_SpellResisted);
                return false;
            }

            if (m_spell.InstrumentRequirement != 0)
            {
                if (!CheckInstrument())
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.WrongInstrument"), eChatType.CT_SpellResisted);
                    return false;
                }
            }
            else if (m_caster.IsSitting) // songs can be played if sitting
            {
                //Purge can be cast while sitting but only if player has negative effect that
                //don't allow standing up (like stun or mez)
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.CantCastWhileSitting"), eChatType.CT_SpellResisted);
                return false;
            }

            if (m_caster.AttackState && m_spell.CastTime != 0)
            {
                if (m_caster.CanCastInCombat(Spell) == false)
                {
                    m_caster.StopAttack();
                    return false;
                }
            }

            if (!m_spell.Uninterruptible && m_spell.CastTime > 0 && m_caster is GamePlayer &&
                m_caster.EffectList.GetOfType<QuickCastEffect>() == null && m_caster.EffectList.GetOfType<MasteryofConcentrationEffect>() == null)
            {
                if (Caster.InterruptAction > 0 && Caster.InterruptAction + Caster.SpellInterruptRecastTime > Caster.CurrentRegion.Time)
                {
                    if (!quiet)
                    {
                        MessageToCaster(
                            LanguageMgr.GetTranslation(
                                (Caster as GamePlayer)?.Client,
                                "GameObjects.GamePlayer.CastSpell.MustWaitBeforeCast",
                                (((Caster.InterruptAction + Caster.SpellInterruptRecastTime) - Caster.CurrentRegion.Time) / 1000 + 1).ToString()
                            ),
                            eChatType.CT_SpellResisted
                        );
                    }
                    return false;
                }
            }

            if (m_spell.RecastDelay > 0)
            {
                int left = m_caster.GetSkillDisabledDuration(m_spell);
                if (left > 0)
                {
                    if (m_caster is NecromancerPet && ((m_caster as NecromancerPet)!.Owner as GamePlayer)!.Client.Account.PrivLevel > (int)ePrivLevel.Player)
                    {
                        // Ignore Recast Timer
                    }
                    else
                    {
                        if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.MustWaitBeforeUse", (left / 1000 + 1).ToString()), eChatType.CT_System);
                        return false;
                    }
                }
            }

            String targetType = m_spell.Target.ToLower();

            //[Ganrod] Nidel: Can cast pet spell on all Pet/Turret/Minion (our pet)
            if (targetType.Equals("pet"))
            {
                if (selectedTarget == null || !Caster.IsControlledNPC(selectedTarget as GameNPC))
                {
                    if (Caster.ControlledBrain != null && Caster.ControlledBrain.Body != null)
                    {
                        selectedTarget = Caster.ControlledBrain.Body;
                    }
                    else
                    {
                        if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MustCastOnControlled"), eChatType.CT_System);
                        return false;
                    }
                }
            }
            if (targetType == "area")
            {
                if (!m_caster.IsWithinRadius(m_caster.GroundTargetPosition, CalculateSpellRange()))
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.AreaTargetOutOfRange"), eChatType.CT_SpellResisted);
                    return false;
                }
                if (!Caster.GroundTargetInView)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.GroundTargetNotInView"), eChatType.CT_SpellResisted);
                    return false;
                }
            }
            else if (targetType != "self" && targetType != "group" && targetType != "pet"
                     && targetType != "controlled" && targetType != "cone" && m_spell.Range > 0)
            {
                // All spells that need a target.

                if (selectedTarget == null || selectedTarget.ObjectState != GameLiving.eObjectState.Active)
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MustSelectTarget"), eChatType.CT_SpellResisted);
                    return false;
                }

                if (!m_caster.IsWithinRadius(selectedTarget, CalculateSpellRange()))
                {
                    if (Caster is GamePlayer && !quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetTooFar"), eChatType.CT_SpellResisted);
                    Caster.Notify(GameLivingEvent.CastFailed,
                                  new CastFailedEventArgs(this, CastFailedEventArgs.Reasons.TargetTooFarAway));
                    return false;
                }

                switch (m_spell.Target.ToLower())
                {
                    case "enemy":
                        if (selectedTarget == m_caster)
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.CantAttackSelf"), eChatType.CT_System);
                            return false;
                        }

                        if (FindStaticEffectOnTarget(selectedTarget, typeof(NecromancerShadeEffect)) != null)
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetInvalid"), eChatType.CT_System);
                            return false;
                        }

                        if (m_spell.SpellType == "Charm" && m_spell.CastTime == 0 && m_spell.Pulse != 0)
                            break;

                        if (m_caster.IsObjectInFront(selectedTarget, 180) == false)
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetNotInView"), eChatType.CT_SpellResisted);
                            Caster.Notify(GameLivingEvent.CastFailed, new CastFailedEventArgs(this, CastFailedEventArgs.Reasons.TargetNotInView));
                            return false;
                        }

                        if (selectedTarget == m_caster.TargetObject && m_caster.TargetInView == false)
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetNotVisible"), eChatType.CT_SpellResisted);
                            Caster.Notify(GameLivingEvent.CastFailed, new CastFailedEventArgs(this, CastFailedEventArgs.Reasons.TargetNotInView));
                            return false;
                        }

                        if (!GameServer.ServerRules.IsAllowedToAttack(Caster, selectedTarget, quiet))
                        {
                            return false;
                        }
                        break;

                    case "corpse":
                        if (selectedTarget.IsAlive || !GameServer.ServerRules.IsSameRealm(Caster, selectedTarget, true))
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.OnlyWorksOnDead"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        break;

                    case "realm":
                        if (!GameServer.ServerRules.IsAllowedToHelp(Caster, selectedTarget, true))
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.OnlyWorksOnFriendlyTargets"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        if (!selectedTarget.IsAlive)
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.CantCastOnDead"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        break;
                }

                //heals/buffs/rez need LOS only to start casting, TargetInView only works if selectedTarget == TargetObject
                if (selectedTarget == Caster.TargetObject && !m_caster.TargetInView && m_spell.Target.ToLower() != "pet")
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetNotInView"), eChatType.CT_SpellResisted);
                    Caster.Notify(GameLivingEvent.CastFailed, new CastFailedEventArgs(this, CastFailedEventArgs.Reasons.TargetNotInView));
                    return false;
                }

                if (m_spell.Target.ToLower() != "corpse" && !selectedTarget.IsAlive)
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "Spell.LifeTransfer.TargetDead", selectedTarget.GetName(0, true)), eChatType.CT_SpellResisted);
                    return false;
                }
            }

            //Ryan: don't want mobs to have reductions in mana
            if (Spell.Power != 0 && m_caster is GamePlayer && (m_caster as GamePlayer)!.CharacterClass.ID != (int)eCharacterClass.Savage && m_caster.Mana < PowerCost(selectedTarget) && Spell.SpellType != "Archery")
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.NotEnoughPower"), eChatType.CT_SpellResisted);
                return false;
            }

            if (m_caster is GamePlayer && m_spell.Concentration > 0)
            {
                if (m_caster.Concentration < m_spell.Concentration)
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.SpellRequiresConcentration", m_spell.Concentration), eChatType.CT_SpellResisted);
                    return false;
                }

                if (m_caster.ConcentrationEffects.ConcSpellsCount >= 50)
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.MaxConcentrationSpells"), eChatType.CT_SpellResisted);
                    return false;
                }
            }

            // Cancel engage if user starts attack
            if (m_caster.IsEngaging)
            {
                EngageEffect engage = m_caster.EffectList.GetOfType<EngageEffect>();
                if (engage != null)
                {
                    engage.Cancel(false);
                }
            }

            if (!(Caster is GamePlayer))
            {
                Caster.Notify(GameLivingEvent.CastSucceeded, this, new PetSpellEventArgs(Spell, SpellLine, selectedTarget));
            }

            return true;
        }

        /// <summary>
        /// Does the area we are in force an LoS check on everything?
        /// </summary>
        /// <param name="living"></param>
        /// <returns></returns>
        protected bool MustCheckLOS(GameLiving living)
        {
            foreach (AbstractArea area in living.CurrentAreas)
            {
                if (area.CheckLOS)
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Check the Line of Sight from you to your pet
        /// </summary>
        /// <param name="player">The player</param>
        /// <param name="response">The result</param>
        /// <param name="targetOID">The target OID</param>
        public virtual void CheckLOSYouToPet(GamePlayer player, ushort response, ushort targetOID)
        {
            if (player == null) // Hmm
                return;
            if ((response & 0x100) == 0x100) // In view ?
                return;
            MessageToLiving(player, LanguageMgr.GetTranslation(player.Client, "SpellHandler.CheckLOSYouToPet.NotInView"), eChatType.CT_SpellResisted);
            InterruptCasting(); // break;
        }

        /// <summary>
        /// Check the Line of Sight from a player to a target
        /// </summary>
        /// <param name="player">The player</param>
        /// <param name="response">The result</param>
        /// <param name="targetOID">The target OID</param>
        public virtual void CheckLOSPlayerToTarget(GamePlayer player, ushort response, ushort targetOID)
        {
            if (player == null) // Hmm
                return;

            if ((response & 0x100) == 0x100) // In view?
                return;

            if (ServerProperties.Properties.ENABLE_DEBUG)
            {
                MessageToCaster(LanguageMgr.GetTranslation(player.Client, "SpellHandler.CheckLOSPlayerToTarget.LoSInterrupt"), eChatType.CT_System);
                log.Debug("LoS Interrupt in CheckLOSPlayerToTarget");
            }

            if (Caster is GamePlayer)
            {
                MessageToCaster(LanguageMgr.GetTranslation(player.Client, "SpellHandler.CheckLOSPlayerToTarget.CantSeeTarget"), eChatType.CT_SpellResisted);
                if (Spell.IsFocus && Spell.IsHarmful)
                {
                    FocusSpellAction(null, Caster, null);
                }
            }

            InterruptCasting();
        }

        /// <summary>
        /// Check the Line of Sight from an npc to a target
        /// </summary>
        /// <param name="player">The player</param>
        /// <param name="response">The result</param>
        /// <param name="targetOID">The target OID</param>
        public virtual void CheckLOSNPCToTarget(GamePlayer player, ushort response, ushort targetOID)
        {
            if (player == null) // Hmm
                return;

            if ((response & 0x100) == 0x100) // In view?
                return;

            if (ServerProperties.Properties.ENABLE_DEBUG)
            {
                MessageToCaster(LanguageMgr.GetTranslation(player.Client, "SpellHandler.CheckLOSNPCToTarget.LoSInterrupt"), eChatType.CT_System);
                log.Debug("LoS Interrupt in CheckLOSNPCToTarget");
            }

            InterruptCasting();
        }
        /// <summary>
        /// Checks after casting before spell is executed
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public virtual bool CheckEndCast(GameLiving target)
        {
            if (m_caster.ObjectState != GameLiving.eObjectState.Active)
            {
                return false;
            }

            if (!m_caster.IsAlive)
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.DeadCantCast"), eChatType.CT_System);
                return false;
            }

            if (m_spell.InstrumentRequirement != 0)
            {
                if (!CheckInstrument())
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.WrongInstrument"), eChatType.CT_SpellResisted);
                    return false;
                }
            }
            else if (m_caster.IsSitting) // songs can be played if sitting
            {
                //Purge can be cast while sitting but only if player has negative effect that
                //don't allow standing up (like stun or mez)
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.CantCastWhileSitting"), eChatType.CT_SpellResisted);
                return false;
            }

            if (m_spell.Target.ToLower() == "area")
            {
                if (!m_caster.IsWithinRadius(m_caster.GroundTargetPosition, CalculateSpellRange()))
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.AreaTargetOutOfRange"), eChatType.CT_SpellResisted);
                    return false;
                }
            }
            else if (m_spell.Target.ToLower() != "self" && m_spell.Target.ToLower() != "group" && m_spell.Target.ToLower() != "cone" && m_spell.Range > 0)
            {
                if (m_spell.Target.ToLower() != "pet")
                {
                    //all other spells that need a target
                    if (target == null || target.ObjectState != GameObject.eObjectState.Active)
                    {
                        if (Caster is GamePlayer)
                            MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MustSelectTarget"), eChatType.CT_SpellResisted);
                        return false;
                    }

                    if (!m_caster.IsWithinRadius(target, CalculateSpellRange()))
                    {
                        if (Caster is GamePlayer)
                            MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetTooFar"), eChatType.CT_SpellResisted);
                        return false;
                    }
                }

                switch (m_spell.Target.ToLower())
                {
                    case "enemy":
                        //enemys have to be in front and in view for targeted spells
                        if (!m_caster.IsObjectInFront(target, 180))
                        {
                            MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetNotInViewSpellFail"), eChatType.CT_SpellResisted);
                            return false;
                        }

                        if (!GameServer.ServerRules.IsAllowedToAttack(Caster, target, false))
                        {
                            return false;
                        }
                        break;

                    case "corpse":
                        if (target.IsAlive || (!(Caster is TextNPC) && !GameServer.ServerRules.IsSameRealm(Caster, target, true)))
                        {
                            MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.OnlyWorksOnDead"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        break;

                    case "realm":
                        if (!GameServer.ServerRules.IsAllowedToHelp(Caster, target, true))
                        {
                            MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.OnlyWorksOnFriendlyTargets"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        if (!target.IsAlive)
                        {
                            MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.CantCastOnDead"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        break;

                    case "pet":
                        /*
                         * [Ganrod] Nidel: Can cast pet spell on all Pet/Turret/Minion (our pet)
                         * -If caster target's isn't own pet.
                         *  -check if caster have controlled pet, select this automatically
                         *  -check if target isn't null
                         * -check if target isn't too far away
                         * If all checks isn't true, return false.
                         */
                        if (target == null || !Caster.IsControlledNPC(target as GameNPC))
                        {
                            if (Caster.ControlledBrain != null && Caster.ControlledBrain.Body != null)
                            {
                                target = Caster.ControlledBrain.Body;
                            }
                            else
                            {
                                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MustCastOnControlled"), eChatType.CT_System);
                                return false;
                            }
                        }
                        //Now check distance for own pet
                        if (!m_caster.IsWithinRadius(target, CalculateSpellRange()))
                        {
                            MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetTooFar"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        break;
                }
            }

            if (m_caster.Mana <= 0 && Spell.Power != 0 && Spell.SpellType != "Archery")
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.NoMoreMana"), eChatType.CT_SpellResisted);
                return false;
            }
            if (Spell.Power != 0 && m_caster.Mana < PowerCost(target) && Spell.SpellType != "Archery")
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.NotEnoughPower"), eChatType.CT_SpellResisted);
                return false;
            }

            if (m_caster is GamePlayer && m_spell.Concentration > 0 && m_caster.Concentration < m_spell.Concentration)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.SpellRequiresConcentration", m_spell.Concentration), eChatType.CT_SpellResisted);
                return false;
            }

            if (m_caster is GamePlayer && m_spell.Concentration > 0 && m_caster.ConcentrationEffects.ConcSpellsCount >= 50)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MaxConcentrationSpells"), eChatType.CT_SpellResisted);
                return false;
            }

            return true;
        }

        public bool CheckDuringCast(GameLiving target)
        {
            return CheckDuringCast(target, false);
        }

        public virtual bool CheckDuringCast(GameLiving target, bool quiet)
        {
            if (Interrupted)
            {
                return false;
            }

            if (!m_spell.Uninterruptible && m_spell.CastTime > 0 && m_caster is GamePlayer &&
                m_caster.EffectList.GetOfType<QuickCastEffect>() == null && m_caster.EffectList.GetOfType<MasteryofConcentrationEffect>() == null)
            {
                if (Caster.InterruptTime > 0 && Caster.InterruptTime > m_started)
                {
                    if (!quiet)
                    {
                        if (Caster.LastInterruptMessage != "")
                            MessageToCaster(Caster.LastInterruptMessage, eChatType.CT_SpellResisted);
                        else
                            MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.InterruptWaitBeforeCast", ((Caster.InterruptTime - m_started) / 1000 + 1).ToString()), eChatType.CT_SpellResisted);
                    }
                    return false;
                }
            }

            if (m_caster.ObjectState != GameLiving.eObjectState.Active)
            {
                return false;
            }

            if (!m_caster.IsAlive)
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.DeadCantCast"), eChatType.CT_System);
                return false;
            }

            if (m_spell.InstrumentRequirement != 0)
            {
                if (!CheckInstrument())
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.WrongInstrument"), eChatType.CT_SpellResisted);
                    return false;
                }
            }
            else if (m_caster.IsSitting) // songs can be played if sitting
            {
                //Purge can be cast while sitting but only if player has negative effect that
                //don't allow standing up (like stun or mez)
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.CantCastWhileSitting"), eChatType.CT_SpellResisted);
                return false;
            }

            if (m_spell.Target.ToLower() == "area")
            {
                if (!m_caster.IsWithinRadius(m_caster.GroundTargetPosition, CalculateSpellRange()))
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.AreaTargetOutOfRange"), eChatType.CT_SpellResisted);
                    return false;
                }
            }
            else if (m_spell.Target.ToLower() != "self" && m_spell.Target.ToLower() != "group" && m_spell.Target.ToLower() != "cone" && m_spell.Range > 0)
            {
                if (m_spell.Target.ToLower() != "pet")
                {
                    //all other spells that need a target
                    if (target == null || target.ObjectState != GameObject.eObjectState.Active)
                    {
                        if (Caster is GamePlayer && !quiet)
                            MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MustSelectTarget"), eChatType.CT_SpellResisted);
                        return false;
                    }

                    if (Caster is GamePlayer && !m_caster.IsWithinRadius(target, CalculateSpellRange()))
                    {
                        if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetTooFar"), eChatType.CT_SpellResisted);
                        return false;
                    }
                }

                switch (m_spell.Target.ToLower())
                {
                    case "enemy":
                        //enemys have to be in front and in view for targeted spells
                        if (Caster is GamePlayer && !m_caster.IsObjectInFront(target, 180) && !Caster.IsWithinRadius(target, 50))
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetNotInViewSpellFail"), eChatType.CT_SpellResisted);
                            return false;
                        }

                        if (ServerProperties.Properties.CHECK_LOS_DURING_CAST)
                        {
                            // If the area forces an LoS check then we do it, otherwise we only check
                            // if caster or target is a player
                            // This will generate an interrupt if LOS check fails

                            if (Caster is GamePlayer casterPlayer)
                            {
                                casterPlayer.Out.SendCheckLOS(Caster, target, new CheckLOSResponse(CheckLOSPlayerToTarget));
                            }
                            else if (target is GamePlayer targetPlayer)
                            {
                                targetPlayer.Out.SendCheckLOS(Caster, target, new CheckLOSResponse(CheckLOSNPCToTarget));
                            }
                            else if (MustCheckLOS(Caster))
                            {
                                GamePlayer checkerPlayer = Caster.CurrentRegion.GetPlayersInRadius(Caster.Coordinate, WorldMgr.VISIBILITY_DISTANCE, false, true).Cast<GamePlayer>().FirstOrDefault(p => Caster.IsVisibleTo(p) && target.IsVisibleTo(p));
                                
                                if (checkerPlayer != null)
                                    checkerPlayer.Out.SendCheckLOS(Caster, target, new CheckLOSResponse(CheckLOSNPCToTarget));
                            }
                        }

                        if (!GameServer.ServerRules.IsAllowedToAttack(Caster, target, quiet))
                        {
                            return false;
                        }
                        break;

                    case "corpse":
                        if (target.IsAlive || (!(Caster is TextNPC) && !GameServer.ServerRules.IsSameRealm(Caster, target, quiet)))
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.OnlyWorksOnDead"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        break;

                    case "realm":
                        if (!GameServer.ServerRules.IsAllowedToHelp(Caster, target, true))
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.OnlyWorksOnFriendlyTargets"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        if (!target.IsAlive)
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.CantCastOnDead"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        break;

                    case "pet":
                        /*
                         * Can cast pet spell on all Pet/Turret/Minion (our pet)
                         * -If caster target's isn't own pet.
                         *  -check if caster have controlled pet, select this automatically
                         *  -check if target isn't null
                         * -check if target isn't too far away
                         * If all checks isn't true, return false.
                         */
                        if (target == null || !Caster.IsControlledNPC(target as GameNPC))
                        {
                            if (Caster.ControlledBrain != null && Caster.ControlledBrain.Body != null)
                            {
                                target = Caster.ControlledBrain.Body;
                            }
                            else
                            {
                                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MustCastOnControlled"), eChatType.CT_System);
                                return false;
                            }
                        }
                        //Now check distance for own pet
                        if (!m_caster.IsWithinRadius(target, CalculateSpellRange()))
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetTooFar"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        break;
                }
            }

            if (m_caster.Mana <= 0 && Spell.Power != 0 && Spell.SpellType != "Archery")
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.NoMoreMana"), eChatType.CT_SpellResisted);
                return false;
            }
            if (Spell.Power != 0 && m_caster.Mana < PowerCost(target) && Spell.SpellType != "Archery")
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.NotEnoughPower"), eChatType.CT_SpellResisted);
                return false;
            }

            if (m_caster is GamePlayer && m_spell.Concentration > 0 && m_caster.Concentration < m_spell.Concentration)
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.SpellRequiresConcentration", m_spell.Concentration), eChatType.CT_SpellResisted);
                return false;
            }

            if (m_caster is GamePlayer && m_spell.Concentration > 0 && m_caster.ConcentrationEffects.ConcSpellsCount >= 50)
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MaxConcentrationSpells"), eChatType.CT_SpellResisted);
                return false;
            }

            return true;
        }

        public bool CheckAfterCast(GameLiving target)
        {
            return CheckAfterCast(target, false);
        }


        public virtual bool CheckAfterCast(GameLiving target, bool quiet)
        {
            if (Interrupted)
            {
                return false;
            }

            if (!m_spell.Uninterruptible && m_spell.CastTime > 0 && m_caster is GamePlayer &&
                m_caster.EffectList.GetOfType<QuickCastEffect>() == null && m_caster.EffectList.GetOfType<MasteryofConcentrationEffect>() == null)
            {
                if (Caster.InterruptTime > 0 && Caster.InterruptTime > m_started)
                {
                    if (!quiet)
                    {
                        if (Caster.LastInterruptMessage != "")
                            MessageToCaster(Caster.LastInterruptMessage, eChatType.CT_SpellResisted);
                        else
                            MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.InterruptWaitBeforeCast", ((Caster.InterruptTime - m_started) / 1000 + 1).ToString()), eChatType.CT_SpellResisted);
                    }
                    Caster.InterruptAction = Caster.CurrentRegion.Time - Caster.SpellInterruptRecastAgain;
                    return false;
                }
            }

            if (m_caster.ObjectState != GameLiving.eObjectState.Active)
            {
                return false;
            }

            if (!m_caster.IsAlive)
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.DeadCantCast"), eChatType.CT_System);
                return false;
            }

            if (m_spell.InstrumentRequirement != 0)
            {
                if (!CheckInstrument())
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.WrongInstrument"), eChatType.CT_SpellResisted);
                    return false;
                }
            }
            else if (m_caster.IsSitting) // songs can be played if sitting
            {
                //Purge can be cast while sitting but only if player has negative effect that
                //don't allow standing up (like stun or mez)
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.CantCastWhileSitting"), eChatType.CT_SpellResisted);
                return false;
            }

            if (m_spell.Target.ToLower() == "area")
            {
                if (!m_caster.IsWithinRadius(m_caster.GroundTargetPosition, CalculateSpellRange()))
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.AreaTargetOutOfRange"), eChatType.CT_SpellResisted);
                    return false;
                }
                if (!Caster.GroundTargetInView)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.GroundTargetNotInView"), eChatType.CT_SpellResisted);
                    return false;
                }
            }
            else if (m_spell.Target.ToLower() != "self" && m_spell.Target.ToLower() != "group" && m_spell.Target.ToLower() != "cone" && m_spell.Range > 0)
            {
                if (m_spell.Target.ToLower() != "pet")
                {
                    //all other spells that need a target
                    if (target == null || target.ObjectState != GameObject.eObjectState.Active)
                    {
                        if (Caster is GamePlayer && !quiet)
                            MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MustSelectTarget"), eChatType.CT_SpellResisted);
                        return false;
                    }

                    if (Caster is GamePlayer && !m_caster.IsWithinRadius(target, CalculateSpellRange()))
                    {
                        if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetTooFar"), eChatType.CT_SpellResisted);
                        return false;
                    }
                }

                switch (m_spell.Target)
                {
                    case "enemy":
                        //enemys have to be in front and in view for targeted spells
                        if (Caster is GamePlayer && !m_caster.IsObjectInFront(target, 180) && !Caster.IsWithinRadius(target, 50))
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "TargetNotInViewSpellFail"), eChatType.CT_SpellResisted);
                            return false;
                        }

                        if (!GameServer.ServerRules.IsAllowedToAttack(Caster, target, quiet))
                        {
                            return false;
                        }
                        break;

                    case "corpse":
                        if (target.IsAlive || (!(Caster is TextNPC) && !GameServer.ServerRules.IsSameRealm(Caster, target, quiet)))
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.OnlyWorksOnDead"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        break;

                    case "realm":
                        if (!GameServer.ServerRules.IsAllowedToHelp(Caster, target, true))
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.OnlyWorksOnFriendlyTargets"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        if (!target.IsAlive)
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.CantCastOnDead"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        break;

                    case "pet":
                        /*
                         * [Ganrod] Nidel: Can cast pet spell on all Pet/Turret/Minion (our pet)
                         * -If caster target's isn't own pet.
                         *  -check if caster have controlled pet, select this automatically
                         *  -check if target isn't null
                         * -check if target isn't too far away
                         * If all checks isn't true, return false.
                         */
                        if (target == null || !Caster.IsControlledNPC(target as GameNPC))
                        {
                            if (Caster.ControlledBrain != null && Caster.ControlledBrain.Body != null)
                            {
                                target = Caster.ControlledBrain.Body;
                            }
                            else
                            {
                                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MustCastOnControlled"), eChatType.CT_System);
                                return false;
                            }
                        }
                        //Now check distance for own pet
                        if (!m_caster.IsWithinRadius(target, CalculateSpellRange()))
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetTooFar"), eChatType.CT_SpellResisted);
                            return false;
                        }
                        break;
                }
            }

            if (m_caster.Mana <= 0 && Spell.Power != 0 && Spell.SpellType != "Archery")
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.NoMoreMana"), eChatType.CT_SpellResisted);
                return false;
            }
            if (Spell.Power != 0 && m_caster.Mana < PowerCost(target) && Spell.SpellType != "Archery")
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.NotEnoughPower"), eChatType.CT_SpellResisted);
                return false;
            }

            if (m_caster is GamePlayer && m_spell.Concentration > 0 && m_caster.Concentration < m_spell.Concentration)
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.SpellRequiresConcentration", m_spell.Concentration), eChatType.CT_SpellResisted);
                return false;
            }

            if (m_caster is GamePlayer && m_spell.Concentration > 0 && m_caster.ConcentrationEffects.ConcSpellsCount >= 50)
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MaxConcentrationSpells"), eChatType.CT_SpellResisted);
                return false;
            }

            return true;
        }


        #endregion

        /// <summary>
        /// Calculates the power to cast the spell
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public virtual int PowerCost(GameLiving target)
        {
            // warlock
            GameSpellEffect effect = SpellHandler.FindEffectOnTarget(m_caster, "Powerless");
            if (effect != null && !m_spell.IsPrimary)
                return 0;

            //1.108 - Valhallas Blessing now has a 75% chance to not use power.
            ValhallasBlessingEffect ValhallasBlessing = m_caster.EffectList.GetOfType<ValhallasBlessingEffect>();
            if (ValhallasBlessing != null && Util.Chance(75))
                return 0;

            //patch 1.108 increases the chance to not use power to 50%.
            FungalUnionEffect FungalUnion = m_caster.EffectList.GetOfType<FungalUnionEffect>();
            {
                if (FungalUnion != null && Util.Chance(50))
                    return 0;
            }

            // Arcane Syphon chance
            int syphon = Caster.GetModified(eProperty.ArcaneSyphon);
            if (syphon > 0)
            {
                if (Util.Chance(syphon))
                {
                    return 0;
                }
            }

            double basepower = m_spell.Power; //<== defined a basevar first then modified this base-var to tell %-costs from absolut-costs

            // percent of maxPower if less than zero
            if (basepower < 0)
            {
                if (Caster is GamePlayer && ((GamePlayer)Caster).CharacterClass.ManaStat != eStat.UNDEFINED)
                {
                    GamePlayer player = Caster as GamePlayer;
                    basepower = player!.CalculateMaxMana(player.Level, player.GetBaseStat(player.CharacterClass.ManaStat)) * basepower * -0.01;
                }
                else
                {
                    basepower = Caster.MaxMana * basepower * -0.01;
                }
            }

            double power = basepower * 1.2; //<==NOW holding basepower*1.2 within 'power'

            eProperty focusProp = SkillBase.SpecToFocus(SpellLine.Spec);
            if (focusProp != eProperty.Undefined)
            {
                double focusBonus = Caster.GetModified(focusProp) * 0.4;
                if (Spell.Level > 0)
                    focusBonus /= Spell.Level;
                if (focusBonus > 0.4)
                    focusBonus = 0.4;
                else if (focusBonus < 0)
                    focusBonus = 0;
                power -= basepower * focusBonus; //<== So i can finally use 'basepower' for both calculations: % and absolut
            }
            else if (Caster is GamePlayer && ((GamePlayer)Caster).CharacterClass.ClassType == eClassType.Hybrid)
            {
                double specBonus = 0;
                if (Spell.Level != 0) specBonus = (((GamePlayer)Caster).GetBaseSpecLevel(SpellLine.Spec) * 0.4 / Spell.Level);

                if (specBonus > 0.4)
                    specBonus = 0.4;
                else if (specBonus < 0)
                    specBonus = 0;
                power -= basepower * specBonus;
            }

            if (!(GameServer.ServerRules.IsPveOnlyBonus(eProperty.SpellPowerCost) && GameServer.ServerRules.IsPvPAction(Caster, target, !Spell.IsHarmful)))
            {
                power *= Caster.GetModified(eProperty.SpellPowerCost) * 0.01;
            }
            
            // doubled power usage if quickcasting
            if (Caster.EffectList.GetOfType<QuickCastEffect>() != null && Spell.CastTime > 0)
                power *= 2;
            return (int)power;
        }

        /// <summary>
        /// Calculates the enduance cost of the spell
        /// </summary>
        /// <returns></returns>
        public virtual int CalculateEnduranceCost()
        {
            return 5;
        }

        /// <summary>
        /// Calculates the range to target needed to cast the spell
        /// NOTE: This method returns a minimum value of 32
        /// </summary>
        /// <returns></returns>
        public virtual int CalculateSpellRange()
        {
            int range = Math.Max(32, (int)(Spell.Range * Caster.GetModified(eProperty.SpellRange) * 0.01));
            return range;
            //Dinberg: add for warlock range primer
        }

        /// <summary>
        /// Called whenever the casters casting sequence is to interrupt immediately
        /// </summary>
        public virtual void InterruptCasting()
        {
            if (Interrupted || !IsCasting)
                return;

            Status = eStatus.Interrupted;

            if (IsCasting)
            {
                foreach (GamePlayer player in m_caster.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    player.Out.SendInterruptAnimation(m_caster);
                }
            }

            if (m_castTimer != null)
            {
                m_castTimer.Stop();
                m_castTimer = null;

                if (m_caster is GamePlayer)
                {
                    ((GamePlayer)m_caster).ClearSpellQueue();
                }
            }

            m_startReuseTimer = false;
            OnAfterSpellCastSequence();
        }

        /// <summary>
        /// Casts a spell after the CastTime delay
        /// </summary>
        protected class DelayedCastTimer : GameTimer
        {
            /// <summary>
            /// The spellhandler instance with callbacks
            /// </summary>
            private readonly SpellHandler m_handler;
            /// <summary>
            /// The target object at the moment of CastSpell call
            /// </summary>
            private readonly GameLiving m_target;
            private readonly GameLiving m_caster;
            private byte m_stage;
            private readonly int m_delay1;
            private readonly int m_delay2;
            private readonly byte m_delay1_substeps;
            private byte m_stepcount;

            /// <summary>
            /// Constructs a new DelayedSpellTimer
            /// </summary>
            /// <param name="actionSource">The caster</param>
            /// <param name="handler">The spell handler</param>
            /// <param name="target">The target object</param>
            /// <param name="delay1">Amount of time to wait in stage 1</param>
            /// <param name="delay2">Amount of time to wait in stage 2</param>
            /// <param name="delay1_substeps">Number of times to repeat stage 1</param>
            public DelayedCastTimer(GameLiving actionSource, SpellHandler handler, GameLiving target, int delay1, int delay2, byte delay1_substeps)
                : base(actionSource.CurrentRegion.TimeManager)
            {
                if (handler == null)
                    throw new ArgumentNullException("handler");

                if (actionSource == null)
                    throw new ArgumentNullException("actionSource");

                m_handler = handler;
                m_target = target;
                m_caster = actionSource;
                m_stage = 0;
                m_delay1 = delay1;
                m_delay2 = delay2;
                m_delay1_substeps = delay1_substeps;
                m_stepcount = 0;
            }

            /// <summary>
            /// Called on every timer tick
            /// </summary>
            public override void OnTick()
            {
                try
                {
                    if (m_stage == 0)
                    {
                        if (!m_handler.CheckAfterCast(m_target))
                        {
                            Interval = 0;
                            m_handler.InterruptCasting();
                            m_handler.OnAfterSpellCastSequence();
                            return;
                        }
                        m_stage = 1;
                        m_handler.Stage = 1;
                        Interval = m_delay1;
                    }
                    else if (m_stage == 1)
                    {
                        ++m_stepcount;
                        if (!m_handler.CheckDuringCast(m_target))
                        {
                            Interval = 0;
                            m_handler.InterruptCasting();
                            m_handler.OnAfterSpellCastSequence();
                            return;
                        }
                        if (m_stepcount < m_delay1_substeps)
                            return;
                        m_stage = 2;
                        m_handler.Stage = 2;
                        Interval = m_delay2;
                    }
                    else if (m_stage == 2)
                    {
                        m_stage = 3;
                        m_handler.Stage = 3;
                        Interval = 100;

                        if (m_handler.CheckEndCast(m_target))
                        {
                            m_handler.FinishSpellCast(m_target);
                        }
                    }
                    else
                    {
                        m_stage = 4;
                        m_handler.Stage = 4;
                        Interval = 0;
                        m_handler.OnAfterSpellCastSequence();
                    }

                    if (m_caster is GamePlayer && ServerProperties.Properties.ENABLE_DEBUG && m_stage < 3)
                    {
                        (m_caster as GamePlayer)!.Out.SendMessage("[DEBUG] step = " + (m_handler.Stage + 1), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }

                    return;
                }
                catch (Exception e)
                {
                    if (log.IsErrorEnabled)
                        log.Error(ToString(), e);
                }

                m_handler.OnAfterSpellCastSequence();
                Interval = 0;
            }

            /// <summary>
            /// Returns short information about the timer
            /// </summary>
            /// <returns>Short info about the timer</returns>
            public override string ToString()
            {
                return new StringBuilder(base.ToString(), 128)
                    .Append(" spellhandler: (").Append(m_handler.ToString()).Append(')')
                    .ToString();
            }
        }

        /// <summary>
        /// Calculates the effective casting time
        /// </summary>
        /// <returns>effective casting time in milliseconds</returns>
        public virtual int CalculateCastingTime()
        {
            return m_caster.CalculateCastingTime(m_spellLine, m_spell);
        }


        #region animations

        /// <summary>
        /// Sends the cast animation
        /// </summary>
        public virtual void SendCastAnimation()
        {
            ushort castTime = (ushort)(CalculateCastingTime() / 100);
            SendCastAnimation(castTime);
        }

        /// <summary>
        /// Sends the cast animation
        /// </summary>
        /// <param name="castTime">The cast time</param>
        public virtual void SendCastAnimation(ushort castTime)
        {
            foreach (GamePlayer player in m_caster.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player == null)
                    continue;
                player.Out.SendSpellCastAnimation(m_caster, m_spell.ClientEffect, castTime);
            }
        }

        /// <summary>
        /// Send the Effect Animation
        /// </summary>
        /// <param name="target">The target object</param>
        /// <param name="boltDuration">The duration of a bolt</param>
        /// <param name="noSound">sound?</param>
        /// <param name="success">spell success?</param>
        public virtual void SendEffectAnimation(GameObject target, ushort boltDuration, bool noSound, byte success)
        {
            if (target == null)
                target = m_caster;

            foreach (GamePlayer player in target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                player.Out.SendSpellEffectAnimation(m_caster, target, m_spell.ClientEffect, boltDuration, noSound, success);
            }
        }

        /// <summary>
        /// Send the Effect Animation
        /// </summary>
        /// <param name="target">The target object</param>
        /// <param name="boltDuration">The duration of a bolt</param>
        /// <param name="noSound">sound?</param>
        /// <param name="success">spell success?</param>
        public virtual void SendHitAnimation(GameObject target, ushort boltDuration, bool noSound, byte success)
        {
            var effect = m_spell.ClientHitEffect;
            if (effect == 0)
                effect = m_spell.ClientEffect;
            if (m_spell.ClientHitEffect == 0)
                return;
            
            if (target == null)
                target = m_caster;

            foreach (GamePlayer player in target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                player.Out.SendSpellEffectAnimation(m_caster, target, m_spell.ClientHitEffect, boltDuration, noSound, success);
            }
        }

        /// <summary>
        /// Send the Effect Animation
        /// </summary>
        /// <param name="target">The target object</param>
        /// <param name="boltDuration">The duration of a bolt</param>
        /// <param name="noSound">sound?</param>
        /// <param name="success">spell success?</param>
        public virtual void SendLaunchAnimation(GameObject target, ushort boltDuration, bool noSound, byte success)
        {
            if (m_spell.ClientLaunchEffect == 0)
                return;
            
            if (target == null)
                target = m_caster;

            foreach (GamePlayer player in target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                player.Out.SendSpellEffectAnimation(m_caster, target, m_spell.ClientLaunchEffect, boltDuration, noSound, success);
            }
        }

        /// <summary>
        /// Send the Interrupt Cast Animation
        /// </summary>
        public virtual void SendInterruptCastAnimation()
        {
            foreach (GamePlayer player in m_caster.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                player.Out.SendInterruptAnimation(m_caster);
            }
        }
        public virtual void SendEffectAnimation(GameObject target, ushort clientEffect, ushort boltDuration, bool noSound, byte success)
        {
            if (target == null)
                target = m_caster;

            foreach (GamePlayer player in target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                player.Out.SendSpellEffectAnimation(m_caster, target, clientEffect, boltDuration, noSound, success);
            }
        }
        #endregion

        /// <summary>
        /// called after normal spell cast is completed and effect has to be started
        /// </summary>
        public virtual void FinishSpellCast(GameLiving target, bool force = false)
        {
            if (Caster is GamePlayer playerCaster)
            {
                if (playerCaster.IsSummoningMount)
                {
                    playerCaster.SendTranslatedMessage( "GameObjects.GamePlayer.UseSlot.CantMountSpell", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    playerCaster.IsOnHorse = false;
                }
                if (!HasPositiveEffect)
                {
                    playerCaster.IsOnHorse = false;
                    if (playerCaster.AttackWeapon is GameInventoryItem weapon)
                    {
                        weapon.OnSpellCast(playerCaster, target, Spell);
                    }
                }
            }
            
            //[Stryve]: Do not break stealth if spell never breaks stealth.
            if (UnstealthCasterOnFinish)
                Caster.Stealth(false);

            // messages
            if (Spell.InstrumentRequirement == 0 && Spell.ClientEffect != 0 && Spell.CastTime > 0)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "RealmAbility.SendCasterSpellEffectAndCastMessage.You", m_spell.Name), eChatType.CT_Spell);
                foreach (GamePlayer player in m_caster.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (player != m_caster)
                    {
                        string message = LanguageMgr.GetTranslation(player.Client, "RealmAbility.SendCastMessage.PlayerCasts", player.GetPersonalizedName(m_caster));
                        player.MessageFromArea(m_caster, message, eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                    }
                }
            }
            
            if (m_spell.Pulse != 0 && m_spell.Frequency > 0)
            {
                CancelAllPulsingSpells(Caster);
                PulsingSpellEffect pulseeffect = new PulsingSpellEffect(this);
                pulseeffect.Start();
                // show animation on caster for positive spells, negative shows on every StartSpell
                if (m_spell.Target == "self" || m_spell.Target == "group")
                    SendEffectAnimation(Caster, 0, false, 1);
                if (m_spell.Target == "pet")
                    SendEffectAnimation(target, 0, false, 1);
                Status = eStatus.Success; // Pulse always succeeds
            }
            
            if (StartSpell(target, force)) // and action
            {
                if (m_ability != null)
                    m_caster.DisableSkill(m_ability.Ability, (m_spell.RecastDelay == 0 ? 3000 : m_spell.RecastDelay));

                // disable spells with recasttimer (Disables group of same type with same delay)
                if (m_spell.RecastDelay > 0 && m_startReuseTimer)
                {
                    if (m_caster is GamePlayer)
                    {
                        ICollection<Tuple<Skill, int>> toDisable = new List<Tuple<Skill, int>>();

                        GamePlayer gp_caster = m_caster as GamePlayer;
                        foreach (var skills in gp_caster!.GetAllUsableSkills())
                            if (skills.Item1 is Spell &&
                                (((Spell)skills.Item1).ID == m_spell.ID || (((Spell)skills.Item1).SharedTimerGroup != 0 && (((Spell)skills.Item1).SharedTimerGroup == m_spell.SharedTimerGroup))))
                                toDisable.Add(new Tuple<Skill, int>((Spell)skills.Item1, m_spell.RecastDelay));

                        foreach (var sl in gp_caster.GetAllUsableListSpells())
                        foreach (var sp in sl.Item2)
                            if (sp is Spell &&
                                (((Spell)sp).ID == m_spell.ID || (((Spell)sp).SharedTimerGroup != 0 && (((Spell)sp).SharedTimerGroup == m_spell.SharedTimerGroup))))
                                toDisable.Add(new Tuple<Skill, int>((Spell)sp, m_spell.RecastDelay));

                        m_caster.DisableSkill(toDisable);
                    }
                    else if (m_caster is GameNPC)
                        m_caster.DisableSkill(m_spell, m_spell.RecastDelay);
                }
            }
            
            //Dinberg: This is where I moved the warlock part (previously found in gameplayer) to prevent
            //cancelling before the spell was fired.
            if (m_spell.SpellType != "Powerless" && m_spell.SpellType != "Range" && m_spell.SpellType != "Uninterruptable")
            {
                GameSpellEffect effect = SpellHandler.FindEffectOnTarget(m_caster, "Powerless");
                if (effect == null)
                    effect = SpellHandler.FindEffectOnTarget(m_caster, "Range");
                if (effect == null)
                    effect = SpellHandler.FindEffectOnTarget(m_caster, "Uninterruptable");

                //if we found an effect, cancel it!
                if (effect != null)
                    effect.Cancel(false);
            }

            //the quick cast is unallowed whenever you miss the spell
            //set the time when casting to can not quickcast during a minimum time
            QuickCastEffect quickcast = m_caster.EffectList.GetOfType<QuickCastEffect>();
            if (quickcast != null && Spell.CastTime > 0)
            {
                m_caster.TempProperties.setProperty(GamePlayer.QUICK_CAST_CHANGE_TICK, m_caster.CurrentRegion.Time);
                m_caster.DisableSkill(SkillBase.GetAbility(Abilities.Quickcast), QuickCastAbilityHandler.DISABLE_DURATION);
                quickcast.Cancel(false);
            }

            GameEventMgr.Notify(GameLivingEvent.CastFinished, m_caster, new CastingEventArgs(this, target, m_lastAttackData));
        }

        protected virtual (string target, ushort radius, bool modified) GetModifiedTarget(GameObject castTarget)
        {
            string modifiedTarget = Spell.Target.ToLowerInvariant();
            var modifiedRadius    = (ushort)Spell.Radius;
            bool targetChanged    = false;
            int newtarget         = 0;
            if (modifiedTarget != "enemy" && modifiedTarget != "realm" && modifiedTarget != "group")
                return (modifiedTarget, modifiedRadius, false);

            GameSpellEffect TargetMod = SpellHandler.FindEffectOnTarget(m_caster, "TargetModifier");
            if (TargetMod == null)
                return (modifiedTarget, modifiedRadius, false);

            newtarget = (int)TargetMod.Spell.Value;
            switch (newtarget)
            {
                case 0: // Apply on heal single
                    if (m_spell.SpellType.ToLower() == "heal" && modifiedTarget == "realm")
                    {
                        modifiedTarget = "group";
                        targetChanged = true;
                    }
                    break;
                case 1: // Apply on heal group
                    if (m_spell.SpellType.ToLower() == "heal" && modifiedTarget == "group")
                    {
                        modifiedTarget = "realm";
                        modifiedRadius = (ushort)m_spell.Range;
                        targetChanged = true;
                    }
                    break;
                case 2: // apply on enemy
                    if (modifiedTarget == "enemy")
                    {
                        if (m_spell.Radius == 0)
                            modifiedRadius = 450;
                        if (m_spell.Radius != 0)
                            modifiedRadius += 300;
                        targetChanged = true;
                    }
                    break;
                case 3: // Apply on buff
                    if (m_spell.Target.ToLower() == "group"
                        && m_spell.Pulse != 0)
                    {
                        modifiedTarget = "realm";
                        modifiedRadius = (ushort)m_spell.Range;
                        targetChanged = true;
                    }
                    break;
            }
            
            if (targetChanged)
            {
                if (TargetMod.Duration < 65535)
                    TargetMod.Cancel(false);
            }
            return (modifiedTarget, modifiedRadius, false);
        }

        /// <summary>
        /// Select all targets for this spell
        /// </summary>
        /// <param name="castTarget"></param>
        /// <returns></returns>
        public virtual IList<GameLiving> SelectTargets(GameObject castTarget, bool force = false)
        {
            var list = new List<LivingDistEntry>(Math.Max(Spell.TargetHardCap, (ushort)8));
            GameLiving target = castTarget as GameLiving;
            var (modifiedTarget, modifiedRadius, targetChanged) = GetModifiedTarget(castTarget);
            int newtarget = 0;

            if (modifiedTarget == "pet" && !HasPositiveEffect)
            {
                modifiedTarget = "enemy";
                //[Ganrod] Nidel: can cast TurretPBAoE on selected Pet/Turret
                if (Spell.SpellType.ToLower() != "TurretPBAoE".ToLower())
                {
                    target = Caster.ControlledBrain.Body;
                }
            }

            bool AddTarget(LivingDistEntry entry)
            {
                if (Spell.TargetHardCap == 0 || list.Count < Spell.TargetHardCap)
                {
                    list.Add(entry);
                    return true;
                }
                else
                {
                    var maxDist = entry.Distance;
                    int index = 0;
                    int found = -1;

                    while (index < list.Count)
                    {
                        if (list[index].Distance > maxDist)
                        {
                            maxDist = list[index].Distance;
                            found = index;
                        }
                        ++index;
                    }
                    if (found == -1)
                        return false;
                    
                    list[found] = entry;
                    return true;
                }
            }

            #region Process the targets
            switch (modifiedTarget)
            {
                #region GTAoE
                // GTAoE
                case "area":
                    //Dinberg - fix for animists turrets, where before a radius of zero meant that no targets were ever
                    //selected!
                    if (Spell.SpellType == "SummonAnimistPet" || Spell.SpellType == "SummonAnimistFnF")
                    {
                        AddTarget(new LivingDistEntry(Caster, -1));
                    }
                    else if (modifiedRadius > 0)
                    {
                        foreach (PlayerDistEntry entry in WorldMgr.GetPlayersCloseToSpot(Caster.GroundTargetPosition, modifiedRadius, true))
                        {
                            if (GameServer.ServerRules.IsAllowedToAttack(Caster, entry.Player, true) || force)
                            {
                                // Apply Mentalist RA5L
                                SelectiveBlindnessEffect SelectiveBlindness = Caster.EffectList.GetOfType<SelectiveBlindnessEffect>();
                                if (SelectiveBlindness != null)
                                {
                                    GameLiving EffectOwner = SelectiveBlindness.EffectSource;
                                    if (EffectOwner == entry.Player)
                                    {
                                        if (Caster is GamePlayer casterPlayer)
                                            casterPlayer.SendTranslatedMessage("GameLiving.AttackData.InvisibleToYou", eChatType.CT_Missed, eChatLoc.CL_SystemWindow, casterPlayer.GetPersonalizedName(entry.Player));
                                    }
                                    else
                                        AddTarget(new LivingDistEntry(entry.Player, entry.Distance));
                                }
                                else
                                    AddTarget(new LivingDistEntry(entry.Player, entry.Distance));
                            }
                        }
                        foreach (NPCDistEntry entry in WorldMgr.GetNPCsCloseToSpot(Caster.GroundTargetPosition, modifiedRadius, true))
                        {
                            GameNPC npc = entry.NPC;
                            if (npc is GameStorm)
                                AddTarget(new LivingDistEntry(entry.NPC, entry.Distance));
                            else if (GameServer.ServerRules.IsAllowedToAttack(Caster, npc, true) || force)
                            {
                                if (!npc.HasAbility("DamageImmunity")) AddTarget(new LivingDistEntry(entry.NPC, entry.Distance));;
                            }
                        }
                    }
                    break;
                #endregion
                #region Corpse
                case "corpse":
                    if (target != null && !target.IsAlive)
                        AddTarget(new LivingDistEntry(target, -1));
                    break;
                #endregion
                #region Pet
                case "pet":
                    {
                        //Start-- [Ganrod] Nidel: Can cast Pet spell on our Minion/Turret pet without ControlledNpc
                        // awesome, Pbaoe with target pet spell ?^_^
                        if (modifiedRadius > 0 && Spell.Range == 0)
                        {
                            foreach (NPCDistEntry pet in Caster.GetNPCsInRadius(true, modifiedRadius, true, false))
                            {
                                if (Caster.IsControlledNPC(pet.NPC))
                                {
                                    AddTarget(new LivingDistEntry(pet.NPC, pet.Distance));
                                }
                            }
                            break;
                        }
                        if (target == null)
                        {
                            break;
                        }

                        GameNPC petBody = target as GameNPC;
                        // check target
                        if (petBody != null && Caster.IsWithinRadius(petBody, Spell.Range))
                        {
                            if (Caster.IsControlledNPC(petBody))
                            {
                                AddTarget(new LivingDistEntry(petBody, -1));
                            }
                        }
                        //check controllednpc if target isn't pet (our pet)
                        if (list.Count < 1 && Caster.ControlledBrain != null)
                        {
                            petBody = Caster.ControlledBrain.Body;
                            if (petBody != null && Caster.IsWithinRadius(petBody, Spell.Range))
                            {
                                AddTarget(new LivingDistEntry(petBody, -1));
                            }
                        }

                        //Single spell buff/heal...
                        if (Spell.Radius == 0)
                        {
                            break;
                        }
                        //Our buff affects every pet in the area of targetted pet (our pets)
                        if (Spell.Radius > 0 && petBody != null)
                        {
                            foreach (NPCDistEntry pet in petBody.GetNPCsInRadius(true, modifiedRadius, true, false))
                            {
                                //ignore target or our main pet already added
                                if (pet.NPC == petBody || !Caster.IsControlledNPC(pet.NPC))
                                {
                                    continue;
                                }
                                AddTarget(new LivingDistEntry(pet.NPC, pet.Distance));
                            }
                        }
                    }
                    //End-- [Ganrod] Nidel: Can cast Pet spell on our Minion/Turret pet without ControlledNpc
                    break;
                #endregion
                #region Enemy
                case "enemy":
                    if (modifiedRadius > 0)
                    {
                        if (Spell.SpellType.ToLower() != "TurretPBAoE".ToLower() && (target == null || Spell.Range == 0))
                            target = Caster;
                        if (target == null)
                            return null;
                        foreach (PlayerDistEntry entry in target.GetPlayersInRadius(true, modifiedRadius, true, false))
                        {
                            if (GameServer.ServerRules.ShouldAOEHitTarget(Spell, Caster, entry.Player))
                            {
                                SelectiveBlindnessEffect SelectiveBlindness = Caster.EffectList.GetOfType<SelectiveBlindnessEffect>();
                                if (SelectiveBlindness != null)
                                {
                                    GameLiving EffectOwner = SelectiveBlindness.EffectSource;
                                    if (EffectOwner == entry.Player)
                                    {
                                        if (Caster is GamePlayer casterPlayer)
                                            casterPlayer.SendTranslatedMessage("GameLiving.AttackData.InvisibleToYou", eChatType.CT_Missed, eChatLoc.CL_SystemWindow, casterPlayer.GetPersonalizedName(entry.Player));
                                    }
                                    else AddTarget(new LivingDistEntry(entry.Player, entry.Distance));
                                }
                                else AddTarget(new LivingDistEntry(entry.Player, entry.Distance));
                            }
                        }
                        foreach (NPCDistEntry entry in target.GetNPCsInRadius(true, modifiedRadius, true, false))
                        {
                            if (GameServer.ServerRules.ShouldAOEHitTarget(Spell, Caster, entry.NPC))
                            {
                                if (!entry.NPC.HasAbility("DamageImmunity"))
                                    AddTarget(new LivingDistEntry(entry.NPC, entry.Distance));
                            }
                        }
                        if (force && !list.Select(e => e.Living).Contains(target))
                            AddTarget(new LivingDistEntry(target, -1));
                    }
                    else
                    {
                        if (target != null && (force || GameServer.ServerRules.IsAllowedToAttack(Caster, target, true)))
                        {
                            // Apply Mentalist RA5L
                            if (Spell.Range > 0)
                            {
                                SelectiveBlindnessEffect SelectiveBlindness = Caster.EffectList.GetOfType<SelectiveBlindnessEffect>();
                                if (SelectiveBlindness != null)
                                {
                                    GameLiving EffectOwner = SelectiveBlindness.EffectSource;
                                    if (EffectOwner == target)
                                    {
                                        if (Caster is GamePlayer player) ((GamePlayer)Caster).Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameLiving.AttackData.InvisibleToYou", target.GetName(0, true)), eChatType.CT_Missed, eChatLoc.CL_SystemWindow);
                                    }
                                    else if (!target.HasAbility("DamageImmunity")) AddTarget(new LivingDistEntry(target, -1));
                                }
                                else if (!target.HasAbility("DamageImmunity")) AddTarget(new LivingDistEntry(target, -1));
                            }
                            else if (!target.HasAbility("DamageImmunity")) AddTarget(new LivingDistEntry(target, -1));
                        }
                    }
                    break;
                #endregion
                #region Realm
                case "realm":
                    if (modifiedRadius > 0)
                    {
                        if (target == null || Spell.Range == 0)
                            target = Caster;
                        
                        foreach (PlayerDistEntry entry in target.GetPlayersInRadius(true, modifiedRadius, true, false))
                        {
                            var player = entry.Player;
                            if (player.IsVisibleTo(Caster) && GameServer.ServerRules.IsAllowedToHelp(Caster, player, true) && player.IsAlive)
                            {
                                AddTarget(entry);
                            }
                        }
                        foreach (NPCDistEntry entry in target.GetNPCsInRadius(true, modifiedRadius, true, false))
                        {
                            var npc = entry.NPC;
                            if (npc.IsVisibleTo(Caster) && (GameServer.ServerRules.IsAllowedToHelp(Caster, npc, true) && npc.IsAlive))
                            {
                                AddTarget(entry);
                            }
                        }
                        if (force && !list.Select(e => e.Living).Contains(target))
                            AddTarget(new LivingDistEntry(target, -1));
                    }
                    else
                    {
                        if (target != null && (GameServer.ServerRules.IsAllowedToHelp(Caster, target, true) || force))
                            AddTarget(new LivingDistEntry(target, -1));
                    }
                    break;
                #endregion
                #region Self
                case "self":
                    {
                        if (modifiedRadius > 0)
                        {
                            if (target == null || Spell.Range == 0)
                                target = Caster;
                            foreach (PlayerDistEntry entry in target.GetPlayersInRadius(true, modifiedRadius, true, false))
                            {
                                var player = entry.Player;
                                if (!GameServer.ServerRules.IsAllowedToAttack(Caster, player, true) || force)
                                {
                                    AddTarget(entry);
                                }
                            }
                            foreach (NPCDistEntry entry in target.GetNPCsInRadius(true, modifiedRadius, true, false))
                            {
                                var npc = entry.NPC;
                                if (!GameServer.ServerRules.IsAllowedToAttack(Caster, npc, true) || force)
                                {
                                    AddTarget(entry);
                                }
                            }
                        }
                        else
                        {
                            AddTarget(new LivingDistEntry(Caster, -1));
                        }
                        break;
                    }
                #endregion
                #region Group
                case "group":
                    {
                        Group group = m_caster.Group;

                        int spellRange;
                        if (Spell.Range == 0)
                            spellRange = modifiedRadius;
                        else
                            spellRange = CalculateSpellRange();

                        if (group == null)
                        {
                            if (m_caster is GamePlayer)
                            {
                                AddTarget(new LivingDistEntry(Caster, -1));

                                IControlledBrain npc = m_caster.ControlledBrain;
                                if (npc != null)
                                {
                                    //Add our first pet
                                    GameNPC petBody2 = npc.Body;
                                    if (m_caster.IsWithinRadius(petBody2, spellRange))
                                        AddTarget(new LivingDistEntry(petBody2, -1));

                                    //Now lets add any subpets!
                                    if (petBody2 != null && petBody2.ControlledNpcList != null)
                                    {
                                        foreach (IControlledBrain icb in petBody2.ControlledNpcList)
                                        {
                                            if (icb != null && m_caster.IsWithinRadius(icb.Body, spellRange))
                                                AddTarget(new LivingDistEntry(icb.Body, -0.5f));
                                        }
                                    }
                                }
                            } // if (m_caster is GamePlayer)
                            else if (m_caster.GetLivingOwner() is {} owner)
                            {
                                if (owner.Group == null)
                                {
                                    // No group, add both the pet and owner to the list
                                    AddTarget(new LivingDistEntry(owner, -1));
                                    AddTarget(new LivingDistEntry(Caster, -1));
                                }
                                else
                                    // Assign the owner's group so they are added to the list
                                    group = owner.Group;
                            }// else if (m_caster is GameNPC...
                            else
                                AddTarget(new LivingDistEntry(m_caster, -1)); // TODO: add owner too ?
                        }// if (group == null)

                        //We need to add the entire group
                        if (group != null)
                        {
                            bool TryAdd(GameLiving living)
                            {
                                if (living == null)
                                    return false;

                                var distanceSq = living.GetDistanceSquaredTo(m_caster);
                                if (distanceSq > (double)(spellRange) * spellRange)
                                    return false;

                                var distance = Math.Sqrt(distanceSq);
                                if (distance > spellRange)
                                    return false;

                                AddTarget(new LivingDistEntry(living, (float)distance));
                                return true;
                            }
                            foreach (GameLiving living in group.GetMembersInTheGroup())
                            {
                                if (!TryAdd(living))
                                    continue;
                                
                                if (living.ControlledBrain is not { Body: {} petBody } pet)
                                    continue;
                                
                                //Add our first pet
                                if (!TryAdd(petBody))
                                    continue;

                                //Now lets add any subpets!
                                if (petBody.ControlledNpcList != null)
                                {
                                    foreach (IControlledBrain icb in petBody.ControlledNpcList)
                                    {
                                        TryAdd(icb.Body);
                                    }
                                }
                            }
                        }

                        break;
                    }
                #endregion
                #region Cone AoE
                case "cone":
                    {
                        target = Caster;
                        foreach (PlayerDistEntry entry in target.GetPlayersInRadius(true, (ushort)Spell.Range, true, false))
                        {
                            var player = entry.Player;
                            if (player == Caster)
                                continue;

                            if (!m_caster.IsObjectInFront(player, (double)(Spell.Radius != 0 ? Spell.Radius : 100), false))
                                continue;

                            if (!GameServer.ServerRules.IsAllowedToAttack(Caster, player, true) || force)
                                continue;

                            AddTarget(entry);
                        }

                        foreach (NPCDistEntry entry in target.GetNPCsInRadius(true, (ushort)Spell.Range, true, false))
                        {
                            var npc = entry.NPC;
                            if (npc == Caster)
                                continue;

                            if (!m_caster.IsObjectInFront(npc, (double)(Spell.Radius != 0 ? Spell.Radius : 100), false))
                                continue;

                            if (!GameServer.ServerRules.IsAllowedToAttack(Caster, npc, true) || force)
                                continue;

                            if (!npc.HasAbility("DamageImmunity"))
                                AddTarget(entry);

                        }
                        break;
                    }
                    #endregion
            }
            #endregion
            return list.Select(entry => entry.Living).ToList();
        }

        protected class SubSpellTimer : GameTimer
        {
            private ISpellHandler m_subspellhandler;
            private GameLiving m_target;

            public SubSpellTimer(GameLiving actionSource, ISpellHandler spellhandler, GameLiving target) : base(actionSource.CurrentRegion.TimeManager)
            {
                m_subspellhandler = spellhandler;
                m_target = target;
            }

            public override void OnTick()
            {
                m_subspellhandler.StartSpell(m_target);
                Stop();
            }
        }

        /// <summary>
        /// Clone a subspell and apply the parent's race multiplier (if any).
        /// This scales Value/Damage and, optionally, Duration, and propagates
        /// the race meta down the chain.
        /// </summary>
        protected virtual Spell GetSubSpellWithRaceMultiplier(Spell parentSpell, Spell subSpell)
        {
            if (parentSpell == null || subSpell == null)
                return subSpell;

            if (Math.Abs(parentSpell.RaceMultiplier - 1.0) < 0.0001)
                return subSpell;

            // Clone DB subspell so we don't mutate the global instance
            Spell cloned = (Spell)subSpell.Clone();

            double m = parentSpell.RaceMultiplier;

            cloned.RaceMultiplier = m;
            cloned.DurationScaledByRace = parentSpell.DurationScaledByRace;

            cloned.Value *= m;
            cloned.Damage *= m;

            if (parentSpell.DurationScaledByRace && cloned.Duration > 0)
            {
                cloned.Duration = (int)(cloned.Duration * m);
            }

            return cloned;
        }

        /// <summary>
        /// Cast all subspell recursively
        /// </summary>
        /// <param name="target"></param>
        public virtual bool CastSubSpells(GameLiving target)
        {
            if (target is null)
                target = Caster;

            bool success = false;

            // Build the linear chain from SubSpellID: m_spell -> sub1 -> sub2 -> ...
            List<int> subSpellList = new List<int>();
            Spell parent = m_spell;

            while (parent != null && parent.SubSpellID > 0)
            {
                subSpellList.Add(parent.SubSpellID);

                Spell next = SkillBase.GetSpellByID(parent.SubSpellID);
                if (next == null)
                    break;

                parent = next;
            }

            // Merge with MultipleSubSpells (parallel) and avoid duplicates
            IEnumerable<int> allSubSpellIDs = subSpellList.Union(m_spell.MultipleSubSpells);

            foreach (int spellID in allSubSpellIDs)
            {
                Spell dbSub = SkillBase.GetSpellByID(spellID);
                if (dbSub == null)
                {
                    log.ErrorFormat(
                        "Spell {0} has subspell {1} which does not exist. Check DB and/or reload spells",
                        m_spell.ID, spellID
                    );
                    continue;
                }

                Spell effectiveSub = GetSubSpellWithRaceMultiplier(m_spell, dbSub);

                //we need subspell ID to be 0, we don't want spells linking off the subspell
                //TODO (Mishura): this comment ^ appears to be leftover from code that was removed

                // We have to scale pet subspells when cast
                if (Caster is GamePet pet && !(Caster is NecromancerPet))
                    pet.ScalePetSpell(effectiveSub);

                // Keep using Reserved_Spells line like original code
                ISpellHandler spellhandler = ScriptMgr.CreateSpellHandler(m_caster, effectiveSub, SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells));

                if (spellhandler == null)
                    continue;

                spellhandler.Parent = this;

                if (m_spell.SubSpellDelay > 0)
                {
                    success = true;
                    new SubSpellTimer(Caster, spellhandler, target).Start(m_spell.SubSpellDelay * 1000);
                }
                else
                {
                    success = success || spellhandler.StartSpell(target);
                }
            }

            return success;
        }


        /// <summary>
        /// Tries to start a spell attached to an item (/use with at least 1 charge)
        /// Override this to do a CheckBeginCast if needed, otherwise spell will always cast and item will be used.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="item"></param>
        public bool StartSpell(GameLiving target, InventoryItem item)
        {
            m_spellItem = item;
            return StartSpell(target);
        }

        /// <summary>
        /// Called when spell effect has to be started and applied to targets
        /// This is typically called after calling CheckBeginCast
        /// </summary>
        /// <param name="target">The current target object</param>
        public bool StartSpell(GameLiving target, bool force = false)
        {
            Status = eStatus.Ready;
            bool result = ExecuteSpell(target, force);
            if (Status == eStatus.Ready)
                Status = result ? eStatus.Success : eStatus.Failure;
            else
                result = Status is eStatus.Success or eStatus.Pass;
            return result;
        }
        
        protected virtual bool ExecuteSpell(GameLiving target, bool force = false)
        {
            // For PBAOE spells always set the target to the caster
            if (Spell.SpellType.ToLower() != "TurretPBAoE".ToLower() && (target == null || (Spell.Radius > 0 && Spell.Range == 0)))
            {
                target = Caster;
            }

            if (m_spellTarget == null)
                m_spellTarget = target;

            if (m_spellTarget == null) return false;

            var targets = SelectTargets(m_spellTarget, force);

            double effectiveness = Caster.Effectiveness;

            if (Caster.EffectList.GetOfType<MasteryofConcentrationEffect>() != null)
            {
                MasteryofConcentrationAbility ra = Caster.GetAbility<MasteryofConcentrationAbility>();
                if (ra != null && ra.Level > 0)
                {
                    effectiveness *= System.Math.Round((double)ra.GetAmountForLevel(ra.Level) / 100, 2);
                }
            }

            //[StephenxPimentel] Reduce Damage if necro is using MoC
            if (Caster is NecromancerPet)
            {
                if ((Caster as NecromancerPet)!.Owner.EffectList.GetOfType<MasteryofConcentrationEffect>() != null)
                {
                    MasteryofConcentrationAbility necroRA = (Caster as NecromancerPet)!.Owner.GetAbility<MasteryofConcentrationAbility>();
                    if (necroRA != null && necroRA.Level > 0)
                    {
                        effectiveness *= System.Math.Round((double)necroRA.GetAmountForLevel(necroRA.Level) / 100, 2);
                    }
                }
            }

            if (Caster is GamePlayer casterPlayer && casterPlayer.CharacterClass.ID == (int)eCharacterClass.Warlock && m_spell.IsSecondary)
            {
                Spell uninterruptibleSpell = Caster.TempProperties.getProperty<Spell>(UninterruptableSpellHandler.WARLOCK_UNINTERRUPTABLE_SPELL);

                if (uninterruptibleSpell != null && uninterruptibleSpell.Value > 0)
                {
                    double nerf = uninterruptibleSpell.Value;
                    effectiveness *= (1 - (nerf * 0.01));
                    Caster.TempProperties.removeProperty(UninterruptableSpellHandler.WARLOCK_UNINTERRUPTABLE_SPELL);
                }
            }
            
            SendLaunchAnimation(Caster, 0, false, 1);

            bool success = false;
            foreach (GameLiving t in targets)
            {
                // Aggressive NPCs will aggro on every target they hit
                // with an AoE spell, whether it landed or was resisted.

                if (Spell.Radius > 0 && Spell.Target.ToLower() == "enemy"
                    && Caster is GameNPC && (Caster as GameNPC)!.Brain is IOldAggressiveBrain)
                    ((Caster as GameNPC)!.Brain as IOldAggressiveBrain)!.AddToAggroList(t, 1);
                if (Util.Chance(CalculateSpellResistChance(t)))
                {
                    OnSpellResisted(t);
                    success = true; // Resist is a success, it should consume item charges. TODO: Maybe we should check effect overwrites before?
                    continue;
                }

                if (Spell.Radius == 0 || HasPositiveEffect)
                {
                    success = ApplyEffectOnTarget(t, effectiveness) || success;
                }
                else if (Spell.Target.ToLower() == "area")
                {
                    int dist = (int)t.Coordinate.DistanceTo(Caster.GroundTargetPosition);
                    if (dist >= 0)
                        success = ApplyEffectOnTarget(t, (effectiveness - CalculateAreaVariance(t, dist, Spell.Radius))) || success;
                }
                else if (Spell.Target.ToLower() == "cone")
                {
                    var dist = (int)t.Coordinate.DistanceTo(Caster.Position);
                    //Cone spells use the range for their variance!
                    if (dist >= 0)
                        success = ApplyEffectOnTarget(t, (effectiveness - CalculateAreaVariance(t, dist, Spell.Range))) || success;
                }
                else
                {
                    var dist = (int)t.Coordinate.DistanceTo(target.Position);
                    if (dist >= 0)
                        success = ApplyEffectOnTarget(t, (effectiveness - CalculateAreaVariance(t, dist, Spell.Radius))) || success;
                }

                if (Caster is GamePet pet && Spell.IsBuff)
                    pet.AddBuffedTarget(target);
            }

            if (Spell.Target.ToLower() == "ground")
            {
                success = ApplyEffectOnTarget(null, 1) || success;
            }

            if (CastSubSpellsWithSpell)
            {
                success = CastSubSpells(target) || success;
            }
            return success;
        }
        
        /// <summary>
        /// Calculate the variance due to the radius of the spell
        /// </summary>
        /// <param name="distance">The distance away from center of the spell</param>
        /// <param name="radius">The radius of the spell</param>
        /// <returns></returns>
        protected virtual double CalculateAreaVariance(GameLiving target, float distance, int radius)
        {
            return ((double)distance / (double)radius);
        }

        /// <summary>
        /// Calculates the effect duration in milliseconds
        /// </summary>
        /// <param name="target">The effect target</param>
        /// <param name="effectiveness">The effect effectiveness</param>
        /// <returns>The effect duration in milliseconds</returns>
        protected virtual int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            double duration = Spell.Duration;
            duration *= (1.0 + m_caster.GetModified(eProperty.SpellDuration) * 0.01);
            if (Spell.InstrumentRequirement != 0)
            {
                InventoryItem instrument = Caster.AttackWeapon;
                if (instrument != null)
                {
                    duration *= 1.0 + Math.Min(1.0, instrument.Level / (double)Caster.Level); // up to 200% duration for songs
                    duration *= instrument.Condition / (double)instrument.MaxCondition * instrument.Quality / 100;
                }
            }

            if (target is GamePlayer { Guild: not null } targetPlayer)
            {
                int guildReduction = targetPlayer.Guild.GetDebuffDurationReduction(this);
                if (guildReduction != 0)
                    duration = (duration * (100 - Math.Min(100, guildReduction))) / 100;
            }

            if (Spell.IsHarmful && !Spell.SpellType.ToLower().StartsWith("style"))
            {
                if (!(GameServer.ServerRules.IsPveOnlyBonus(eProperty.NegativeReduction) && GameServer.ServerRules.IsPvPAction(Caster, target)))
                    duration *= (1.0 - target.GetModified(eProperty.NegativeReduction) * 0.01);
            }

            duration *= effectiveness;
            if (duration < 1)
                duration = 1;
            else if (duration > (Spell.Duration * 4))
                duration = (Spell.Duration * 4);
            return (int)duration;
        }

        /// <summary>
        /// Creates the corresponding spell effect for the spell
        /// </summary>
        /// <param name="target"></param>
        /// <param name="effectiveness"></param>
        /// <returns></returns>
        protected virtual GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
        {
            int freq = Spell != null ? Spell.Frequency : 0;
            return new GameSpellEffect(this, CalculateEffectDuration(target, effectiveness), freq, effectiveness);
        }

        public virtual bool HasPositiveOrSpeedEffect()
        {
            return Spell.SpellType is not "Unpetrify" && (HasPositiveEffect || Spell.SpellType is "Stun" or "Stylestun" or "Mesmerize" or "SpeedDecrease" or "Slow" or "StyleSpeedDecrease" or "VampSpeedDecrease");
        }

        /// <summary>
        /// Apply effect on target or do spell action if non duration spell
        /// </summary>
        /// <param name="target">target that gets the effect</param>
        /// <param name="effectiveness">factor from 0..1 (0%-100%)</param>
        /// <returns>Whether the spell succeeded or not, i.e. take a charge away from items or not</returns>
        public virtual bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target is ShadowNPC)
                return false;

            if (target is GamePlayer)
            {
                GameSpellEffect effect1;
                effect1 = SpellHandler.FindEffectOnTarget(target, "Phaseshift");
                if ((effect1 != null && (Spell.SpellType != "SpreadHeal" || Spell.SpellType != "Heal" || Spell.SpellType != "SpeedEnhancement")))
                {
                    MessageTranslationToCaster("SpellHandler.PhaseshiftedCantBeAffected", eChatType.CT_SpellResisted, m_caster.GetPersonalizedName(target));
                    return false;
                }
            }


            if ((target is Keeps.GameKeepDoor || target is Keeps.GameKeepComponent))
            {
                bool isAllowed = false;
                bool isSilent = false;

                if (Spell.Radius == 0)
                {
                    switch (Spell.SpellType.ToLower())
                    {
                        case "archery":
                        case "bolt":
                        case "bomber":
                        case "damagespeeddecrease":
                        case "directdamage":
                        case "magicalstrike":
                        case "siegearrow":
                        case "summontheurgistpet":
                        case "directdamagewithdebuff":
                            isAllowed = true;
                            break;
                    }
                }

                if (Spell.Radius > 0)
                {
                    // pbaoe is allowed, otherwise door is in range of a AOE so don't spam caster with a message
                    if (Spell.Range == 0)
                        isAllowed = true;
                    else
                        isSilent = true;
                }

                if (!isAllowed)
                {
                    if (!isSilent)
                    {
                        MessageTranslationToCaster("SpellHandler.NoEffectOnTarget", eChatType.CT_SpellResisted, m_caster.GetPersonalizedName(target));
                    }

                    return false;
                }
            }

            if (m_spellLine.KeyName == GlobalSpellsLines.Item_Effects || m_spellLine.KeyName == GlobalSpellsLines.Combat_Styles_Effect || m_spellLine.KeyName == GlobalSpellsLines.Potions_Effects || m_spellLine.KeyName == Specs.Savagery || m_spellLine.KeyName == GlobalSpellsLines.Character_Abilities || m_spellLine.KeyName == "OffensiveProc")
                effectiveness = 1.0; // TODO player.PlayerEffectiveness

            SendHitAnimation(target, 0, false, 1);
            
            if (effectiveness <= 0)
                return true; // no effect

            // Apply effect for Duration Spell.
            if ((Spell.Duration > 0 && Spell.Target.ToLower() != "area") || Spell.Concentration > 0)
            {
                return OnDurationEffectApply(target, effectiveness);
            }
            else
            {
                return OnDirectEffect(target, effectiveness);
            }
        }

        /// <summary>
        /// Called when cast sequence is complete
        /// </summary>
        public virtual void OnAfterSpellCastSequence()
        {
            if (Status == eStatus.Ready && log.IsWarnEnabled)
            {
                log.Warn($"SpellHandler {this} ended with status Ready, this is likely a mistake & will cause certain events to not be handled, for example item charges won't be consumed");
            }
            if (CastingCompleteEvent != null)
            {
                CastingCompleteEvent(this);
            }
        }

        /// <summary>
        /// Determines whether this spell being applied is better than existing one
        /// </summary>
        /// <param name="oldeffect"></param>
        /// <param name="neweffect"></param>
        /// <returns>true if this spell is better version than compare spell</returns>
        public virtual bool ShouldOverwriteOldEffect(GameSpellEffect oldeffect, GameSpellEffect neweffect)
        {
            Spell oldspell = oldeffect.Spell;
            Spell newspell = neweffect.Spell;
            //			if (oldspell.SpellType != newspell.SpellType)
            //			{
            //				if (Log.IsWarnEnabled)
            //					Log.Warn("Spell effect compare with different types " + oldspell.SpellType + " <=> " + newspell.SpellType + "\n" + Environment.StackTrace);
            //				return false;
            //			}
            if (oldspell.IsConcentration)
                return false;
            if (newspell.Damage < oldspell.Damage)
                return false;
            if (newspell.Value < oldspell.Value)
                return false;
            //makes problems for immunity effects
            if (!oldeffect.ImmunityState && !newspell.IsConcentration)
            {
                if (neweffect.Duration <= oldeffect.RemainingTime)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Determines wether this spell is compatible with given spell
        /// and therefore overwritable by better versions
        /// spells that are overwritable cannot stack
        /// </summary>
        /// <param name="compare"></param>
        /// <returns></returns>
        public virtual bool IsOverwritable(GameSpellEffect compare)
        {
            if (Spell.EffectGroup != 0 || compare.Spell.EffectGroup != 0)
                return Spell.EffectGroup == compare.Spell.EffectGroup;
            if (compare.Spell.SpellType != Spell.SpellType)
                return false;
            return true;
        }

        /// <summary>
        /// Determines wether this spell can be disabled
        /// by better versions spells that stacks without overwriting
        /// </summary>
        /// <param name="compare"></param>
        /// <returns></returns>
        public virtual bool IsCancellable(GameSpellEffect compare)
        {
            if (compare.SpellHandler != null)
            {
                if ((compare.SpellHandler.AllowCoexisting || AllowCoexisting)
                    && (!compare.SpellHandler.SpellLine.KeyName.Equals(SpellLine.KeyName, StringComparison.OrdinalIgnoreCase)
                        || compare.SpellHandler.Spell.IsInstantCast != Spell.IsInstantCast))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Determines wether new spell is better than old spell and should disable it
        /// </summary>
        /// <param name="oldeffect"></param>
        /// <param name="neweffect"></param>
        /// <returns></returns>
        public virtual bool ShouldCancelOldEffect(GameSpellEffect oldeffect, GameSpellEffect neweffect)
        {
            if (neweffect.SpellHandler.Spell.Value >= oldeffect.SpellHandler.Spell.Value)
                return true;

            return false;
        }

        public virtual void OnBetterThan(GameLiving target, GameSpellEffect oldEffect, GameSpellEffect newEffect)
        {
            SpellHandler otherHandler = (SpellHandler)newEffect.SpellHandler;
            otherHandler.SendSpellResistAnimation(target);
            if (otherHandler.Caster.GetController() is GamePlayer playerCaster)
            {
                eChatType noOverwrite = (newEffect.Spell.Pulse == 0) ? eChatType.CT_SpellResisted : eChatType.CT_SpellPulse;
                if (target == playerCaster)
                {
                    if (oldEffect.ImmunityState)
                    {
                        playerCaster.SendTranslatedMessage("SpellHandler.CantHaveEffectAgainYet", noOverwrite);
                    }
                    else
                    {
                        playerCaster.SendTranslatedMessage("SpellHandler.AlreadyHaveEffectWait", noOverwrite);
                    }
                }
                else
                {
                    if (oldEffect.ImmunityState)
                    {
                        playerCaster.SendTranslatedMessage("SpellHandler.TargetCantHaveEffectAgainYet", noOverwrite, eChatLoc.CL_SystemWindow, playerCaster.GetPersonalizedName(target));
                    }
                    else
                    {
                        playerCaster.SendTranslatedMessage("SpellHandler.WaitUntilExpires", noOverwrite, eChatLoc.CL_SystemWindow, playerCaster.GetPersonalizedName(target));
                    }
                }
            }
        }

        public virtual bool PreventsApplication(GameSpellEffect self, GameSpellEffect other)
        {
            return false;
        }

        /// <summary>
        /// Execute Duration Spell Effect on Target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="effectiveness"></param>
        public virtual bool OnDurationEffectApply(GameLiving target, double effectiveness)
        {
            if (!target.IsAlive || target.EffectList == null)
                return false;

            GameSpellEffect neweffect = CreateSpellEffect(target, effectiveness);
            return OnDurationEffectApply(target, neweffect, effectiveness);
        }
        
        public virtual bool OnDurationEffectApply(GameLiving target, GameSpellEffect? neweffect, double effectiveness)
        {
            if (!target.IsAlive || target.EffectList == null)
                return false;

            if (neweffect == null)
                return false;

            // Store Overwritable or Cancellable
            var enable = true;
            var cancellableEffects = new List<GameSpellEffect>(1);
            GameSpellEffect overwriteEffect = null;
            List<GameSpellEffect> removingEffects = new List<GameSpellEffect>(1);
            
            var newSpell = ((SpellHandler)neweffect.SpellHandler);
            foreach (var oldEffect in target.EffectList.OfType<GameSpellEffect>().Where(effect => effect.SpellHandler != null))
            {
                var oldSpell = ((SpellHandler)oldEffect.SpellHandler);
                if (oldSpell.PreventsApplication(oldEffect, neweffect))
                {
                    // Old Spell is Better than new one
                    oldSpell.OnBetterThan(target, oldEffect, neweffect);
                    // Prevent Adding.
                    return false;
                }

                if (!oldSpell.IsOverwritable(neweffect))
                {
                    // Old spell cannot be overwritten, but is it removed by this spell?
                    if (!oldEffect.ImmunityState && PreventsApplication(neweffect, oldEffect))
                    {
                        removingEffects.Add(oldEffect);
                    }
                }
                else
                {
                    // If we can cancel spell effect we don't need to overwrite it
                    if (oldSpell.IsCancellable(neweffect))
                    {
                        // Spell is better than existing "Cancellable" or it should start disabled
                        if (ShouldCancelOldEffect(oldEffect, neweffect))
                            cancellableEffects.Add(oldEffect);
                        else
                            enable = false;
                    }
                    else
                    {
                        // Check for Overwriting.
                        if (ShouldOverwriteOldEffect(oldEffect, neweffect))
                        {
                            if (overwriteEffect != null)
                            {
                                // We are already overwriting. But is this newest effect better than the new one?
                                if (ShouldOverwriteOldEffect(overwriteEffect, neweffect))
                                {
                                    removingEffects.Add(overwriteEffect);
                                }
                            }
                            overwriteEffect = oldEffect;
                        }
                        else
                        {
                            // Old Spell is Better than new one
                            ((SpellHandler)oldEffect.SpellHandler).OnBetterThan(target, oldEffect, neweffect);
                            // Prevent Adding.
                            return false;
                        }
                    }
                }
            }

            // Send the attack before starting the effect to avoid effects proccing off themselves
            if (!HasPositiveEffect)
            {
                AttackData ad = CalculateInitialAttack(target, effectiveness);
                target.OnAttackedByEnemy(ad);

                m_lastAttackData = ad;

                // Treat non-damaging effects as attacks to trigger an immediate response and BAF
                if (ad.Damage == 0 && ad.Target is GameNPC { Brain: IOldAggressiveBrain aggroBrain })
                {
                    aggroBrain.AddToAggroList(Caster, 1);
                }
            }

            // Register Effect list Changes
            target.EffectList.BeginChanges();
            try
            {
                // Check for disabled effect
                foreach (var disableEffect in cancellableEffects)
                    disableEffect.DisableEffect(false);

                foreach (var removedEffect in removingEffects)
                    removedEffect.Cancel(false);

                if (overwriteEffect != null)
                {
                    if (enable)
                        overwriteEffect.Overwrite(neweffect);
                    else
                        overwriteEffect.OverwriteDisabled(neweffect);
                }
                else
                {
                    if (enable)
                        neweffect.Start(target);
                    else
                        neweffect.StartDisabled(target);
                }
            }
            finally
            {
                target.EffectList.CommitChanges();
            }
            return true;
        }

        /// <summary>
        /// Calculates the initial attack generated for a duration effect.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="effectiveness"></param>
        /// <returns></returns>
        public virtual AttackData CalculateInitialAttack(GameLiving target, double effectiveness)
        {
            AttackData ad = new AttackData();
            ad.Attacker = Caster;
            ad.Target = target;
            ad.AttackType = AttackData.eAttackType.Spell;
            ad.SpellHandler = this;
            ad.AttackResult = GameLiving.eAttackResult.HitUnstyled;
            ad.IsSpellResisted = false;
            return ad;
        }

        /// <summary>
        /// Called when Effect is Added to target Effect List
        /// </summary>
        /// <param name="effect"></param>
        public virtual void OnEffectAdd(GameSpellEffect effect)
        {
        }

        /// <summary>
        /// Check for Spell Effect Removed to Enable Best Cancellable
        /// </summary>
        /// <param name="effect"></param>
        /// <param name="overwrite"></param>
        public virtual void OnEffectRemove(GameSpellEffect effect, bool overwrite)
        {
            if (!overwrite)
            {
                if (Spell.IsFocus)
                {
                    FocusSpellAction(null, Caster, null);
                }
                // Re-Enable Cancellable Effects.
                var enableEffect = effect.Owner.EffectList.OfType<GameSpellEffect>()
                    .Where(eff => eff != effect && eff.SpellHandler != null && eff.SpellHandler.IsOverwritable(effect) && eff.SpellHandler.IsCancellable(effect));

                // Find Best Remaining Effect
                GameSpellEffect best = null;
                foreach (var eff in enableEffect)
                {
                    if (best == null)
                        best = eff;
                    else if (best.SpellHandler.ShouldCancelOldEffect(best, eff))
                        best = eff;
                }

                if (best != null)
                {
                    effect.Owner.EffectList.BeginChanges();
                    try
                    {
                        // Enable Best Effect
                        best.EnableEffect();
                    }
                    finally
                    {
                        effect.Owner.EffectList.CommitChanges();
                    }
                }
            }
        }

        /// <summary>
        /// execute non duration spell effect on target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="effectiveness"></param>
        public virtual bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            return true;
        }

        /// <summary>
        /// When an applied effect starts
        /// duration spells only
        /// </summary>
        /// <param name="effect"></param>
        public virtual void OnEffectStart(GameSpellEffect effect)
        {
            if (Spell.Pulse == 0)
                SendEffectAnimation(effect.Owner, 0, false, 1);
            if (Spell.IsFocus) // Add Event handlers for focus spell
            {
                GameEventMgr.RemoveHandler(Caster, GameLivingEvent.AttackFinished, new DOLEventHandler(FocusSpellAction));
                GameEventMgr.RemoveHandler(Caster, GameLivingEvent.CastStarting, new DOLEventHandler(FocusSpellAction));
                GameEventMgr.RemoveHandler(Caster, GameLivingEvent.Moving, new DOLEventHandler(FocusSpellAction));
                GameEventMgr.RemoveHandler(Caster, GameLivingEvent.Dying, new DOLEventHandler(FocusSpellAction));
                GameEventMgr.RemoveHandler(Caster, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(FocusSpellAction));
                GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.Dying, new DOLEventHandler(FocusSpellAction));
                Caster.TempProperties.setProperty(FOCUS_SPELL, effect);
                GameEventMgr.AddHandler(Caster, GameLivingEvent.AttackFinished, new DOLEventHandler(FocusSpellAction));
                GameEventMgr.AddHandler(Caster, GameLivingEvent.CastStarting, new DOLEventHandler(FocusSpellAction));
                GameEventMgr.AddHandler(Caster, GameLivingEvent.Moving, new DOLEventHandler(FocusSpellAction));
                GameEventMgr.AddHandler(Caster, GameLivingEvent.Dying, new DOLEventHandler(FocusSpellAction));
                GameEventMgr.AddHandler(Caster, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(FocusSpellAction));
                GameEventMgr.AddHandler(effect.Owner, GameLivingEvent.Dying, new DOLEventHandler(FocusSpellAction));
            }
        }

        /// <summary>
        /// When an applied effect pulses
        /// duration spells only
        /// </summary>
        /// <param name="effect"></param>
        public virtual void OnEffectPulse(GameSpellEffect effect)
        {
            if (effect.Owner.IsAlive == false)
            {
                effect.Cancel(false);
            }
        }

        /// <summary>
        /// When an applied effect expires.
        /// Duration spells only.
        /// </summary>
        /// <param name="effect">The expired effect</param>
        /// <param name="noMessages">true, when no messages should be sent to player and surrounding</param>
        /// <returns>immunity duration in milliseconds</returns>
        public virtual int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            return 0;
        }

        /// <summary>
        /// Calculates chance of spell getting resisted
        /// </summary>
        /// <param name="target">the target of the spell</param>
        /// <returns>chance that spell will be resisted for specific target</returns>
        public virtual int CalculateSpellResistChance(GameLiving target)
        {
            if (target != null && SpellHandler.FindEffectOnTarget(target, "Damnation") != null && ((HasPositiveEffect && SpellHandler.FindEffectOnTarget(target, "Heal") == null) || Spell.SpellType == "Disease"))
            {
                return 100;
            }

            if (m_spellLine.KeyName == GlobalSpellsLines.Combat_Styles_Effect || HasPositiveEffect)
            {
                return 0;
            }

            if (m_spellLine.KeyName == GlobalSpellsLines.Item_Effects && m_spellItem != null)
            {
                GamePlayer playerCaster = Caster as GamePlayer;
                if (playerCaster != null)
                {
                    int itemSpellLevel = m_spellItem.Template.LevelRequirement > 0 ? m_spellItem.Template.LevelRequirement : Math.Min(playerCaster.MaxLevel, m_spellItem.Level);
                    return 100 - (85 + ((itemSpellLevel - target!.Level) / 2));
                }
            }

            return 100 - CalculateToHitChance(target);
        }

        /// <summary>
        /// When spell was resisted
        /// </summary>
        /// <param name="target">the target that resisted the spell</param>
        public virtual void OnSpellResisted(GameLiving target)
        {
            SendSpellResistAnimation(target);
            SendSpellResistMessages(target);
            SendSpellResistNotification(target);
            StartSpellResistInterruptTimer(target);
            StartSpellResistLastAttackTimer(target);
        }

        /// <summary>
        /// Send Spell Resisted Animation
        /// </summary>
        /// <param name="target"></param>
        public virtual void SendSpellResistAnimation(GameLiving target)
        {
            if (Spell.Pulse == 0 || !HasPositiveEffect)
                SendEffectAnimation(target, 0, false, 0);
        }

        /// <summary>
        /// Send Spell Resist Messages to Caster and Target
        /// </summary>
        /// <param name="target"></param>
        public virtual void SendSpellResistMessages(GameLiving target)
        {
            // Deliver message to the target, if the target is a pet, to its
            // owner instead.
            if (target.GetPlayerOwner() is GamePlayer owner)
            {
                this.MessageToLiving(owner, eChatType.CT_SpellResisted, LanguageMgr.GetTranslation(owner.Client, "SpellHandler.PetResistsEffect", owner.GetPersonalizedName(target)));
            }
            else if (target is GamePlayer targetPlayer)
            {
                if (SpellHandler.FindEffectOnTarget(target, "Damnation") != null && ((HasPositiveEffect && SpellHandler.FindEffectOnTarget(target, "Heal") == null) || Spell.SpellType == "Disease"))
                {
                    MessageToLiving(targetPlayer, LanguageMgr.GetTranslation(targetPlayer.Client, "SpellHandler.YouDamnedResistEffect"), eChatType.CT_SpellResisted);
                }
                else
                {
                    MessageToLiving(targetPlayer, LanguageMgr.GetTranslation(targetPlayer.Client, "SpellHandler.YouResistEffect"), eChatType.CT_SpellResisted);
                }
            }

            // Deliver message to the caster as well.
            if (target != null && SpellHandler.FindEffectOnTarget((Caster as GamePlayer), "Damnation") != null && ((HasPositiveEffect && SpellHandler.FindEffectOnTarget(target, "Heal") == null) || Spell.SpellType == "Disease"))
            {
                this.MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetDamnedResistsEffect", m_caster.GetPersonalizedName(target)), eChatType.CT_SpellResisted);
            }
            else
            {
                this.MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetResistsEffect", m_caster.GetPersonalizedName(target)), eChatType.CT_SpellResisted);
            }
        }

        /// <summary>
        /// Send Spell Attack Data Notification to Target when Spell is Resisted
        /// </summary>
        /// <param name="target"></param>
        public virtual void SendSpellResistNotification(GameLiving target)
        {
            // Report resisted spell attack data to any type of living object, no need
            // to decide here what to do. For example, NPCs will use their brain.
            // "Just the facts, ma'am, just the facts."
            AttackData ad = new AttackData();
            ad.Attacker = Caster;
            ad.Target = target;
            ad.AttackType = AttackData.eAttackType.Spell;
            ad.SpellHandler = this;
            ad.AttackResult = GameLiving.eAttackResult.Missed;
            ad.IsSpellResisted = true;
            target.OnAttackedByEnemy(ad);

        }

        /// <summary>
        /// Start Spell Interrupt Timer when Spell is Resisted
        /// </summary>
        /// <param name="target"></param>
        public virtual void StartSpellResistInterruptTimer(GameLiving target)
        {
            // Spells that would have caused damage or are not instant will still
            // interrupt a casting player.
            if (!(Spell.SpellType.IndexOf("debuff", StringComparison.OrdinalIgnoreCase) >= 0 && Spell.CastTime == 0))
                target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
        }

        /// <summary>
        /// Start Last Attack Timer when Spell is Resisted
        /// </summary>
        /// <param name="target"></param>
        public virtual void StartSpellResistLastAttackTimer(GameLiving target)
        {
            if (target.Realm == 0 || Caster.Realm == 0)
            {
                target.LastAttackedByEnemyTickPvE = target.CurrentRegion.Time;
                Caster.LastAttackTickPvE = Caster.CurrentRegion.Time;
            }
            else
            {
                target.LastAttackedByEnemyTickPvP = target.CurrentRegion.Time;
                Caster.LastAttackTickPvP = Caster.CurrentRegion.Time;
            }
        }

        #region messages

        /// <summary>
        /// Sends a message to the caster, if the caster is a controlled
        /// creature, to the player instead (only spell hit and resisted
        /// messages).
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        public void MessageToCaster(string message, eChatType type)
        {
            if (Caster is GamePlayer player)
            {
                player.MessageToSelf(message, type);
            }
            else if (Caster.GetPlayerOwner() is {} owner)
            {
                owner.MessageFromControlled(message, type);
            }
        }

        /// <summary>
        /// Sends a message to the caster, if the caster is a controlled
        /// creature, to the player instead (only spell hit and resisted
        /// messages).
        /// </summary>
        /// <param name="key"></param>
        /// <param name="type"></param>
        /// <param name="args"></param>
        public void MessageTranslationToCaster(string key, eChatType type, params object[] args)
        {
            if (Caster == null)
                return;
            
            if (Caster is GamePlayer player)
            {
                player.MessageToSelf(LanguageMgr.GetTranslation(player, key, args), type);
            }
            else if (Caster.GetPlayerOwner() is {} owner)
            {
                owner.MessageFromControlled(LanguageMgr.GetTranslation(owner, key, args), type);
            }
        }

        public void ErrorTranslationToCaster(string key, eChatType type = eChatType.CT_SpellResisted, params object[] args)
        {
            if (Spell.Radius > 0 || Spell.Target is "ground") 
                return; // Don't send errors for AOEs

            if (Parent is null)
                return; // Don't send errors for subspells
            
            MessageTranslationToCaster(key, type, args);
        }

        /// <summary>
        /// sends a message to a living
        /// </summary>
        /// <param name="living"></param>
        /// <param name="message"></param>
        /// <param name="type"></param>
        public void MessageToLiving(GameLiving living, string message, eChatType type)
        {
            if (message != null && message.Length > 0)
            {
                living.MessageToSelf(message, type);
            }
        }

        /// <summary>
        /// Sends a message to the caster, if the caster is a controlled
        /// creature, to the player instead (only spell hit and resisted
        /// messages).
        /// </summary>
        /// <param name="key"></param>
        /// <param name="type"></param>
        /// <param name="args"></param>
        public void MessageTranslationToLiving(GameLiving living, string key, eChatType type, params object[] args)
        {
            if (living is GamePlayer player)
            {
                player.MessageToSelf(LanguageMgr.GetTranslation(player, key, args), type);
            }
            else if (living.GetPlayerOwner() is {} owner)
            {
                owner.MessageFromControlled(LanguageMgr.GetTranslation(owner, key, args), type);
            }
        }

        /// <summary>
        /// Hold events for focus spells
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected virtual void FocusSpellAction(DOLEvent e, object sender, EventArgs args)
        {
            GameLiving living = sender as GameLiving;
            if (living == null) return;

            GameSpellEffect currentEffect = (GameSpellEffect)living.TempProperties.getProperty<object>(FOCUS_SPELL, null);
            if (currentEffect == null)
                return;

            GameEventMgr.RemoveHandler(Caster, GameLivingEvent.AttackFinished, new DOLEventHandler(FocusSpellAction));
            GameEventMgr.RemoveHandler(Caster, GameLivingEvent.CastStarting, new DOLEventHandler(FocusSpellAction));
            GameEventMgr.RemoveHandler(Caster, GameLivingEvent.Moving, new DOLEventHandler(FocusSpellAction));
            GameEventMgr.RemoveHandler(Caster, GameLivingEvent.Dying, new DOLEventHandler(FocusSpellAction));
            GameEventMgr.RemoveHandler(Caster, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(FocusSpellAction));
            GameEventMgr.RemoveHandler(currentEffect.Owner, GameLivingEvent.Dying, new DOLEventHandler(FocusSpellAction));
            Caster.TempProperties.removeProperty(FOCUS_SPELL);

            CancelPulsingSpell(Caster, currentEffect.Spell.SpellType);
            currentEffect.Cancel(false);

            MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.LostFocusOnSpell", currentEffect.Spell.Name), eChatType.CT_SpellExpires);

            if (e == GameLivingEvent.Moving)
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.InterruptFocus"), eChatType.CT_Important);
        }
        #endregion

        /// <summary>
        /// Ability to cast a spell
        /// </summary>
        public ISpellCastingAbilityHandler Ability
        {
            get { return m_ability; }
            set { m_ability = value; }
        }

        public Spell Spell => m_spell;
        public SpellLine SpellLine => m_spellLine;
        public virtual string CostType => "Power";
        public GameLiving Caster => m_caster;
        public bool IsCasting
            => m_castTimer != null && m_castTimer.IsAlive;

        public virtual bool HasPositiveEffect => m_spell.IsHelpful;

        public virtual bool CanBeRightClicked => HasPositiveEffect;

        /// <summary>
        /// Is this Spell purgeable
        /// </summary>
        public virtual bool IsUnPurgeAble => false;

        public byte DelveInfoDepth
        {
            get { return m_delveInfoDepth; }
            set { m_delveInfoDepth = value; }
        }

        public virtual IList<string> DelveInfo
        {
            get
            {
                var list = new List<string>(32);
                list.Add(Spell.Description);
                list.Add(" ");
                if (Spell.InstrumentRequirement != 0)
                    list.Add(GetTranslation("DelveInfo.InstrumentRequire", GlobalConstants.InstrumentTypeToName(Spell.InstrumentRequirement)));
                if (Spell.Damage != 0)
                    list.Add(GetTranslation("DelveInfo.Damage", Spell.Damage.ToString("0.###;0.###'%'")));
                if (Spell.LifeDrainReturn != 0)
                    list.Add(GetTranslation("DelveInfo.HealthReturned", Spell.LifeDrainReturn));
                else if (Spell.Value != 0)
                    list.Add(GetTranslation("DelveInfo.Value", Spell.Value.ToString("0.###;0.###'%'")));
                list.Add(GetTranslation("DelveInfo.Target", Spell.Target));
                if (Spell.Range != 0)
                    list.Add(GetTranslation("DelveInfo.Range", Spell.Range));
                if (Spell.Duration >= ushort.MaxValue * 1000)
                    list.Add(GetTranslation("DelveInfo.Duration") + " Permanent.");
                else if (Spell.Duration > 60000)
                    list.Add(GetTranslation("DelveInfo.Duration") + " " + Spell.Duration / 60000 + ":" + (Spell.Duration % 60000 / 1000).ToString("00") + " min");
                else if (Spell.Duration != 0)
                    list.Add(GetTranslation("DelveInfo.Duration") + " " + (Spell.Duration / 1000).ToString("0' sec';'Permanent.';'Permanent.'"));
                if (Spell.Frequency != 0)
                    list.Add(GetTranslation("DelveInfo.Frequency", (Spell.Frequency * 0.001).ToString("0.0")));
                if (Spell.Power != 0)
                    list.Add(GetTranslation("DelveInfo.PowerCost", Spell.Power.ToString("0;0'%'")));
                list.Add(GetTranslation("DelveInfo.CastingTime", (Spell.CastTime * 0.001).ToString("0.0## sec;-0.0## sec;'instant'")));
                if (Spell.RecastDelay > 60000)
                    list.Add(GetTranslation("DelveInfo.RecastTime") + " " + (Spell.RecastDelay / 60000).ToString() + ":" + (Spell.RecastDelay % 60000 / 1000).ToString("00") + " min");
                else if (Spell.RecastDelay > 0)
                    list.Add(GetTranslation("DelveInfo.RecastTime") + " " + (Spell.RecastDelay / 1000).ToString() + " sec");
                if (Spell.Concentration != 0)
                    list.Add(GetTranslation("DelveInfo.ConcentrationCost", Spell.Concentration));
                if (Spell.Radius != 0)
                    list.Add(GetTranslation("DelveInfo.Radius", Spell.Radius));
                if (Spell.DamageType != eDamageType.Natural)
                    list.Add(GetTranslation("DelveInfo.Damage", GlobalConstants.DamageTypeToName(Spell.DamageType)));
                if (Spell.IsFocus)
                    list.Add(GetTranslation("DelveInfo.Focus"));

                return list;
            }
        }

        private string GetTranslation(string translationId, params object[] args)
        {
            GamePlayer player = Caster.GetController() as GamePlayer;

            if (player != null) return LanguageMgr.GetTranslation(player.Client, translationId, args);
            else return LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, translationId, args);
        }

        public ISpellHandler Parent { get; set; }

        // warlock add
        public static GameSpellEffect FindEffectOnTarget(GameLiving target, string spellType, string spellName)
        {
            lock (target.EffectList)
            {
                foreach (IGameEffect fx in target.EffectList)
                {
                    if (!(fx is GameSpellEffect))
                        continue;
                    GameSpellEffect effect = (GameSpellEffect)fx;
                    if (fx is GameSpellAndImmunityEffect && ((GameSpellAndImmunityEffect)fx).ImmunityState)
                        continue; // ignore immunity effects

                    if (effect.SpellHandler.Spell != null && (effect.SpellHandler.Spell.SpellType == spellType) && (effect.SpellHandler.Spell.Name == spellName))
                    {
                        return effect;
                    }
                }
            }
            return null;
        }
        /// <summary>
        /// Find effect by spell type
        /// </summary>
        /// <returns>first occurance of effect in target's effect list or null</returns>
        public static GameSpellEffect FindEffectOnTarget(GameLiving target, string spellType)
        {
            if (target == null)
                return null;

            lock (target.EffectList)
            {
                foreach (IGameEffect fx in target.EffectList)
                {
                    if (!(fx is GameSpellEffect))
                        continue;
                    GameSpellEffect effect = (GameSpellEffect)fx;
                    if (fx is GameSpellAndImmunityEffect && ((GameSpellAndImmunityEffect)fx).ImmunityState)
                        continue; // ignore immunity effects
                    if (effect.SpellHandler.Spell != null && (effect.SpellHandler.Spell.SpellType == spellType))
                    {
                        return effect;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Find effect by spell handler
        /// </summary>
        /// <returns>first occurance of effect in target's effect list or null</returns>
        public static GameSpellEffect FindEffectOnTarget(GameLiving target, ISpellHandler spellHandler)
        {
            lock (target.EffectList)
            {
                foreach (IGameEffect effect in target.EffectList)
                {
                    GameSpellEffect gsp = effect as GameSpellEffect;
                    if (gsp == null)
                        continue;
                    if (gsp.SpellHandler != spellHandler)
                        continue;
                    if (gsp is GameSpellAndImmunityEffect && ((GameSpellAndImmunityEffect)gsp).ImmunityState)
                        continue; // ignore immunity effects
                    return gsp;
                }
            }
            return null;
        }

        /// <summary>
        /// Find effect by spell handler
        /// </summary>
        /// <returns>first occurance of effect in target's effect list or null</returns>
        public static GameSpellEffect FindEffectOnTarget<T>(GameLiving target) where T : SpellHandler
        {
            lock (target.EffectList)
            {
                foreach (IGameEffect effect in target.EffectList)
                {
                    GameSpellEffect gsp = effect as GameSpellEffect;
                    if (gsp == null)
                        continue;
                    if (gsp.SpellHandler is not T)
                        continue;
                    if (gsp is GameSpellAndImmunityEffect && ((GameSpellAndImmunityEffect)gsp).ImmunityState)
                        continue; // ignore immunity effects
                    return gsp;
                }
            }
            return null;
        }

        /// <summary>
        /// Find effect by spell handler
        /// </summary>
        /// <returns>first occurance of effect in target's effect list or null</returns>
        public static GameSpellEffect FindEffectOnTarget(GameLiving target, Func<IGameEffect, bool> predicate, bool skipImmunity = true)
        {
            lock (target.EffectList)
            {
                foreach (IGameEffect effect in target.EffectList)
                {
                    GameSpellEffect gsp = effect as GameSpellEffect;
                    if (gsp == null)
                        continue;
                    if (skipImmunity && gsp is GameSpellAndImmunityEffect { ImmunityState: true })
                        continue;
                    if (!predicate(effect))
                        continue;
                    return gsp;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns true if the target has the given static effect, false
        /// otherwise.
        /// </summary>
        public static IGameEffect FindStaticEffectOnTarget(GameLiving target, Type effectType)
        {
            if (target == null)
                return null;

            lock (target.EffectList)
            {
                foreach (IGameEffect effect in target.EffectList)
                    if (effect.GetType() == effectType)
                        return effect;
            }
            return null;
        }

        /// <summary>
        /// Find pulsing spell by spell handler
        /// </summary>
        /// <param name="living"></param>
        /// <param name="handler"></param>
        /// <returns>first occurance of spellhandler in targets' conc list or null</returns>
        public static PulsingSpellEffect FindPulsingSpellOnTarget(GameLiving living, ISpellHandler handler)
        {
            lock (living.ConcentrationEffects)
            {
                foreach (IConcentrationEffect concEffect in living.ConcentrationEffects)
                {
                    PulsingSpellEffect pulsingSpell = concEffect as PulsingSpellEffect;
                    if (pulsingSpell == null) continue;
                    if (pulsingSpell.SpellHandler == handler)
                        return pulsingSpell;
                }
                return null;
            }
        }

        #region various helpers

        /// <summary>
        /// Level mod for effect between target and caster if there is any
        /// </summary>
        /// <returns></returns>
        public virtual double GetLevelModFactor()
        {
            return 0.02;  // Live testing done Summer 2009 by Bluraven, Tolakram  Levels 40, 45, 50, 55, 60, 65, 70
        }

        /// <summary>
        /// Calculates min damage variance %
        /// </summary>
        /// <param name="target">spell target</param>
        /// <param name="min">returns min variance</param>
        /// <param name="max">returns max variance</param>
        public virtual void CalculateDamageVariance(GameLiving target, out double min, out double max)
        {
            if (m_spellLine.KeyName == GlobalSpellsLines.Item_Effects)
            {
                min = 1.0;
                max = 1.25;
                return;
            }

            if (m_spellLine.KeyName == GlobalSpellsLines.Combat_Styles_Effect)
            {
                if (UseMinVariance)
                {
                    min = 1.50;
                }
                else
                {
                    min = 1.00;
                }

                max = 1.50;

                return;
            }

            if (m_spellLine.KeyName == GlobalSpellsLines.Reserved_Spells)
            {
                min = max = 1.0;
                return;
            }

            int speclevel = 1;
            var level = Caster.Level;

            if (Caster is NecromancerPet pet)
            {
                var owner = pet.GetLivingOwner();
                if (owner != null)
                {
                    if (!string.IsNullOrWhiteSpace(SpellLine.Spec))
                        speclevel = owner.GetModifiedSpecLevel(SpellLine.Spec);
                    else
                        speclevel = pet.GetModifiedSpecLevel(SpellLine.KeyName);

                    if (speclevel <= 1)
                        speclevel = owner.Level;

                    if (owner is GamePlayer gp)
                        level = gp.Level;
                }
            }
            else if (Caster is GameNPC npc)
                speclevel = npc.Level  * 2 / 3; // divide for variance
            else if (Caster is GamePlayer p)
                speclevel = Math.Max(Caster.GetModifiedSpecLevel(SpellLine.Spec), Caster.GetModifiedSpecLevel(SpellLine.KeyName));

            min = 1;
            max = 1;

            if (m_spellLine.IsBaseLine)
            {
                if (speclevel >= level)
                    min = 1;
                else if (target.Level == 0 || level == 0)
                    min = 1;
                else 
                    min = 0.25 + Math.Min(.75, .75 * ((double)speclevel / level));
            }
            else
            {
                min = 1;
            }

            if (min < 0)
                min = 0;

            if (min > max)
                min = max;
        }

        /// <summary>
        /// Player pet damage cap
        /// This simulates a player casting a baseline nuke with the capped damage near (but not exactly) that of the equivilent spell of the players level.
        /// This cap is not applied if the player is level 50
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public virtual double CapPetSpellDamage(double damage, GamePlayer player)
        {
            double cappedDamage = damage;

            if (player.Level < 13)
            {
                cappedDamage = 4.1 * player.Level;
            }

            if (player.Level < 50)
            {
                cappedDamage = 3.8 * player.Level;
            }

            return Math.Min(damage, cappedDamage);
        }


        /// <summary>
        /// Put a calculated cap on NPC damage to solve a problem where an npc is given a high level spell but needs damage
        /// capped to the npc level.  This uses player spec nukes to calculate damage cap.
        /// NPC's level 50 and above are not capped
        /// </summary>
        /// <param name="damage"></param>
        /// <param name="player"></param>
        /// <returns></returns>
        public virtual double CapNPCSpellDamage(double damage, GameNPC npc)
        {
            if (npc.Level < 50)
            {
                return Math.Min(damage, 4.7 * npc.Level);
            }

            return damage;
        }

        /// <summary>
        /// Calculates the base 100% spell damage which is then modified by damage variance factors
        /// </summary>
        /// <returns></returns>
        public virtual double CalculateDamageBase(GameLiving target)
        {
            double spellDamage = Spell.Damage;
            GamePlayer player = Caster as GamePlayer;

            // For pets the stats of the owner have to be taken into account.

            if (Caster is GameNPC && ((Caster as GameNPC)!.Brain) is IControlledBrain)
            {
                player = (((Caster as GameNPC)!.Brain) as IControlledBrain)!.Owner as GamePlayer;
            }

            if (player != null)
            {
                if (Caster is GamePet pet)
                {
                    // There is no reason to cap pet spell damage if it's being scaled anyway.
                    if (ServerProperties.Properties.PET_SCALE_SPELL_MAX_LEVEL <= 0)
                        spellDamage = CapPetSpellDamage(spellDamage, player);

                    spellDamage *= ((pet.Intelligence + 200) / 275.0);
                }

                if (SpellLine.KeyName == GlobalSpellsLines.Combat_Styles_Effect)
                {
                    double stats = Caster.GetModified(eProperty.Strength);
                    if (player.CharacterClass.ID == (int) eCharacterClass.Reaver)
                    {
                        stats += Caster.GetModified(eProperty.Dexterity);
                        stats /= 2d;
                    }
                    spellDamage *= (stats + 225) / 225d;
                    spellDamage *= 0.9;
                }

                else if (player.CharacterClass.ManaStat != eStat.UNDEFINED
                    && SpellLine.KeyName != GlobalSpellsLines.Combat_Styles_Effect
                    && m_spellLine.KeyName != GlobalSpellsLines.Mundane_Poisons
                    && SpellLine.KeyName != GlobalSpellsLines.Item_Effects
                    && player.CharacterClass.ID != (int)eCharacterClass.MaulerAlb
                    && player.CharacterClass.ID != (int)eCharacterClass.MaulerMid
                    && player.CharacterClass.ID != (int)eCharacterClass.MaulerHib
                    && player.CharacterClass.ID != (int)eCharacterClass.Vampiir)
                {
                    int manaStatValue = player.GetModified((eProperty)player.CharacterClass.ManaStat);
                    spellDamage *= (manaStatValue + 200) / 275.0;
                    spellDamage *= 0.9;
                }
            }
            else if (Caster is GameNPC)
            {
                if (SpellLine.KeyName == GlobalSpellsLines.Combat_Styles_Effect)
                {
                    var stats = Caster.GetModified(eProperty.Dexterity) + Caster.GetModified(eProperty.Strength);
                    spellDamage *= (stats / 2d + 150) / 225d;
                }
                else
                {
                    int manaStatValue = Caster.GetModified(eProperty.Intelligence);
                    spellDamage = CapNPCSpellDamage(spellDamage, (GameNPC)Caster)*(manaStatValue + 200)/275.0;
                }
                spellDamage *= 0.9;
            }

            if (spellDamage < 0)
                spellDamage = 0;

            return spellDamage;
        }

        /// <summary>
        /// Calculates the chance that the spell lands on target
        /// can be negative or above 100%
        /// </summary>
        /// <param name="target">spell target</param>
        /// <returns>chance that the spell lands on target</returns>
        public virtual int CalculateToHitChance(GameLiving target)
        {
            if (Parent != null && Parent is EarthquakeSpellHandler earthquake)
                return earthquake.CalculateToHitChance(target);

            int spellLevel = Spell.Level;

            GameLiving caster = null;
            if (m_caster is GameNPC && (m_caster as GameNPC)!.Brain is ControlledNpcBrain)
            {
                caster = ((ControlledNpcBrain)((GameNPC)m_caster).Brain).Owner;
            }
            else
            {
                caster = m_caster;
            }

            int spellbonus = caster.GetModified(eProperty.SpellLevel);
            spellLevel += spellbonus;

            GamePlayer playerCaster = caster as GamePlayer;

            if (playerCaster != null)
            {
                if (spellLevel > playerCaster.SpellMaxLevel)
                {
                    spellLevel = playerCaster.SpellMaxLevel;
                }
            }

            GameSpellEffect effect = FindEffectOnTarget(m_caster, "HereticPiercingMagic");
            if (effect != null)
            {
                spellLevel += (int)effect.Spell.Value;
            }

            if (playerCaster != null && (m_spellLine.KeyName == GlobalSpellsLines.Combat_Styles_Effect || m_spellLine.KeyName.StartsWith(GlobalSpellsLines.Champion_Lines_StartWith)))
            {
                spellLevel = Math.Min(playerCaster.SpellMaxLevel, target!.Level);
            }

            int bonustohit = m_caster.GetModified(eProperty.ToHitBonus);

            //Piercing Magic affects to-hit bonus too
            GameSpellEffect resPierce = SpellHandler.FindEffectOnTarget(m_caster, "PenetrateResists");
            if (resPierce != null)
                bonustohit += (int)resPierce.Spell.Value;

            /*
            http://www.camelotherald.com/news/news_article.php?storyid=704

            Q: Spell resists. Can you give me more details as to how the system works?

            A: Here's the answer, straight from the desk of the spell designer:

            "Spells have a factor of (spell level / 2) added to their chance to hit. (Spell level defined as the level the spell is awarded, chance to hit defined as
            the chance of avoiding the "Your target resists the spell!" message.) Subtracted from the modified to-hit chance is the target's (level / 2).
            So a L50 caster casting a L30 spell at a L50 monster or player, they have a base chance of 85% to hit, plus 15%, minus 25% for a net chance to hit of 75%.
            If the chance to hit goes over 100% damage or duration is increased, and if it goes below 55%, you still have a 55% chance to hit but your damage
            or duration is penalized. If the chance to hit goes below 0, you cannot hit at all. Once the spell hits, damage and duration are further modified
            by resistances.

            Note:  The last section about maintaining a chance to hit of 55% has been proven incorrect with live testing.  The code below is very close to live like.
            - Tolakram
             */

            int missrate = 15;
            if (!(GameServer.ServerRules.IsPveOnlyBonus(eProperty.DefensiveBonus) && GameServer.ServerRules.IsPvPAction(Caster, target)))
                missrate += target.GetModified(eProperty.DefensiveBonus);
            if (caster is GamePlayer && target is GamePlayer)
            {
                missrate = (int)(missrate * ServerProperties.Properties.PVP_BASE_MISS_MULTIPLIER);
            }
            else
            {
                missrate = (int)(missrate * ServerProperties.Properties.PVE_BASE_MISS_MULTIPLIER);
            }

            if (caster.EffectList.GetOfType<AdrenalineMageSpellEffect>() != null)
            {
                missrate -= AdrenalineMageSpellEffect.HIT_BONUS;
            }

            int hitchance = 100 - missrate + ((spellLevel - target!.Level) / 2) + bonustohit;

            if (!(caster is GamePlayer && target is GamePlayer))
            {
                hitchance -= (int)(m_caster.GetConLevel(target) * ServerProperties.Properties.PVE_SPELL_CONHITPERCENT);
                hitchance += Math.Max(0, target.Attackers.Count - 1) * ServerProperties.Properties.MISSRATE_REDUCTION_PER_ATTACKERS;
            }

            // [Freya] Nidel: Harpy Cloak : They have less chance of landing melee attacks, and spells have a greater chance of affecting them.
            if ((target is GamePlayer))
            {
                GameSpellEffect harpyCloak = FindEffectOnTarget(target, "HarpyFeatherCloak");
                if (harpyCloak != null)
                {
                    hitchance += (int)((hitchance * harpyCloak.Spell.Value) * 0.01);
                }
            }

            return hitchance;
        }

        /// <summary>
        /// Calculates damage to target with resist chance and stores it in ad
        /// </summary>
        /// <param name="target">spell target</param>
        /// <returns>attack data</returns>
        public AttackData CalculateDamageToTarget(GameLiving target)
        {
            return CalculateDamageToTarget(target, 1);
        }


        /// <summary>
        /// Adjust damage based on chance to hit.
        /// </summary>
        /// <param name="damage"></param>
        /// <param name="hitChance"></param>
        /// <returns></returns>
        public virtual int AdjustDamageForHitChance(int damage, int hitChance)
        {
            int adjustedDamage = damage;

            if (hitChance < 55)
            {
                adjustedDamage += (int)(adjustedDamage * (hitChance - 55) * ServerProperties.Properties.SPELL_HITCHANCE_DAMAGE_REDUCTION_MULTIPLIER * 0.01);
            }

            return Math.Max(adjustedDamage, 1);
        }


        /// <summary>
        /// Calculates damage to target with resist chance and stores it in ad
        /// </summary>
        /// <param name="target">spell target</param>
        /// <param name="effectiveness">value from 0..1 to modify damage</param>
        /// <returns>attack data</returns>
        public virtual AttackData CalculateDamageToTarget(GameLiving target, double effectiveness)
        {
            AttackData ad = new AttackData();
            ad.Attacker = m_caster;
            ad.Target = target;
            ad.AttackType = AttackData.eAttackType.Spell;
            ad.SpellHandler = this;
            ad.AttackResult = GameLiving.eAttackResult.HitUnstyled;

            m_lastAttackData = ad;

            double minVariance;
            double maxVariance;

            CalculateDamageVariance(target, out minVariance, out maxVariance);
            double spellDamage = CalculateDamageBase(target);

            effectiveness += m_caster.GetModified(eProperty.SpellDamage) * 0.01;
            if (m_caster is GamePlayer)
            {
                // Relic bonus applied to damage, does not alter effectiveness or increase cap
                spellDamage *= (1.0 + RelicMgr.GetRelicBonusModifier(m_caster.Realm, eRelicType.Magic));
            }

            // Apply casters effectiveness
            spellDamage *= m_caster.Effectiveness;

            int finalDamage = Util.Random((int)(minVariance * spellDamage), (int)(maxVariance * spellDamage));

            // Live testing done Summer 2009 by Bluraven, Tolakram  Levels 40, 45, 50, 55, 60, 65, 70
            // Damage reduced by chance < 55, no extra damage increase noted with hitchance > 100
            int hitChance = CalculateToHitChance(ad.Target);
            finalDamage = AdjustDamageForHitChance(finalDamage, hitChance);

            // apply spell effectiveness
            finalDamage = (int)(finalDamage * effectiveness);

            if ((m_caster is GamePlayer || (m_caster is GameNPC && (m_caster as GameNPC)!.Brain is IControlledBrain && m_caster.Realm != 0)))
            {
                if (target is GamePlayer)
                    finalDamage = (int)((double)finalDamage * ServerProperties.Properties.PVP_SPELL_DAMAGE);
                else if (target is GameNPC)
                    finalDamage = (int)((double)finalDamage * ServerProperties.Properties.PVE_SPELL_DAMAGE);
            }

            // Well the PenetrateResistBuff is NOT ResistPierce
            GameSpellEffect penPierce = SpellHandler.FindEffectOnTarget(m_caster, "PenetrateResists");
            if (penPierce != null)
            {
                finalDamage = (int)(finalDamage * (1.0 + penPierce.Spell.Value / 100.0));
            }

            int cdamage = 0;
            if (finalDamage < 0)
                finalDamage = 0;

            eDamageType damageType = DetermineSpellDamageType();

            #region Resists
            eProperty property = target.GetResistTypeForDamage(damageType);
            // The Daoc resistsystem is since 1.65 a 2category system.
            // - First category are Item/Race/Buff/RvrBanners resists that are displayed in the characteroverview.
            // - Second category are resists that are given through RAs like avoidance of magic, brilliance aura of deflection.
            //   Those resist affect ONLY the spelldamage. Not the duration, not the effectiveness of debuffs.
            // so calculation is (finaldamage * Category1Modification) * Category2Modification
            // -> Remark for the future: VampirResistBuff is Category2 too.
            // - avi

            #region Primary Resists
            int primaryResistModifier = ad.Target.GetResist(damageType);

            /* Resist Pierce
             * Resipierce is a special bonus which has been introduced with TrialsOfAtlantis.
             * At the calculation of SpellDamage, it reduces the resistance that the victim recives
             * through ITEMBONUSES for the specified percentage.
             * http://de.daocpedia.eu/index.php/Resistenz_durchdringen (translated)
             */
            int resiPierce = Caster.GetModified(eProperty.ResistPierce);
            GamePlayer ply = Caster as GamePlayer;
            if (resiPierce > 0 && Spell.SpellType != "Archery")
            {
                //substract max ItemBonus of property of target, but atleast 0.
                primaryResistModifier -= Math.Max(0, Math.Min(ad.Target.ItemBonus[(int)property], resiPierce));
            }
            #endregion

            #region Secondary Resists
            //Using the resist BuffBonusCategory2 - its unused in ResistCalculator
            int secondaryResistModifier = target.SpecBuffBonusCategory[(int)property];

            if (secondaryResistModifier > 80)
                secondaryResistModifier = 80;
            #endregion

            int resistModifier = 0;
            //primary resists
            resistModifier += (int)(finalDamage * (double)primaryResistModifier * -0.01);
            //secondary resists
            resistModifier += (int)((finalDamage + (double)resistModifier) * (double)secondaryResistModifier * -0.01);
            //apply resists
            finalDamage += resistModifier;

            #endregion

            // Apply damage cap (this can be raised by effectiveness)
            if (finalDamage > DamageCap(effectiveness))
            {
                finalDamage = (int)DamageCap(effectiveness);
            }

            if (finalDamage < 0)
                finalDamage = 0;

            int criticalchance = (m_caster.SpellCriticalChance);

            if (Util.Chance(Math.Min(50, criticalchance)) && (finalDamage >= 1))
            {
                int critmax = (ad.Target is GamePlayer) ? finalDamage / 2 : finalDamage;
                cdamage = Util.Random(finalDamage / 10, critmax); //think min crit is 10% of damage
            }
            //Andraste
            if (ad.Target is GamePlayer && ad.Target.GetModified(eProperty.Conversion) > 0)
            {
                int manaconversion = (int)Math.Round(((double)ad.Damage + (double)ad.CriticalDamage) * (double)ad.Target.GetModified(eProperty.Conversion) / 200);
                //int enduconversion=(int)Math.Round((double)manaconversion*(double)ad.Target.MaxEndurance/(double)ad.Target.MaxMana);
                int enduconversion = (int)Math.Round(((double)ad.Damage + (double)ad.CriticalDamage) * (double)ad.Target.GetModified(eProperty.Conversion) / 200);
                if (ad.Target.Mana + manaconversion > ad.Target.MaxMana) manaconversion = ad.Target.MaxMana - ad.Target.Mana;
                if (ad.Target.Endurance + enduconversion > ad.Target.MaxEndurance) enduconversion = ad.Target.MaxEndurance - ad.Target.Endurance;
                if (manaconversion < 1) manaconversion = 0;
                if (enduconversion < 1) enduconversion = 0;
                if (manaconversion >= 1) (ad.Target as GamePlayer)!.Out.SendMessage(LanguageMgr.GetTranslation((ad.Target as GamePlayer)?.Client, "GameLiving.AttackData.GainPowerPoints", manaconversion), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                if (enduconversion >= 1) (ad.Target as GamePlayer)!.Out.SendMessage(LanguageMgr.GetTranslation((ad.Target as GamePlayer)?.Client, "GameLiving.AttackData.GainEndurancePoints", enduconversion), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                ad.Target.Endurance += enduconversion; if (ad.Target.Endurance > ad.Target.MaxEndurance) ad.Target.Endurance = ad.Target.MaxEndurance;
                ad.Target.Mana += manaconversion; if (ad.Target.Mana > ad.Target.MaxMana) ad.Target.Mana = ad.Target.MaxMana;
            }

            ad.Damage = finalDamage;
            ad.CriticalDamage = cdamage;
            ad.DamageType = damageType;
            ad.Modifier = resistModifier;

            m_lastAttackData = ad;
            return ad;
        }

        public virtual double DamageCap(double effectiveness)
        {
            return Spell.Damage * 3.0 * effectiveness;
        }

        /// <summary>
        /// What damage type to use.  Overriden by archery
        /// </summary>
        /// <returns></returns>
        public virtual eDamageType DetermineSpellDamageType()
        {
            return Spell.DamageType;
        }

        /// <summary>
        /// Sends damage text messages but makes no damage
        /// </summary>
        /// <param name="ad"></param>
        public virtual void SendDamageMessages(AttackData ad)
        {
            string modmessage = "";

            //Update value if npc is Invincible Group
            if (Caster is GamePlayer && ad.Target is GameNPC npc)
            {
                if (npc.IsInvincible(ad.DamageType))
                {
                    ad.Damage = 0;
                    ad.CriticalDamage = 0;
                    OnSpellResisted(ad.Target);
                    return;
                }
            }

            if (ad.Modifier > 0)
                modmessage = " (+" + ad.Modifier + ")";
            if (ad.Modifier < 0)
                modmessage = " (" + ad.Modifier + ")";
            if (Caster is GamePlayer or NecromancerPet)
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HitTarget", Caster.GetPersonalizedName(ad.Target), ad.Damage, modmessage), eChatType.CT_YouHit);
            else if (Caster is GameNPC)
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.NPCHitTarget", Caster.Name, ad.Target.GetName(0, false), ad.Damage, modmessage), eChatType.CT_YouHit);
            if (ad.CriticalDamage > 0)
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.CriticalHit", ad.CriticalDamage), eChatType.CT_YouHit);
        }

        /// <summary>
        /// Make damage to target and send spell effect but no messages
        /// </summary>
        /// <param name="ad"></param>
        /// <param name="showEffectAnimation"></param>
        public virtual void DamageTarget(AttackData ad, bool showEffectAnimation)
        {
            DamageTarget(ad, showEffectAnimation, 0x14); //spell damage attack result
        }

        /// <summary>
        /// Make damage to target and send spell effect but no messages
        /// </summary>
        /// <param name="ad"></param>
        /// <param name="showEffectAnimation"></param>
        /// <param name="attackResult"></param>
        public virtual void DamageTarget(AttackData ad, bool showEffectAnimation, int attackResult)
        {
            ad.AttackResult = GameLiving.eAttackResult.HitUnstyled;

            if (showEffectAnimation)
            {
                SendEffectAnimation(ad.Target, 0, false, 1);
            }

            if (ad.Damage > 0)
                foreach (GamePlayer player in ad.Target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    player.Out.SendCombatAnimation(ad.Attacker, ad.Target, 0, 0, 0, 0, (byte)attackResult, ad.Target.HealthPercent);

            // send animation before dealing damage else dead livings show no animation
            ad.Target.OnAttackedByEnemy(ad);

            if (ad.Target is GameNPC npc)
            {
                //Check if enemy is invincible
                if (npc.IsInvincible(ad.DamageType))
                {
                    ad.Damage = 0;
                    ad.CriticalDamage = 0;
                }
            }

            ad.Attacker.DealDamage(ad);
            // Treat non-damaging effects as attacks to trigger an immediate response and BAF
            if (ad.Damage == 0 && ad.Target is GameNPC { Brain: IOldAggressiveBrain aggroBrain })
            {
                aggroBrain.AddToAggroList(Caster, 1);
            }

            m_lastAttackData = ad;
        }

        #endregion

        #region saved effects
        public virtual PlayerXEffect GetSavedEffect(GameSpellEffect effect)
        {
            return null;
        }

        public virtual void OnEffectRestored(GameSpellEffect effect, int[] vars)
        { }

        public virtual int OnRestoredEffectExpires(GameSpellEffect effect, int[] vars, bool noMessages)
        {
            return 0;
        }
        #endregion

        #region tooltip handling

        public virtual string GetDelveDescription(GameClient delveClient)
        {
            SpellHandlerAttribute attr = (SpellHandlerAttribute)Attribute.GetCustomAttribute(GetType(), typeof(SpellHandlerAttribute));
            if (attr != null)
            {
                if (LanguageMgr.TryGetTranslation(out string translation, delveClient, "SpellDescription." + attr.SpellType + ".MainDescription"))
                {
                    return translation;
                }
            }
            /*if (ShortDescription != null)
            {
                return ShortDescription;
            }*/
            return LanguageMgr.GetTranslation(delveClient, "SpellHandler.DefaultDescription", GetType().ToString().Split('.').Last(), Spell.Value, Spell.Damage);
        }

        [Obsolete("Use & override GetDelveDescription instead")]
        public virtual string ShortDescription
        {
            get => null;
        }
        #endregion
    }
}
