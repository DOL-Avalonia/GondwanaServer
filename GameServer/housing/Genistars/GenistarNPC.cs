using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.Geometry;
using DOL.GS.Housing;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.GS.Styles;
using DOL.Language;
using System;
using System.Collections;
using System.Linq;

namespace DOL.GS.Scripts
{
    public class GenistarNPC : GameNPC
    {
        public DBGenistar DBRecord { get; private set; }
        private RegionTimer m_dormantTimer;

        public GenistarNPC() : base()
        {
            SetOwnBrain(new GenistarNPCBrain());
            this.SaveInDB = false;
        }

        public override byte Level
        {
            get => DBRecord == null ? base.Level : GenistarPet.CalculateGenistarLevel(DBRecord.OwnerID, DBRecord.GenistarExperience);
            set => base.Level = value;
        }

        public void LoadFromGenistarDB(DBGenistar record)
        {
            DBRecord = record;
            INpcTemplate baseTemplate = NpcTemplateMgr.GetTemplate(DBRecord.BaseTemplateID);
            if (baseTemplate != null) LoadTemplate(baseTemplate);

            // Phenotype Name Resolution
            Name = !string.IsNullOrEmpty(DBRecord.CustomName)
                ? DBRecord.CustomName
                : (!string.IsNullOrEmpty(DBRecord.Name) ? DBRecord.Name : (baseTemplate?.Name ?? "Genistar"));
            Model = DBRecord.CurrentModel > 0 ? DBRecord.CurrentModel : Model;

            if (!string.IsNullOrEmpty(DBRecord.EquipmentTemplateID))
            {
                GameNpcInventoryTemplate newInv = new GameNpcInventoryTemplate();
                if (newInv.LoadFromDatabase(DBRecord.EquipmentTemplateID))
                {
                    this.Inventory = newInv.CloseTemplate();
                    this.EquipmentTemplateID = DBRecord.EquipmentTemplateID;
                }
            }

            this.VisibleWeaponsDb = DBRecord.VisibleWeaponSlot;
            if (this.VisibleWeaponsDb == 2) this.SwitchWeapon(GameLiving.eActiveWeaponSlot.TwoHanded);
            else if (this.VisibleWeaponsDb == 3) this.SwitchWeapon(GameLiving.eActiveWeaponSlot.Distance);
            else this.SwitchWeapon(GameLiving.eActiveWeaponSlot.Standard);

            // Project persistent Phenotype onto active DAoC creature instance
            this.Strength = (short)DBRecord.Strength;
            this.Constitution = (short)DBRecord.Constitution;
            this.Dexterity = (short)DBRecord.Dexterity;
            this.Quickness = (short)DBRecord.Quickness;
            this.Intelligence = (short)DBRecord.Intelligence;
            this.Empathy = (short)DBRecord.Empathy;
            this.Piety = (short)DBRecord.Piety;
            this.Charisma = (short)DBRecord.Charisma;

            this.ArmorFactor = DBRecord.ArmorFactor;
            this.WeaponDps = DBRecord.WeaponDPS;
            this.MaxSpeedBase = (short)DBRecord.BaseMaxSpeed;

            if (DBRecord.IsGhost) Flags |= eFlags.GHOST;
            if (DBRecord.IsStealthed) Flags |= eFlags.STEALTH;

            if (DBRecord.State == (int)eGenistarState.Recovering)
            {
                Name = $"Dormant {Name}";
                Flags |= eFlags.PEACE | eFlags.STATUE | eFlags.CANTTARGET;
                SetOwnBrain(new BlankBrain());

                // ClientsEffect visual while in biological coma
                m_dormantTimer = new RegionTimer(this, new RegionTimerCallback(DormantWakeupTick), 3000);
            }
            else
            {
                SetOwnBrain(new GenistarNPCBrain());
            }

            this.Level = this.Level;
            this.Tension = DBRecord.CurrentTension;
            this.AutoSetStats();
            this.Flags |= eFlags.PEACE;
        }

        private int DormantWakeupTick(RegionTimer timer)
        {
            if (DBRecord == null) return 0;

            foreach (GamePlayer p in this.GetPlayersInRadius(1500))
            {
                p.Out.SendSpellEffectAnimation(this, this, 10, 0, false, 1);
            }

            if (GameTimer.GetTickCount() >= DBRecord.TimerEnd)
            {
                DBRecord.State = (int)eGenistarState.Hatched;
                GameServer.Database.SaveObject(DBRecord);

                Name = Name.Replace("Dormant ", "");
                Flags &= ~eFlags.STATUE;
                Flags &= ~eFlags.CANTTARGET;
                SetOwnBrain(new GenistarNPCBrain());

                GamePlayer owner = WorldMgr.GetClientByPlayerID(DBRecord.OwnerID, true, false)?.Player;
                if (owner != null)
                    owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "Genistar.GenistarNPC.Awakened", Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);

                m_dormantTimer = null;
                return 0;
            }
            return 3000;
        }

        public override IList GetExamineMessages(GamePlayer player)
        {
            IList list = new ArrayList();
            list.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Genistar.GenistarNPC.Examine", GetName(0, false), GetPronoun(0, true), GetAggroLevelString(player, false)));
            return list;
        }

        public override bool Interact(GamePlayer player)
        {
            string lang = player.Client?.Account?.Language ?? "EN";

            if (DBRecord == null || player.InternalID != DBRecord.OwnerID)
                return base.Interact(player);

            if (DBRecord.State == (int)eGenistarState.Recovering)
            {
                long remainingSeconds = (DBRecord.TimerEnd - GameTimer.GetTickCount()) / 1000;
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.GenistarNPC.DormantComa", remainingSeconds), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return true;
            }

            if (player.Level >= 20)
            {
                bool carriesExistingContainer = false;
                lock (player.Inventory)
                {
                    carriesExistingContainer = player.Inventory
                        .GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.PlayerPaperDoll)
                        .Any(i => i != null && (i.Id_nb.StartsWith("genistar_pet") || i.Id_nb.StartsWith("genistar_remains")));
                }

                if (carriesExistingContainer || player.ControlledBrain != null)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.GenistarNPC.AlreadyCarry"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    return true;
                }

                player.TempProperties.setProperty("GenistarInteract", this);
                player.Out.SendCustomDialog(
                    LanguageMgr.GetTranslation(lang, "Genistar.GenistarNPC.PackDialog", Name),
                    new CustomDialogResponse(InteractCallback));
            }
            else
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.GenistarNPC.Level20Required"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
            return base.Interact(player);
        }

        private void InteractCallback(GamePlayer player, byte response)
        {
            GenistarNPC pet = player.TempProperties.getProperty<GenistarNPC>("GenistarInteract", null);
            if (pet == null) return;

            if (response == 1)
            {
                player.TempProperties.removeProperty("GenistarInteract");
                PackGenistar(player, pet);
            }
            else
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Genistar.GenistarNPC.RenameHint"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        private void PackGenistar(GamePlayer player, GenistarNPC pet)
        {
            ItemTemplate baseTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>("genistar_pet");
            if (baseTemplate != null)
            {
                ItemUnique unique = new ItemUnique(baseTemplate);
                string disp = !string.IsNullOrEmpty(pet.DBRecord.CustomName) ? pet.DBRecord.CustomName : pet.DBRecord.Name;
                unique.Name = $"Genistar: {disp}";
                unique.PackageID = pet.DBRecord.GenistarID;
                unique.Charges = 1;
                unique.MaxCharges = 1;
                int newCondition = (int)((baseTemplate.MaxCondition * (double)pet.HealthPercent) / 100.0);
                unique.Condition = Math.Max(1, newCondition);

                GameServer.Database.AddObject(unique);

                InventoryItem petItem = GameInventoryItem.Create(unique);
                petItem.PackageID = pet.DBRecord.GenistarID;
                petItem.Count = 1;
                petItem.Condition = unique.Condition;

                if (player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, petItem))
                {
                    House house = HouseMgr.GetHouse(pet.DBRecord.HouseNumber);
                    if (house != null) house.UpdateGenistarVisual(pet.DBRecord.PlaceholderKey, 1293);

                    pet.DBRecord.CurrentTension = pet.Tension;
                    pet.DBRecord.State = (int)eGenistarState.InContainer;
                    GameServer.Database.SaveObject(pet.DBRecord);

                    pet.RemoveFromWorld();
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Genistar.GenistarNPC.PackSuccess", pet.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    GameServer.Database.DeleteObject(unique);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Genistar.GenistarNPC.PackFail"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
        }

        public int GetEarnedIndex()
        {
            if (DBRecord == null) return 0;
            int earnedIndex = 0;
            for (int i = 1; i < GenistarPet.GenistarXpCurve.Length; i++)
            {
                if (DBRecord.GenistarExperience >= GenistarPet.GenistarXpCurve[i]) earnedIndex = i;
                else break;
            }
            return earnedIndex;
        }

        public override void AutoSetStats()
        {
            for (int i = 1; i < (int)eProperty.MaxProperty; i++)
            {
                ItemBonus[i] = 0;
            }

            base.AutoSetStats();
            if (DBRecord == null) return;

            MaxTension = DBRecord.MaxTension;
            int templateBaseSize = DBRecord.Size > 0 ? DBRecord.Size : 40;
            int earnedIndex = GetEarnedIndex();
            double scaleFactor = 1.0 + (earnedIndex * 0.11);
            byte liveCompoundedSize = (byte)Math.Min(255, Math.Max(1, (int)Math.Round(templateBaseSize * scaleFactor)));

            if (this.Size != liveCompoundedSize)
            {
                this.Size = liveCompoundedSize;
            }

            GenistarEquipmentMgr.ApplyEquipmentBonuses(DBRecord, this);
        }

        public override void SwitchWeapon(eActiveWeaponSlot slot)
        {
            base.SwitchWeapon(slot);
            if (DBRecord != null) GenistarEquipmentMgr.ApplyEquipmentBonuses(DBRecord, this);
        }

        protected override AttackData MakeAttack(GameObject target, InventoryItem weapon, Style style, double effectiveness, int interruptDuration, bool dualWield, bool ignoreLOS, bool isCounterAttack)
        {
            AttackData ad = base.MakeAttack(target, weapon, style, effectiveness, interruptDuration, dualWield, ignoreLOS, isCounterAttack);

            if (ad != null && (ad.AttackResult == eAttackResult.HitStyle || ad.AttackResult == eAttackResult.HitUnstyled))
            {
                if (DBRecord != null && !string.IsNullOrEmpty(DBRecord.ProcSpellID))
                {
                    if (int.TryParse(DBRecord.ProcSpellID, out int procId))
                    {
                        if (Util.Chance(15))
                        {
                            Spell procSpell = SkillBase.GetSpellByID(procId);
                            SpellLine procLine = SkillBase.GetSpellLine(GlobalSpellsLines.Item_Effects);

                            if (procSpell != null && procLine != null && target is GameLiving targetLiving && targetLiving.IsAlive)
                            {
                                ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(this, procSpell, procLine);
                                if (spellHandler != null)
                                {
                                    spellHandler.IgnoreDamageCap = true;
                                    spellHandler.StartSpell(targetLiving);
                                }
                            }
                        }
                    }
                }
            }
            return ad;
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (DBRecord.State == (int)eGenistarState.Recovering) return false;

            GamePlayer player = source as GamePlayer;
            string lang = player?.Client?.Account?.Language ?? "EN";

            if (player == null || player.InternalID != DBRecord.OwnerID)
            {
                if (player != null) player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.GenistarNPC.RefuseStrangers"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            ItemchargeXRaces foodInfo = GameServer.Database.SelectObject<ItemchargeXRaces>(DB.Column("ItemTemplate").IsEqualTo(item.Id_nb));
            if (foodInfo != null)
            {
                int foodType = GetFoodEffect(foodInfo, DBRecord.BodyType);
                if (foodType > 0)
                {
                    ConsumeFood(player, foodType);
                    if (item.Count > 1) player.Inventory.RemoveCountFromStack(item, 1);
                    else player.Inventory.RemoveItem(item);
                    return true;
                }
            }

            if (item.Template.Flags >= 30 && item.Template.Flags <= 40)
            {
                if (GenistarEquipmentMgr.TryEquipGear(player, DBRecord, this, item))
                {
                    return true;
                }
                return false;
            }

            // Invalid Item
            player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.GenistarNPC.CantEquip"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return false;
        }

        private int GetFoodEffect(ItemchargeXRaces food, int bodyType)
        {
            return (NpcTemplateMgr.eBodyType)bodyType switch
            {
                NpcTemplateMgr.eBodyType.Animal => food.FoodAnimal,
                NpcTemplateMgr.eBodyType.Demon => food.FoodDemon,
                NpcTemplateMgr.eBodyType.Dragon => food.FoodDragon,
                NpcTemplateMgr.eBodyType.Elemental => food.FoodElemental,
                NpcTemplateMgr.eBodyType.Giant => food.FoodGiant,
                NpcTemplateMgr.eBodyType.Humanoid => food.FoodHumanoid,
                NpcTemplateMgr.eBodyType.Insect => food.FoodInsect,
                NpcTemplateMgr.eBodyType.Magical => food.FoodMagical,
                NpcTemplateMgr.eBodyType.Reptile => food.FoodReptile,
                NpcTemplateMgr.eBodyType.Plant => food.FoodPlant,
                NpcTemplateMgr.eBodyType.Undead => food.FoodUndead,
                _ => 0
            };
        }

        private void ConsumeFood(GamePlayer owner, int foodType)
        {
            owner.Out.SendSpellEffectAnimation(this, this, 7035, 0, false, 1);
            string lang = owner.Client?.Account?.Language ?? "EN";

            if (foodType == 99) // Toxic payload
            {
                owner.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.GenistarNPC.RejectSpoiled", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                this.IsStunned = true;
                new RegionTimer(this, _ => { this.IsStunned = false; return 0; }).Start(30000);
                return;
            }

            if (foodType == 98) // The Resequencer
            {
                ChangeModel(owner);
                return;
            }

            // Standard Food Payload = +5 Counts per feed (2 Feeds rolls over +1 point)
            int feedCredit = 5;

            switch (foodType)
            {
                // Core Attributes
                case 1: AddStatRollover(owner, () => DBRecord.CountStrength, v => DBRecord.CountStrength = v, () => DBRecord.Strength, v => DBRecord.Strength = v, () => DBRecord.BaseStrength, v => DBRecord.BaseStrength = v, v => this.Strength = v, feedCredit, "Strength"); break;
                case 2: AddStatRollover(owner, () => DBRecord.CountConstitution, v => DBRecord.CountConstitution = v, () => DBRecord.Constitution, v => DBRecord.Constitution = v, () => DBRecord.BaseConstitution, v => DBRecord.BaseConstitution = v, v => this.Constitution = v, feedCredit, "Constitution"); break;
                case 3: AddStatRollover(owner, () => DBRecord.CountQuickness, v => DBRecord.CountQuickness = v, () => DBRecord.Quickness, v => DBRecord.Quickness = v, () => DBRecord.BaseQuickness, v => DBRecord.BaseQuickness = v, v => this.Quickness = v, feedCredit, "Quickness"); break;
                case 4: AddStatRollover(owner, () => DBRecord.CountIntelligence, v => DBRecord.CountIntelligence = v, () => DBRecord.Intelligence, v => DBRecord.Intelligence = v, () => DBRecord.BaseIntelligence, v => DBRecord.BaseIntelligence = v, v => this.Intelligence = v, feedCredit, "Intelligence"); break;
                case 5: AddStatRollover(owner, () => DBRecord.CountPiety, v => DBRecord.CountPiety = v, () => DBRecord.Piety, v => DBRecord.Piety = v, () => DBRecord.BasePiety, v => DBRecord.BasePiety = v, v => this.Piety = v, feedCredit, "Piety"); break;
                case 6: AddStatRollover(owner, () => DBRecord.CountEmpathy, v => DBRecord.CountEmpathy = v, () => DBRecord.Empathy, v => DBRecord.Empathy = v, () => DBRecord.BaseEmpathy, v => DBRecord.BaseEmpathy = v, v => this.Empathy = v, feedCredit, "Empathy"); break;
                case 7: AddStatRollover(owner, () => DBRecord.CountDexterity, v => DBRecord.CountDexterity = v, () => DBRecord.Dexterity, v => DBRecord.Dexterity = v, () => DBRecord.BaseDexterity, v => DBRecord.BaseDexterity = v, v => this.Dexterity = v, feedCredit, "Dexterity"); break;
                case 8: AddStatRollover(owner, () => DBRecord.CountCharisma, v => DBRecord.CountCharisma = v, () => DBRecord.Charisma, v => DBRecord.Charisma = v, () => DBRecord.BaseCharisma, v => DBRecord.BaseCharisma = v, v => this.Charisma = v, feedCredit, "Charisma"); break;

                // Resists
                case 9: AddResistRollover(owner, () => DBRecord.CountResistBody, v => DBRecord.CountResistBody = v, () => DBRecord.ResistBody, v => DBRecord.ResistBody = v, eProperty.Resist_Body, feedCredit, "Body Resist"); break;
                case 10: AddResistRollover(owner, () => DBRecord.CountResistCold, v => DBRecord.CountResistCold = v, () => DBRecord.ResistCold, v => DBRecord.ResistCold = v, eProperty.Resist_Cold, feedCredit, "Cold Resist"); break;
                case 11: AddResistRollover(owner, () => DBRecord.CountResistCrush, v => DBRecord.CountResistCrush = v, () => DBRecord.ResistCrush, v => DBRecord.ResistCrush = v, eProperty.Resist_Crush, feedCredit, "Crush Resist"); break;
                case 12: AddResistRollover(owner, () => DBRecord.CountResistEnergy, v => DBRecord.CountResistEnergy = v, () => DBRecord.ResistEnergy, v => DBRecord.ResistEnergy = v, eProperty.Resist_Energy, feedCredit, "Energy Resist"); break;
                case 13: AddResistRollover(owner, () => DBRecord.CountResistHeat, v => DBRecord.CountResistHeat = v, () => DBRecord.ResistHeat, v => DBRecord.ResistHeat = v, eProperty.Resist_Heat, feedCredit, "Heat Resist"); break;
                case 14: AddResistRollover(owner, () => DBRecord.CountResistMatter, v => DBRecord.CountResistMatter = v, () => DBRecord.ResistMatter, v => DBRecord.ResistMatter = v, eProperty.Resist_Matter, feedCredit, "Matter Resist"); break;
                case 15: AddResistRollover(owner, () => DBRecord.CountResistSlash, v => DBRecord.CountResistSlash = v, () => DBRecord.ResistSlash, v => DBRecord.ResistSlash = v, eProperty.Resist_Slash, feedCredit, "Slash Resist"); break;
                case 16: AddResistRollover(owner, () => DBRecord.CountResistSpirit, v => DBRecord.CountResistSpirit = v, () => DBRecord.ResistSpirit, v => DBRecord.ResistSpirit = v, eProperty.Resist_Spirit, feedCredit, "Spirit Resist"); break;
                case 17: AddResistRollover(owner, () => DBRecord.CountResistThrust, v => DBRecord.CountResistThrust = v, () => DBRecord.ResistThrust, v => DBRecord.ResistThrust = v, eProperty.Resist_Thrust, feedCredit, "Thrust Resist"); break;
                case 18: AddResistRollover(owner, () => DBRecord.CountResistNatural, v => DBRecord.CountResistNatural = v, () => DBRecord.ResistNatural, v => DBRecord.ResistNatural = v, eProperty.Resist_Natural, feedCredit, "Essence Resist"); break;

                // Combat Stats
                case 19: AddIntRollover(owner, () => DBRecord.CountWeaponDPS, v => DBRecord.CountWeaponDPS = v, () => DBRecord.WeaponDPS, v => DBRecord.WeaponDPS = v, () => DBRecord.BaseDPS, v => DBRecord.BaseDPS = v, v => this.WeaponDps = v, feedCredit, "Weapon DPS"); break;
                case 20: AddIntRollover(owner, () => DBRecord.CountArmorFactor, v => DBRecord.CountArmorFactor = v, () => DBRecord.ArmorFactor, v => DBRecord.ArmorFactor = v, () => DBRecord.BaseAF, v => DBRecord.BaseAF = v, v => this.ArmorFactor = v, feedCredit, "Armor Factor"); break;
                case 21: AddIntRollover(owner, () => DBRecord.CountEvadeChance, v => DBRecord.CountEvadeChance = v, () => DBRecord.EvadeChance, v => DBRecord.EvadeChance = v, () => DBRecord.EvadeChance, v => DBRecord.EvadeChance = v, v => this.EvadeChance = (byte)v, feedCredit, "Evade Chance"); break;
                case 22: AddIntRollover(owner, () => DBRecord.CountBlockChance, v => DBRecord.CountBlockChance = v, () => DBRecord.BlockChance, v => DBRecord.BlockChance = v, () => DBRecord.BlockChance, v => DBRecord.BlockChance = v, v => this.BlockChance = (byte)v, feedCredit, "Block Chance"); break;
                case 23: AddIntRollover(owner, () => DBRecord.CountParryChance, v => DBRecord.CountParryChance = v, () => DBRecord.ParryChance, v => DBRecord.ParryChance = v, () => DBRecord.ParryChance, v => DBRecord.ParryChance = v, v => this.ParryChance = (byte)v, feedCredit, "Parry Chance"); break;
                case 24: AddIntRollover(owner, () => DBRecord.CountLeftHandSwingChance, v => DBRecord.CountLeftHandSwingChance = v, () => DBRecord.LeftHandSwingChance, v => DBRecord.LeftHandSwingChance = v, () => DBRecord.LeftHandSwingChance, v => DBRecord.LeftHandSwingChance = v, v => this.LeftHandSwingChance = (byte)v, feedCredit, "Left Hand Swing Chance"); break;/*
                case 25: AddIntRollover(owner, () => DBRecord.CountCounterAttackChance, v => DBRecord.CountCounterAttackChance = v, () => DBRecord.CounterAttackChance, v => DBRecord.CounterAttackChance = v, () => DBRecord.CounterAttackChance, v => DBRecord.CounterAttackChance = v, v => this.CounterAttackChance = v, feedCredit, "Counterattack Chance"); break;
                case 26: AddIntRollover(owner, () => DBRecord.CountProcSpellChance, v => DBRecord.CountProcSpellChance = v, () => DBRecord.ProcSpellChance, v => DBRecord.ProcSpellChance = v, () => DBRecord.ProcSpellChance, v => DBRecord.ProcSpellChance = v, v => this.ProcSpellChance = v, feedCredit, "Proc Spell Chance"); break;
                case 28: AddIntRollover(owner, () => DBRecord.CountMaxTension, v => DBRecord.CountMaxTension = v, () => DBRecord.MaxTension, v => DBRecord.MaxTension = v, () => DBRecord.MaxTension, v => DBRecord.MaxTension = v, v => this.MaxTension = v, feedCredit, "Max Tension Reduction"); break;

                // Unique Enhancements
                case 29: AddIntRollover(owner, () => DBRecord.CountSpellmagicABS, v => DBRecord.CountSpellmagicABS = v, () => DBRecord.SpellmagicABS, v => DBRecord.SpellmagicABS = v, () => DBRecord.SpellmagicABS, v => DBRecord.SpellmagicABS = v, v => this.SpellmagicABS = v, feedCredit, "Spell magic ABS"); break;
                case 30: AddIntRollover(owner, () => DBRecord.CountDotABS, v => DBRecord.CountDotABS = v, () => DBRecord.DotABS, v => DBRecord.DotABS = v, () => DBRecord.DotABS, v => DBRecord.DotABS = v, v => this.DotABS = v, feedCredit, "Dot ABS"); break;
                case 31: AddIntRollover(owner, () => DBRecord.CountMeleeABS, v => DBRecord.CountMeleeABS = v, () => DBRecord.MeleeABS, v => DBRecord.MeleeABS = v, () => DBRecord.MeleeABS, v => DBRecord.MeleeABS = v, v => this.MeleeABS = v, feedCredit, "Spell magic ABS"); break;
                case 32: AddIntRollover(owner, () => DBRecord.CountMaxHealth, v => DBRecord.CountMaxHealth = v, () => DBRecord.MaxHealth, v => DBRecord.MaxHealth = v, () => DBRecord.MaxHealth, v => DBRecord.MaxHealth = v, v => this.MaxHealth = v, feedCredit, "Max Health"); break;
                case 33: AddIntRollover(owner, () => DBRecord.CountEffectivenessMod, v => DBRecord.CountEffectivenessMod = v, () => DBRecord.EffectivenessMod, v => DBRecord.EffectivenessMod = v, () => DBRecord.EffectivenessMod, v => DBRecord.EffectivenessMod = v, v => this.EffectivenessMod = v, feedCredit, "Effectiveness Modifier"); break;
                case 34: AddIntRollover(owner, () => DBRecord.CountAblativeShield, v => DBRecord.CountAblativeShield = v, () => DBRecord.AblativeShield, v => DBRecord.AblativeShield = v, () => DBRecord.AblativeShield, v => DBRecord.AblativeShield = v, v => this.AblativeShield = v, feedCredit, "Ablative Shield Modifier"); break;
                case 35: AddIntRollover(owner, () => DBRecord.CountCCResist, v => DBRecord.CountCCResist = v, () => DBRecord.CCResist, v => DBRecord.CCResist = v, () => DBRecord.CCResist, v => DBRecord.CCResist = v, v => this.CCResist = v, feedCredit, "Crowd Control Resist Chance"); break;
                case 36: AddIntRollover(owner, () => DBRecord.CountDebuffResist, v => DBRecord.CountDebuffResist = v, () => DBRecord.DebuffResist, v => DBRecord.DebuffResist = v, () => DBRecord.DebuffResist, v => DBRecord.DebuffResist = v, v => this.DebuffResist = v, feedCredit, "Debuff Resist Chance"); break;
                case 37: AddIntRollover(owner, () => DBRecord.CountCastRange, v => DBRecord.CountCastRange = v, () => DBRecord.CastRange, v => DBRecord.CastRange = v, () => DBRecord.CastRange, v => DBRecord.CastRange = v, v => this.CastRange = v, feedCredit, "Cast Range Increase"); break;*/

                // Evolution Magic/Spells
                case 38: AddEvolutionRollover(owner, () => DBRecord.CountEvolveCold, v => DBRecord.CountEvolveCold = v, true, "dd_cold", "Cold Magic"); break;
                case 39: AddEvolutionRollover(owner, () => DBRecord.CountEvolveSpirit, v => DBRecord.CountEvolveSpirit = v, true, "dd_spirit", "Spirit Magic"); break;
                case 40: AddEvolutionRollover(owner, () => DBRecord.CountEvolveMatter, v => DBRecord.CountEvolveMatter = v, true, "dot_matter", "Matter Venom"); break;
                case 41: AddEvolutionRollover(owner, () => DBRecord.CountEvolveFire, v => DBRecord.CountEvolveFire = v, true, "dd_fire", "Fire Magic"); break;
                case 42: AddEvolutionRollover(owner, () => DBRecord.CountEvolveEnergy, v => DBRecord.CountEvolveEnergy = v, true, "dd_energy", "Energy Magic"); break;
                case 43: AddEvolutionRollover(owner, () => DBRecord.CountEvolveBody, v => DBRecord.CountEvolveBody = v, true, "dd_body", "Body Magic"); break;
                case 44: AddEvolutionRollover(owner, () => DBRecord.CountEvolveHeal, v => DBRecord.CountEvolveHeal = v, true, "heal", "Restoration"); break;

                // Evolution Styles
                case 45: AddEvolutionRollover(owner, () => DBRecord.CountEvolveHammer, v => DBRecord.CountEvolveHammer = v, false, "hammer", "Hammer Technique"); break;
                case 46: AddEvolutionRollover(owner, () => DBRecord.CountEvolveSword, v => DBRecord.CountEvolveSword = v, false, "sword", "Blademastery"); break;
                case 47: AddEvolutionRollover(owner, () => DBRecord.CountEvolveThrust, v => DBRecord.CountEvolveThrust = v, false, "pierce", "Piercing Strike"); break;
            }
        }

        // 4-Way Synchronized Rollover Delegate for Standard 16-bit Attributes
        private void AddStatRollover(GamePlayer owner, Func<int> getCount, Action<int> setCount, Func<int> getPheno, Action<int> setPheno, Func<short> getGeno, Action<short> setGeno, Action<short> setLiveInstance, int countsToAdd, string statDisplayName)
        {
            int c = getCount() + countsToAdd;
            if (c >= 10)
            {
                int earned = c / 10;
                c %= 10;

                short newGeno = (short)(getGeno() + earned);
                setGeno(newGeno);

                int newPheno = getPheno() + earned;
                setPheno(newPheno);

                setLiveInstance((short)newPheno);

                GameServer.Database.SaveObject(DBRecord);
                this.AutoSetStats(); // Force engine recalculation of derived MaxHP / Swing

                owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "Genistar.GenistarNPC.StatExpand", Name, earned, statDisplayName), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
            setCount(c);
        }

        private void AddIntRollover(GamePlayer owner, Func<int> getCount, Action<int> setCount, Func<int> getPheno, Action<int> setPheno, Func<int> getGeno, Action<int> setGeno, Action<int> setLiveInstance, int countsToAdd, string statDisplayName)
        {
            int c = getCount() + countsToAdd;
            if (c >= 10)
            {
                int earned = c / 10;
                c %= 10;

                int newGeno = getGeno() + earned;
                setGeno(newGeno);

                int newPheno = getPheno() + earned;
                setPheno(newPheno);

                setLiveInstance(newPheno);

                GameServer.Database.SaveObject(DBRecord);
                this.AutoSetStats();

                owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "Genistar.GenistarNPC.CombatSharpen", Name, earned, statDisplayName), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
            setCount(c);
        }

        private void AddResistRollover(GamePlayer owner, Func<int> getCount, Action<int> setCount, Func<int> getResistDB, Action<int> setResistDB, eProperty resistProp, int countsToAdd, string resistDisplayName)
        {
            int c = getCount() + countsToAdd;
            if (c >= 10)
            {
                int earned = c / 10;
                c %= 10;

                int newResist = getResistDB() + earned;
                setResistDB(newResist);

                ItemBonus[(int)resistProp] = newResist;

                GameServer.Database.SaveObject(DBRecord);

                owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "Genistar.GenistarNPC.ResistHarden", Name, earned, resistDisplayName), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
            setCount(c);
        }

        private void AddEvolutionRollover(GamePlayer owner, Func<int> getCount, Action<int> setCount, bool isSpell, string keyword, string skillDisplayName)
        {
            int c = getCount() + 5;
            if (c >= 10)
            {
                c -= 10;
                EvolveSkill(isSpell, keyword);
                owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "Genistar.GenistarNPC.SkillEvolve", Name, skillDisplayName), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
            setCount(c);
        }

        private void ChangeModel(GamePlayer owner)
        {
            INpcTemplate tmpl = NpcTemplateMgr.GetTemplate(DBRecord.BaseTemplateID);
            string lang = owner.Client?.Account?.Language ?? "EN";

            if (tmpl == null || string.IsNullOrEmpty(tmpl.Model)) return;

            string[] models = tmpl.Model.Split(';');
            if (models.Length <= 1)
            {
                owner.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.GenistarNPC.MorphStatic", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            ushort oldModel = DBRecord.CurrentModel;
            ushort newModel = oldModel;
            int attempts = 0;

            while (newModel == oldModel && attempts < 10)
            {
                if (ushort.TryParse(models[Util.Random(models.Length - 1)], out ushort m))
                    newModel = m;
                attempts++;
            }

            if (newModel != oldModel)
            {
                owner.Out.SendSpellEffectAnimation(owner, this, 7033, 0, false, 1);
                DBRecord.CurrentModel = newModel;
                this.Model = newModel;
                GameServer.Database.SaveObject(DBRecord);

                owner.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.GenistarNPC.MorphShift", Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
        }

        private void EvolveSkill(bool isSpell, string familyKeyword)
        {
            string targetString = isSpell ? DBRecord.Spells : DBRecord.Styles;
            if (string.IsNullOrEmpty(targetString)) return;

            var list = targetString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            bool changed = false;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Contains(familyKeyword))
                {
                    string prefix = "genistar_lv";
                    int lvIndex = list[i].IndexOf(prefix) + prefix.Length;
                    if (lvIndex >= prefix.Length && lvIndex < list[i].Length)
                    {
                        string numStr = "";
                        while (lvIndex < list[i].Length && char.IsDigit(list[i][lvIndex])) { numStr += list[i][lvIndex]; lvIndex++; }
                        if (int.TryParse(numStr, out int currentLv))
                        {
                            list[i] = list[i].Replace($"lv{currentLv}", $"lv{currentLv + 1}");
                            changed = true;
                        }
                    }
                }
            }
            if (changed)
            {
                if (isSpell) DBRecord.Spells = string.Join(";", list);
                else DBRecord.Styles = string.Join(";", list);
                GameServer.Database.SaveObject(DBRecord);
            }
        }
    }

    public class GenistarNPCBrain : StandardMobBrain
    {
        private long m_nextRoamTick = 0;

        public override void Think()
        {
            base.Think();

            if (Body is not GenistarNPC genistar || genistar.DBRecord == null || genistar.DBRecord.State == (int)eGenistarState.Recovering) return;
            if (Body.IsCasting || Body.AttackState) return;

            GamePlayer owner = WorldMgr.GetClientByPlayerID(genistar.DBRecord.OwnerID, true, false)?.Player;

            if (owner == null || owner.CurrentRegionID != Body.CurrentRegionID)
            {
                if (Body.CurrentFollowTarget != null)
                {
                    Body.StopFollowing();
                    Body.WalkToSpawn();
                }
                else
                {
                    RoamInGarden();
                }
                return;
            }

            double distToOwner = Body.GetDistanceTo(owner);
            double ownerDistFromHome = owner.Coordinate.DistanceTo(Body.Home.Coordinate);

            if (ownerDistFromHome <= 1000 && distToOwner <= 250)
            {
                if (Body.CurrentFollowTarget != owner) Body.Follow(owner, 100, 1000);
            }
            else if (ownerDistFromHome > 1100 && Body.CurrentFollowTarget == owner)
            {
                Body.StopFollowing();
                owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "Genistar.GenistarNPCBrain.StopFollow", Body.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                Body.WalkToSpawn();
            }
            else if (Body.CurrentFollowTarget == null)
            {
                RoamInGarden();
            }
        }

        private void RoamInGarden()
        {
            if (Body.IsMoving || Body.IsMovingOnPath || Body.CurrentRegion.Time < m_nextRoamTick) return;
            m_nextRoamTick = Body.CurrentRegion.Time + Util.Random(5000, 15000);

            int maxDist = 400;
            Coordinate dest = Coordinate.Create(Body.Home.X + Util.Random(-maxDist, maxDist), Body.Home.Y + Util.Random(-maxDist, maxDist), Body.Home.Z);
            Body.WalkTo(dest, (short)Math.Max(50, Body.MaxSpeedBase / 2));
        }
    }
}