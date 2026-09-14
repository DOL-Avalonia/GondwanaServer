using DOL.GS.PacketHandler;
using DOL.Language;

/*1,Ballista,1,ammo,0.46,1
2,Catapult,2,ammo,0.39,1
3,Trebuchet,2,ammo,1.03,1
4,Scorpion,1,ammo,0.22,1
5,Oil,3,ammo,0,1*/
/*ID,Textual Name,Load start,Load End,Fire Start,Fire End,Release Point
1,Ballista,30,60,0,30,12
2,Ram,30,60,0,30,12
3,Mangonel,30,200,0,30,12
4,trebuchet,97,180,0,96,
5,Ballista Low,11,30,0,10,
6,Ballista High,20,90,0,19,
7,Catapult W,40,111,0,40,
8,Catapult G,40,111,0,40,
9,Ram High,0,12,13,80,
10,Ram Mid,0,12,13,80,
11,Ram Low,0,12,13,80,*/
namespace DOL.GS
{
    /// <summary>
    /// Field ballista: asks the operator's client for line of sight before firing, then deals ranged damage.
    /// </summary>
    public class GameSiegeBallista : GameSiegeWeapon
    {
        /// <summary>
        /// Damage against structures;
        /// living targets and siege weapons take three times this value.
        /// </summary>
        private const int BASE_DAMAGE = 75;

        private readonly FireLosCheckListener m_fireLosCheckListener;

        // Time (GameLoop time) until which we consider a LoS answer to be pending. Every request is answered or times out,
        // this only prevents a double request while the operator spams the fire button.
        private long m_fireLosPendingUntil;

        public GameSiegeBallista() : base()
        {
            MeleeDamageType = eDamageType.Thrust;
            Name = "field ballista";
            AmmoType = 0x18;
            Model = 0x0A55;
            Effect = 0x089A;
            ActionDelay = new int[] { 0, 5000, 10000, 0, 1100 };
            m_fireLosCheckListener = new FireLosCheckListener(this);
        }

        public override void Fire()
        {
            // Not ready, nobody at the controls or no living target:
            // let the base weapon print its messages ("not ready...") or handle a ground target.
            if (CurrentState != eState.Ready)
            {
                base.Fire();
                return;
            }

            GamePlayer operatorPlayer = Owner as GamePlayer;
            GameLiving target = TargetObject as GameLiving;
            if (operatorPlayer == null || target == null)
            {
                base.Fire();
                return;
            }

            if (m_fireLosPendingUntil > GameLoop.GameLoopTime)
                return; // an answer is already on its way

            m_fireLosPendingUntil = GameLoop.GameLoopTime + LosCheckHandler.Timeout * 2;

            // Cached / geometry-resolved answers call OnFireLosResponse synchronously, before this returns.
            if (!operatorPlayer.Out.SendLosCheckRequest(this, target, m_fireLosCheckListener))
            {
                // Nobody can answer (should not happen for a playing client): never block the player's action.
                m_fireLosPendingUntil = 0;
                base.Fire();
            }
        }

        private void OnFireLosResponse(LosCheckResponse response, ushort targetId)
        {
            m_fireLosPendingUntil = 0;

            if (ObjectState != eObjectState.Active)
                return;

            GamePlayer operatorPlayer = Owner as GamePlayer;
            if (operatorPlayer == null)
                return;

            // The operator re-aimed at something else while we were waiting: ignore this answer.
            if (TargetObject == null || (ushort)TargetObject.ObjectID != targetId)
                return;

            if (response == LosCheckResponse.False)
            {
                operatorPlayer.Out.SendMessage(LanguageMgr.GetTranslation(operatorPlayer.Client.Account.Language, "GameSiegeWeapon.TargetOutOfSight"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            // quiet == false: the server rules print the reason to the player themselves when the attack is refused.
            if (TargetObject is GameLiving living && !GameServer.ServerRules.IsAllowedToAttack(operatorPlayer, living, false))
                return;

            base.Fire();
        }

        public override void DoDamage()
        {
            base.DoDamage();

            if (TargetObject is not GameLiving target || !target.IsAlive || target.ObjectState != eObjectState.Active)
                return;

            GameLiving attacker = Owner ?? this;
            if (!GameServer.ServerRules.IsAllowedToAttack(attacker, target, true))
                return;

            int damageAmount = CalcDamageToTarget(target) + Util.Random(50);

            AttackData ad = new AttackData();
            ad.Attacker = attacker;
            ad.Target = target;
            ad.AttackType = AttackData.eAttackType.Ranged;
            ad.AttackResult = eAttackResult.HitUnstyled;
            ad.Damage = damageAmount;
            ad.DamageType = MeleeDamageType;

            target.TakeDamage(attacker, MeleeDamageType, damageAmount, 0);
            target.OnAttackedByEnemy(ad);

            if (Owner is GamePlayer operatorPlayer)
            {
                operatorPlayer.Out.SendMessage($"The {Name} hits {operatorPlayer.GetPersonalizedName(target)} for {damageAmount} damage!", eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
            }

            foreach (GamePlayer player in target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                player.Out.SendCombatAnimation(this, target, 0x0000, 0x0000, 0x00, 0x00, 0x14, target.HealthPercent);
        }

        /// <summary>
        /// Base damage is meant for doors / keep components; players, NPCs and other siege weapons take triple.
        /// </summary>
        public virtual int CalcDamageToTarget(GameLiving target)
        {
            if (target is GameSiegeWeapon || target is GamePlayer || target is GameNPC)
                return BASE_DAMAGE * 3;
            return BASE_DAMAGE;
        }

        public override bool ReceiveItem(GameLiving source, Database.InventoryItem item)
        {
            //todo check if bullet
            return base.ReceiveItem(source, item);
        }

        private sealed class FireLosCheckListener : ILosCheckListener
        {
            private readonly GameSiegeBallista _ballista;

            public FireLosCheckListener(GameSiegeBallista ballista)
            {
                _ballista = ballista;
            }

            public void HandleLosCheckResponse(GamePlayer losChecker, LosCheckResponse response, ushort sourceId, ushort targetId)
            {
                _ballista.OnFireLosResponse(response, targetId);
            }
        }
    }
}
