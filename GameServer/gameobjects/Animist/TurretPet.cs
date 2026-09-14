using DOL.AI.Brain;

namespace DOL.GS
{
    public class TurretPet : GamePet
    {
        public TurretPet(INpcTemplate template)
            : base(template)
        {
        }

        private Spell turretSpell;

        /// <summary>
        /// Get first spell only
        /// </summary>
        public Spell TurretSpell
        {
            get { return turretSpell; }
            set { turretSpell = value; }
        }

        /// <summary>
        /// Not all summoned turrets 'll throw ambient texts
        /// let's say 20%
        /// </summary>
        public override void BuildAmbientTexts()
        {
            base.BuildAmbientTexts();
            if (ambientTexts.Count > 0)
                foreach (var at in ambientTexts)
                    at.Chance /= 5;
        }

        public override void StartAttack(GameObject attackTarget)
        {
            if (attackTarget == null)
                return;

            if (attackTarget is GameLiving && GameServer.ServerRules.IsAllowedToAttack(this, (GameLiving)attackTarget, true) == false)
                return;

            if (Brain is IControlledBrain controlledBrain)
            {
                if (controlledBrain.AggressionState == eAggressionState.Passive)
                    return;
                if (GetPlayerOwner() is GamePlayer playerowner)
                    playerowner.Stealth(false);
            }

            TargetObject = attackTarget;
            if (TargetObject.Realm == 0 || Realm == 0)
                m_lastAttackTickPvE = CurrentRegion.Time;
            else
                m_lastAttackTickPvP = CurrentRegion.Time;

            if (AttackerTracker.Count == 0)
            {
                if (SpellTimer == null)
                    SpellTimer = new SpellAction(this);
                if (!SpellTimer.IsAlive)
                    SpellTimer.Start(1);
            }

            if (Brain is TurretMainPetTankBrain)
            {
                base.StartAttack(TargetObject);
            }
        }

        /// <summary>
        /// [Ganrod] Nidel: Don't interrupt turret cast.
        /// </summary>
        /// <param name="duration"></param>
        /// <param name="attackType"></param>
        /// <param name="attacker"></param>
        public override void StartInterruptTimer(AttackData attack, int duration)
        {
            return;
        }

        public override bool IsObjectInFront(GameObject target, double viewangle, bool rangeCheck = true)
        {
            return true;
        }
    }
}
