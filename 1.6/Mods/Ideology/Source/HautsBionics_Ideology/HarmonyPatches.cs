using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

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
        //archotech worship drives (or variants thereof, from Anomaly or Archotech Expanded Prosthetics) prevent random conversion attempts from doing anything to their hosts
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
        //AWDs + variants thereof prevent pawns from changing ideoligion to a non-archist one.
        public static bool HVB_SetIdeoPrefix(Pawn_IdeoTracker __instance, Ideo ideo)
        {
            if (Current.ProgramState == ProgramState.Playing)
            {
                Pawn pawn = GetInstanceField(typeof(Pawn_IdeoTracker), __instance, "pawn") as Pawn;
                if (ideo == null || ideo.StructureMeme == null || ideo.StructureMeme != DefDatabase<MemeDef>.GetNamed("Structure_Archist"))
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
        //the layer of pawngen that assigns artificial body parts also has a recursive 20% chance to grant scar kintsugi to Pain is Virtue believers. Maybe they should have graphic data... hm.
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
}
