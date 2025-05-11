using HarmonyLib;
using HautsBionics;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using HautsFramework;
using UnityEngine;
using System.Reflection;
using VanillaGenesExpanded;
using VFECore;

namespace HautsBionics_Biotech
{
    [StaticConstructorOnStartup]
    public static class HautsBionics_Biotech
    {
        private static readonly Type patchType = typeof(HautsBionics_Biotech);
        static HautsBionics_Biotech()
        {
        }
    }
    [DefOf]
    public static class HVBDefOf_Biotech
    {
        static HVBDefOf_Biotech()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HVBDefOf_Biotech));
        }
        public static HediffDef HVB_ArchotechMechaqueen;
    }
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
                this.parent.Severity = Math.Max(Math.Max(totalComplexity,totalComplexity2), 1f);
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
    public class HediffCompProperties_OnlyYourMechsAura : HediffCompProperties_AuraHediff
    {
        public HediffCompProperties_OnlyYourMechsAura()
        {
            this.compClass = typeof(HedifComp_OnlyYourMechsAura);
        }
    }
    public class HedifComp_OnlyYourMechsAura : HediffComp_AuraHediff
    {
        public override bool ValidatePawn(Pawn originator, Pawn p, bool inCaravan)
        {
            if (originator.mechanitor != null && originator.mechanitor.OverseenPawns != null && originator.mechanitor.OverseenPawns.Contains(p))
            {
                return base.ValidatePawn(originator, p, inCaravan);
            }
            return false;
        }
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
        }
    }
    public class HediffCompProperties_HBS : HediffCompProperties
    {
        public HediffCompProperties_HBS()
        {
            this.compClass = typeof(HediffComp_HBS);
        }
        public float hemogenDrainPerDay;
    }
    public class HediffComp_HBS : HediffComp
    {
        public HediffCompProperties_HBS Props
        {
            get
            {
                return (HediffCompProperties_HBS)this.props;
            }
        }
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (this.Pawn.IsHashIntervalTick(15) && this.Pawn.genes != null)
            {
                Gene_Hemogen gene_Hemogen = this.Pawn.genes.GetFirstGeneOfType<Gene_Hemogen>();
                if (gene_Hemogen != null)
                {
                    gene_Hemogen.Value -= this.Props.hemogenDrainPerDay/4000f;
                    this.parent.Severity = gene_Hemogen.Value;
                } else {
                    this.parent.Severity = this.parent.def.minSeverity;
                }
            }
        }
    }
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
            return pawn.Faction == null || pawn.Faction != billDoerFaction || pawn.IsQuestLodger();
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
            return pawn.Faction == null || pawn.Faction != billDoerFaction || pawn.IsQuestLodger();
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