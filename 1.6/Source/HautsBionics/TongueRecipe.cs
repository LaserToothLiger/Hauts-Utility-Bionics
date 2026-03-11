using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HautsBionics
{
    /*tongues are to jaws as hands are to arms. Remove the latter, and the former comes off as well. This mostly makes sense, except that, in conjunction with how replacement body parts work, having a replacement jaw removes your ability to have a tongue.
     * This is stupid. I mean, I agree that since human tongues are anchored to the lower mandible, replacing the latter would logically result in the former being removed - the part I disagree with is that you can't add a custom tongue of your choice.
     * This recipe is akin to the recipe that adds a replacement body part. However, it can be added even if the parent body part of the part it's supposed to replace is artificial.
     * In layman's terms, this is a recipe for adding tongues, which works so long as you have a jaw. Doesn't matter if that jaw is natural or bionic or some secret third thing.*/
    public class Recipe_AddTongue : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => pawn.def.race.body.AllParts.Contains(record) && (record.parent == null || !pawn.health.hediffSet.PartIsMissing(record.parent)) && !pawn.health.hediffSet.hediffs.Any((Hediff x) => x.Part == record && (x.def == recipe.addsHediff || !recipe.CompatibleWithHediff(x.def))));
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            bool flag2 = !PawnGenerator.IsBeingGenerated(pawn) && this.IsViolationOnPawn(pawn, part, Faction.OfPlayer);
            Hediff hediff = null;
            if (billDoer != null)
            {
                if (base.CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[] { billDoer, pawn });
                hediff = pawn.health.hediffSet.GetDirectlyAddedPartFor(part);
                if (part != null)
                {
                    MedicalRecipesUtility.RestorePartAndSpawnAllPreviousParts(pawn, part, billDoer.Position, billDoer.Map);
                }
                if (MedicalRecipesUtility.IsClean(pawn, part) && flag2 && part.def.spawnThingOnRemoved != null)
                {
                    ThoughtUtility.GiveThoughtsForPawnOrganHarvested(pawn, billDoer);
                }
                if (flag2)
                {
                    base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
                }
                if (ModsConfig.IdeologyActive)
                {
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.InstalledProsthetic, billDoer.Named(HistoryEventArgsNames.Doer)), true);
                }
            }
            else if (pawn.Map != null)
            {
                if (part != null)
                {
                    MedicalRecipesUtility.RestorePartAndSpawnAllPreviousParts(pawn, part, pawn.Position, pawn.Map);
                }
            }
            else if (part != null)
            {
                pawn.health.RestorePart(part, null, true);
            }
            pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
            if (hediff != null)
            {
                hediff.Notify_SurgicallyReplaced(billDoer);
            }
        }
        public override bool IsViolationOnPawn(Pawn pawn, BodyPartRecord part, Faction billDoerFaction)
        {
            return ((pawn.Faction != billDoerFaction && pawn.Faction != null) || pawn.IsQuestLodger()) && (this.recipe.addsHediff.addedPartProps == null || !this.recipe.addsHediff.addedPartProps.betterThanNatural) && HealthUtility.PartRemovalIntent(pawn, part) == BodyPartRemovalIntent.Harvest;
        }
    }
}