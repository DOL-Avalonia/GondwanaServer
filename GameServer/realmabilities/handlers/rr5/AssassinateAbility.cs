using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;
using System.Collections.Generic;

namespace DOL.GS.RealmAbilities
{
    /// <summary>
    /// Assassinate inherit of RR5RealmAbility (RealmLevel >= 40 and free)
    /// </summary>
    public class AssassinateAbility : RR5RealmAbility
    {
        private DBSpell _dbspell;
        private Spell _spell;
        private SpellLine _spellline;
        private AssassinateHandler dd;
        private GamePlayer _player;

        public AssassinateAbility(DBAbility dba, int level) : base(dba, level)
        {
            CreateSpell();
        }

        private void CreateSpell()
        {
            _dbspell = new DBSpell
            {
                Name = "Assassinate",
                Target = "enemy",
                Type = "Assassinate",
                CastTime = 15,
                Range = 750
            };

            _spell = new Spell(_dbspell, 0); // make spell level 0 so it bypasses the spec level adjustment code
            _spellline = new SpellLine("RAs", "RealmAbilities", "RealmAbilities", true);
        }

        protected bool CastSpell(GameLiving target)
        {
            if (target.IsAlive && _spell != null)
            {
                dd = ScriptMgr.CreateSpellHandler(_player, _spell, _spellline) as AssassinateHandler;
                dd!.IgnoreDamageCap = true;
                return dd.CastSpell(target);
            }
            return false;
        }

        public override void Execute(GameLiving living)
        {
            if (CheckPreconditions(living, DEAD | SITTING | MEZZED | STUNNED))
            {
                return;
            }
            _player = living as GamePlayer;

            CreateSpell();
            if (_player!.TargetObject is GameLiving)
            {
                CastSpell(_player.TargetObject as GameLiving);
            }
            else
            {
                _player.Out.SendMessage(LanguageMgr.GetTranslation(_player.Client.Account.Language, "AssassinateAbility.Target"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
        }

        public override int GetReUseDelay(int level)
        {
            return 600;
        }

        public override void AddEffectsInfo(IList<string> list, GameClient client)
        {
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "AssassinateAbility.AddEffectsInfo.Info1"));
            list.Add("");
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "AssassinateAbility.AddEffectsInfo.Info2"));
            list.Add("");
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "AssassinateAbility.AddEffectsInfo.Info3"));
        }
    }
}