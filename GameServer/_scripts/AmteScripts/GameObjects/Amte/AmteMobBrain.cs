using System;
using System.Collections.Generic;
using System.Linq;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.RealmAbilities;
using DOL.GS.Scripts;

namespace DOL.AI.Brain
{
    public class AmteMobBrain : StandardMobBrain
    {
        public int AggroLink { get; set; }
        private const int MinProtectionScan = 1000;
        public override int ThinkInterval => Math.Max(1000, 3000 - AggroLevel * 20);

        public AmteMobBrain()
        {
            AggroLink = -1;
        }

        public AmteMobBrain(ABrain brain)
        {
            if (!(brain is IOldAggressiveBrain))
                return;
            var old = (IOldAggressiveBrain)brain;
            AggroLevel = old.AggroLevel;
            AggroRange = old.AggroRange;
        }

        public override void Think()
        {
            // 1. Check for distressed Mages BEFORE standard logic
            if (Body.IsAlive && !Body.IsReturningHome && !Body.IsIncapacitated && !Body.IsPeaceful)
            {
                if (Body is AmteMob || Body is TerritoryGuard)
                {
                    if (CheckProtectiveInstincts())
                    {
                        return;
                    }
                }
            }

            base.Think();
        }

        /// <summary>
        /// Scans for friendly MageMobs who are being attacked.
        /// </summary>
        protected virtual bool CheckProtectiveInstincts()
        {
            if (Body.TargetObject is GamePlayer) return false;

            int scanRange = AggroRange > MinProtectionScan ? AggroRange : MinProtectionScan;
            int engageRange = AggroRange > 250 ? AggroRange : 250;

            // Find nearby MageMobs in combat within scan range
            foreach (MageMob mage in Body.GetNPCsInRadius((ushort)scanRange).OfType<MageMob>())
            {
                if (!mage.InCombat || mage.TargetObject == null) continue;

                if (Body.IsWithinRadius(mage.TargetObject, engageRange + 200) || mage.GetDistanceTo(mage.TargetObject) < 400)
                {
                    Body.StopFollowing();
                    Body.TargetObject = mage.TargetObject;

                    AddToAggroList(mage.TargetObject as GameLiving, 200);
                    Body.StartAttack(Body.TargetObject);

                    if (Util.Chance(20))
                    {
                        Body.Say("Protect the Battlemage!");
                    }
                    return true;
                }
            }
            return false;
        }

        public override int CalculateAggroLevelToTarget(GameLiving target)
        {
            // Get owner if target is pet
            GameLiving realTarget = target;
            float aggroMultiplier = 1.0f;
            if (target is GameNPC targetNPC)
            {
                var owner = target.GetLivingOwner();
                if (owner != null)
                    realTarget = owner;
                // FollowingFriendMob will have higher aggro
                if (realTarget is FollowingFriendMob { PlayerFollow: not null } followMob)
                {
                    aggroMultiplier = followMob.AggroMultiplier;
                    realTarget = followMob.PlayerFollow;
                }
            }

            if (GameServer.ServerRules.IsSameRealm(Body, realTarget, true))
                return 0;

            if (realTarget.IsObjectGreyCon(Body))
                return 0; // only attack if green+ to target

            int aggro = AggroLevel;
            if (target is GamePlayer player)
            {
                if (Body.Faction != null)
                    aggro = Body.Faction.GetAggroToFaction(player);
                if (aggro > 1 && player.Client.IsDoubleAccount)
                    aggro += 20;
            }

            return Math.Min(100, (int)(aggro * aggroMultiplier));
        }

        public override void CheckAbilities()
        {
            // load up abilities
            if (Body.Abilities != null && Body.Abilities.Count > 0)
            {
                foreach (var ab in Body.Abilities.Values)
                {
                    switch (ab.KeyName)
                    {
                        case Abilities.ChargeAbility:
                            {
                                if (Body.TargetObject is GameLiving target
                                    && !Body.IsWithinRadius(Body.TargetObject, 1000)
                                    && GameServer.ServerRules.IsAllowedToAttack(Body, target, true))
                                {
                                    ChargeAbility charge = Body.GetAbility<ChargeAbility>();
                                    if (charge != null && Body.GetSkillDisabledDuration(charge) <= 0)
                                    {
                                        charge.Execute(Body);
                                    }
                                }

                                break;
                            }
                        case Abilities.Quickcast:
                            {
                                INPCAbilityActionHandler handler = (INPCAbilityActionHandler)SkillBase.GetAbilityActionHandler(ab.KeyName);
                                if (handler != null)
                                {
                                    handler.Execute(ab, Body);
                                }
                                break;
                            }
                    }
                }
            }
        }

        protected override void AttackMostWanted()
        {
            base.AttackMostWanted();
            if (!Body.IsCasting)
                CheckAbilities();
        }
    }
}