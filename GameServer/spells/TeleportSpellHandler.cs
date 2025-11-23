using DOL.AI.Brain;
using DOL.gameobjects.CustomNPC;
using DOL.GS.Scripts;
using DOL.GS.ServerProperties;
using DOL.Language;
using System.Collections.Generic;
using System.Diagnostics;


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

        /// <inheritdoc />
        public override int CalculateSpellResistChance(GameLiving target)
        {
            if (target is GameNPC npc)
            {
                if (target is not (AmteMob or GameMobKamikaze or SplitMob or GamePet or StaticMob))
                    return 100; // Resist always

                if (npc.IsBoss)
                    return 100;

                if (target is TerritoryGuard or TerritoryBoss)
                    return 100;

                if (npc is IllusionBladePet or AstralPet or { Brain: TurretBrain or TurretFNFBrain or MLBrain })
                    return 100;

                if (npc.Brain is BomberBrain)
                    return 100;
            }
            return 0;
            //return base.CalculateSpellResistChance(target);
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target is ShadowNPC)
                return false;

            var resistChance = CalculateSpellResistChance(target);
            if (resistChance >= 100 || Util.Chance(resistChance))
            {
                SendSpellResistAnimation(target);
                return true;
            }

            return OnDirectEffect(target, effectiveness);
        }

        /// <inheritdoc />
        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (Spell.LifeDrainReturn <= 0)
            {
                return false;
            }

            if (target is IllusionPet)
            {
                if (target.IsAlive)
                    target.Die(m_caster);
                return true;
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
            
            if (target is GamePet pet)
            {
                if (!GameMath.IsWithinRadius2D(tPPoint.Position, target.Position, ControlledNpcBrain.MAX_OWNER_FOLLOW_DIST))
                {
                    if (pet.Owner is GamePlayer playerOwner)
                        playerOwner.CommandNpcRelease();
                    else
                        pet.RemoveFromWorld();
                    return true;
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