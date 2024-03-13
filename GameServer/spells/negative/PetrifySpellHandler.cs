using DOL.AI.Brain;
using DOL.Events;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOL.GS.Spells
{
    [SpellHandler("Petrify")]
    public class PetrifySpellHandler : SpellHandler
    {
        public PetrifySpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
        }

        public override void ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            GameSpellEffect damnationEffect = SpellHandler.FindEffectOnTarget(target, "Damnation");
            if (damnationEffect != null)
            {
                if (Caster is GamePlayer player)
                    MessageToCaster(LanguageMgr.GetTranslation(player.Client, "Damnation.Target.Resist", (Caster as GamePlayer).GetPersonalizedName(target)), eChatType.CT_SpellResisted);
                return;
            }
            base.ApplyEffectOnTarget(target, effectiveness);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            GameLiving living = effect.Owner;
            foreach (GameSpellEffect activeEffect in living.EffectList.ToList())
            {
                if (activeEffect.SpellHandler is SpellHandler spell && (spell.Spell.Pulse > 0 || spell.HasPositiveOrSpeedEffect()))
                    activeEffect.Cancel(false);
            }

            living.IsStunned = true;
            living.StopAttack();
            living.StopCurrentSpellcast();
            living.DisableTurning(true);

            if (living is GamePlayer player)
            {
                living.TempProperties.setProperty("OriginalModel", living.Model);
                switch (living.Race)
                {
                    case 1:
                        if (living.Gender == eGender.Female)
                            living.Model = 1316;
                        else
                            living.Model = 1315;
                        break;
                    case 2:
                        if (living.Gender == eGender.Female)
                            living.Model = 1322;
                        else
                            living.Model = 1321;
                        break;
                    case 3:
                        if (living.Gender == eGender.Female)
                            living.Model = 1318;
                        else
                            living.Model = 1317;
                        break;
                    case 4:
                        if (living.Gender == eGender.Female)
                            living.Model = 1320;
                        else
                            living.Model = 1319;
                        break;
                    case 5:
                        if (living.Gender == eGender.Female)
                            living.Model = 1328;
                        else
                            living.Model = 1327;
                        break;
                    case 6:
                        if (living.Gender == eGender.Female)
                            living.Model = 1326;
                        else
                            living.Model = 1325;
                        break;
                    case 7:
                        if (living.Gender == eGender.Female)
                            living.Model = 1332;
                        else
                            living.Model = 1331;
                        break;
                    case 8:
                        if (living.Gender == eGender.Female)
                            living.Model = 1330;
                        else
                            living.Model = 1329;
                        break;
                    case 9:
                        if (living.Gender == eGender.Female)
                            living.Model = 1340;
                        else
                            living.Model = 1339;
                        break;
                    case 10:
                        if (living.Gender == eGender.Female)
                            living.Model = 1338;
                        else
                            living.Model = 1337;
                        break;
                    case 11:
                        if (living.Gender == eGender.Female)
                            living.Model = 1344;
                        else
                            living.Model = 1343;
                        break;
                    case 12:
                        if (living.Gender == eGender.Female)
                            living.Model = 1342;
                        else
                            living.Model = 1341;
                        break;
                    case 13:
                        if (living.Gender == eGender.Female)
                            living.Model = 1314;
                        else
                            living.Model = 1313;
                        break;
                    case 14:
                        if (living.Gender == eGender.Female)
                            living.Model = 1334;
                        else
                            living.Model = 1333;
                        break;
                    case 15:
                        if (living.Gender == eGender.Female)
                            living.Model = 1346;
                        else
                            living.Model = 1345;
                        break;
                    case 16:
                        if (living.Gender == eGender.Female)
                            living.Model = 1324;
                        else
                            living.Model = 1323;
                        break;
                    case 17:
                        if (living.Gender == eGender.Female)
                            living.Model = 1336;
                        else
                            living.Model = 1335;
                        break;
                    case 18:
                        if (living.Gender == eGender.Female)
                            living.Model = 1348;
                        else
                            living.Model = 1347;
                        break;
                    case 19:
                        living.Model = 1574;
                        break;
                    case 20:
                        living.Model = 1577;
                        break;
                    case 21:
                        living.Model = 1580;
                        break;
                    default:
                        break;
                }
                player.Out.SendUpdatePlayer();
                player.Out.SendUpdateMaxSpeed();
                if (player.Group != null)
                    player.Group.UpdateMember(player, false, false);
            }
            GameEventMgr.AddHandler(living, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(EventHandler));
            if (Caster is GamePlayer casterPlayer)
            {
                MessageToLiving(casterPlayer, LanguageMgr.GetTranslation(casterPlayer.Client, "Petrify.Self.Message"), eChatType.CT_Spell);
            }
            SendEffectAnimation(effect.Owner, 0, false, 1);
        }

        public void EventHandler(DOLEvent e, object sender, EventArgs arguments)
        {
            if (!(arguments is AttackedByEnemyEventArgs args))
            {
                return;
            }
            AttackData ad = args.AttackData;
            double absorbPercent = 0;
            if (args.AttackData.AttackResult == GameLiving.eAttackResult.HitUnstyled
                || args.AttackData.AttackResult == GameLiving.eAttackResult.HitStyle)
                absorbPercent = 50;
            if (args.AttackData.AttackType is AttackData.eAttackType.Spell or AttackData.eAttackType.DoT)
                absorbPercent = 75;

            int damageAbsorbed = (int)(0.01 * absorbPercent * (ad.Damage + ad.CriticalDamage));

            ad.Damage -= damageAbsorbed;

            if (ad.Target is GamePlayer player)
                MessageToLiving(player, LanguageMgr.GetTranslation(player.Client, "Petrify.Self.Absorb", damageAbsorbed), eChatType.CT_Spell);
            if (ad.Attacker is GamePlayer attacker)
                MessageToLiving(attacker, LanguageMgr.GetTranslation(attacker.Client, "Petrify.Target.Absorbs", damageAbsorbed), eChatType.CT_Spell);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            GameLiving living = effect.Owner;

            living.IsStunned = false;
            living.DisableTurning(false);

            if (living is GamePlayer player)
            {
                living.Model = living.TempProperties.getProperty<ushort>("OriginalModel");
                MessageToLiving(player, LanguageMgr.GetTranslation(player.Client, "Petrify.Self.Unpetrify"), eChatType.CT_Spell);
                player.Out.SendUpdatePlayer();
                player.Client.Out.SendUpdateMaxSpeed();
                if (player.Group != null)
                    player.Group.UpdateMember(player, false, false);
            }
            else if (living is GameNPC ncp && ncp.Brain is IOldAggressiveBrain aggroBrain)
                aggroBrain.AddToAggroList(Caster, 1);

            GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(EventHandler));
            return base.OnEffectExpires(effect, noMessages);
        }

        public override string ShortDescription
            => $"Petrifies the target and turns it into a statue. The target is completely paralysed and absorbs 50% of physical damages as well as 75% of magical damages it might suffer if attacked.";
    }
}