using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HautsBionics
{
    /*Add-ons are neither body part replacements, nor implants. You can install them in an organic body part OR a replacement - it really doesn't care, the part just needs to not be missing.
     * A different recipe worker is needed for those intended to be added to the hand or foot. It is always available on whichever of those is specified in its appliedOnFixedBodyParts PROVIDED that they aren't missing.
     * It is also available on any part in its appliedOnFixedBodyParts that has been replaced. This is important because a bionic arm deletes all body parts below the shoulder, and bionic legs delete feet.
     * This enables e.g. Knuckle Cannons to be installed in the hands, but if a Drill Arm has replaced the arm then they can be installed in the arm, and if an Archotech Arm has replaced the shoulder then they can be installed at the shoulder.*/
    public class Recipe_AddAddOn : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null).Contains(record) && !pawn.health.hediffSet.hediffs.Any((Hediff x) => x.Part == record && (x.def == recipe.addsHediff || !recipe.CompatibleWithHediff(x.def))));
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null)
            {
                if (base.CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[]
                {
                    billDoer,
                    pawn
                });
            }
            pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
        }
    }
    public class Recipe_AddAddOnToLimb : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null).Contains(record) && (record.groups.Contains(DefDatabase<BodyPartGroupDef>.GetNamed("Hands")) || record.groups.Contains(DefDatabase<BodyPartGroupDef>.GetNamed("Feet")) || pawn.health.hediffSet.HasDirectlyAddedPartFor(record)) && !pawn.health.hediffSet.hediffs.Any((Hediff x) => x.Part == record && (x.def == recipe.addsHediff || !recipe.CompatibleWithHediff(x.def))));
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null)
            {
                if (base.CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[]
                {
                    billDoer,
                    pawn
                });
            }
            pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
        }
    }
    /*this could almost be a DME, except that there are certain circumstances in which losing a body part does not necessarily remove the add-ons that are on it. This comp takes care of that.*/
    public class HediffComp_AddOn : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (this.Pawn.health.hediffSet.PartIsMissing(this.parent.Part))
            {
                this.Pawn.health.RemoveHediff(this.parent);
            }
        }
    }
}
