using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HautsBionics
{
    /*bionic marrow can no longer be installed if the pawn's remaining marrow substitution stat hits 0. (it has a default of 15 and additional sources of it are quite sparse, so most pawns can only have 15 marrow subs)
     * This recipe also makes it so that if the particular marrow being installed already exists, the level juts gets upped by 1.*/
    public class Recipe_AddMarrow : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn.GetStatValue(HVBDefOf.HVB_RemainingMarrow) <= 0)
            {
                return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => pawn.GetStatValue(HVBDefOf.HVB_RemainingMarrow) > 0);
            }
            return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => !pawn.health.hediffSet.hediffs.Any((Hediff x) => x.Part == record && !recipe.CompatibleWithHediff(x.def)));
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[]
                {
                    billDoer,
                    pawn
                });
                Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(this.recipe.addsHediff, false);
                if (firstHediffOfDef == null)
                {
                    pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
                }
                else
                {
                    ((Hediff_Level)firstHediffOfDef).ChangeLevel(1);
                }
                if (!PawnGenerator.IsBeingGenerated(pawn) && this.IsViolationOnPawn(pawn, part, Faction.OfPlayer))
                {
                    base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
                }
                if (ModsConfig.IdeologyActive)
                {
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.InstalledProsthetic, billDoer.Named(HistoryEventArgsNames.Doer)), true);
                }
            }
        }
    }
    /*All marrow substitutes should have this class. When added/leveled up/removed, they force the Hediff_MarrowCounter to be reevaluated.
     * MarrowCounter hediff inflicts -1 remaining marrow per 1 point of severity. Whenever it reevaluates, its severity is set to the number of Hediff_Marrows on the pawn.*/
    public class Hediff_Marrow : Hediff_Level
    {
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HVBDefOf.HVB_AugmentedMarrowCounter, false);
            if (firstHediffOfDef == null)
            {
                Hediff hediff = HediffMaker.MakeHediff(HVBDefOf.HVB_AugmentedMarrowCounter, this.pawn, this.pawn.health.hediffSet.GetBrain());
                pawn.health.AddHediff(hediff, this.pawn.health.hediffSet.GetBrain(), null, null);
            }
            else
            {
                ((Hediff_MarrowCounter)firstHediffOfDef).MatchCurrentMarrowLevel();
            }
        }
        public override void ChangeLevel(int levelOffset)
        {
            base.ChangeLevel(levelOffset);
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HVBDefOf.HVB_AugmentedMarrowCounter, false);
            if (firstHediffOfDef == null)
            {
                Hediff hediff = HediffMaker.MakeHediff(HVBDefOf.HVB_AugmentedMarrowCounter, this.pawn, this.pawn.health.hediffSet.GetBrain());
                pawn.health.AddHediff(hediff, this.pawn.health.hediffSet.GetBrain(), null, null);
                hediff.Severity = 1f;
            }
            else
            {
                firstHediffOfDef.Severity += levelOffset;
            }
        }
        public override void PostRemoved()
        {
            base.PostRemoved();
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HVBDefOf.HVB_AugmentedMarrowCounter, false);
            if (firstHediffOfDef != null)
            {
                ((Hediff_MarrowCounter)firstHediffOfDef).MatchCurrentMarrowLevel();
            }
        }
    }
    public class Hediff_MarrowCounter : Hediff
    {
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            this.MatchCurrentMarrowLevel();
        }
        public override void PostRemoved()
        {
            base.PostRemoved();
            int marrowTotal = this.CountMarrow();
            if (marrowTotal != 0)
            {
                Hediff hediff = HediffMaker.MakeHediff(this.def, this.pawn, this.pawn.health.hediffSet.GetBrain());
                this.pawn.health.AddHediff(hediff, this.pawn.health.hediffSet.GetBrain(), null, null);
                ((Hediff_MarrowCounter)hediff).MatchCurrentMarrowLevel();
            }
        }
        public int CountMarrow()
        {
            int marrowTotal = 0;
            foreach (Hediff h in this.pawn.health.hediffSet.hediffs)
            {
                if (h is Hediff_Marrow hm)
                {
                    marrowTotal += hm.level;
                }
            }
            return marrowTotal;
        }
        public void MatchCurrentMarrowLevel()
        {
            this.Severity = this.CountMarrow();
        }
    }
}
