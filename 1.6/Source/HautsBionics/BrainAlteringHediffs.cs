using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace HautsBionics
{
    /*cogno-berserkers grant "hediff" when the pawn is harmed by damage. If it already exists, then inttead its severity increases by severityGain, and its duration is set to setDurationTo. Fairly str8forward field naming.
     * Duration of hediff is determined by a Disappears comp rather than decrementing severity because severity is used to stack the intensity of the hediff's benefits.*/
    public class HediffCompProperties_GainRageWhenHit : HediffCompProperties
    {
        public HediffCompProperties_GainRageWhenHit()
        {
            this.compClass = typeof(HediffComp_GainRageWhenHit);
        }
        public HediffDef hediff;
        public float severityGain;
        public int setDurationTo;
    }
    public class HediffComp_GainRageWhenHit : HediffComp
    {
        public HediffCompProperties_GainRageWhenHit Props
        {
            get
            {
                return (HediffCompProperties_GainRageWhenHit)this.props;
            }
        }
        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (totalDamageDealt > 0 && dinfo.Def != null && dinfo.Def.harmsHealth)
            {
                Hediff hediff = this.Pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.hediff);
                if (hediff != null)
                {
                    hediff.Severity += this.Props.severityGain;
                    HediffComp_Disappears hcd = hediff.TryGetComp<HediffComp_Disappears>();
                    if (hcd != null)
                    {
                        hcd.SetDuration(this.Props.setDurationTo);
                    }
                }
                else
                {
                    hediff = HediffMaker.MakeHediff(this.Props.hediff, this.Pawn);
                    this.Pawn.health.AddHediff(hediff);
                }
            }
        }
    }
    /*cogno-straitjacket's pain offset increases as its severity does. Its severity increases over time while in any mental state (due to a comp).
     * In order for the pain offset to update, we need to dirty cache the pawn's hediffs. Obviously we don't want to be doing this regularly (and if the severity is currently 0, we have no need to), hence the restrictions.
     * If the pawn becomes downed while it's inflicting pain (whether or not the pain had anything to do with that), the implant also inflicts a coma*/
    public class Hediff_SanityKeeper : Hediff_Implant
    {
        public override float PainOffset
        {
            get
            {
                return Math.Max(0f, this.Severity - this.def.minSeverity);
            }
        }
        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            if (this.Severity > this.def.minSeverity)
            {
                if (this.pawn.IsHashIntervalTick(100, delta))
                {
                    this.pawn.health.hediffSet.DirtyCache();
                    if (this.pawn.health.ShouldBeDowned() && this.pawn.InMentalState)
                    {
                        Hediff hediff = HediffMaker.MakeHediff(HVBDefOf.HVB_CognoStraitjacketComa, this.pawn, this.pawn.health.hediffSet.GetBrain());
                        this.pawn.health.AddHediff(hediff);
                    }
                }
            }
        }
    }
    /*resocialization is the result of a brain surgery which aggressively strips away the subject's capacity for noncompliance.
     * Hence, its addition removes a random amount of will and resistance from prisoners, and it "masks" all non-suppressed, non-genetic, non-Excise Trait Exempt traits.
     * (Genetic traits are not masked because removal of a genetic trait also removes the gene, and resoc has no justification to edit pawn genes)
     * It will periodically mask traits. Masked traits are removed from the pawn, but will be given back to it if the hediff is removed.
     * Masking of a trait unsuppresses any traits it suppressed, which would themselves become eligible for masking on the next pass.
     * Removal of resocialization has a 50% chance to add a berserk-inducing mental disorder called broken resoc. This can be cured by simply readministering resocialization (or by healer serums)*/
    public class Hediff_Resoc : Hediff_Implant
    {
        public override string TipStringExtra
        {
            get
            {
                if (this.hiddenTraits.Count > 0)
                {
                    string allHiddenTraits = "Masking the following traits: ";
                    for (int i = 0; i < this.hiddenTraits.Count; i++)
                    {
                        if (i != this.hiddenTraits.Count - 1)
                        {
                            allHiddenTraits += this.hiddenTraits[i].Label + ", ";
                        }
                        else
                        {
                            allHiddenTraits += this.hiddenTraits[i].Label;
                        }
                    }
                    return base.TipStringExtra + allHiddenTraits;
                }
                else
                {
                    return base.TipStringExtra;
                }
            }
        }
        public override void PostAdd(DamageInfo? dinfo)
        {
            if (this.pawn.GuestStatus == GuestStatus.Prisoner)
            {
                float will = this.pawn.guest.will;
                if (will > 0f)
                {
                    this.pawn.guest.will -= will * Rand.Value;
                }
                float resist = this.pawn.guest.resistance;
                if (resist > 0f)
                {
                    this.pawn.guest.resistance -= resist * Rand.Value;
                }
            }
            if (!PawnGenerator.IsBeingGenerated(this.pawn))
            {
                this.MaskTraits();
            }
            if (this.pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_BrokenResoc))
            {
                this.pawn.health.RemoveHediff(this.pawn.health.hediffSet.GetFirstHediffOfDef(HVBDefOf.HVB_BrokenResoc));
            }
            base.PostAdd(dinfo);
        }
        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (this.pawn.IsHashIntervalTick(150, delta))
            {
                this.MaskTraits();
            }
        }
        public override void PostRemoved()
        {
            if (this.pawn.story != null)
            {
                foreach (Trait t in this.hiddenTraits)
                {
                    Trait tnew = new Trait(t.def, t.Degree);
                    this.pawn.story.traits.GainTrait(tnew);
                    foreach (Trait t2 in this.pawn.story.traits.allTraits)
                    {
                        if (!t2.Suppressed && tnew != t2 && (tnew.def.ConflictsWith(t2) || tnew.def == t2.def))
                        {
                            t2.suppressedByTrait = true;
                        }
                    }
                }
            }
            if (Rand.Value <= 0.5f)
            {
                Hediff hediff = HediffMaker.MakeHediff(HVBDefOf.HVB_BrokenResoc, this.pawn, this.pawn.health.hediffSet.GetBrain());
                this.pawn.health.AddHediff(hediff, this.pawn.health.hediffSet.GetBrain());
            }
            base.PostRemoved();
        }
        private void MaskTraits()
        {
            if (this.pawn.story != null && !this.pawn.story.traits.allTraits.NullOrEmpty<Trait>())
            {
                bool hasRemovableTrait = true;
                while (hasRemovableTrait)
                {
                    List<Trait> removableTraits = new List<Trait>();
                    foreach (Trait t in this.pawn.story.traits.allTraits)
                    {
                        if (!TraitModExtensionUtility.IsExciseTraitExempt(t.def, true) && !t.Suppressed && t.sourceGene == null)
                        {
                            removableTraits.Add(t);
                        }
                    }
                    if (removableTraits.Count == 0)
                    {
                        hasRemovableTrait = false;
                    }
                    else
                    {
                        Trait toRemove = removableTraits.RandomElement();
                        this.hiddenTraits.Add(toRemove);
                        this.pawn.story.traits.RemoveTrait(toRemove);
                        foreach (Trait t in this.pawn.story.traits.allTraits)
                        {
                            if (t.suppressedByTrait && toRemove.def.ConflictsWith(t))
                            {
                                t.suppressedByTrait = false;
                            }
                        }
                    }
                }
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look<Trait>(ref this.hiddenTraits, "hiddenTraits", LookMode.Deep, Array.Empty<object>());
        }
        public List<Trait> hiddenTraits = new List<Trait>();
    }
}
