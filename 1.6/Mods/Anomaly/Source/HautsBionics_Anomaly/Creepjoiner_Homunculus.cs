using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace HautsBionics_Anomaly
{
    /*The Homunculus trait uses a Framework tool to prevent surgical removal of its bionics. Completing the associated dark research project unlocks a recipe to liberate 3-4 of its bionics from its body
     * at the cost of irrecoverably asploding the Homunculus into gore and metalhorrors. No corpse.*/
    public class Recipe_VivisectHomunculus : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            Pawn pawn = thing as Pawn;
            return pawn != null && pawn.story != null && pawn.story.traits.HasTrait(HVBAnomalyDefOf.HVB_HomunculusTrait);
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (pawn.story != null && pawn.story.traits.HasTrait(HVBAnomalyDefOf.HVB_HomunculusTrait))
            {
                pawn.story.traits.RemoveTrait(pawn.story.traits.GetTrait(HVBAnomalyDefOf.HVB_HomunculusTrait));
                List<Hediff> hediffsToRemove = new List<Hediff>();
                foreach (Hediff h in pawn.health.hediffSet.hediffs)
                {
                    if (h.def.comps != null && h.def.HasComp(typeof(HediffComp_Voidshard)))
                    {
                        hediffsToRemove.Add(h);
                    }
                }
                List<Hediff> bionicsToExtract = new List<Hediff>();
                int toExtract = Math.Min((int)Rand.RangeInclusive(3, 4), hediffsToRemove.Count);
                int counter = 250;
                while (bionicsToExtract.Count < toExtract && counter > 0)
                {
                    Hediff h = hediffsToRemove.RandomElement();
                    if (!bionicsToExtract.Contains(h))
                    {
                        bionicsToExtract.Add(h);
                    }
                    counter--;
                }
                if (pawn.SpawnedOrAnyParentSpawned)
                {
                    foreach (Hediff h in bionicsToExtract)
                    {
                        if (h.def.spawnThingOnRemoved != null)
                        {
                            GenSpawn.Spawn(h.def.spawnThingOnRemoved, pawn.PositionHeld, pawn.MapHeld, WipeMode.Vanish);
                        }
                    }
                    for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
                    {
                        if (pawn.health.hediffSet.hediffs[i].def.comps != null && pawn.health.hediffSet.hediffs[i].def.HasComp(typeof(HediffComp_Voidshard)))
                        {
                            pawn.health.RemoveHediff(pawn.health.hediffSet.hediffs[i]);
                            EffecterDefOf.MeatExplosion.Spawn(pawn.PositionHeld, pawn.MapHeld, 1f).Cleanup();
                        }
                    }
                    pawn.Kill(null);
                    if (pawn.Corpse != null)
                    {
                        pawn.Corpse.Destroy(DestroyMode.KillFinalize);
                    }
                    else if (!pawn.Dead)
                    {
                        pawn.Destroy(DestroyMode.KillFinalize);
                    }
                }
            }
        }
    }
    /*this is what unlocks the research project, since you can only unlock dark research projects when the player "sees" a creature of a new ThingDef (Homunculi share the same ThingDef as any other creepjoiner, so this is not feasible)
     * or when you encounter a new incident for the first time. This incident happens whenever a Homunculus spawns (done by Harmony patch), and it has no effects and a duration of Nope.*/
    public class IncidentWorker_HomunculusExamined : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            return true;
        }
    }
}
