using DOL.AI.Brain;
using DOL.gameobjects.CustomNPC;
using DOL.GS.Scripts;
using DOL.GS.ServerProperties;
using DOL.Language;
using System.Collections.Generic;


namespace DOL.GS.Spells
{
    [SpellHandler("Teleport")]
    public class TeleportSpellHandler : SpellHandler
    {
        string zoneName;
        public TeleportSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
            TPPoint tPPoint = TeleportMgr.LoadTP((ushort)Spell.LifeDrainReturn);
            zoneName = WorldMgr.GetRegion(tPPoint.Region).GetZone(tPPoint.Position.Coordinate).Description;
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target is ShadowNPC)
                return false;

            if (target is GameNPC npc)
            {
                if (npc is AmteMob || npc is GameMobKamikaze || npc is SplitMob || npc is GamePet || npc is StaticMob)
                {
                    if (npc is TerritoryGuard || npc is TerritoryBoss || npc is IllusionBladePet || npc is AstralPet || npc.IsBoss)
                    {
                        SendSpellResistAnimation(npc);
                        return false;
                    }
                }
                else
                {
                    SendSpellResistAnimation(npc);
                    return false;
                }

                if (npc.Brain is TurretBrain || npc.Brain is TurretFNFBrain || npc.Brain is MLBrain)
                {
                    SendSpellResistAnimation(npc);
                    return false;
                }

                if (npc.Brain is BomberBrain)
                {
                    return false;
                }

                if (npc.Brain is IllusionPetBrain)
                {
                    if (npc.IsAlive)
                        npc.Die(m_caster);
                    return true;
                }
            }

            if (Spell.LifeDrainReturn <= 0)
            {
                return false;
            }
            
            TPPoint tPPoint = TeleportMgr.LoadTP((ushort)Spell.LifeDrainReturn);
            if (target.TPPoint != null && target.TPPoint.DbTPPoint.TPID == tPPoint.DbTPPoint.TPID)
            {
                tPPoint = target.TPPoint.GetNextTPPoint();
            }
            else
            {
                switch (tPPoint.Type)
                {
                    case Database.eTPPointType.Random:
                        tPPoint = tPPoint.GetNextTPPoint();
                        break;
                    case Database.eTPPointType.Smart:
                        tPPoint = tPPoint.GetSmarttNextPoint();
                        break;
                }
            }
            target.TPPoint = tPPoint;
            target.MoveTo(tPPoint.Position.With(target.Orientation));
            return true;
        }

        public override IList<string> DelveInfo
        {
            get
            {
                var list = new List<string>();
                if (Spell.LifeDrainReturn != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Destination", zoneName));
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Target", Spell.Target));
                if (Spell.Range != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Range", Spell.Range));
                if (Spell.Power != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.PowerCost", Spell.Power.ToString("0;0'%'")));
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.CastingTime", (Spell.CastTime * 0.001).ToString("0.0## sec;-0.0## sec;'instant'")));
                if (Spell.RecastDelay > 60000)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.RecastTime") + (Spell.RecastDelay / 60000).ToString() + ":" + (Spell.RecastDelay % 60000 / 1000).ToString("00") + " min");
                else if (Spell.RecastDelay > 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.RecastTime") + (Spell.RecastDelay / 1000).ToString() + " sec");
                if (Spell.Radius != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Radius", Spell.Radius));

                return list;
            }
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            int recastSeconds = Spell.RecastDelay / 1000;
            string mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Teleport.MainDescription", Spell.Name, zoneName);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }

    }
}