using DOL.Database;
using DOL.GS;
using DOL.GS.RealmAbilities;
using DOL.GS.Spells;
using DOL.Language;
using System.Collections.Generic;
using DOL.GS.PacketHandler;

namespace DOL.GS.RealmAbilities
{
    /// <summary>
    /// RR5: Call of Shadows (10 min reuse, 30s duration)
    /// </summary>
    public class CallOfShadowsAbility : RR5RealmAbility
    {
        private static DBSpell _dbspell;
        private Spell _spell;
        private SpellLine _spellline;
        private GamePlayer _player;

        public CallOfShadowsAbility(DBAbility dba, int level) : base(dba, level)
        {
        }

        private void BuildSpell()
        {
            _spellline = new SpellLine("RAs", "RealmAbilities", "RealmAbilities", true);

            if (_dbspell == null)
            {
                _dbspell = new DBSpell
                {
                    SpellID = 8889,
                    TooltipId = 8889,
                    Name = "Call of Shadows",
                    Icon = 7051,
                    ClientEffect = 15184,
                    Target = "self",
                    Type = "CallOfShadows",
                    Duration = 30,
                    CastTime = 0,
                    MoveCast = false,
                    Uninterruptible = false,
                    Range = 0,
                };
                SkillBase.AddScriptedSpell(_spellline.KeyName, new Spell(_dbspell, 0));
            }

            _spell = new Spell(_dbspell, 0);
        }

        public override void Execute(GameLiving living)
        {
            if (CheckPreconditions(living, DEAD | SITTING | MEZZED | STUNNED))
                return;

            _player = living as GamePlayer;
            if (_player!.IsRiding)
            {
                _player.Out.SendMessage(LanguageMgr.GetTranslation(_player.Client.Account.Language, "GameObjects.GamePlayer.CastSpell.CannotCastRiding"), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                return;
            }

            BuildSpell();

            var handler = ScriptMgr.CreateSpellHandler(_player, _spell, _spellline) as CallOfShadowsSpellHandler;
            handler?.StartSpell(_player);
        }

        public override int GetReUseDelay(int level) => 600;

        public static void AddDelveInfos(IList<string> list, GameClient client)
        {
            var language = client.Account.Language;
            list.Add(LanguageMgr.GetTranslation(language, "CallOfShadowsAbility.AddEffectsInfo.Info1"));
            list.Add("");
            list.Add(LanguageMgr.GetTranslation(language, "CallOfShadowsAbility.AddEffectsInfo.Info2"));
            list.Add(LanguageMgr.GetTranslation(language, "CallOfShadowsAbility.AddEffectsInfo.Info3"));
            list.Add(LanguageMgr.GetTranslation(language, "CallOfShadowsAbility.AddEffectsInfo.Info4"));
        }

        public override void AddEffectsInfo(IList<string> list, GameClient client)
        {
            AddDelveInfos(list, client);
            list.Add("");
            list.Add(LanguageMgr.GetTranslation(client.Account.Language, "CallOfShadowsAbility.AddEffectsInfo.Info5"));
        }
    }
}
