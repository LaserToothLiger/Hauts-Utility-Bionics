using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace HautsBionics_Ideology
{
    [StaticConstructorOnStartup]
    public static class HautsBionics_Ideology
    {
        private static readonly Type patchType = typeof(HautsBionics_Ideology);
        static HautsBionics_Ideology()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.hautsbionics.ideology");
            harmony.Patch(AccessTools.Method(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.IdeoConversionAttempt)),
                          prefix: new HarmonyMethod(patchType, nameof(HVB_IdeoConversionAttemptPrefix)));
            harmony.Patch(AccessTools.Method(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.SetIdeo)),
                          prefix: new HarmonyMethod(patchType, nameof(HVB_SetIdeoPrefix)));
            harmony.Patch(AccessTools.Method(typeof(PawnTechHediffsGenerator), nameof(PawnTechHediffsGenerator.GenerateTechHediffsFor)),
                          postfix: new HarmonyMethod(patchType, nameof(HVBI_GenerateTechHediffsForPostfix)));
        }
        internal static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
        public static bool HVB_IdeoConversionAttemptPrefix(Pawn_IdeoTracker __instance, Ideo initiatorIdeo)
        {
            Pawn pawn = GetInstanceField(typeof(Pawn_IdeoTracker), __instance, "pawn") as Pawn;
            if (initiatorIdeo.StructureMeme != DefDatabase<MemeDef>.GetNamed("Structure_Archist"))
            {
                foreach (Hediff h in pawn.health.hediffSet.hediffs)
                {
                    if (h is Hediff_ArchoWorship)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        public static bool HVB_SetIdeoPrefix(Pawn_IdeoTracker __instance, Ideo ideo)
        {
            if (Current.ProgramState == ProgramState.Playing)
            {
                Pawn pawn = GetInstanceField(typeof(Pawn_IdeoTracker), __instance, "pawn") as Pawn;
                if (ideo != null && ideo.StructureMeme != null && ideo.StructureMeme != DefDatabase<MemeDef>.GetNamed("Structure_Archist"))
                {
                    foreach (Hediff h in pawn.health.hediffSet.hediffs)
                    {
                        if (h is Hediff_ArchoWorship)
                        {
                            __instance.OffsetCertainty(1000f);
                            return false;
                        }
                    }
                }
            }
            return true;
        }
        public static void HVBI_GenerateTechHediffsForPostfix(Pawn pawn)
        {
            if (Rand.Value <= 0.2f && pawn.ideo != null && pawn.ideo.Ideo != null && pawn.ideo.Ideo.HasMeme(DefDatabase<MemeDef>.GetNamedSilentFail("PainIsVirtue")))
            {
                IEnumerable<RecipeDef> source = from x in DefDatabase<RecipeDef>.AllDefs
                                                where x.workerClass == typeof(Recipe_ScarKintsugi) && pawn.def.AllRecipes.Contains(x)
                                                select x;
                if (source.Any<RecipeDef>())
                {
                    RecipeDef recipeDef = source.RandomElement<RecipeDef>();
                    if (recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).Any<BodyPartRecord>())
                    {
                        recipeDef.Worker.ApplyOnPawn(pawn, recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).RandomElement<BodyPartRecord>(), null, new List<Thing>(), null);
                        HautsBionics_Ideology.HVBI_GenerateTechHediffsForPostfix(pawn);
                    }
                }
            }
        }
    }
    [DefOf]
    public static class HVBDefOf_Ideology
    {
        static HVBDefOf_Ideology()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HVBDefOf_Ideology));
        }
        public static HediffDef HVB_ArchotechWorshipDrive;

        public static AbilityDef HVB_ArchoConvert;
    }
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
            }
        }
    }
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
    public class Hediff_WorkDriver : Hediff_Level
    {
        public override void PostTick()
        {
            base.PostTick();
            if (this.pawn.IsHashIntervalTick(250))
            {
                Hediff h = this.pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("WorkDrive"));
                if (h != null)
                {
                    this.pawn.health.RemoveHediff(h);
                }
            }
        }
    }
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
                } else {
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
        /*public override void PostRemoved()
        {
            base.PostRemoved();
            if (this.pawn.SpawnedParentOrMe != null)
            {
                GenSpawn.Spawn(this.def.spawnThingOnRemoved, this.pawn.SpawnedParentOrMe.Position, this.pawn.SpawnedParentOrMe.Map, WipeMode.Vanish);
            } else if (this.pawn.IsCaravanMember()) {
                Thing thing = ThingMaker.MakeThing(this.def.spawnThingOnRemoved, null);
                this.pawn.GetCaravan().AddPawnOrItem(thing, false);
            }
        }*/
    }
}
