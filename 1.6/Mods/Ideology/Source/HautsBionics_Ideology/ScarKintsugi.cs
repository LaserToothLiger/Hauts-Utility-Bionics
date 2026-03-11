using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HautsBionics_Ideology
{
    /*This makes it so you can't add scar kintsugi to pawns who don't have the Pain is Virtue meme, and permits their application only on body parts that have scars or scarifications
     * The actual process removes a scar or scarification on that body part, and replaces it with the addsHediff of equivalent severity to the removed hediff*/
    public class Recipe_ScarKintsugi : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn.ideo != null && pawn.ideo.Ideo != null && pawn.ideo.Ideo.memes != null && pawn.ideo.Ideo.HasMeme(DefDatabase<MemeDef>.GetNamedSilentFail("PainIsVirtue")))
            {
                List<Hediff> allHediffs = pawn.health.hediffSet.hediffs;
                int num;
                for (int i = 0; i < allHediffs.Count; i = num + 1)
                {
                    if (allHediffs[i].Part != null && allHediffs[i].Part.depth == BodyPartDepth.Outside && allHediffs[i] is Hediff_Injury hi && (allHediffs[i].def == HediffDefOf.Scarification || !(hi is Hediff_ScarKintsugi)) && hi.TryGetComp<HediffComp_GetsPermanent>() != null && hi.TryGetComp<HediffComp_GetsPermanent>().IsPermanent)
                    {
                        yield return allHediffs[i].Part;
                    }
                    num = i;
                }
                yield break;
            }
            yield break;
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
            }
            Hediff_Injury hediff = (Hediff_Injury)pawn.health.hediffSet.hediffs.Find((Hediff x) => x.Part == part && x is Hediff_Injury hi && !(hi is Hediff_ScarKintsugi) && hi.TryGetComp<HediffComp_GetsPermanent>() != null && hi.TryGetComp<HediffComp_GetsPermanent>().IsPermanent);
            if (hediff != null)
            {
                float severity = hediff.Severity;
                pawn.health.RemoveHediff(hediff);
                Hediff kintsugi = HediffMaker.MakeHediff(this.recipe.addsHediff, pawn, part);
                kintsugi.Severity = severity;
                pawn.health.AddHediff(kintsugi, part, null, null);
                kintsugi.Severity = severity;//maybe this will help with the funkiness
            }
            pawn.Drawer.renderer.SetAllGraphicsDirty();
        }
    }
    //scar kintsugi is an injury, but it is always permanent. It's solid metal! ...Don't think too hard about how that's supposed to work in-universe.
    public class Hediff_ScarKintsugi : Hediff_Injury
    {
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (this.TryGetComp<HediffComp_GetsPermanent>() != null)
            {
                this.TryGetComp<HediffComp_GetsPermanent>().IsPermanent = true;
            }
        }
    }
    /*xpath patching of the Scarification precepts makes them also attribute mood and opinion for having scar kintsugi, just like ordinary scarifications except the effect can stack.
     * Scarification: Horrible does not interact with scar kintsugi*/
    public class ThoughtWorker_Precept_ScarKintsugi_Social : ThoughtWorker_Precept_Social
    {
        protected override ThoughtState ShouldHaveThought(Pawn p, Pawn otherPawn)
        {
            int num = 0;
            foreach (Precept precept in p.Ideo.PreceptsListForReading)
            {
                num = Mathf.Max(num, precept.def.requiredScars);
            }
            if (num == 0)
            {
                return ThoughtState.Inactive;
            }
            return this.CountKintsugis(p) < num ? ThoughtState.Inactive : ThoughtState.ActiveAtStage(0);
        }
        public int CountKintsugis(Pawn pawn)
        {
            int num = 0;
            using (List<Hediff>.Enumerator enumerator = pawn.health.hediffSet.hediffs.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current is Hediff_ScarKintsugi)
                    {
                        num++;
                    }
                }
            }
            return num;
        }
    }
    public class ThoughtWorker_Precept_ScarKintsugi : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if (!p.IsColonist)
            {
                return false;
            }
            if (this.CountKintsugis(p) >= p.ideo.Ideo.RequiredScars)
            {
                return this.ProcessedState(0, p);
            }
            return false;
        }
        private ThoughtState ProcessedState(int index, Pawn p)
        {
            if (this.def.stages[index].baseMoodEffect < 0f && this.def.minExpectationForNegativeThought != null && p.MapHeld != null && ExpectationsUtility.CurrentExpectationFor(p.MapHeld).order < this.def.minExpectationForNegativeThought.order)
            {
                return false;
            }
            return ThoughtState.ActiveAtStage(index);
        }
        public int CountKintsugis(Pawn pawn)
        {
            int num = 0;
            using (List<Hediff>.Enumerator enumerator = pawn.health.hediffSet.hediffs.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current is Hediff_ScarKintsugi)
                    {
                        num++;
                    }
                }
            }
            return num;
        }
    }
}
