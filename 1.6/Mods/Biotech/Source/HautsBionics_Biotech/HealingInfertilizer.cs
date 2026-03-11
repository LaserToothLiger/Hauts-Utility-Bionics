using RimWorld;
using System.Collections.Generic;
using Verse;

namespace HautsBionics_Biotech
{
    /*the healing infertilizer periodically inflicts a stacking negative fertility offset, and turns into a nonfunctional version once the pawn's fertility hits 0. (These are handled via other comps)
     * This nonfunctional version is a different HediffDef, which is targeted by a recipe with this worker. The recipe sets that hediff to its max severity; its highest stage has the original
     * injury healing factor boost, but since this new hediff also decrements its own severity over time, eventually the buff will be lost once more, necessitating further use of this medical operation to keep it functional.*/
    public class Recipe_RefuelInfertilizer : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            List<Hediff> allHediffs = pawn.health.hediffSet.hediffs;
            int num;
            for (int i = 0; i < allHediffs.Count; i = num + 1)
            {
                if (allHediffs[i].Part != null && allHediffs[i].def == recipe.addsHediff && allHediffs[i].Visible)
                {
                    yield return allHediffs[i].Part;
                }
                num = i;
            }
            yield break;
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(this.recipe.addsHediff);
            if (hediff != null)
            {
                hediff.Severity = hediff.def.maxSeverity;
            }
        }
    }
}