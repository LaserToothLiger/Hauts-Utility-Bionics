using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace HautsBionics_Anomaly
{
    /*voidshards do not have downsides if the monolith is inactive or if the void has been disrupted, so at levels 0 and 5 vsdownsides remove themselves.
     * They also require at least one voidshard to populate their "causativeHediffs" List, else they get removed. Logically, once a voidshard stops existing, whatever downsides it causes should stop existing in short order.
     * I have to go back later and make this work with CustomAnomalyPlaystyleActivityLevels from the Framework*/
    public class HediffComp_VoidshardDownside : HediffComp
    {
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.causativeHediffs == null || this.causativeHediffs.Count == 0)
            {
                this.Pawn.health.RemoveHediff(this.parent);
            }
            else
            {
                this.parent.Severity = this.causativeHediffs.Count - 1;
                if (this.Pawn.IsHashIntervalTick(250, delta) && ((Find.Anomaly.Level <= 0 || Find.Anomaly.Level > 5) || (this.Pawn.Faction != null && (this.Pawn.Faction == Faction.OfEntities || this.Pawn.Faction == Faction.OfHoraxCult))))
                {
                    this.Pawn.health.RemoveHediff(this.parent);
                }
            }
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Collections.Look<Hediff>(ref this.causativeHediffs, "causativeHediffs", LookMode.Deep, Array.Empty<object>());
        }
        public List<Hediff> causativeHediffs;
    }
    /*There are two Corruption downsides, each of which adds a distinct, unique disease ("hediff" field). The mechanics are very similar though - it gets inflicted on an MTB, they can't be applied to Homunculi creepjoiners,
     * and anything mechanism that can partly or totally immunize you to a disease could immunize you to them.
     * mtbLossPerExtraSeverity: per each point of Severity the hediff has, subtract this much from the MTB*/
    public class HediffCompProperties_Corruptor : HediffCompProperties
    {
        public HediffCompProperties_Corruptor()
        {
            this.compClass = typeof(HediffComp_Corruptor);
        }
        public float corruptionMtbDays = 50f;
        public float mtbLossPerExtraSeverity;
        public HediffDef hediff;
    }
    public class HediffComp_Corruptor : HediffComp
    {
        public HediffCompProperties_Corruptor Props
        {
            get
            {
                return (HediffCompProperties_Corruptor)this.props;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(150, delta) && (this.Pawn.story == null || !this.Pawn.story.traits.HasTrait(HVBAnomalyDefOf.HVB_HomunculusTrait)) && this.Pawn.health.immunity.DiseaseContractChanceFactor(this.Props.hediff, this.parent.Part) > 0.001f && Rand.MTBEventOccurs(Math.Max(this.Props.corruptionMtbDays - (this.Props.mtbLossPerExtraSeverity * this.parent.Severity), 0.001f), 60000f, 150f) && !this.Pawn.health.hediffSet.HasHediff(this.Props.hediff))
            {
                this.Pawn.health.AddHediff(this.Props.hediff, this.parent.Part, null, null);
            }
        }
    }
    /*The Incubator downside creates a random anomaly entity when the host pawn dies. Well, I guess you could make it spawn any pawn kind in the XML, but I've set the list to a variety of anomaly entities because obviously.
     * Each PKD in the possiblePawnKinds has an equal chance; the int keys are minimum thresholds of Severity that must be reached in order for that pawnkind to be added to the pool.
     * letterLabel|Text: the label and text of the red letter that gets sent to you when the pawn is created
     * Like other downsides, the Homunculus creepjoiner trait prevents its effects.
     * Spawned pawns are stunned on creation, and they're always of the Entities faction. The game will flip you off in the error log once if you make a humanlike of this faction, but there's nothing actually wrong with doing so
     * (I think) so you could totally make it spawn like. Ancient soldiers. Or yttakin. I think that would be really funny, angry-ass chewbacca pop out of a corpse.
     * Random roll is done at moment of death, so you can savescum for a different spawned pawn.*/
    public class HediffCompProperties_Incubator : HediffCompProperties
    {
        public HediffCompProperties_Incubator()
        {
            this.compClass = typeof(HediffComp_Incubator);
        }
        [MustTranslate]
        public string letterLabel;
        [MustTranslate]
        public string letterText;
        public Dictionary<PawnKindDef, int> possiblePawnKinds;
    }
    public class HediffComp_Incubator : HediffComp
    {
        public HediffCompProperties_Incubator Props
        {
            get
            {
                return (HediffCompProperties_Incubator)this.props;
            }
        }
        public override void Notify_PawnKilled()
        {
            base.Notify_PawnKilled();
            if (this.Pawn.story == null || !this.Pawn.story.traits.HasTrait(HVBAnomalyDefOf.HVB_HomunculusTrait))
            {
                List<PawnKindDef> couldSpawn = new List<PawnKindDef>();
                foreach (PawnKindDef pkd in this.Props.possiblePawnKinds.Keys)
                {
                    if (this.Props.possiblePawnKinds.TryGetValue(pkd) <= this.parent.Severity + 1f)
                    {
                        couldSpawn.Add(pkd);
                    }
                }
                if (couldSpawn.Count > 0)
                {
                    PawnKindDef toSpawn = couldSpawn.RandomElement();
                    Pawn pawn = this.parent.pawn;
                    Pawn pawn2 = PawnGenerator.GeneratePawn(new PawnGenerationRequest(toSpawn, Faction.OfEntities, PawnGenerationContext.NonPlayer, -1, false, false, false, true, false, 1f, false, true, false, true, true, false, false, false, false, 0f, 0f, null, 1f, null, null, null, null, null, new float?(0f), new float?(0f), null, null, null, null, null, false, false, false, false, null, null, null, null, null, 0f, DevelopmentalStage.Adult, null, null, null, false, false, false, -1, 0, false));
                    GenSpawn.Spawn(pawn2, CellFinder.StandableCellNear(pawn.Position, pawn.Map, 2f, null), pawn.Map, WipeMode.Vanish);
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
        }
    }
    /*The Insinuation downside randomly generates stacks of a thought (nullified by the Homunculus trait, ofc) that scales with anomalous activity.
     * Also have to make this work with CAPAL.*/
    public class Thought_Subversion : Thought_Memory
    {
        protected override float BaseMoodOffset
        {
            get
            {
                return this.CurStage.baseMoodEffect * Math.Min((Find.Anomaly.Level > 5f ? 1f : Find.Anomaly.Level), 3f);
            }
        }
    }
}
