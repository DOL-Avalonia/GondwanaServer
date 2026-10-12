using DOL.GS;
using DOL.GS.Scripts;

namespace DOL.AI.Brain
{
    public class AreaEffectBrain : APlayerVicinityBrain
    {
        public override int ThinkInterval
        {
            get { return 1000; }
        }

        public override void Think()
        {
            AreaEffect areaEffect = Body as AreaEffect;

            if (areaEffect != null)
            {
                areaEffect.CheckGroupMob();
                if (areaEffect.SpellID != 0)
                    areaEffect.ApplySpell();
                else
                    areaEffect.ApplyEffect();

                AreaEffect nextArea = areaEffect.CheckFamily();

                if (nextArea != null)
                {
                    int delay = areaEffect.IntervalMin > 0 ? areaEffect.IntervalMin * 1000 : 1;
                    new ECSGameTimer(nextArea, t =>
                    {
                        nextArea.CallAreaEffect();
                        return 0;
                    }, delay);
                }
            }
        }
    }
}