using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;

namespace DOL.GS.Spells
{
    [SpellHandler("SummonGenistarPet")]
    public class SummonGenistarPet : SummonSpellHandler
    {
        public SummonGenistarPet(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            if (!base.CheckBeginCast(selectedTarget, quiet)) return false;

            if (Caster is GamePlayer player)
            {
                string lang = player.Client?.Account?.Language ?? "EN";

                if (player.IsInPvP || player.IsInRvR || player.isInBG)
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation(lang, "Genistar.SummonGenistar.PvPContested"), eChatType.CT_System);
                    return false;
                }

                bool hasIllusionOrMultipet = false;
                lock (player.EffectList)
                {
                    foreach (IGameEffect effect in player.EffectList)
                    {
                        if (effect is GameSpellEffect spellEffect)
                        {
                            if (spellEffect.SpellHandler is IllusionSpell || spellEffect.SpellHandler is SummonMultiTemplatePetsHandler)
                            {
                                hasIllusionOrMultipet = true;
                                break;
                            }
                        }
                    }
                }

                if (player.ControlledBrain != null || hasIllusionOrMultipet)
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation(lang, "Genistar.SummonGenistar.AlreadySummoned"), eChatType.CT_System);
                    return false;
                }

                if (player.IsOnHorse || player.Steed != null || player.IsSummoningMount || player.IsRiding)
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation(lang, "Genistar.SummonGenistar.Mounted"), eChatType.CT_System);
                    return false;
                }
            }

            return true;
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            GamePlayer player = Caster as GamePlayer;
            if (player == null) return false;
            string lang = player.Client?.Account?.Language ?? "EN";

            InventoryItem castItem = m_spellItem;

            if (castItem == null || !castItem.Id_nb.StartsWith("genistar_pet") || string.IsNullOrEmpty(castItem.PackageID))
            {
                lock (player.Inventory)
                {
                    foreach (InventoryItem item in player.Inventory.AllItems)
                    {
                        if (item.Id_nb.StartsWith("genistar_pet") && !string.IsNullOrEmpty(item.PackageID))
                        {
                            castItem = item;
                            break;
                        }
                    }
                }
            }

            if (castItem == null)
            {
                MessageToCaster(LanguageMgr.GetTranslation(lang, "Genistar.SummonGenistar.NeedItem"), eChatType.CT_System);
                return false;
            }

            string genistarID = castItem.PackageID;

            DBGenistar dbRecord = GameServer.Database.SelectObject<DBGenistar>(DB.Column("GenistarID").IsEqualTo(genistarID));
            if (dbRecord == null)
            {
                MessageToCaster(LanguageMgr.GetTranslation(lang, "Genistar.SummonGenistar.NotFoundDB"), eChatType.CT_System);
                return false;
            }

            INpcTemplate template = NpcTemplateMgr.GetTemplate(dbRecord.BaseTemplateID);
            if (template == null) return false;

            int storedCondition = castItem.Condition;
            int storedMaxCondition = castItem.MaxCondition;

            string targetUniqueKey = (castItem.Template is ItemUnique uTmpl) ? uTmpl.Id_nb : null;

            if (!player.Inventory.RemoveItem(castItem))
            {
                MessageToCaster(LanguageMgr.GetTranslation(lang, "Genistar.SummonGenistar.FailedUse"), eChatType.CT_System);
                return false;
            }

            if (!string.IsNullOrEmpty(targetUniqueKey))
            {
                ItemUnique dbUnique = GameServer.Database.FindObjectByKey<ItemUnique>(targetUniqueKey);
                if (dbUnique != null)
                {
                    GameServer.Database.DeleteObject(dbUnique);
                }
            }

            if (m_spellItem == castItem)
            {
                m_spellItem = null;
            }

            dbRecord.State = (int)eGenistarState.Hatched;
            GameServer.Database.SaveObject(dbRecord);

            Scripts.GenistarPet pet = new Scripts.GenistarPet(player);
            pet.LoadFromGenistarDB(dbRecord);
            pet.Position = Caster.Position;
            pet.Realm = Caster.Realm;

            m_pet = pet;
            m_pet.AddToWorld();

            double healthPct = 1.0;
            if (storedMaxCondition > 0)
            {
                healthPct = (double)storedCondition / storedMaxCondition;
            }
            if (healthPct <= 0.0) healthPct = 0.01;

            new RegionTimer(m_pet, new RegionTimerCallback(timer =>
            {
                if (m_pet != null && m_pet.IsAlive)
                {
                    m_pet.Health = Math.Max(1, (int)(m_pet.MaxHealth * healthPct));

                    if (healthPct < 1.0)
                    {
                        m_pet.BossAblativeShieldCurrent = 0;
                    }
                }
                return 0;
            }), 100);

            if (m_pet.Brain is IControlledBrain cb)
            {
                SetBrainToOwner(cb);
                cb.UpdatePetWindow();
            }

            GameSpellEffect effect = CreateSpellEffect(target, effectiveness);
            effect.Start(m_pet);

            AddHandlers();
            Caster.OnPetSummoned(m_pet);

            return true;
        }

        protected override void OnNpcReleaseCommand(DOLEvent e, object sender, EventArgs arguments)
        {
            if (m_pet is Scripts.GenistarPet genPet && genPet.IsAlive && genPet.Health > 0)
            {
                genPet.PackPetIntoContainer();
            }

            base.OnNpcReleaseCommand(e, sender, arguments);
        }
    }
}