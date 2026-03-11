using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace HautsBionics_Anomaly
{
    /*while at max severity, the Archotech Truelife reverts the mutation of its host every "periodicity" ticks, adding a resurrection coma. Another hediff increments its severity over time, so that acts like a cooldown.
     * Does not do anything while the pawn has the Rising hediff, to prevent some kind of error I forget about.
     * Stores "factionAtTimeOfDeath", which is ewisott, so that it can reimpose this after reverting the mutation. This is in case of bullshit like a Death Pall turning the person into an Entities faction member - them returning to life would be
     *   worthless if they didn't also return to their OG faction.
     * revertedMutantDefs: if not specified, it will work on any mutation. If specified, it will only revert mutations on this list.
     * reversionText: displayed in the top left corner when it works, provided the game ShouldSendNotificationAbout that pawn.*/
    public class HediffCompProperties_Truelife : HediffCompProperties
    {
        public HediffCompProperties_Truelife()
        {
            this.compClass = typeof(HediffComp_Truelife);
        }
        public List<MutantDef> revertedMutantDefs;
        public int periodicity;
        public string reversionText;
    }
    public class HediffComp_Truelife : HediffComp
    {
        public HediffCompProperties_Truelife Props
        {
            get
            {
                return (HediffCompProperties_Truelife)this.props;
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            this.factionAtTimeOfDeath = this.Pawn.Faction;
        }
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            this.factionAtTimeOfDeath = this.Pawn.Faction;
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.parent.Severity == this.parent.def.maxSeverity && this.Pawn.IsHashIntervalTick(this.Props.periodicity, delta) && this.Pawn.mutant != null && this.Pawn.mutant.Def != null && !this.Pawn.health.hediffSet.HasHediff(HediffDefOf.Rising) && (this.Props.revertedMutantDefs == null || this.Props.revertedMutantDefs.Contains(this.Pawn.mutant.Def)))
            {
                this.Pawn.mutant.Revert();
                this.Pawn.health.Notify_Resurrected();
                Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.ResurrectionSickness, this.Pawn, null);
                if (!this.Pawn.health.WouldDieAfterAddingHediff(hediff))
                {
                    this.Pawn.health.AddHediff(hediff, null, null, null);
                }
                if (this.factionAtTimeOfDeath != null)
                {
                    this.Pawn.SetFaction(this.factionAtTimeOfDeath);
                }
                this.parent.Severity = this.parent.def.minSeverity;
                if (PawnUtility.ShouldSendNotificationAbout(this.Pawn))
                {
                    Messages.Message(this.Props.reversionText.Translate().CapitalizeFirst().Formatted(this.Pawn.Named("PAWN")).AdjustedFor(this.Pawn, "PAWN", true).Resolve(), this.Pawn, MessageTypeDefOf.NeutralEvent, true);
                }
            }
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look<Faction>(ref this.factionAtTimeOfDeath, "factionAtTimeOfDeath", false);
        }
        public Faction factionAtTimeOfDeath;
    }
}
