using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.GS.Styles;
using DOL.Language;
using log4net;
using System;
using System.Collections;

namespace DOL.GS.Scripts
{
    public class GenistarPet : GamePet
    {
        public DBGenistar DBRecord { get; private set; }

        public GenistarPet(GamePlayer owner) : base(new GenistarPetBrain(owner))
        {
            this.OwnerID = owner.InternalID;
        }

        public static readonly long[] GenistarXpCurve =
        {
            0,          // Index 0  (Owner - 15)
            50000,      // Index 1  (Owner - 14)
            150000,     // Index 2  (Owner - 13)
            300000,     // Index 3  (Owner - 12)
            500000,     // Index 4  (Owner - 11)
            750000,     // Index 5  (Owner - 10)
            1050000,    // Index 6  (Owner - 9)
            1400000,    // Index 7  (Owner - 8)
            1800000,    // Index 8  (Owner - 7)
            2250000,    // Index 9  (Owner - 6)
            2750000,    // Index 10 (Owner - 5)
            3300000,    // Index 11 (Owner - 4)
            3900000,    // Index 12 (Owner - 3)
            4550000,    // Index 13 (Owner - 2)
            5250000,    // Index 14 (Owner - 1)
            6000000,    // Index 15 (Owner + 0)
            8500000,    // Index 16 (Owner + 1) [+2.5M]
            12000000,   // Index 17 (Owner + 2) [+3.5M]
            17000000,   // Index 18 (Owner + 3) [+5.0M]
            24000000,   // Index 19 (Owner + 4) [+7.0M]
            35000000,   // Index 20 (Owner + 5) [+11.0M]
            50000000    // Index 21 (Overflow Cap)
        };

        public int GetEarnedIndex()
        {
            if (DBRecord == null) return 0;
            int earnedIndex = 0;
            for (int i = 1; i < GenistarXpCurve.Length; i++)
            {
                if (DBRecord.GenistarExperience >= GenistarXpCurve[i]) earnedIndex = i;
                else break;
            }
            return earnedIndex;
        }

        public static byte CalculateGenistarLevel(string ownerId, long exp)
        {
            int ownerLevel = 50;
            GameClient client = WorldMgr.GetClientByPlayerID(ownerId, true, false);
            if (client?.Player != null) ownerLevel = client.Player.Level;
            else
            {
                DOLCharacters dbChar = GameServer.Database.FindObjectByKey<DOLCharacters>(ownerId);
                if (dbChar != null) ownerLevel = dbChar.Level;
            }

            int baseLevel = Math.Max(1, ownerLevel - 15);
            int earnedIndex = 0;

            for (int i = 1; i < GenistarXpCurve.Length; i++)
            {
                if (exp >= GenistarXpCurve[i]) earnedIndex = i;
                else break;
            }

            return (byte)Math.Min(baseLevel + earnedIndex, ownerLevel + 5);
        }

        public override byte Level
        {
            get => DBRecord == null ? base.Level : CalculateGenistarLevel(DBRecord.OwnerID, DBRecord.GenistarExperience);
            set => base.Level = value;
        }

        public void AddGenistarExperience(long amount)
        {
            if (DBRecord != null && amount > 0)
            {
                byte oldLevel = this.Level;
                DBRecord.GenistarExperience += amount;
                GameServer.Database.SaveObject(DBRecord);

                byte newLevel = this.Level;

                if (newLevel > oldLevel)
                {
                    if (GetLivingOwner() is GamePlayer owner)
                    {
                        owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "Genistar.GenistarPet.LevelUp", newLevel), eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
                    }

                    this.AutoSetStats();

                    if (this.Brain is IControlledBrain cb)
                    {
                        cb.UpdatePetWindow();
                    }

                    foreach (GamePlayer observer in GetPlayersInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
                    {
                        bool wasTarget = observer.TargetObject == this;
                        observer.Out.SendObjectRemove(this);
                        observer.Out.SendNPCCreate(this);

                        if (this.Inventory != null)
                        {
                            observer.Out.SendLivingEquipmentUpdate(this);
                        }

                        if (wasTarget)
                        {
                            observer.Out.SendChangeTarget(this);
                        }
                    }

                    new RegionTimer(this, _ =>
                    {
                        foreach (GamePlayer obs in this.GetPlayersInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
                        {
                            obs.Out.SendSpellEffectAnimation(this, this, 8007, 0, false, 1);
                        }
                        return 0;
                    }).Start(150);
                }
            }
        }

        public override IList GetExamineMessages(GamePlayer player)
        {
            IList list = new ArrayList();
            string msg = LanguageMgr.GetTranslation(player.Client.Account.Language, "Genistar.GenistarPet.Examine", GetName(0, false), GetPronoun(0, true), GetAggroLevelString(player, false));
            list.Add(msg);
            return list;
        }

        public void LoadFromGenistarDB(DBGenistar record)
        {
            DBRecord = record;
            INpcTemplate baseTemplate = NpcTemplateMgr.GetTemplate(DBRecord.BaseTemplateID);

            var originalBrain = this.Brain;
            if (baseTemplate != null) LoadTemplate(baseTemplate);

            if (originalBrain is IControlledBrain && this.Brain != originalBrain)
                SetOwnBrain(originalBrain);

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

            OverrideSpellsAndStyles();

            IsBoss = true;
            BossSpellABS = DBRecord.SpellmagicABS;
            BossMeleeABS = DBRecord.MeleeABS;
            BossDotABS = DBRecord.DotABS;
            BossMaxHealthMod = DBRecord.MaxHealth;
            BossEffectivenessMod = DBRecord.EffectivenessMod;
            BossCCResist = DBRecord.CCResist;
            BossDebuffResist = DBRecord.DebuffResist;
            BossCastRangeMod = DBRecord.CastRange;
            BossAblativeShieldMult = DBRecord.AblativeShield;
            BossAblativeErodible = DBRecord.ErodibleAblative == 1;

            if (DBRecord.IsGhost) Flags |= eFlags.GHOST;
            if (DBRecord.IsStealthed) Flags |= eFlags.STEALTH;

            this.Level = this.Level;
            this.Tension = DBRecord.CurrentTension;
            this.AutoSetStats();
        }

        private void OverrideSpellsAndStyles()
        {
            if (!string.IsNullOrEmpty(DBRecord.Spells))
            {
                Spells.Clear();
                foreach (string sName in DBRecord.Spells.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var spellDb = GameServer.Database.SelectObject<DBSpell>(DB.Column("PackageID").IsEqualTo(sName));
                    if (spellDb != null) Spells.Add(new Spell(spellDb, 1));
                }
            }

            if (!string.IsNullOrEmpty(DBRecord.Styles))
            {
                Styles.Clear();
                foreach (string stName in DBRecord.Styles.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var styleDb = GameServer.Database.SelectObject<DBStyle>(DB.Column("Name").IsEqualTo(stName));
                    if (styleDb != null) Styles.Add(new Style(styleDb));
                }
            }
        }

        public override void AutoSetStats()
        {
            for (int i = 1; i < (int)eProperty.MaxProperty; i++)
            {
                ItemBonus[i] = 0;
            }

            base.AutoSetStats();
            if (DBRecord == null) return;

            Strength = (short)Math.Max(1, DBRecord.Strength);
            Constitution = (short)Math.Max(1, DBRecord.Constitution);
            Quickness = (short)Math.Max(1, DBRecord.Quickness);
            Dexterity = (short)Math.Max(1, DBRecord.Dexterity);
            Intelligence = (short)Math.Max(1, DBRecord.Intelligence);
            Empathy = (short)Math.Max(1, DBRecord.Empathy);
            Piety = (short)Math.Max(1, DBRecord.Piety);
            Charisma = (short)Math.Max(1, DBRecord.Charisma);

            ArmorFactor = DBRecord.ArmorFactor;
            WeaponDps = DBRecord.WeaponDPS;
            WeaponSpd = DBRecord.WeaponSpd;
            MaxTension = DBRecord.MaxTension;

            // Project concrete Resists
            ItemBonus[(int)eProperty.Resist_Body] = DBRecord.ResistBody;
            ItemBonus[(int)eProperty.Resist_Energy] = DBRecord.ResistEnergy;
            ItemBonus[(int)eProperty.Resist_Matter] = DBRecord.ResistMatter;
            ItemBonus[(int)eProperty.Resist_Heat] = DBRecord.ResistHeat;
            ItemBonus[(int)eProperty.Resist_Cold] = DBRecord.ResistCold;
            ItemBonus[(int)eProperty.Resist_Spirit] = DBRecord.ResistSpirit;
            ItemBonus[(int)eProperty.Resist_Crush] = DBRecord.ResistCrush;
            ItemBonus[(int)eProperty.Resist_Slash] = DBRecord.ResistSlash;
            ItemBonus[(int)eProperty.Resist_Thrust] = DBRecord.ResistThrust;
            ItemBonus[(int)eProperty.Resist_Natural] = DBRecord.ResistNatural;

            // Project secondary combat chances
            ItemBonus[(int)eProperty.BlockChance] = DBRecord.BlockChance;
            ItemBonus[(int)eProperty.EvadeChance] = DBRecord.EvadeChance;
            ItemBonus[(int)eProperty.ParryChance] = DBRecord.ParryChance;
            ItemBonus[(int)eProperty.OffhandChanceBonus] = DBRecord.LeftHandSwingChance;
            ItemBonus[(int)eProperty.CounterAttack] = DBRecord.CounterAttackChance;

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
            GamePlayer player = source as GamePlayer;
            if (player != null && DBRecord != null && player.InternalID == DBRecord.OwnerID)
            {
                if (item.Template.Flags >= 30 && item.Template.Flags <= 40)
                {
                    if (GenistarEquipmentMgr.TryEquipGear(player, DBRecord, this, item))
                    {
                        return true;
                    }
                    return false;
                }
                else
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Genistar.GenistarPet.CantEquip"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
            }

            return base.ReceiveItem(source, item);
        }

        public void PackPetIntoContainer()
        {
            if (DBRecord == null) return;

            if (!this.IsAlive || this.Health <= 0 || DBRecord.State == (int)eGenistarState.InContainer || DBRecord.State == (int)eGenistarState.Dead) return;

            GamePlayer owner = (this.Brain as IControlledBrain)?.Owner as GamePlayer;
            if (owner == null)
            {
                owner = WorldMgr.GetClientByPlayerID(DBRecord.OwnerID, true, false)?.Player;
            }

            ItemTemplate baseTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>("genistar_pet");
            if (baseTemplate == null) return;

            ItemUnique unique = new ItemUnique(baseTemplate);
            string dispName = !string.IsNullOrEmpty(DBRecord.CustomName) ? DBRecord.CustomName : DBRecord.Name;
            unique.Name = $"Genistar: {dispName}";
            unique.PackageID = DBRecord.GenistarID;
            unique.Charges = 1;
            unique.MaxCharges = 1;

            int newCondition = (int)((baseTemplate.MaxCondition * (double)this.HealthPercent) / 100.0);
            unique.Condition = Math.Max(1, newCondition);

            GameServer.Database.AddObject(unique);

            InventoryItem petItem = GameInventoryItem.Create(unique);
            petItem.PackageID = DBRecord.GenistarID;
            petItem.Count = 1;

            petItem.Condition = unique.Condition;
            DBRecord.CurrentTension = this.Tension;
            DBRecord.State = (int)eGenistarState.InContainer;
            GameServer.Database.SaveObject(DBRecord);

            if (owner != null && owner.Inventory != null)
            {
                lock (owner.Inventory)
                {
                    if (!owner.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, petItem))
                    {
                        WorldInventoryItem drop = new WorldInventoryItem(petItem);
                        drop.Position = owner.Position;
                        drop.CurrentRegionID = owner.CurrentRegionID;
                        drop.AddOwner(owner);
                        drop.AddToWorld();
                        owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "Genistar.GenistarPet.PackFull", Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else
                    {
                        owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "Genistar.GenistarPet.PackSuccess", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                }
            }
            else
            {
                // OFFLINE / LINKDEAD INJECTION
                petItem.OwnerID = DBRecord.OwnerID;

                var existingItems = GameServer.Database.SelectObjects<InventoryItem>(
                    DB.Column("OwnerID").IsEqualTo(DBRecord.OwnerID).And(
                    DB.Column("SlotPosition").IsGreaterOrEqualTo((int)eInventorySlot.FirstBackpack).And(
                    DB.Column("SlotPosition").IsLessOrEqualTo((int)eInventorySlot.LastBackpack)))
                );

                int highestSlot = (int)eInventorySlot.FirstBackpack - 1;
                foreach (var itm in existingItems)
                {
                    if (itm.SlotPosition > highestSlot) highestSlot = itm.SlotPosition;
                }

                if (highestSlot < (int)eInventorySlot.LastBackpack)
                {
                    petItem.SlotPosition = highestSlot + 1;
                    GameServer.Database.AddObject(petItem);
                }
                else
                {
                    GameServer.Database.DeleteObject(unique);
                    DBRecord.State = (int)eGenistarState.Hatched;
                    GameServer.Database.SaveObject(DBRecord);
                }
            }
        }

        public override void Delete()
        {
            if (this.IsAlive && this.Health > 0)
            {
                PackPetIntoContainer();
            }

            base.Delete();
        }

        public override void Die(GameObject killer)
        {
            if (DBRecord != null)
            {
                DBRecord.CurrentTension = (int)(this.Tension * 0.60);
                GameServer.Database.SaveObject(DBRecord);
            }

            base.Die(killer);

            if (DBRecord != null)
            {
                DBRecord.State = (int)eGenistarState.Dead;
                DBRecord.TimerEnd = GameTimer.GetTickCount() + (1 * 60 * 60 * 1000);
                GameServer.Database.SaveObject(DBRecord);

                Housing.House house = Housing.HouseMgr.GetHouse(DBRecord.HouseNumber);
                if (house != null) house.UpdateGenistarVisual(DBRecord.PlaceholderKey, 1293);

                if (GetLivingOwner() is GamePlayer owner)
                {
                    owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "Genistar.GenistarPet.Died"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);

                    ItemTemplate deadTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>("genistar_remains");
                    if (deadTemplate != null)
                    {
                        ItemUnique unique = new ItemUnique(deadTemplate);
                        unique.PackageID = DBRecord.GenistarID;
                        string disp = !string.IsNullOrEmpty(DBRecord.CustomName) ? DBRecord.CustomName : DBRecord.Name;
                        unique.Name = $"Remains: {disp}";

                        GameServer.Database.AddObject(unique);

                        InventoryItem remains = GameInventoryItem.Create(unique);
                        remains.PackageID = DBRecord.GenistarID;
                        remains.Count = 1;

                        if (!owner.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, remains))
                        {
                            WorldInventoryItem drop = new WorldInventoryItem(remains) { Position = this.Position };
                            drop.CurrentRegionID = this.CurrentRegionID;
                            drop.AddOwner(owner);
                            drop.AddToWorld();
                            owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "Genistar.GenistarPet.RemainsDropped"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Custom AI Brain for Summoned Genistars to handle auto-packing on mount
    /// </summary>
    public class GenistarPetBrain : ControlledNpcBrain
    {
        private long m_nextRangedAttack = 0;

        public GenistarPetBrain(GameLiving owner) : base(owner) { }

        protected virtual bool ProcessGenistarRangedAttack()
        {
            if (Body.ActiveWeaponSlot != GameLiving.eActiveWeaponSlot.Distance) return false;
            if (!Body.AttackState || Body.TargetObject == null) return false;

            GameLiving target = Body.TargetObject as GameLiving;
            if (target == null || !target.IsAlive) return false;

            double dist = Body.GetDistanceTo(target);

            if (dist > 150 && dist <= 1500)
            {
                if (Body.IsMoving) Body.StopMoving();
                Body.TurnTo(target);

                if (Body.CurrentRegion.Time >= m_nextRangedAttack)
                {
                    int weaponSpeed = Body.WeaponSpd > 0 ? Body.WeaponSpd : 40;
                    m_nextRangedAttack = Body.CurrentRegion.Time + (weaponSpeed * 100);

                    foreach (GamePlayer p in Body.GetPlayersInRadius(2000))
                        p.Out.SendCombatAnimation(Body, target, 0, 0, 0, 0, 0x0B, target.HealthPercent);

                    double damage = (Body.WeaponDps * (weaponSpeed / 10.0)) * 1.5;

                    foreach (GamePlayer p in Body.GetPlayersInRadius(2000))
                        p.Out.SendSpellEffectAnimation(Body, target, 4, 0, false, 1);

                    target.TakeDamage(Body, eDamageType.Thrust, (int)damage, 0);
                }
                return true;
            }
            else if (dist > 1500)
            {
                Body.Follow(target, 1000, 1500);
                return true;
            }

            return false;
        }

        public override void Think()
        {
            if (Owner is GamePlayer playerOwner)
            {
                if (playerOwner.Client == null || playerOwner.Client.ClientState != GameClient.eClientState.Playing)
                {
                    playerOwner.CommandNpcRelease();
                    return;
                }

                if (playerOwner.CurrentRegionID != Body.CurrentRegionID)
                {
                    playerOwner.CommandNpcRelease();
                    return;
                }

                if (playerOwner.IsOnHorse || playerOwner.Steed != null || playerOwner.IsRiding)
                {
                    playerOwner.Out.SendMessage(LanguageMgr.GetTranslation(playerOwner.Client.Account.Language, "Genistar.GenistarPet.CannotOnMount", Body.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    playerOwner.CommandNpcRelease();
                    return;
                }
            }

            if (ProcessGenistarRangedAttack()) return;
            base.Think();
        }
    }
}