using RimWorld;
using System.Collections.Generic;
using Verse;

namespace HautsBionics_Ideology
{
    /*the Work Driver is intended as a continuous, micro-free, slightly worse alternative work speed bonus to Work Drive.
     * It doesn't stack with Work Drive, because it removes it periodically, as shown here.*/
    public class Hediff_WorkDriver : Hediff_Level
    {
        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            if (this.pawn.IsHashIntervalTick(250, delta))
            {
                Hediff h = this.pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("WorkDrive"));
                if (h != null)
                {
                    this.pawn.health.RemoveHediff(h);
                }
            }
        }
    }
    /*the archotech worship drive sets its victim er I mean host to an archotech-structure ideology. 70% of the time, this is a preexisting such ideo
     * The rest of the time, it generates it from scratch.*/
    public class Hediff_ArchoWorship : Hediff_Implant
    {
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (this.pawn.ideo != null && this.pawn.ideo.Ideo != null && this.pawn.ideo.Ideo.memes != null && !this.pawn.Ideo.classicMode && this.pawn.ideo.Ideo.StructureMeme != DefDatabase<MemeDef>.GetNamed("Structure_Archist"))
            {
                List<Ideo> archistIdeos = new List<Ideo>();
                foreach (Ideo i in Find.IdeoManager.IdeosListForReading)
                {
                    if (i.StructureMeme == DefDatabase<MemeDef>.GetNamed("Structure_Archist"))
                    {
                        archistIdeos.Add(i);
                    }
                }
                if (archistIdeos.Count > 0 && Rand.Value <= 0.7f)
                {
                    this.pawn.ideo.SetIdeo(archistIdeos.RandomElement<Ideo>());
                }
                else
                {
                    List<MemeDef> disagreedMemes = new List<MemeDef>();
                    if (this.pawn.story != null)
                    {
                        foreach (MemeDef m in DefDatabase<MemeDef>.AllDefsListForReading)
                        {
                            if (m.disagreeableTraits != null && m.disagreeableTraits.Count > 0)
                            {
                                bool addToList = true;
                                foreach (TraitRequirement t in m.disagreeableTraits)
                                {
                                    if (this.pawn.story.traits.HasTrait(t.def))
                                    {
                                        addToList = false;
                                        disagreedMemes.Add(m);
                                        break;
                                    }
                                }
                                if (!addToList)
                                {
                                    continue;
                                }
                            }
                        }
                    }
                    IdeoGenerationParms parms;
                    parms = new IdeoGenerationParms(Faction.OfPlayer.def, false, null, disagreedMemes);
                    Ideo newIdeo = IdeoGenerator.MakeIdeo(DefDatabase<IdeoFoundationDef>.AllDefs.RandomElement<IdeoFoundationDef>());
                    newIdeo.culture = this.pawn.ideo.Ideo.culture;
                    newIdeo.foundation.RandomizePlace();
                    newIdeo.memes.Clear();
                    newIdeo.memes.Add(DefDatabase<MemeDef>.GetNamed("Structure_Archist"));
                    int impact = 0;
                    while (impact < 8)
                    {
                        MemeDef meme = DefDatabase<MemeDef>.GetRandom();
                        if (meme.category == MemeCategory.Normal && !disagreedMemes.Contains(meme))
                        {
                            newIdeo.memes.Add(meme);
                        }
                        impact += meme.impact;
                        if (Rand.Value <= 0.55f)
                        {
                            break;
                        }
                    }
                    newIdeo.SortMemesInDisplayOrder();
                    newIdeo.classicExtraMode = parms.classicExtra;
                    IdeoFoundation_Deity ideoFoundation_Deity;
                    if ((ideoFoundation_Deity = (newIdeo.foundation as IdeoFoundation_Deity)) != null)
                    {
                        ideoFoundation_Deity.GenerateDeities();
                    }
                    newIdeo.foundation.GenerateTextSymbols();
                    newIdeo.foundation.GenerateLeaderTitle();
                    newIdeo.foundation.RandomizeIcon();
                    newIdeo.foundation.RandomizePrecepts(true, parms);
                    newIdeo.RegenerateDescription(true);
                    newIdeo.foundation.RandomizeStyles();
                    this.pawn.ideo.SetIdeo(newIdeo);
                    Find.IdeoManager.Add(newIdeo);
                }
                if (PawnUtility.ShouldSendNotificationAbout(this.pawn))
                {
                    Messages.Message("HVB_ArchoWorshipIdeoChange".Translate().CapitalizeFirst().Formatted(this.pawn.Name.ToStringShort, this.pawn.ideo.Ideo.name.CapitalizeFirst()), this.pawn, MessageTypeDefOf.NeutralEvent, true);
                }
            }
        }
    }
}
