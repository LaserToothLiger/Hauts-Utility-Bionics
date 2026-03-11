using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HautsBionics_Anomaly
{
    /*Voidshards are archotech bionics infused with shards. The etymology is fairly obvious. While most voidshards have their severity (or some other property) scale with the anomalous activity level, they don't ALL share that.
     * They do all share two things, though, which are what are handled by this comp:
     * 1) Removing them spawns a metalhorror. letterLabel|Text determine the contents of the red letter sent to you when this happens, although in practice the letters are so similar I should probably simplify this and
     *   just make a single language key for the label and one for the text, which just fill in a {0} or something with the voidshard's name.
     * 2) They have a hidden downside - sometimes. Evil science has a cost!
     *   downsides: the list of hediffs that a Voidshard will add on creation, or...
     *   timeToRerollDownside: ...after a random amount of ticks from within this range. (As indicated by the name, when a downside is added in this way, the Voidshard first removes its prior downside).
     *   chanceForNothing: the chance that the downside is instead a dud that doesn't do anything, but has the exact same label and tooltip as any of the other downsides (in case a player turns on Show hidden hediffs, this serves to still occlude info).*/
    public class HediffCompProperties_Voidshard : HediffCompProperties
    {
        public HediffCompProperties_Voidshard()
        {
            this.compClass = typeof(HediffComp_Voidshard);
        }
        [MustTranslate]
        public string letterLabel;
        [MustTranslate]
        public string letterText;
        public List<HediffDef> downsides;
        public float chanceForNothing = 0.5f;
        public IntRange timeToRerollDownside = new IntRange(720000, 1680000);
    }
    public class HediffComp_Voidshard : HediffComp
    {
        public HediffCompProperties_Voidshard Props
        {
            get
            {
                return (HediffCompProperties_Voidshard)this.props;
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (Find.Anomaly.Level > 0 && Find.Anomaly.Level < 6)
            {
                this.GenerateDownside();
            }
            this.timeToNextDownsideReroll = this.Props.timeToRerollDownside.RandomInRange;
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (Find.Anomaly.Level > 0 && Find.Anomaly.Level < 6)
            {
                if (this.timeToNextDownsideReroll > 0)
                {
                    this.timeToNextDownsideReroll -= delta;
                }
                else
                {
                    this.RerollDownside();
                }
            }
            else
            {
                this.timeToNextDownsideReroll = 0;
            }
        }
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            this.RemoveCurrentDownside();
            this.HorrorAttack();
        }
        private void HorrorAttack()
        {
            if (this.parent.pawn.SpawnedOrAnyParentSpawned)
            {
                Pawn pawn = this.parent.pawn;
                Pawn pawn2 = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Metalhorror, Faction.OfEntities, PawnGenerationContext.NonPlayer, -1, false, false, false, true, false, 1f, false, true, false, true, true, false, false, false, false, 0f, 0f, null, 1f, null, null, null, null, null, new float?(0f), new float?(0f), null, null, null, null, null, false, false, false, false, null, null, null, null, null, 0f, DevelopmentalStage.Adult, null, null, null, false, false, false, -1, 0, false));
                GenSpawn.Spawn(pawn2, CellFinder.StandableCellNear(pawn.PositionHeld, pawn.MapHeld, 2f, null), pawn.MapHeld, WipeMode.Vanish);
                pawn2.stances.stunner.StunFor(new IntRange(120, 240).RandomInRange, null, true, true, false);
                CompInspectStringEmergence compInspectStringEmergence = pawn2.TryGetComp<CompInspectStringEmergence>();
                if (compInspectStringEmergence != null)
                {
                    compInspectStringEmergence.sourcePawn = pawn;
                }
                TaggedString label = this.Props.letterLabel.Formatted(pawn.Named("PAWN"));
                TaggedString text = this.Props.letterText.Formatted(pawn.Named("PAWN"));
                Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.ThreatBig, pawn2, null, null, null, null, 0, true);
            }
        }
        public void RerollDownside()
        {
            this.RemoveCurrentDownside();
            this.GenerateDownside();
            this.timeToNextDownsideReroll = this.Props.timeToRerollDownside.RandomInRange;
        }
        public void RemoveCurrentDownside()
        {
            List<Hediff> hediffs = this.Pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                if (hediffs[i] is HediffWithComps hwc)
                {
                    HediffComp_VoidshardDownside vsd = hwc.TryGetComp<HediffComp_VoidshardDownside>();
                    if (vsd != null && vsd.causativeHediffs.Contains(this.parent))
                    {
                        vsd.causativeHediffs.Remove(this.parent);
                        if (vsd.causativeHediffs.Count == 0)
                        {
                            this.Pawn.health.RemoveHediff(hediffs[i]);
                        }
                    }
                }
            }
        }
        public HediffDef PickDownside()
        {
            return (Rand.Chance(this.Props.chanceForNothing) ? HVBAnomalyDefOf.HVB_DownsideNothing : this.Props.downsides.RandomElement());
        }
        public void GenerateDownside()
        {
            HediffDef hd = this.PickDownside();
            Hediff hvsd = this.Pawn.health.hediffSet.GetFirstHediffOfDef(hd);
            if (hvsd == null)
            {
                Hediff hediff = HediffMaker.MakeHediff(hd, this.Pawn);
                if (hediff is HediffWithComps hwc)
                {
                    HediffComp_VoidshardDownside vsd = hwc.TryGetComp<HediffComp_VoidshardDownside>();
                    if (vsd != null)
                    {
                        if (vsd.causativeHediffs == null)
                        {
                            vsd.causativeHediffs = new List<Hediff>();
                        }
                        vsd.causativeHediffs.Add(this.parent);
                    }
                }
                this.Pawn.health.AddHediff(hediff, null);
            }
            else
            {
                if (hvsd is HediffWithComps hwc)
                {
                    HediffComp_VoidshardDownside vsd = hwc.TryGetComp<HediffComp_VoidshardDownside>();
                    if (vsd != null)
                    {
                        if (vsd.causativeHediffs == null)
                        {
                            vsd.causativeHediffs = new List<Hediff>();
                        }
                        vsd.causativeHediffs.Add(this.parent);
                    }
                }
            }
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look<int>(ref this.timeToNextDownsideReroll, "timeToNextDownsideReroll", this.Props.timeToRerollDownside.RandomInRange, false);
        }
        public int timeToNextDownsideReroll;
    }
    /*Voidshards don't have itemized versions - you need to target the specific archotech bionic (removesHediff), and it gets "upgraded" by the installation of shards to its VS version (addsHediff).
     * They don't have bespoke removal recipes, though. You can remove them just fine to get the archotech versions. Plus a metalhorror. The Void is generous like that.*/
    public class Recipe_InstallVoidImplant : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            List<Hediff> allHediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < allHediffs.Count; i++)
            {
                if (allHediffs[i].Part != null && allHediffs[i].def == recipe.removesHediff && allHediffs[i].Visible)
                {
                    yield return allHediffs[i].Part;
                }
            }
            yield break;
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            MedicalRecipesUtility.IsClean(pawn, part);
            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[]
                {
                    billDoer,
                    pawn
                });
                if (!pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null).Contains(part))
                {
                    return;
                }
                Hediff hediff = pawn.health.hediffSet.hediffs.FirstOrDefault((Hediff x) => x.def == this.recipe.removesHediff);
                if (hediff != null)
                {
                    pawn.health.RemoveHediff(hediff);
                    pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
                }
            }
            if (this.IsViolationOnPawn(pawn, part, Faction.OfPlayer))
            {
                base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
            }
        }
    }
    public class Recipe_InstallVoidArtificialBodyPart : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            List<Hediff> allHediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < allHediffs.Count; i++)
            {
                if (allHediffs[i].Part != null && allHediffs[i].def == recipe.removesHediff && allHediffs[i].Visible)
                {
                    yield return allHediffs[i].Part;
                }
            }
            yield break;
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Hediff hediff = null;
            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[]
                {
                    billDoer,
                    pawn
                });
                hediff = pawn.health.hediffSet.GetDirectlyAddedPartFor(part);
                if (part != null)
                {
                    for (int i = 0; i < part.parts.Count; i++)
                    {
                        MedicalRecipesUtility.RestorePartAndSpawnAllPreviousParts(pawn, part.parts[i], billDoer.Position, billDoer.Map);
                    }
                    pawn.health.RestorePart(part, null, true);
                }
                if (!PawnGenerator.IsBeingGenerated(pawn) && this.IsViolationOnPawn(pawn, part, Faction.OfPlayer))
                {
                    base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
                }
            }
            else if (part != null)
            {
                for (int i = 0; i < part.parts.Count; i++)
                {
                    MedicalRecipesUtility.RestorePartAndSpawnAllPreviousParts(pawn, part.parts[i], pawn.Position, pawn.Map);
                }
                pawn.health.RestorePart(part, null, true);
            }
            pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
            if (hediff != null)
            {
                hediff.Notify_SurgicallyReplaced(billDoer);
            }
        }
    }
}
