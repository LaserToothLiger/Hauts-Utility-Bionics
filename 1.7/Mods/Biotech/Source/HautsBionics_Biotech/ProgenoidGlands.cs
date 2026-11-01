using RimWorld;
using Verse;

namespace HautsBionics_Biotech
{
    /*each progenoid gland is composed of two different hediffs: the one that can't be harvested (which is the version created by installing the implant) and the one that it transforms into once it reaches sufficient severity, which CAN be harvested.
     * The not-ready (unripe?) versions start at a severity = severityPerComplexity * [whichever is higher, the pawn's total endogenetic complexity or xenogenetic cpx].
     *   Severity drops by 1 per day (handled by a vanilla hediff comp) until it hits nearabout the minimum value, at which point it transforms into the ripe version (handled by ChangeBelowSeverity, a Framework comp).
     * canTranscribeArchiteGenes: imparts the pawn's archogenes into the produced xenogerm. This also alters the severity equation, by adding the [total archite capsule cost*severityPerArchites of the endo/xenogerm] to each of those cpx sums.
     * dropXenogermOnDeath: on death, the xenogerm or endogerm is automatically produced (preferrentially the former). This DOES bypass the need for surgery I guess...
     * whenever DropEndogerm or DropXenogerm is triggered, its severity is reset.
     * Yes, you can give someone with a low cpx a progenoid, then change their geneset to something more complex, allowing you to harvest a relatively complex xenogerm for a low cooldown.
     *      That's up to you chief, I don't really care if you 'cheat' and it's both simpler and more mod-compatible to not patch that exploit.*/
    public class HediffCompProperties_ProgenoidCharging : HediffCompProperties
    {
        public HediffCompProperties_ProgenoidCharging()
        {
            this.compClass = typeof(HediffComp_ProgenoidCharging);
        }
        public float severityPerComplexity = 1f;
        public bool canTranscribeArchiteGenes;
        public float severityPerArchites = 1f;
        public bool dropXenogermOnDeath;
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
        public void SetSeverityToGeneticComplexity()
        {
            float totalComplexity = 0f;
            float totalComplexity2 = 0f;
            if (this.Pawn.genes != null && this.Pawn.genes.GenesListForReading.Count > 0)
            {
                if (this.Pawn.genes.Endogenes.Count > 0)
                {
                    foreach (Gene g in this.Pawn.genes.Endogenes)
                    {
                        totalComplexity += g.def.biostatArc <= 0 ? g.def.biostatCpx * this.Props.severityPerComplexity : (this.Props.canTranscribeArchiteGenes ? (g.def.biostatArc * this.Props.severityPerArchites) + (g.def.biostatCpx * this.Props.severityPerComplexity) : 0f);
                    }
                }
                if (this.Pawn.genes.Xenogenes.Count > 0)
                {
                    foreach (Gene g in this.Pawn.genes.Xenogenes)
                    {
                        totalComplexity2 += g.def.biostatArc <= 0 ? g.def.biostatCpx * this.Props.severityPerComplexity : (this.Props.canTranscribeArchiteGenes ? (g.def.biostatArc * this.Props.severityPerArchites) + (g.def.biostatCpx * this.Props.severityPerComplexity) : 0f);
                    }
                }
                this.parent.Severity = Math.Max(Math.Max(totalComplexity, totalComplexity2), 1f) + this.parent.def.minSeverity;
            } else {
                this.Pawn.health.RemoveHediff(this.parent);
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            this.SetSeverityToGeneticComplexity();
        }
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            if (this.Props.dropXenogermOnDeath && this.parent.CurStageIndex == 0)
            {
                if (this.parent.pawn.genes.Xenogenes.Count > 0)
                {
                    this.DropXenogerm();
                } else if (this.parent.pawn.genes.Endogenes.Count > 0) {
                    this.DropEndogerm();
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
            this.SetSeverityToGeneticComplexity();
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
            this.SetSeverityToGeneticComplexity();
        }
    }
    /*you can choose to either generate a xenogerm of the pawn's endogenes or xenogenes, but not both at once. This is only usable on progenoid hediffs that are in their first stage (as you can see in the XML, the 'ripe' stage)
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
                    if (allHediffs[i].Part != null && allHediffs[i].def == recipe.removesHediff && allHediffs[i].Visible && allHediffs[i].CurStageIndex == 0)
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
                if (hediff != null)
                {
                    HediffComp_ProgenoidCharging progchar = hediff.TryGetComp<HediffComp_ProgenoidCharging>();
                    progchar.DropEndogerm();
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
                    if (allHediffs[i].Part != null && allHediffs[i].def == recipe.removesHediff && allHediffs[i].Visible && allHediffs[i].CurStageIndex == 0)
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
                if (hediff != null)
                {
                    HediffComp_ProgenoidCharging progchar = hediff.TryGetComp<HediffComp_ProgenoidCharging>();
                    if (progchar != null)
                    {
                        progchar.DropXenogerm();
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
