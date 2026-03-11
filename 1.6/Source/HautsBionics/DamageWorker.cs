using RimWorld;
using Verse;

namespace HautsBionics
{
    //on dealing damage to a plant (how?) or flesh creature, archotech food disintegrators convert that damage into nutrition
    public class DamageWorker_DisintegrationBite : DamageWorker_Bite
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            DamageResult dR = base.Apply(dinfo, thing);
            if (dinfo.Instigator is Pawn pawn && pawn.needs != null && pawn.needs.food != null && (thing is Plant || (thing is Pawn p && p.RaceProps.IsFlesh)))
            {
                pawn.needs.food.CurLevel += dR.totalDamageDealt / 1000f;
            }
            return dR;
        }
    }
}
