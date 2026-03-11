using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HautsBionics_Biotech
{
    /*each progenoid gland is composed of two different hediffs: the one that can't be harvested (which is the version created by installing the implant) and the one that it transforms into once it reaches sufficient severity, which CAN be harvested.
     * The not-ready (unripe?) versions start at a severity = severityPerComplexity * [whichever is higher, the pawn's total endogenetic complexity or xenogenetic cpx].
     *   Severity drops by 1 per day (handled by a vanilla hediff comp) until it hits nearabout the minimum value, at which point it transforms into the ripe version (handled by ChangeBelowSeverity, a Framework comp).
     * canTranscribeArchiteGenes: imparts the pawn's archogenes into the produced xenogerm. This also alters the severity equation, by adding the [total archite capsule cost*severityPerArchites of the endo/xenogerm] to each of those cpx sums.
     * dropXenogermOnDeath: on death, the xenogerm or endogerm is produced (preferrentially the former), then the hediff removes itself. That sounds wonky, but because the endo/xenogerm dropping methods create the "transformsToWhenHarvested" hediff
     *   on the pawn, in practice it looks like it's just expended its charge. (this is of course reliant on you not turning this flag on for the unripe variant, turning it on for the ripe variant, and setting the ripe's transformsToWhenHarvested
     *   to the unripe variant)
     * naturally, transformsToWhenHarvested is also added to the pawn when you use the recipes to harvest the e/xgerm. And then the recipe destroys the original hediff, once more looking like a charge expenditure.*/
    public class HediffCompProperties_ProgenoidCharging : HediffCompProperties
    {
        public HediffCompProperties_ProgenoidCharging()
        {
            this.compClass = typeof(HediffComp_ProgenoidCharging);
        }
        public float severityPerComplexity = 1f;
        public bool canTranscribeArchiteGenes = false;
        public float severityPerArchites = 1f;
        public HediffDef transformsToWhenHarvested = null;
        public bool dropXenogermOnDeath = false;
    }
    public class HediffComp_ProgenoidCharging : HediffComp
    {
        public HediffCompProperties_ProgenoidCharging Props
        {
            get
            {
                return (HediffCompProperties_ProgenoidCharging)this.props;
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            float totalComplexity = 0f;
            float totalComplexity2 = 0f;
            if (this.parent.pawn.genes != null && this.parent.pawn.genes.GenesListForReading.Count > 0)
            {
                if (this.parent.pawn.genes.Endogenes.Count > 0)
                {
                    foreach (Gene g in this.parent.pawn.genes.Endogenes)
                    {
                        totalComplexity += g.def.biostatArc <= 0 ? g.def.biostatCpx * this.Props.severityPerComplexity : (this.Props.canTranscribeArchiteGenes ? (g.def.biostatArc * this.Props.severityPerArchites) + (g.def.biostatCpx * this.Props.severityPerComplexity) : 0f);
                    }
                }
                if (this.parent.pawn.genes.Xenogenes.Count > 0)
                {
                    foreach (Gene g in this.parent.pawn.genes.Xenogenes)
                    {
                        totalComplexity2 += g.def.biostatArc <= 0 ? g.def.biostatCpx * this.Props.severityPerComplexity : (this.Props.canTranscribeArchiteGenes ? (g.def.biostatArc * this.Props.severityPerArchites) + (g.def.biostatCpx * this.Props.severityPerComplexity) : 0f);
                    }
                }
                this.parent.Severity = Math.Max(Math.Max(totalComplexity, totalComplexity2), 1f);
            }
            else
            {
                this.parent.pawn.health.RemoveHediff(this.parent);
            }
        }
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            if (this.Props.dropXenogermOnDeath)
            {
                bool didDrop = false;
                if (this.parent.pawn.genes.Xenogenes.Count > 0)
                {
                    this.DropXenogerm();
                    didDrop = true;
                }
                else if (this.parent.pawn.genes.Endogenes.Count > 0)
                {
                    this.DropEndogerm();
                    didDrop = true;
                }
                if (didDrop)
                {
                    this.parent.pawn.health.RemoveHediff(this.parent);
                }
            }
        }
        public void DropEndogerm()
        {
            Xenogerm xenogerm = (Xenogerm)ThingMaker.MakeThing(ThingDefOf.Xenogerm, null);
            List<Genepack> noGenepacks = new List<Genepack>();
            string name = "HVB_ProgenoidXenogerm".Translate(this.parent.pawn.Name.ToStringShort);
            xenogerm.Initialize(noGenepacks, (this.parent.pawn.genes.XenotypeLabel != null) ? this.parent.pawn.genes.XenotypeLabel.Trim() : name, (this.parent.pawn.genes.iconDef != null) ? this.parent.pawn.genes.iconDef : XenotypeIconDefOf.Basic);
            foreach (Gene g in this.parent.pawn.genes.Endogenes)
            {
                if (g.def.biostatArc <= 0 || this.Props.canTranscribeArchiteGenes)
                {
                    xenogerm.GeneSet.AddGene(g.def);
                }
            }
            GenSpawn.Spawn(xenogerm, this.parent.pawn.PositionHeld, this.parent.pawn.MapHeld, WipeMode.Vanish);
            if (this.Props.transformsToWhenHarvested != null)
            {
                Hediff hediffToAdd = HediffMaker.MakeHediff(this.Props.transformsToWhenHarvested, this.parent.pawn, this.parent.Part);
                this.parent.pawn.health.AddHediff(hediffToAdd, this.parent.Part);
            }
        }
        public void DropXenogerm()
        {
            Xenogerm xenogerm = (Xenogerm)ThingMaker.MakeThing(ThingDefOf.Xenogerm, null);
            List<Genepack> noGenepacks = new List<Genepack>();
            string name = "HVB_ProgenoidXenogerm".Translate(this.parent.pawn.Name.ToStringShort);
            xenogerm.Initialize(noGenepacks, (this.parent.pawn.genes.XenotypeLabel != null) ? this.parent.pawn.genes.XenotypeLabel.Trim() : name, (this.parent.pawn.genes.iconDef != null) ? this.parent.pawn.genes.iconDef : XenotypeIconDefOf.Basic);
            foreach (Gene g in this.parent.pawn.genes.Xenogenes)
            {
                if (g.def.biostatArc <= 0 || this.Props.canTranscribeArchiteGenes)
                {
                    xenogerm.GeneSet.AddGene(g.def);
                }
            }
            GenSpawn.Spawn(xenogerm, this.parent.pawn.PositionHeld, this.parent.pawn.MapHeld, WipeMode.Vanish);
            if (this.Props.transformsToWhenHarvested != null)
            {
                Hediff hediffToAdd = HediffMaker.MakeHediff(this.Props.transformsToWhenHarvested, this.parent.pawn, this.parent.Part);
                this.parent.pawn.health.AddHediff(hediffToAdd, this.parent.Part);
            }
        }
    }
    /*you can choose to either generate a xenogerm of the pawn's endogenes or xenogenes, but not both at once.
     * this triggers either the DropEndogerm or DropXenogerm methods of the ProgenoidCharging comp*/
    public class Recipe_HarvestProgenoidEndo : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn.genes != null && pawn.genes.Endogenes.Count > 0)
            {
                List<Hediff> allHediffs = pawn.health.hediffSet.hediffs;
                int num;
                for (int i = 0; i < allHediffs.Count; i = num + 1)
                {
                    if (allHediffs[i].Part != null && allHediffs[i].def == recipe.removesHediff && allHediffs[i].Visible)
                    {
                        yield return allHediffs[i].Part;
                    }
                    num = i;
                }
                yield break;
            }
        }
        public override bool IsViolationOnPawn(Pawn pawn, BodyPartRecord part, Faction billDoerFaction)
        {
            return (pawn.Faction != billDoerFaction && pawn.Faction != null) || pawn.IsQuestLodger();
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null && pawn.genes.Endogenes != null)
            {
                if (base.CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[] { billDoer, pawn });
                if (!pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null).Contains(part))
                {
                    return;
                }
                Hediff hediff = pawn.health.hediffSet.hediffs.FirstOrDefault((Hediff x) => x.def == this.recipe.removesHediff && x.Part == part);
                if (hediff != null && hediff is HediffWithComps h)
                {
                    foreach (HediffComp hc in h.comps)
                    {
                        if (hc is HediffComp_ProgenoidCharging progchar)
                        {
                            progchar.DropEndogerm();
                            pawn.health.RemoveHediff(h);
                            break;
                        }
                    }
                }
            }
            if (this.IsViolationOnPawn(pawn, part, Faction.OfPlayer))
            {
                base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
            }
        }
    }
    public class Recipe_HarvestProgenoidXeno : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn.genes != null && pawn.genes.Xenogenes.Count > 0)
            {
                List<Hediff> allHediffs = pawn.health.hediffSet.hediffs;
                int num;
                for (int i = 0; i < allHediffs.Count; i = num + 1)
                {
                    if (allHediffs[i].Part != null && allHediffs[i].def == recipe.removesHediff && allHediffs[i].Visible)
                    {
                        yield return allHediffs[i].Part;
                    }
                    num = i;
                }
                yield break;
            }
        }
        public override bool IsViolationOnPawn(Pawn pawn, BodyPartRecord part, Faction billDoerFaction)
        {
            return (pawn.Faction != billDoerFaction && pawn.Faction != null) || pawn.IsQuestLodger();
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null && pawn.genes.Xenogenes != null)
            {
                if (base.CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[] { billDoer, pawn });
                if (!pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null).Contains(part))
                {
                    return;
                }
                Hediff hediff = pawn.health.hediffSet.hediffs.FirstOrDefault((Hediff x) => x.def == this.recipe.removesHediff && x.Part == part);
                if (hediff != null && hediff is HediffWithComps h)
                {
                    foreach (HediffComp hc in h.comps)
                    {
                        if (hc is HediffComp_ProgenoidCharging progchar)
                        {
                            progchar.DropXenogerm();
                            pawn.health.RemoveHediff(h);
                            break;
                        }
                    }
                }
            }
            if (this.IsViolationOnPawn(pawn, part, Faction.OfPlayer))
            {
                base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
            }
        }
    }
}
