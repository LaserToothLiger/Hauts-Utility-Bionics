using HautsFramework;
using RimWorld;
using System;
using Verse;

namespace HautsBionics
{
    //radio surfing ear mood boost scales with hearing
    public class ThoughtWorker_RadioSurfingEar : ThoughtWorker_Hediff
    {
        public override float MoodMultiplier(Pawn p)
        {
            return base.MoodMultiplier(p) * p.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
        }
    }
    //inhaling nose is very sensitive to rot stink, filth, and the smells of nature
    public class Thought_GreatInhalation : Thought_Situational
    {
        protected override float BaseMoodOffset
        {
            get
            {
                float num = 0f;
                if (this.pawn.Spawned && this.pawn.Map != null)
                {
                    if (this.pawn.Position.AnyGas(this.pawn.Map, GasType.RotStink))
                    {
                        num -= 5;
                    }
                    if (this.pawn.Position.GetRoom(this.pawn.Map) != null && !this.pawn.Position.GetRoom(this.pawn.Map).PsychologicallyOutdoors)
                    {
                        num += (int)Math.Min(2f * this.pawn.Position.GetRoom(this.pawn.Map).GetStat(RoomStatDefOf.Cleanliness), 0);
                    }
                    else
                    {
                        num -= (Find.WorldGrid[this.pawn.Map.Tile].PrimaryBiome.diseaseMtbDays / 30f);
                    }
                    return num;
                }
                if (this.pawn.Tile != -1)
                {
                    num -= (1 + Find.WorldGrid[this.pawn.Tile].PrimaryBiome.diseaseMtbDays / 30f);
                }
                return num;
            }
        }
    }
    //opinion others have of a pawn with internal perfumist
    public class ThoughtWorker_TheNoseKnows : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn pawn, Pawn other)
        {
            if (!RelationsUtility.PawnsKnowEachOther(pawn, other))
            {
                return false;
            }
            if (other.health.hediffSet.HasHediff(this.def.hediff) && !pawn.health.hediffSet.PartIsMissing(pawn.RaceProps.body.GetPartsWithDef(DefDatabase<BodyPartDef>.GetNamed("Nose")).RandomElement()))
            {
                return ThoughtState.ActiveDefault;
            }
            return false;
        }
    }
}
