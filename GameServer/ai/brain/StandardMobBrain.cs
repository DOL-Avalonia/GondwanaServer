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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DOL.Events;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.Spells;
using DOL.GS.Movement;
using DOL.GS.PacketHandler;
using DOL.GS.SkillHandler;
using DOL.GS.Keeps;
using DOL.Language;
using log4net;
using DOL.gameobjects.CustomNPC;
using DOL.GS.Scripts;
using DOL.MobGroups;
using DOL.GS.Geometry;
using System.Linq;
using Vector3 = System.Numerics.Vector3;

namespace DOL.AI.Brain
{
    /// <summary>
    /// Standard brain for standard mobs
    /// </summary>
    public class StandardMobBrain : APlayerVicinityBrain, IOldAggressiveBrain
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        public const int MAX_AGGRO_DISTANCE = 3600;
        public const int MAX_AGGRO_LIST_DISTANCE = 6000;
        public const int MAX_PET_AGGRO_DISTANCE = 512; // Tolakram - Live test with caby pet - I was extremely close before auto aggro

        // Used for AmbientBehaviour "Seeing" - maintains a list of GamePlayer in range
        public List<GamePlayer> PlayersSeen = new List<GamePlayer>();

        /// <summary>
        /// Was the mob in combat last tick?
        /// </summary>
        protected bool m_wasInCombat = false;

        /// <summary>
        /// Constructs a new StandardMobBrain
        /// </summary>
        public StandardMobBrain()
            : base()
        {
            AggroLevel = 0;
            AggroRange = 0;
        }

        /// <summary>
        /// Returns the string representation of the StandardMobBrain
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return base.ToString() + ", m_aggroLevel=" + AggroLevel.ToString() + ", m_aggroMaxRange=" + AggroRange.ToString();
        }

        /// <inheritdoc />
        public override bool Start()
        {
            if (!base.Start())
            {
                return false;
            }

            m_wasInCombat = false;
            return true;
        }

        public override bool Stop()
        {
            // tolakram - when the brain stops, due to either death or no players in the vicinity, clear the aggro list
            if (base.Stop())
            {
                ClearAggroList();
                return true;
            }

            return false;
        }

        #region AI

        /// <summary>
        /// Do the mob AI
        /// </summary>
        public override void Think()
        {
            //Satyr:
            //This is a general information. When i review this Think-Procedure and the interaction between it and some
            //code of GameNPC.cs i have the feeling this is a mixture of much ideas of diffeent people, much unfinished
            //features like random-walk which does not actually fit to the rest of this Brain-logic.
            //In other words:
            //If somebody feeling like redoing this stuff completly i would appreciate it. It might be worth redoing
            //instead of trying desperately to make something work that is simply chaoticly moded by too much
            //diffeent inputs.
            //For NOW i made the aggro working the following way (close to live but not yet 100% equal):
            //Mobs will not aggro on their way back home (in fact they should even under some special circumstances)
            //They will completly forget all Aggro when respawned and returned Home.

            bool wasInCombat = m_wasInCombat;
            m_wasInCombat = Body.InCombat;

            // If the NPC is tethered and has been pulled too far it will
            // de-aggro and return to its spawn point.
            if (Body.IsOutOfTetherRange && !Body.InCombat)
            {
                Body.Reset();
                return;
            }

            if (Body.IsIncapacitated)
                return;

            // If the NPC is Moving on path, it can detect closed doors and open them
            if (Body.IsMovingOnPath) DetectDoor();

            // Note: Offensive spells are checked in GameNPC:SpellAction timer

            // If NPC doing a full reset, we don't think further
            if (Body.IsResetting)
            {
                return;
            }

            // If NPC has a max distance and we are outside, full reset
            if (Body.MaxDistance != 0)
            {
                var distance = Body.Position.Coordinate.DistanceTo(Body.SpawnPosition);
                int maxdistance = Body.MaxDistance > 0 ? Body.MaxDistance : -Body.MaxDistance * AggroRange / 100;
                if (maxdistance > 0 && distance > maxdistance)
                {
                    Body.Reset();
                    return;
                }
            }

            // Recently dropped out of combat, reset
            if (wasInCombat && !Body.InCombat)
            {
                Body.Reset();
                if (Body.IsWithinRadius(Body.IsMovingOnPath ? Body.CurrentWayPoint.Coordinate : Body.SpawnPosition.Coordinate, 500))
                {
                    // Not very far - keep thinking, aggro, etc
                    Body.IsResetting = false;
                }
                else
                {
                    return;
                }
            }
            
            //Lets just let CheckSpells() make all the checks for us
            //Check for just positive spells
            if (CheckSpells(eCheckSpellType.Defensive))
                return;

            if (AggroRange > 0)
            {
                var currentPlayersSeen = new List<GamePlayer>();
                foreach (GamePlayer player in Body.GetPlayersInRadius((ushort)AggroRange, true))
                {
                    if (!PlayersSeen.Contains(player))
                    {
                        Body.FireAmbientSentence(GameNPC.eAmbientTrigger.seeing, player as GameLiving);
                        PlayersSeen.Add(player);
                    }
                    currentPlayersSeen.Add(player);
                }

                for (int i = 0; i < PlayersSeen.Count; i++)
                {
                    if (!currentPlayersSeen.Contains(PlayersSeen[i])) PlayersSeen.RemoveAt(i);
                }
            }
            
            // First, if we are part of a MobGroup under attack, help them
            List<GameNPC> friends = new();
            Body.ForeachMobGroup(group =>
            {
                switch (group.AssistRange)
                {
                    case 0:
                        return;

                    case < 0:
                        group.ForeachMob(friends.Add);
                        break;

                    case > 0:
                        group.ForeachMob(friends.Add, list => list.Where(friend => friend.IsWithinRadius(this.Body.Coordinate, (float)group.AssistRange)));
                        break;
                }
            });
            friends.ForEach(friend => this.TryHelp(friend));

            //If we have an aggrolevel above 0, we check for players and npcs in the area to attack
            if (!Body.AttackState && AggroLevel > 0)
            {
                CheckPlayerAggro();
                CheckNPCAggro();
            }

            // If we found a target to aggro, stop thinking and attack
            if (HasAggro)
            {
                Body.FireAmbientSentence(GameNPC.eAmbientTrigger.fighting, Body.TargetObject as GameLiving);
                AttackMostWanted();
                return;
            }

            // Reset target
            Body.TargetObject = null;
            if (Body.AttackState)
                Body.StopAttack();

            //If this NPC can randomly walk around, we allow it to walk around
            if (CanRandomWalk && !Body.IsRoaming && Util.Chance(DOL.GS.ServerProperties.Properties.GAMENPC_RANDOMWALK_CHANCE))
            {
                var target = GetRandomWalkTarget();
                if (Util.IsNearDistance(target, Body.Coordinate, GameNPC.CONST_WALKTOTOLERANCE))
                {
                    Body.TurnTo(target);
                }
                else
                {
                    Body.PathTo(target, 50);
                }

                Body.FireAmbientSentence(GameNPC.eAmbientTrigger.roaming);
            }

            CheckStealth();
        }

        public virtual Coordinate GetFormationCoordinate(Coordinate loc)
        {
            var x = loc.X;
            var y = loc.Y;
            var z = loc.Z;
            var isNotInFormation = !CheckFormation(ref x, ref y, ref z);
            if(isNotInFormation) return Coordinate.Nowhere;

            return Coordinate.Create(x,y,z);
        }

        /// <summary>
        /// Check if the NPC can be stealthed
        /// </summary>
        public virtual void CheckStealth()
        {
            if (Body.CanStealth && !Body.IsStealthed && !Body.InCombat && !Body.IsCasting)
                Body.Stealth(true);
        }

        public int GetGroupMobAggroMultiplier(GamePlayer player)
        {
            int multiplier = 1;
            if (Body.MobGroups is { Count: >0 })
            {
                foreach (MobGroup group in Body.MobGroups)
                {
                    if (!group.IsQuestConditionFriendly && group.HasPlayerCompletedQuests(player))
                    {
                        multiplier *= group.CompletedQuestAggro;
                    }
                }
            }
            return multiplier;
        }

        public int GetGroupMobRangeMultiplier(GamePlayer player)
        {
            int multiplier = 1;
            if (Body.MobGroups is { Count: > 0 })
            {
                foreach (MobGroup group in Body.MobGroups)
                {
                    if (!group.IsQuestConditionFriendly && group.HasPlayerCompletedQuests(player))
                    {
                        multiplier *= group.CompletedQuestRange;
                    }
                }
            }
            return multiplier;
        }

        public virtual bool TryHelp(GameNPC npc)
        {
            if (npc.Brain is StandardMobBrain stdBrain)
            {
                stdBrain.AddAggroListTo(this);
                return true;
            }
            else
            {
                bool found = false;
                foreach (var attacker in npc.Attackers.OfType<GameLiving>())
                {
                    if (attacker is GameNPC npcAttacker)
                    {
                        found = found || TryAggro(npcAttacker);
                    }
                    else if (attacker is GamePlayer playerAttacker)
                    {
                        found = found || TryAggro(playerAttacker);
                    }
                }
                return found;
            }
        }

        /// <summary>
        /// Try aggroing an NPC
        /// </summary>
        /// <param name="npc"></param>
        protected virtual bool TryAggro(GameNPC npc)
        {
            if (!GameServer.ServerRules.IsAllowedToAttack(Body, npc, true))
                return false;

            if (m_aggroTable.ContainsKey(npc))
                return false;

            if (!npc.IsAlive || npc.ObjectState != GameObject.eObjectState.Active)
                return false;

            if (npc is GameTaxi)
                return false;

            if (CalculateAggroLevelToTarget(npc) > 0)
            {
                if (npc.Brain is ControlledNpcBrain or FollowingFriendMobBrain) // This is a pet or charmed creature, checkLOS
                    AddToAggroList(npc, 1, true);
                else
                    AddToAggroList(npc, 1);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Try aggroing an NPC
        /// </summary>
        /// <param name="npc"></param>
        protected virtual bool TryAggro(GamePlayer player)
        {
            // Don't aggro on immune players.
            if (!GameServer.ServerRules.IsAllowedToAttack(Body, player, true))
                return false;

            if (Body.GetDistanceTo(player) > (ushort)AggroRange * GetGroupMobRangeMultiplier(player))
                return false;

            if (player.EffectList.GetOfType<NecromancerShadeEffect>() != null)
                return false;

            int aggrolevel = 0;

            if (Body.Faction != null)
            {
                aggrolevel = Body.Faction.GetAggroToFaction(player);
                if (aggrolevel < 0)
                    aggrolevel = 0;
            }

            if (aggrolevel <= 0 && AggroLevel <= 0)
                return false;

            if (m_aggroTable.ContainsKey(player))
                return false;

            if (!player.IsAlive || player.ObjectState != GameObject.eObjectState.Active || player.IsStealthed)
                return false;

            if (player.Steed != null)
                return false;

            if (CalculateAggroLevelToTarget(player) > 0)
            {
                AddToAggroList(player, 1, true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check for aggro against close NPCs
        /// </summary>
        protected virtual void CheckNPCAggro()
        {
            if (HasAggro || Body.CurrentZone == null) return;

            foreach (GameNPC npc in Body.GetNPCsInRadius((ushort)(AggroRange), Body.CurrentRegion.IsDungeon ? false : true))
            {
                TryAggro(npc);
            }
        }

        /// <summary>
        /// Check for aggro against players
        /// </summary>
        protected virtual void CheckPlayerAggro()
        {
            if (HasAggro || Body.CurrentZone == null) return;

            foreach (GamePlayer player in Body.GetPlayersInRadius(MAX_AGGRO_DISTANCE, Body.CurrentZone.IsDungeon ? false : true))
            {
                TryAggro(player);
            }
        }

        /// <summary>
        /// The interval for thinking, min 1.5 seconds
        /// 10 seconds for 0 aggro mobs
        /// </summary>
        public override int ThinkInterval
        {
            get { return Math.Max(1500, 10000 - AggroLevel * 100); }
        }

        /// <summary>
        /// If this brain is part of a formation, it edits it's values accordingly.
        /// </summary>
        /// <param name="x">The x-coordinate to refer to and change</param>
        /// <param name="y">The x-coordinate to refer to and change</param>
        /// <param name="z">The x-coordinate to refer to and change</param>
        public virtual bool CheckFormation(ref int x, ref int y, ref int z)
        {
            return false;
        }

        /// <summary>
        /// Checks the Abilities
        /// </summary>
        public virtual void CheckAbilities()
        {
            //See CNPC
        }

        #endregion

        #region Aggro

        /// <summary>
        /// List of livings that this npc has aggro on, living => aggroamount
        /// </summary>
        protected readonly Dictionary<GameLiving, long> m_aggroTable = new Dictionary<GameLiving, long>();

        /// <summary>
        /// The aggression table for this mob
        /// </summary>
        public Dictionary<GameLiving, long> AggroTable
        {
            get { return m_aggroTable; }
        }

        /// <summary>
        /// Aggressive Level in % 0..100, 0 means not Aggressive
        /// </summary>
        public int AggroLevel { get; set; }

        /// <summary>
        /// Range in that this npc aggros
        /// </summary>
        public int AggroRange { get; set; }

        /// <summary>
        /// Checks whether living has someone on its aggrolist
        /// </summary>
        public virtual bool HasAggro
            => AggroTable.Count > 0;

        /// <summary>
        /// Add aggro table of this brain to that of another living.
        /// </summary>
        /// <param name="brain">The target brain.</param>
        public void AddAggroListTo(StandardMobBrain brain)
        {
            // TODO: This should actually be the other way round, but access
            // to m_aggroTable is restricted and needs to be threadsafe.

            // do not modify aggro list if dead
            if (!brain.Body.IsAlive) return;

            lock ((m_aggroTable as ICollection).SyncRoot)
            {
                Dictionary<GameLiving, long>.Enumerator dictEnum = m_aggroTable.GetEnumerator();
                while (dictEnum.MoveNext())
                    brain.AddToAggroList(dictEnum.Current.Key, Body.MaxHealth);
            }
        }

        // LOS Check on natural aggro (aggrorange & aggrolevel)
        // This part is here due to los check constraints;
        // Otherwise, it should be in CheckPlayerAggro() method.
        private bool m_AggroLOS;
        public virtual bool AggroLOS
        {
            get { return m_AggroLOS; }
            set { m_AggroLOS = value; }
        }
        private void CheckAggroLOS(GamePlayer player, ushort response, ushort targetOID)
        {
            if ((response & 0x100) == 0x100)
                AggroLOS = true;
            else
                AggroLOS = false;
        }

        /// <summary>
        /// Add living to the aggrolist
        /// aggroamount can be negative to lower amount of aggro
        /// </summary>
        /// <param name="living"></param>
        /// <param name="aggroamount"></param>
        public void AddToAggroList(GameLiving living, int aggroamount)
        {
            AddToAggroList(living, aggroamount, false);
        }

        /// <summary>
        /// Add living to the aggrolist
        /// aggroamount can be negative to lower amount of aggro
        /// </summary>
        /// <param name="living"></param>
        /// <param name="aggroamount"></param>
        /// <param name="CheckLOS"></param>
        public virtual void AddToAggroList(GameLiving living, int aggroamount, bool CheckLOS)
        {
            if (m_body.IsConfused) return;

            // tolakram - duration spell effects will attempt to add to aggro after npc is dead
            if (!m_body.IsAlive) return;

            if (living == null) return;

            // Check LOS (walls, pits, etc...) before  attacking, player + pet
            // Be sure the aggrocheck is triggered by the brain on Think() method
            if (DOL.GS.ServerProperties.Properties.ALWAYS_CHECK_LOS && CheckLOS)
            {
                GamePlayer thisLiving = living as GamePlayer ?? living.GetPlayerOwner();

                if (thisLiving != null)
                {
                    thisLiving.Out.SendCheckLOS(Body, living, new CheckLOSResponse(CheckAggroLOS));
                    if (!AggroLOS) return;
                }
            }

            BringFriends(living);

            //Handle trigger to say sentance on first aggro.
            if (m_aggroTable.Count < 1)
            {
                Body.FireAmbientSentence(GameNPC.eAmbientTrigger.aggroing, living);
            }

            // only protect if gameplayer and aggroamout > 0
            if (living is GamePlayer && aggroamount > 0)
            {
                GamePlayer player = (GamePlayer)living;

                if (player.Group != null)
                { // player is in group, add whole group to aggro list
                    lock ((m_aggroTable as ICollection).SyncRoot)
                    {
                        foreach (GamePlayer p in player.Group.GetPlayersInTheGroup())
                        {
                            if (!m_aggroTable.ContainsKey(p))
                            {
                                m_aggroTable[p] = 1L;   // add the missing group member on aggro table
                            }
                        }
                    }
                }

                //ProtectEffect protect = (ProtectEffect) player.EffectList.GetOfType(typeof(ProtectEffect));
                foreach (ProtectEffect protect in player.EffectList.GetAllOfType<ProtectEffect>())
                {
                    // if no aggro left => break
                    if (aggroamount <= 0) break;

                    //if (protect==null) continue;
                    if (protect.ProtectTarget != living) continue;
                    if (protect.ProtectSource.IsStunned) continue;
                    if (protect.ProtectSource.IsMezzed) continue;
                    if (protect.ProtectSource.IsSitting) continue;
                    if (protect.ProtectSource.ObjectState != GameObject.eObjectState.Active) continue;
                    if (!protect.ProtectSource.IsAlive) continue;
                    if (!protect.ProtectSource.InCombat) continue;

                    if (!living.IsWithinRadius(protect.ProtectSource, ProtectAbilityHandler.PROTECT_DISTANCE))
                        continue;
                    // P I: prevents 10% of aggro amount
                    // P II: prevents 20% of aggro amount
                    // P III: prevents 30% of aggro amount
                    // guessed percentages, should never be higher than or equal to 50%
                    int abilityLevel = protect.ProtectSource.GetAbilityLevel(Abilities.Protect);
                    int protectAmount = (int)((abilityLevel * 0.10) * aggroamount);

                    if (protectAmount > 0)
                    {
                        aggroamount -= protectAmount;
                        if (protect.ProtectSource is GamePlayer playerSource)
                            playerSource.Out.SendMessage(LanguageMgr.GetTranslation(playerSource.Client.Account.Language, "AI.Brain.StandardMobBrain.YouProtDist", player.GetName(0, false),
                                Body.GetName(0, false, playerSource.Client.Account.Language, Body)), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                        lock ((m_aggroTable as ICollection).SyncRoot)
                        {
                            if (m_aggroTable.ContainsKey(protect.ProtectSource))
                                m_aggroTable[protect.ProtectSource] += protectAmount;
                            else
                                m_aggroTable[protect.ProtectSource] = protectAmount;
                        }
                    }
                }
            }

            lock ((m_aggroTable as ICollection).SyncRoot)
            {
                if (m_aggroTable.ContainsKey(living))
                {
                    long amount = m_aggroTable[living];
                    amount += aggroamount;

                    // can't be removed this way, set to minimum
                    if (amount <= 0)
                        amount = 1L;

                    m_aggroTable[living] = amount;
                }
                else
                {
                    if (aggroamount > 0)
                    {
                        m_aggroTable[living] = aggroamount;
                    }
                    else
                    {
                        m_aggroTable[living] = 1L;
                    }
                }
            }
        }

        /// <summary>
        /// Get current amount of aggro on aggrotable
        /// </summary>
        /// <param name="living"></param>
        /// <returns></returns>
        public virtual long GetAggroAmountForLiving(GameLiving living)
        {
            lock ((m_aggroTable as ICollection).SyncRoot)
            {
                if (m_aggroTable.ContainsKey(living))
                {
                    return m_aggroTable[living];
                }
                return 0;
            }
        }

        /// <summary>
        /// Remove one living from aggro list
        /// </summary>
        /// <param name="living"></param>
        public virtual void RemoveFromAggroList(GameLiving living)
        {
            lock ((m_aggroTable as ICollection).SyncRoot)
            {
                m_aggroTable.Remove(living);
            }
        }

        /// <summary>
        /// Remove all livings from the aggrolist
        /// </summary>
        public virtual void ClearAggroList()
        {
            CanBAF = true; // Mobs that drop out of combat can BAF again

            lock ((m_aggroTable as ICollection).SyncRoot)
            {
                m_aggroTable.Clear();
                Body.TempProperties.removeProperty(Body.Attackers);
            }
        }

        /// <summary>
        /// Makes a copy of current aggro list
        /// </summary>
        /// <returns></returns>
        public virtual Dictionary<GameLiving, long> CloneAggroList()
        {
            lock ((m_aggroTable as ICollection).SyncRoot)
            {
                return new Dictionary<GameLiving, long>(m_aggroTable);
            }
        }

        /// <summary>
        /// Selects and attacks the next target or does nothing
        /// </summary>
        protected virtual void AttackMostWanted()
        {
            if (!IsActive)
                return;

            Body.TargetObject = CalculateNextAttackTarget();

            if (Body.TargetObject != null)
            {
                if (!CheckSpells(eCheckSpellType.Offensive))
                {
                    Body.StartAttack(Body.TargetObject);
                }
            }
        }

        /// <summary>
        /// Returns the best target to attack
        /// </summary>
        /// <returns>the best target</returns>
        protected virtual GameLiving CalculateNextAttackTarget()
        {
            GameLiving maxAggroObject = null;
            Dictionary<GameLiving, long> table;
            lock ((m_aggroTable as ICollection).SyncRoot)
            {
                table = new(m_aggroTable);
            }
            double maxAggro = 0;
            Dictionary<GameLiving, long>.Enumerator aggros = table.GetEnumerator();
            List<GameLiving> removable = new List<GameLiving>();
            while (aggros.MoveNext())
            {
                GameLiving living = aggros.Current.Key;

                // check to make sure this target is still valid
                if (living.IsAlive == false ||
                    living.ObjectState != GameObject.eObjectState.Active ||
                    living.IsStealthed ||
                    !Body.IsWithinRadius2D(living, MAX_AGGRO_LIST_DISTANCE))
                {
                    removable.Add(living);
                    continue;
                }

                // Don't bother about necro shade, can't attack it anyway.
                if (living.EffectList.GetOfType<NecromancerShadeEffect>() != null)
                    continue;

                long amount = aggros.Current.Value;

                if (living.IsAlive
                    && amount > maxAggro
                    && living.CurrentRegion == Body.CurrentRegion
                    && living.ObjectState == GameObject.eObjectState.Active
                    && GameServer.ServerRules.IsAllowedToAttack(Body, living, true))
                {
                    float distance = Body.GetDistanceTo(living.Position, 0);
                    int maxAggroDistance = (this is IControlledBrain) ? MAX_PET_AGGRO_DISTANCE : MAX_AGGRO_DISTANCE;

                    if (distance <= maxAggroDistance)
                    {
                        double aggro = amount * Math.Min(500.0 / distance, 1);
                        if (aggro > maxAggro)
                        {
                            maxAggroObject = living;
                            maxAggro = aggro;
                        }
                    }
                }
            }

            foreach (GameLiving l in removable)
            {
                RemoveFromAggroList(l);
                Body.RemoveAttacker(l);
            }

            if (maxAggroObject == null)
            {
                lock ((m_aggroTable as ICollection).SyncRoot)
                    m_aggroTable.Clear();
            }

            return maxAggroObject;
        }

        /// <summary>
        /// calculate the aggro of this npc against another living
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public virtual int CalculateAggroLevelToTarget(GameLiving target)
        {
            // Get owner if target is pet
            GameLiving realTarget = target;
            var realAggroLevel = AggroLevel;
            if (target is GameNPC targetNPC)
            {
                GameLiving owner = targetNPC.GetLivingOwner();
                if (owner != null)
                    realTarget = owner;
                // FollowingFriendMob will have higher aggro
                if (realTarget is FollowingFriendMob { PlayerFollow: not null } followMob)
                {
                    realAggroLevel = (int)(AggroLevel * followMob.AggroMultiplier);
                    realTarget = followMob.PlayerFollow;
                }
            }

            // Withdraw if can't attack.
            if (!GameServer.ServerRules.IsAllowedToAttack(Body, realTarget, true))
                return 0;


            // only attack if green+ to target
            if (realTarget.IsObjectGreyCon(Body))
                return 0;

            // If this npc have Faction return the AggroAmount to Player
            if (Body.Faction != null)
            {
                if (realTarget is GamePlayer playerTarget)
                {
                    return Math.Min(100, (int)(Body.Faction.GetAggroToFaction(playerTarget) * GetGroupMobAggroMultiplier(playerTarget)));
                }
                else if (realTarget is GameNPC npcTarget && Body.Faction.EnemyFactions.Contains(npcTarget.Faction))
                {
                    return 100;
                }
            }

            //we put this here to prevent aggroing non-factions npcs
            if (Body.Realm == eRealm.None && realTarget is GameNPC)
                return 0;

            if (realTarget is GamePlayer)
            {
                return Math.Min(100, (int)(realAggroLevel * GetGroupMobAggroMultiplier((GamePlayer)realTarget)));
            }
            else
            {
                return Math.Min(100, (int)(realAggroLevel));
            }
        }

        /// <summary>
        /// Receives all messages of the body
        /// </summary>
        /// <param name="e">The event received</param>
        /// <param name="sender">The event sender</param>
        /// <param name="args">The event arguments</param>
        public override void Notify(DOLEvent e, object sender, EventArgs args)
        {
            base.Notify(e, sender, args);

            if (!IsActive) return;

            if (sender == Body)
            {
                if (e == GameObjectEvent.TakeDamage)
                {
                    if (args is not TakeDamageEventArgs { DamageSource: GameLiving living } eArgs) return;

                    int aggro = eArgs.DamageAmount + eArgs.CriticalAmount;
                    if (eArgs.DamageSource is GameNPC npc)
                    {
                        // owner gets 25% of aggro
                        GameLiving owner = npc.GetLivingOwner();
                        if (owner != null)
                        {
                            AddToAggroList(owner, (int)Math.Max(1, aggro * 0.25));
                            aggro = (int)Math.Max(1, aggro * 0.75);
                        }
                    }
                    AddToAggroList((GameLiving)eArgs.DamageSource, aggro);
                    return;
                }
                else if (e == GameLivingEvent.AttackedByEnemy)
                {
                    if (args is not AttackedByEnemyEventArgs eArgs) return;
                    OnAttackedByEnemy(eArgs.AttackData);
                    return;
                }
                else if (e == GameLivingEvent.Dying)
                {
                    // clean aggro table
                    ClearAggroList();
                    return;
                }
                else if (e == GameNPCEvent.FollowLostTarget) // this means we lost the target
                {
                    if (args is not FollowLostTargetEventArgs eArgs) return;
                    OnFollowLostTarget(eArgs.LostTarget);
                    return;
                }
                else if (e == GameLivingEvent.CastFailed)
                {
                    if (args is not CastFailedEventArgs realArgs || realArgs.Reason == CastFailedEventArgs.Reasons.AlreadyCasting || realArgs.Reason == CastFailedEventArgs.Reasons.CrowdControlled)
                        return;
                    Body.StartAttack(Body.TargetObject);
                }
            }

            if (e == GameLivingEvent.EnemyHealed)
            {
                if (args is EnemyHealedEventArgs { HealSource: GameLiving } eArgs)
                {
                    // first check to see if the healer is in our aggrolist so we don't go attacking anyone who heals
                    if (m_aggroTable.ContainsKey(eArgs.HealSource as GameLiving))
                    {
                        if (eArgs.HealSource is GamePlayer || (eArgs.HealSource is GameNPC && !((GameNPC)eArgs.HealSource).IsPeaceful))
                        {
                            AddToAggroList((GameLiving)eArgs.HealSource, eArgs.HealAmount);
                        }
                    }
                }
                return;
            }
            else if (e == GameLivingEvent.EnemyKilled)
            {
                if (args is EnemyKilledEventArgs eArgs)
                {
                    // transfer all controlled target aggro to the owner
                    if (eArgs.Target is GameNPC && eArgs.Target.GetLivingOwner() is {} owner)
                    {
                        long contrAggro = GetAggroAmountForLiving(eArgs.Target);
                        AddToAggroList(owner, (int)contrAggro);
                    }

                    Body.Attackers.Remove(eArgs.Target);
                    AttackMostWanted();
                }
                return;
            }
            else if (e == GameLivingEvent.TargetInRange)
            {
                if (AggroRange > 0 && args is TargetInRangeEventArgs eArgs && sender is GameLiving target)
                {
                    if (target is FollowingFriendMob { PlayerFollow: not null } followMob)
                    {
                        float range = Body.CurrentRegion.IsDungeon ? Body.GetDistanceTo(target) : Body.GetDistance2DTo(target.Position);
                        if (!HasAggro && range < (AggroRange * GetGroupMobRangeMultiplier(followMob.PlayerFollow) *
                            followMob.AggroMultiplier))
                        {
                            TryAggro(followMob);
                        }
                    }
                }

                return;
            }

        }

        /// <summary>
        /// Lost follow target event
        /// </summary>
        /// <param name="target"></param>
        protected virtual void OnFollowLostTarget(GameObject target)
        {
            AttackMostWanted();
            if (!Body.AttackState)
                Body.Reset();
        }

        /// <summary>
        /// Attacked by enemy event
        /// </summary>
        /// <param name="ad"></param>
        protected virtual void OnAttackedByEnemy(AttackData ad)
        {
            if (!Body.AttackState
                && Body.IsAlive
                && Body.ObjectState == GameObject.eObjectState.Active)
            {
                if (ad.AttackResult == GameLiving.eAttackResult.Missed)
                {
                    AddToAggroList(ad.Attacker, 1);
                }

                Body.StartAttack(ad.Attacker);
            }
        }

        #endregion

        #region Bring a Friend
        /// <summary>
        /// Initial range to try to get BAFs from.
        /// May be overloaded for specific brain types, ie. dragons or keep guards
        /// </summary>
        protected virtual ushort BAFInitialRange
        {
            get { return DOL.GS.ServerProperties.Properties.INITIAL_BAF_RANGE; }
        }

        /// <summary>
        /// Max range to try to get BAFs from.
        /// May be overloaded for specific brain types, ie.dragons or keep guards
        /// </summary>
        protected virtual ushort BAFMaxRange
        {
            get
            {
                return DOL.GS.ServerProperties.Properties.MAX_BAF_RANGE;
            }
        }

        /// <summary>
        /// Max range to try to look for nearby players.
        /// May be overloaded for specific brain types, ie.dragons or keep guards
        /// </summary>
        protected virtual ushort BAFPlayerRange
        {
            get
            {
                return 5000;
            }
        }

        /// <summary>
        /// Can the mob bring a friend?
        /// Set to false when a mob BAFs or is brought by a friend.
        /// </summary>
        public virtual bool CanBAF { get; set; } = true;

        /// <summary>
        /// Bring friends when this mob aggros
        /// </summary>
        /// <param name="attacker">Whoever triggered the BAF</param>
        protected virtual void BringFriends(GameLiving attacker)
        {
            if (!CanBAF)
                return;

            GamePlayer puller = attacker.GetPlayerOwner();  // player that triggered the BAF

            // Only BAF on players and pets of players
            if (puller is null)
                return;

            CanBAF = false; // Mobs only BAF once per fight

            int numAttackers = 0;

            List<GamePlayer> victims = null; // Only instantiated if we're tracking potential victims

            // These are only used if we have to check for duplicates
            HashSet<String> countedVictims = null;
            HashSet<String> countedAttackers = null;

            BattleGroup bg = puller.TempProperties.getProperty<object>(BattleGroup.BATTLEGROUP_PROPERTY, null) as BattleGroup;

            // Check group first to minimize the number of HashSet.Add() calls
            if (puller.Group is Group group)
            {
                if (DOL.GS.ServerProperties.Properties.BAF_MOBS_COUNT_BG_MEMBERS && bg != null)
                    countedAttackers = new HashSet<String>(); // We have to check for duplicates when counting attackers

                if (!DOL.GS.ServerProperties.Properties.BAF_MOBS_ATTACK_PULLER)
                {
                    if (DOL.GS.ServerProperties.Properties.BAF_MOBS_ATTACK_BG_MEMBERS && bg != null)
                    {
                        // We need a large enough victims list for group and BG, and also need to check for duplicate victims
                        victims = new List<GamePlayer>(group.MemberCount + bg.PlayerCount - 1);
                        countedVictims = new HashSet<String>();
                    }
                    else
                        victims = new List<GamePlayer>(group.MemberCount);
                }

                foreach (GamePlayer player in group.GetPlayersInTheGroup())
                    if (player != null && (player.InternalID == puller.InternalID || player.IsWithinRadius2D(puller, BAFPlayerRange)))
                    {
                        numAttackers++;

                        if (countedAttackers != null)
                            countedAttackers.Add(player.InternalID);

                        if (victims != null)
                        {
                            victims.Add(player);

                            if (countedVictims != null)
                                countedVictims.Add(player.InternalID);
                        }
                    }
            } // if (puller.Group is Group group)

            // Do we have to count BG members, or add them to victims list?
            if ((bg != null) && (DOL.GS.ServerProperties.Properties.BAF_MOBS_COUNT_BG_MEMBERS
                || (DOL.GS.ServerProperties.Properties.BAF_MOBS_ATTACK_BG_MEMBERS && !DOL.GS.ServerProperties.Properties.BAF_MOBS_ATTACK_PULLER)))
            {
                if (victims == null && DOL.GS.ServerProperties.Properties.BAF_MOBS_ATTACK_BG_MEMBERS && !DOL.GS.ServerProperties.Properties.BAF_MOBS_ATTACK_PULLER)
                    // Puller isn't in a group, so we have to create the victims list for the BG
                    victims = new List<GamePlayer>(bg.PlayerCount);

                foreach (GamePlayer player in bg.GetPlayersInTheBattleGroup())
                    if (player != null && (player.InternalID == puller.InternalID || player.IsWithinRadius2D(puller, BAFPlayerRange)))
                    {
                        if (DOL.GS.ServerProperties.Properties.BAF_MOBS_COUNT_BG_MEMBERS
                            && (countedAttackers == null || !countedAttackers.Contains(player.InternalID)))
                            numAttackers++;

                        if (victims != null && (countedVictims == null || !countedVictims.Contains(player.InternalID)))
                            victims.Add(player);
                    }
            } // if ((bg != null) ...

            if (numAttackers == 0)
                // Player is alone
                numAttackers = 1;

            int additionalChance = DOL.GS.ServerProperties.Properties.BAF_ADDITIONAL_CHANCE;

            if (attacker.ControlledBrain is TurretFNFBrain && DOL.GS.ServerProperties.Properties.LIMIT_BAF_ADDITIONAL_CHANCE_TURRET > 0)
                // in dungeon LIMIT_BAF_ADDITIONAL_CHANCE_TURRET / 2
                additionalChance = (int)(additionalChance / DOL.GS.ServerProperties.Properties.LIMIT_BAF_ADDITIONAL_CHANCE_TURRET);

            int percentBAF = DOL.GS.ServerProperties.Properties.BAF_INITIAL_CHANCE + ((numAttackers - 1) * additionalChance);

            int maxAdds = percentBAF / 100; // Multiple of 100 are guaranteed BAFs

            // Calculate chance of an addition add based on the remainder
            if (Util.Chance(percentBAF % 100))
                maxAdds++;

            if (maxAdds > 0)
            {
                int numAdds = 0; // Number of mobs currently BAFed
                ushort range = BAFInitialRange; // How far away to look for friends

                // Try to bring closer friends before distant ones.
                while (numAdds < maxAdds && range <= BAFMaxRange)
                {
                    foreach (GameNPC npc in Body.GetNPCsInRadius(range))
                    {
                        if (npc == Body)
                            continue;
                        if (numAdds >= maxAdds)
                            break;

                        // If it's a friend, have it attack
                        if (npc.IsFriend(Body) && npc.IsAggressive && npc.IsAvailable && npc.Brain is StandardMobBrain brain)
                        {
                            brain.CanBAF = false; // Mobs brought cannot bring friends of their own

                            GamePlayer target;
                            if (victims != null && victims.Count > 0)
                                target = victims[Util.Random(0, victims.Count - 1)];
                            else
                                target = puller;

                            brain.AddToAggroList(target, 1);
                            brain.AttackMostWanted();
                            numAdds++;
                        }
                    }// foreach

                    // Increase the range for finding friends to join the fight.
                    range *= 2;
                } // while
            } // if (maxAdds > 0)
        } // BringFriends()

        #endregion

        #region Spells

        public enum eCheckSpellType
        {
            Offensive,
            Defensive
        }
        public bool waitingForMana = false;
        /// <summary>
        /// Checks if any spells need casting
        /// </summary>
        /// <param name="type">Which type should we go through and check for?</param>
        /// <returns></returns>
        public virtual bool CheckSpells(eCheckSpellType type)
        {
            if (Body.IsCasting)
                return true;

            bool casted = false;

            if (Body != null && Body.Spells != null && Body.Spells.Count > 0)
            {
                ArrayList spell_rec = new ArrayList();
                Spell spellToCast = null;
                bool needpet = false;
                bool needheal = false;
                // Don't cast if mana is too low
                if (waitingForMana && Body.Mana > Body.MaxMana * 0.5)
                {
                    waitingForMana = false;
                }
                else if (waitingForMana && !Body.canQuickCast)
                {
                    return false;
                }
                else if (Body.Mana < Body.MaxMana * 0.2)
                {
                    waitingForMana = true;
                    return false;
                }

                if (type == eCheckSpellType.Defensive)
                {
                    foreach (Spell spell in Body.Spells)
                    {
                        if (Body.GetSkillDisabledDuration(spell) > 0) continue;
                        if (spell.Target.ToLower() == "enemy" || spell.Target.ToLower() == "area" || spell.Target.ToLower() == "cone") continue;
                        // If we have no pets
                        if (Body.ControlledBrain == null)
                        {
                            if (spell.SpellType.ToLower() == "pet") continue;
                            if (spell.SpellType.ToLower().Contains("summon"))
                            {
                                spell_rec.Add(spell);
                                needpet = true;
                            }
                        }
                        if (Body.ControlledBrain != null && Body.ControlledBrain.Body != null)
                        {
                            if (Util.Chance(30) && Body.ControlledBrain != null && spell.SpellType.ToLower() == "heal" &&
                                Body.GetDistanceTo(Body.ControlledBrain.Body) <= spell.Range &&
                                Body.ControlledBrain.Body.HealthPercent < DOL.GS.ServerProperties.Properties.NPC_HEAL_THRESHOLD
                                && spell.Target.ToLower() != "self")
                            {
                                spell_rec.Add(spell);
                                needheal = true;
                            }
                            if (LivingHasEffect(Body.ControlledBrain!.Body, spell) && (spell.Target.ToLower() != "self")) continue;
                        }
                        if (!needpet && !needheal)
                            spell_rec.Add(spell);
                    }
                    if (spell_rec.Count > 0)
                    {
                        spellToCast = (Spell)spell_rec[Util.Random((spell_rec.Count - 1))];
                        if (!Body.IsReturningHome)
                        {
                            if ((spellToCast!.Uninterruptible || Body.canQuickCast) && CheckDefensiveSpells(spellToCast))
                                casted = true;
                            else
                                if (!Body.IsBeingInterrupted && CheckDefensiveSpells(spellToCast))
                                casted = true;
                        }
                    }
                }
                else if (type == eCheckSpellType.Offensive)
                {
                    foreach (Spell spell in Body.Spells)
                    {
                        if (Body.GetSkillDisabledDuration(spell) > 0)
                        {
                            continue;
                        }
                        if (spell.Target.ToLower() is not ("enemy" or "area" or "cone"))
                        {
                            continue;
                        }

                        if (LivingHasEffect(Body.TargetObject as GameLiving, spell) && spell.SpellType is not ("DirectDamage" or "DirectDamageWithDebuff"))
                        {
                            continue;
                        }

                        if (spell.CastTime > 0)
                        {
                            if (Body.CanCastHarmfulSpells)
                            {
                                spell_rec.Add(spell);
                            }
                        }
                        else
                        {
                            if (Body.CanCastInstantHarmfulSpells)
                            {
                                spell_rec.Add(spell);
                            }
                        }
                    }
                    if (spell_rec.Count > 0)
                    {
                        spellToCast = (Spell)spell_rec[Util.Random((spell_rec.Count - 1))];


                        if ((spellToCast!.Uninterruptible || Body.canQuickCast) && CheckOffensiveSpells(spellToCast))
                            casted = true;
                        else
                            if (!Body.IsBeingInterrupted && CheckOffensiveSpells(spellToCast))
                            casted = true;
                    }
                }

                return casted;
            }
            return casted;
        }

        protected bool SelectCureTarget(Spell spell, IEnumerable<string> spellTypes, out GameObject target)
        {
            target = null;

            // Check self
            if (HasNegativeEffect(Body, spellTypes))
            {
                target = Body;
                return true;
            }

            // Check pet
            if (Body.ControlledBrain != null && Body.ControlledBrain.Body != null
                && HasNegativeEffect(Body.ControlledBrain.Body, spellTypes)
                && Body.GetDistanceTo(Body.ControlledBrain.Body) <= spell.Range
                && spell.Target.ToLower() != "self")
            {
                target = Body.ControlledBrain.Body;
                return true;
            }

            // Check realm (friendly NPCs)
            if (spell.Target.ToLower() == "realm")
            {
                foreach (GameNPC npc in Body.GetNPCsInRadius((ushort)Math.Max(spell.Radius, spell.Range)))
                {
                    if (Body.IsFriend(npc) && Util.Chance(60) && HasNegativeEffect(npc, spellTypes))
                    {
                        target = npc;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks defensive spells.  Handles buffs, heals, etc.
        /// </summary>
        protected virtual bool CheckDefensiveSpells(Spell spell)
        {
            if (spell == null) return false;
            if (Body.GetSkillDisabledDuration(spell) > 0) return false;

            bool casted = false;

            // clear current target, set target based on spell type, cast spell, return target to original target
            GameObject lastTarget = Body.TargetObject;

            Body.TargetObject = null;
            switch (spell.SpellType.ToUpper())
            {
                #region Buffs
                case "ACUITYBUFF":
                case "AFHITSBUFF":
                case "ALLMAGICRESISTSBUFF":
                case "ARMORABSORPTIONBUFF":
                case "ARMORFACTORBUFF":
                case "BODYRESISTBUFF":
                case "BODYSPIRITENERGYBUFF":
                case "BUFF":
                case "CELERITYBUFF":
                case "COLDRESISTBUFF":
                case "COMBATSPEEDBUFF":
                case "CONSTITUTIONBUFF":
                case "COURAGEBUFF":
                case "CRUSHSLASHTHRUSTBUFF":
                case "DEXTERITYBUFF":
                case "DEXTERITYQUICKNESSBUFF":
                case "EFFECTIVENESSBUFF":
                case "ENDURANCEREGENBUFF":
                case "ENERGYRESISTBUFF":
                case "FATIGUECONSUMPTIONBUFF":
                case "FELXIBLESKILLBUFF":
                case "HASTEBUFF":
                case "HEALTHREGENBUFF":
                case "HEATCOLDMATTERBUFF":
                case "HEATRESISTBUFF":
                case "HEROISMBUFF":
                case "KEEPDAMAGEBUFF":
                case "MAGICRESISTSBUFF":
                case "MATTERRESISTBUFF":
                case "MELEEDAMAGEBUFF":
                case "MESMERIZEDURATIONBUFF":
                case "MLABSBUFF":
                case "PALADINARMORFACTORBUFF":
                case "PARRYBUFF":
                case "POWERHEALTHENDURANCEREGENBUFF":
                case "POWERREGENBUFF":
                case "SAVAGECOMBATSPEEDBUFF":
                case "SAVAGECRUSHRESISTANCEBUFF":
                case "SAVAGEDPSBUFF":
                case "SAVAGEPARRYBUFF":
                case "SAVAGESLASHRESISTANCEBUFF":
                case "SAVAGETHRUSTRESISTANCEBUFF":
                case "SPIRITRESISTBUFF":
                case "STRENGTHBUFF":
                case "STRENGTHCONSTITUTIONBUFF":
                case "SUPERIORCOURAGEBUFF":
                case "TOHITBUFF":
                case "WEAPONSKILLBUFF":
                case "DAMAGEADD":
                case "OFFENSIVEPROC":
                case "DEFENSIVEPROC":
                case "DAMAGESHIELD":
                case "BLADETURN":
                case "BOTHABLATIVEARMOR":
                case "SPELLREFLECTION":
                case "ALLSTATSBUFF":
                case "TRIGGERBUFF":
                case "TENSIONBUFF":
                case "BATTLEFEVERDURATIONBUFF":
                case "SPELLSHIELD":
                case "DEBUFFIMMUNITY":
                case "CRITICALMAGICALBUFF":
                case "CRITICALMELEEBUFF":
                case "POWERSHIELD":
                case "MAGICHEALABSORB":
                    {
                        // Buff self, if not in melee, but not each and every mob
                        // at the same time, because it looks silly.
                        if (!LivingHasEffect(Body, spell) && !Body.AttackState && Util.Chance(40) && spell.Target.ToLower() != "pet")
                        {
                            Body.TargetObject = Body;
                            break;
                        }
                        if (Body.ControlledBrain != null && Body.ControlledBrain.Body != null && Util.Chance(40) && Body.GetDistanceTo(Body.ControlledBrain.Body) <= spell.Range && !LivingHasEffect(Body.ControlledBrain.Body, spell) && spell.Target.ToLower() != "self")
                        {
                            Body.TargetObject = Body.ControlledBrain.Body;
                            break;
                        }
                        if (spell.Target == "realm")
                        {
                            foreach (GameNPC npc in Body.GetNPCsInRadius((ushort)Math.Max(spell.Radius, spell.Range)))
                            {
                                if (GameServer.ServerRules.IsAllowedToHelp(Body, npc, true) && Body.IsFriend(npc) && Util.Chance(60) && !LivingHasEffect(npc, spell))
                                {
                                    Body.TargetObject = npc;
                                    break;
                                }
                            }
                        }
                        break;
                    }
                #endregion Buffs

                #region Disease Cure/Poison Cure/Summon
                case "CUREDISEASE":
                    {
                        if (SelectCureTarget(spell, CureSpellConstants.CureDiseaseSpellTypes, out GameObject target))
                            Body.TargetObject = target;
                        break;
                    }
                case "CUREPOISON":
                    {
                        if (SelectCureTarget(spell, CureSpellConstants.CurePoisonSpellTypes, out GameObject target))
                            Body.TargetObject = target;
                        break;
                    }
                case "CURENEARSIGHT":
                    {
                        if (SelectCureTarget(spell, CureSpellConstants.CureNearsightSpellTypes, out GameObject target))
                            Body.TargetObject = target;
                        break;
                    }
                case "CUREMEZZ":
                    {
                        if (SelectCureTarget(spell, CureSpellConstants.CureMezzSpellTypes, out GameObject target))
                            Body.TargetObject = target;
                        break;
                    }
                case "ARAWNCURE":
                    {
                        if (SelectCureTarget(spell, CureSpellConstants.ArawnCureSpellTypes, out GameObject target))
                            Body.TargetObject = target;
                        break;
                    }
                case "UNPETRIFY":
                    {
                        if (SelectCureTarget(spell, CureSpellConstants.CurePetrifySpellTypes, out GameObject target))
                            Body.TargetObject = target;
                        break;
                    }
                case "CUREALL":
                    {
                        if (SelectCureTarget(spell, CureSpellConstants.CureAllSpellTypes, out GameObject target))
                            Body.TargetObject = target;
                        break;
                    }
                case "SUMMON":
                    Body.TargetObject = Body;
                    break;
                case "SUMMONMINION":
                    //If the list is null, lets make sure it gets initialized!
                    if (Body.ControlledNpcList == null)
                        Body.InitControlledBrainArray(2);
                    else
                    {
                        //Let's check to see if the list is full - if it is, we can't cast another minion.
                        //If it isn't, let them cast.
                        IControlledBrain[] icb = Body.ControlledNpcList;
                        int numberofpets = 0;
                        for (int i = 0; i < icb.Length; i++)
                        {
                            if (icb[i] != null)
                                numberofpets++;
                        }
                        if (numberofpets >= icb.Length)
                            break;
                    }
                    Body.TargetObject = Body;
                    break;
                #endregion Disease Cure/Poison Cure/Summon

                #region Heals
                case "COMBATHEAL":
                case "HEAL":
                case "HEALOVERTIME":
                case "MERCHEAL":
                case "OMNIHEAL":
                case "PBAEHEAL":
                case "SPREADHEAL":
                    if (spell.Target.ToLower() == "self")
                    {
                        // if we have a self heal and health is less than 75% then heal, otherwise return false to try another spell or do nothing
                        if (Body.HealthPercent < DOL.GS.ServerProperties.Properties.NPC_HEAL_THRESHOLD)
                        {
                            Body.TargetObject = Body;
                        }
                        break;
                    }

                    // Chance to heal self when dropping below 30%, do NOT spam it.
                    if (Body.HealthPercent < (DOL.GS.ServerProperties.Properties.NPC_HEAL_THRESHOLD / 2.0)
                        && Util.Chance(10) && spell.Target.ToLower() != "pet")
                    {
                        Body.TargetObject = Body;
                        break;
                    }

                    if (Body.ControlledBrain != null && Body.ControlledBrain.Body != null
                        && Body.GetDistanceTo(Body.ControlledBrain.Body) <= spell.Range
                        && Body.ControlledBrain.Body.HealthPercent < DOL.GS.ServerProperties.Properties.NPC_HEAL_THRESHOLD
                        && spell.Target.ToLower() != "self")
                    {
                        Body.TargetObject = Body.ControlledBrain.Body;
                        break;
                    }

                    if (spell.Target.ToLower() == "realm")
                    {
                        foreach (GameNPC npc in Body.GetNPCsInRadius((ushort)Math.Max(spell.Radius, spell.Range)))
                        {
                            if (Body.IsFriend(npc) && Util.Chance(60) && npc.HealthPercent < DOL.GS.ServerProperties.Properties.NPC_HEAL_THRESHOLD)
                            {
                                Body.TargetObject = npc;
                                break;
                            }
                        }
                    }
                    break;
                #endregion

                case "SummonWood":
                    //Body.TargetObject = Body;
                    break;
                //case "SummonAnimistFnF":
                //case "SummonAnimistPet":
                case "SUMMONCOMMANDER":
                case "SUMMONDRUIDPET":
                case "SUMMONHUNTERPET":
                case "SUMMONNECROPET":
                case "SummonHunterPet":
                case "SummonMastery":
                case "SummonMercenary":
                case "SummonMonster":
                case "SummonNoveltyPet":
                case "SummonSalamander":
                case "SummonSiegeWeapon":
                case "SummonWarcrystal":
                case "SummonTitan":
                case "SUMMONUNDERHILL":
                case "SUMMONSIMULACRUM":
                case "SUMMONSPIRITFIGHTER":
                    //case "SummonTheurgistPet":
                    if (Body.ControlledBrain != null)
                        break;
                    Body.TargetObject = Body;
                    break;

                default:
                    //log.Warn($"CheckDefensiveSpells() encountered an unknown spell type [{spell.SpellType}]");
                    break;
            }

            if (Body.TargetObject != null && (spell.Duration == 0 || (Body.TargetObject is GameLiving living && !(living is ShadowNPC) && LivingHasEffect(living, spell) == false)))
            {
                casted = Body.CastSpell(spell, m_mobSpellLine);

                if (casted && spell.CastTime > 0)
                {
                    if (Body.IsMoving)
                        Body.StopFollowing();

                    if (Body.TargetObject != Body)
                        Body.TurnTo(Body.TargetObject);
                }
            }

            Body.TargetObject = lastTarget;

            return casted;
        }

        /// <summary>
        /// Checks offensive spells.  Handles dds, debuffs, etc.
        /// </summary>
        protected virtual bool CheckOffensiveSpells(Spell spell)
        {
            if (spell.Target.ToLower() != "enemy" && spell.Target.ToLower() != "area" && spell.Target.ToLower() != "cone")
                return false;

            bool casted = false;

            if (Body.TargetObject is GameLiving living && !(living is ShadowNPC) && (spell.Duration == 0 || !living.HasEffect(spell) || spell.SpellType.ToUpper() == "DIRECTDAMAGEWITHDEBUFF"))
            {
                // Offensive spells require the caster to be facing the target
                if (Body.TargetObject != Body)
                    Body.TurnTo(Body.TargetObject);

                casted = Body.CastSpell(spell, m_mobSpellLine);

                if (casted && spell.CastTime > 0 && Body.IsMoving)
                    Body.StopFollowing();
            }
            return casted;
        }

        /// <summary>
        /// Checks Instant Spells.  Handles Taunts, shouts, stuns, etc.
        /// </summary>
        protected virtual bool CheckInstantSpells(Spell spell)
        {
            GameObject lastTarget = Body.TargetObject;
            Body.TargetObject = null;

            switch (spell.SpellType)
            {
                #region Enemy Spells
                case "DirectDamage":
                case "Lifedrain":
                case "DirectDamageWithDebuff":
                case "AcuityDebuff":
                case "DexterityDebuff":
                case "QuicknessDebuff":
                case "ConstitutionDebuff":
                case "MatterResistDebuff":
                case "HeatResistDebuff":
                case "ColdResistDebuff":
                case "EnergyResistDebuff":
                case "DexterityQuicknessDebuff":
                case "StrengthConstitutionDebuff":
                case "CombatSpeedDebuff":
                case "ArmorAbsorptionDebuff":
                case "ArmorFactorDebuff":
                case "DamageOverTime":
                case "MeleeDamageDebuff":
                case "AllStatsPercentDebuff":
                case "CrushSlashThrustDebuff":
                case "WeaponSkillConstitutionDebuff":
                case "EffectivenessDebuff":
                case "FumbleChanceDebuff":
                case "ToHitDebuff":
                case "AllStatsDebuff":
                case "FatigueConsumptionDebuff":
                case "CastingSpeedDebuff":
                case "Disease":
                case "Stun":
                case "Mez":
                case "Taunt":
                case "Slow":
                case "Petrify":
                case "Demi":
                case "Quarter":
                case "Morph":
                case "Earthquake":
                case "Damnation":
                case "DeathClaw":
                case "CallAreaEffect":
                case "BumpSpell":
                case "OmniHarm":
                    if (!LivingHasEffect(lastTarget as GameLiving, spell))
                    {
                        Body.TargetObject = lastTarget;
                    }
                    break;
                #endregion

                #region Combat Spells
                case "CombatHeal":
                case "DamageAdd":
                case "ArmorFactorBuff":
                case "DexterityQuicknessBuff":
                case "EnduranceRegenBuff":
                case "CombatSpeedBuff":
                case "AblativeArmor":
                case "OffensiveProc":
                    if (!LivingHasEffect(Body, spell))
                    {
                        Body.TargetObject = Body;
                    }
                    break;
                    #endregion
            }

            if (Body.TargetObject != null && (spell.Duration == 0 || (Body.TargetObject is GameLiving living && LivingHasEffect(living, spell) == false)))
            {
                Body.CastSpell(spell, m_mobSpellLine);
                Body.TargetObject = lastTarget;
                return true;
            }

            Body.TargetObject = lastTarget;
            return false;
        }

        protected static SpellLine m_mobSpellLine = SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells);

        /// <summary>
        /// Checks if the living target has a spell effect
        /// </summary>
        /// <param name="target">The target living object</param>
        /// <param name="spell">The spell to check</param>
        /// <returns>True if the living has the effect</returns>
        public static bool LivingHasEffect(GameLiving target, Spell spell)
        {
            if (target == null)
                return true;

            if (target is GamePlayer && (target as GamePlayer)!.CharacterClass.ID == (int)eCharacterClass.Vampiir)
            {
                switch (spell.SpellType)
                {
                    case "StrengthConstitutionBuff":
                    case "DexterityQuicknessBuff":
                    case "StrengthBuff":
                    case "DexterityBuff":
                    case "ConstitutionBuff":
                    case "AcuityBuff":

                        return true;
                }
            }

            lock (target.EffectList)
            {
                //Check through each effect in the target's effect list
                foreach (IGameEffect effect in target.EffectList)
                {
                    //If the effect we are checking is not a gamespelleffect keep going
                    if (effect is GameSpellEffect == false)
                        continue;

                    GameSpellEffect speffect = effect as GameSpellEffect;

                    //if the effect effectgroup is the same as the checking spells effectgroup then these are considered the same
                    if (speffect!.Spell.SpellType == spell.SpellType || (speffect.Spell.EffectGroup != 0 && speffect.Spell.EffectGroup == spell.EffectGroup))
                        return true;
                }
            }

            //the answer is no, the effect has not been found
            return false;
        }

        protected bool HasNegativeEffect(GameLiving target, IEnumerable<string> spellTypes)
        {
            if (target == null) return false;

            foreach (IGameEffect effect in target.EffectList)
            {
                if (effect is not GameSpellEffect speffect) continue;
                if (spellTypes.Contains(speffect.Spell.SpellType))
                    return true;
            }

            return false;
        }


        #endregion

        #region Random Walk
        public virtual bool CanRandomWalk
        {
            get
            {
                /* Roaming:
                   <0 means random range
                   0 means no roaming
                   >0 means range of roaming
                   defaut roaming range is defined in CanRandomWalk method
                 */
                if (!DOL.GS.ServerProperties.Properties.ALLOW_ROAM)
                    return false;
                if (Body.RoamingRange == 0)
                    return false;
                if (!string.IsNullOrWhiteSpace(Body.PathID))
                    return false;
                return true;
            }
        }
        
        public virtual Coordinate GetRandomWalkTarget()
        {
            if (PathCalculator.IsSupported(Body))
            {
                int radius = Body.RoamingRange > 0 ? Body.RoamingRange : 500;
                var target = PathingMgr.Instance.GetRandomPointAsync(Body.CurrentZone, Body.Coordinate, radius);
                if (target.HasValue)
                    return Coordinate.Create(x: (int)target.Value.X, y: (int)target.Value.Y, z: (int)target.Value.Z);
            }

            int maxRoamingRadius = Body.CurrentRegion.IsDungeon ? 5 : 500;

            if (Body.RoamingRange > 0)
                maxRoamingRadius = Body.RoamingRange;

            double targetX = Body.SpawnPosition.X + Util.Random(-maxRoamingRadius, maxRoamingRadius);
            double targetY = Body.SpawnPosition.Y + Util.Random(-maxRoamingRadius, maxRoamingRadius);

            return Coordinate.Create(x: (int)targetX, y: (int)targetY, z: Body.SpawnPosition.Z);
        }

        #endregion
        #region DetectDoor
        public virtual void DetectDoor()
        {
            ushort range = (ushort)((ThinkInterval / 800) * Body.CurrentWayPoint.MaxSpeed);

            foreach (IDoor door in Body.CurrentRegion.GetDoorsInRadius(Body.Coordinate, range, false))
            {
                if (door is GameKeepDoor)
                {
                    if (Body.Realm != door.Realm) return;
                    door.Open();
                    //Body.Say("GameKeep Door is near by");
                    //somebody can insert here another action for GameKeep Doors
                    return;
                }
                else
                {
                    door.Open();
                    return;
                }
            }
            return;
        }
        #endregion
    }
}
