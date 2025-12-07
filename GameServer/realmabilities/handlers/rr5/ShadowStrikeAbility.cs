using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;
using System.Collections.Generic;

namespace DOL.GS.RealmAbilities
{
    /// <summary>
    /// Shadow Strike Ability inherit of RR5RealmAbility (RealmLevel >= 40 and free)
    /// </summary>
    public class ShadowStrikeAbility : RR5RealmAbility
    {
        private DBSpell _dbspell;
        private Spell _spell;
        private SpellLine _spellline;
        private ShadowStrikeSpellHandler dd;
        private GamePlayer _player;

        public ShadowStrikeAbility(DBAbility dba, int level) : base(dba, level)
        {
            CreateSpell();
        }

        private void CreateSpell()
        {
            _dbspell = new DBSpell
            {
                Name = "Shadow Strike",
                Icon = 7073,
                ClientEffect = 12011,
                IsFocus = true,
                Target = "enemy",
                Type = "ShadowStrike",
                CastTime = 10,
                MoveCast = false,
                Range = 1000
            };

            _spell = new Spell(_dbspell, 0); // make spell level 0 so it bypasses the spec level adjustment code
            _spellline = new SpellLine("RAs", "RealmAbilities", "RealmAbilities", true);
        }

        protected bool CastSpell(GameLiving target)
        {
            if (target.IsAlive && _spell != null)
            {
                dd = ScriptMgr.CreateSpellHandler(_player, _spell, _spellline) as ShadowStrikeSpellHandler;
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
                _player.Out.SendMessage(LanguageMgr.GetTranslation(_player.Client.Account.Language, "ShadowStrikeAbility.Target"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
        }

        public override int GetReUseDelay(int level)
        {
            return 600;
        }

        public override void AddEffectsInfo(IList<string> list, GameClient client)
        {
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "ShadowStrikeAbility.AddEffectsInfo.Info1"));
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "ShadowStrikeAbility.AddEffectsInfo.Info2"));
            list.Add("");
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "ShadowStrikeAbility.AddEffectsInfo.Info3"));
        }
    }
}