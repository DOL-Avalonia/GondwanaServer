using System.Collections;
using System.Collections.Generic;
using DOL.GS;
using log4net;
using DOL.GS.Spells;

namespace DOL.AI.Brain
{
    public class TurretFNFBrain : TurretBrain
    {
        private static ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        protected override bool CanAddToAggroListFromMultipleLosChecks => true;

        public TurretFNFBrain(GameLiving owner) : base(owner)
        {
        }

        /// <summary>
        /// Get a random target from aggro table
        /// </summary>
        /// <returns></returns>
        protected override GameLiving CalculateNextAttackTarget()
        {
            List<GameLiving> newTargets = new List<GameLiving>();
            List<GameLiving> oldTargets = new List<GameLiving>();
            base.CalculateNextAttackTarget();

            foreach (GameLiving living in m_aggroTable.GetUnordered())
            {
                if (!living.IsAlive || living.CurrentRegion != Body.CurrentRegion || living.ObjectState != GameObject.eObjectState.Active)
                    continue;

                if (living.IsMezzed || living.IsStealthed)
                    continue;

                if (!Body.IsWithinRadius2D(living, MAX_AGGRO_DISTANCE))
                    continue;

                if (!Body.IsWithinRadius2D(living, ((TurretPet)Body).TurretSpell.Range))
                    continue;

                if (((TurretPet)Body).TurretSpell.SpellType != "SpeedDecrease" && SpellHandler.FindEffectOnTarget(living, "SpeedDecrease") != null)
                    continue;

                if (((TurretPet)Body).TurretSpell.SpellType == "SpeedDecrease" && living.HasAbility(Abilities.RootImmunity))
                    continue;

                GameNPC npc = living as GameNPC;
                if ((living.GetType().Name == "GuardNPC" || living.GetType().Name == "GuardOutlaw") && !((StandardMobBrain)npc!.Brain).IsInAggroList(Owner))
                    continue;

                newTargets.Add(living);
            }

            foreach (GamePlayer living in Body.GetPlayersInRadius((ushort)((TurretPet)Body).TurretSpell.Range, Body.CurrentRegion.IsDungeon ? false : true))
            {
                if (!GameServer.ServerRules.IsAllowedToAttack(Body, living, true))
                    continue;

                if (living.IsInvulnerableToAttack)
                    continue;

                if (!living.IsAlive || living.CurrentRegion != Body.CurrentRegion || living.ObjectState != GameObject.eObjectState.Active)
                    continue;

                if (living.IsMezzed || living.IsStealthed)
                    continue;

                if (((TurretPet)Body).TurretSpell.SpellType != "SpeedDecrease" && SpellHandler.FindEffectOnTarget(living, "SpeedDecrease") != null)
                    continue;


                if (LivingHasEffect(living, ((TurretPet)Body).TurretSpell))
                {
                    oldTargets.Add(living);
                }
                else
                {
                    newTargets.Add(living as GameLiving);
                }
            }

            foreach (GameNPC living in Body.GetNPCsInRadius((ushort)((TurretPet)Body).TurretSpell.Range, Body.CurrentRegion.IsDungeon ? false : true))
            {
                if (!GameServer.ServerRules.IsAllowedToAttack(Body, living, true))
                    continue;

                if (!living.IsAlive || living.CurrentRegion != Body.CurrentRegion || living.ObjectState != GameObject.eObjectState.Active)
                    continue;

                if (living.IsMezzed || living.IsStealthed)
                    continue;

                if (((TurretPet)Body).TurretSpell.SpellType == "SpeedDecrease" && SpellHandler.FindEffectOnTarget(living, "SpeedDecrease") != null)
                    continue;

                if (((TurretPet)Body).TurretSpell.SpellType == "SpeedDecrease" && (living.HasAbility(Abilities.RootImmunity) || living.HasAbility(Abilities.DamageImmunity)))
                    continue;

                if ((living.GetType().Name == "GuardNPC" || living.GetType().Name == "GuardOutlaw") && !((StandardMobBrain)living.Brain).IsInAggroList(Owner))
                    continue;

                if (LivingHasEffect(living, ((TurretPet)Body).TurretSpell))
                {
                    oldTargets.Add(living);
                }
                else
                {
                    newTargets.Add(living as GameLiving);
                }
            }

            // always favor previous targets and new targets that have not been attacked first, then re-attack old targets

            if (newTargets.Count > 0)
            {
                return newTargets[Util.Random(newTargets.Count - 1)];
            }
            else if (oldTargets.Count > 0)
            {
                return oldTargets[Util.Random(oldTargets.Count - 1)];
            }

            m_aggroTable.Clear();
            return null;
        }

        protected override void OnAttackedByEnemy(AttackData ad)
        {
            AddToAggroList(ad.Attacker, (ad.Attacker.Level + 1) << 1);
        }

        /// <summary>
        /// Updates the pet window
        /// </summary>
        public override void UpdatePetWindow()
        {
        }
    }
}