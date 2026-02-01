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

        /// <inheritdoc />
        protected override bool ThinkScan()
        {
            if (Body.TargetObject is not GamePlayer)
            {
                int scanRange = Math.Max(AggroRange, MinProtectionScan);
                int engageRange = Math.Max(AggroRange, 250);

                // Find nearby MageMobs in combat within scan range
                foreach (MageMob mage in Body.GetNPCsInRadius((ushort)scanRange).OfType<MageMob>())
                {
                    if (!mage.InCombat || mage.TargetObject is not GameLiving mageTarget) continue;

                    if (Body.IsWithinRadius(mageTarget, engageRange + 200) || mage.IsWithinRadius(mage.TargetObject, 400))
                    {
                        if (AggroTable.ContainsKey(mageTarget)) 
                            continue; // already aggroed on this target

                        AddToAggroList(mageTarget, 200);

                        if (Util.Chance(20))
                        {
                            Body.Say("Protect the Battlemage!");
                        }
                        return true;
                    }
                }
            }
            return base.ThinkScan();
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