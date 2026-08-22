using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VEF.Abilities;
using Verse;
using Verse.AI;

namespace HautsBionics
{
    [StaticConstructorOnStartup]
    public static class HautsBionics
    {
        private static readonly Type patchType = typeof(HautsBionics);
        static HautsBionics()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.hautsbionics.main");
            harmony.Patch(AccessTools.Method(typeof(ThingSetMaker), nameof(ThingSetMaker.Generate), new[] { typeof(ThingSetMakerParams) }),
                          postfix: new HarmonyMethod(patchType, nameof(HVB_ThingSetMaker_GeneratePostfix)));
            MethodInfo methodInfo = typeof(PawnTechHediffsGenerator).GetMethod("InstallPart", BindingFlags.NonPublic | BindingFlags.Static);
            harmony.Patch(methodInfo,
                          prefix: new HarmonyMethod(patchType, nameof(HVB_InstallPartPrefix)));
            harmony.Patch(AccessTools.Method(typeof(PawnTechHediffsGenerator), nameof(PawnTechHediffsGenerator.GenerateTechHediffsFor)),
                          postfix: new HarmonyMethod(patchType, nameof(HVB_GenerateTechHediffsForPostfix)));
            harmony.Patch(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RestorePart)),
                          postfix: new HarmonyMethod(patchType, nameof(HVB_RestorePartPostfix)));
            harmony.Patch(AccessTools.Method(typeof(ArmorUtility), nameof(ArmorUtility.GetPostArmorDamage)),
                          postfix: new HarmonyMethod(patchType, nameof(HVB_GetPostArmorDamagePostfix)));
            harmony.Patch(AccessTools.Method(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith)),
                          prefix: new HarmonyMethod(patchType, nameof(HVB_TryInteractWithPrefix)));
            harmony.Patch(AccessTools.Method(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState)),
                          postfix: new HarmonyMethod(patchType, nameof(HVB_TryStartMentalStatePostfix)));
            harmony.Patch(AccessTools.Method(typeof(FoodUtility), nameof(FoodUtility.ThoughtsFromIngesting)),
                          prefix: new HarmonyMethod(patchType, nameof(HVB_ThoughtsFromIngestingPrefix)));
            MethodInfo methodInfo2 = typeof(ImmunityHandler).GetMethod("AnyHediffMakesFullyImmuneTo", BindingFlags.Instance | BindingFlags.NonPublic);
            harmony.Patch(methodInfo2,
                          postfix: new HarmonyMethod(patchType, nameof(HVB_AnyHediffMakesFullyImmuneToPostfix)));
            methodInfo = typeof(PawnFlyer).GetMethod("LandingEffects", BindingFlags.NonPublic | BindingFlags.Instance);
            harmony.Patch(methodInfo,
                          postfix: new HarmonyMethod(patchType, nameof(HVB_LandingEffectsPostfix)));
            methodInfo = typeof(AbilityPawnFlyer).GetMethod("LandingEffects", BindingFlags.NonPublic | BindingFlags.Instance);
            if (ModsConfig.OdysseyActive)
            {
                MethodInfo methodInfoO1 = typeof(WorldComponent_GravshipController).GetMethod("LandingEnded", BindingFlags.NonPublic | BindingFlags.Instance);
                harmony.Patch(methodInfoO1,
                              prefix: new HarmonyMethod(patchType, nameof(HVBLandingEndedPrefix)));
                MethodInfo methodInfoO2 = typeof(IngestionOutcomeDoer_Psilocap).GetMethod("DoIngestionOutcomeSpecial", BindingFlags.NonPublic | BindingFlags.Instance);
                harmony.Patch(methodInfoO2,
                              prefix: new HarmonyMethod(patchType, nameof(HVB_DoIngestionOutcomeSpecialPrefix)));
            }
            Log.Message("HVB_Initialize".Translate().CapitalizeFirst());
        }
        internal static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
        /*handles the mod setting that multiplies the number of bionics sold. As you can see, it only works on the conventional StockGenerator_Tag, so there may be mods out there which populate trader stocks with bionics via some other means
         * or using some other tags entirely which would therefore not be affected. This is sufficiently robust that I don't expect it to be an issue, though.*/
        public static void HVB_ThingSetMaker_GeneratePostfix(ref List<Thing> __result, ThingSetMakerParams parms)
        {
            if (parms.traderDef != null && parms.traderDef.stockGenerators != null)
            {
                foreach (StockGenerator sg in parms.traderDef.stockGenerators)
                {
                    if (sg is StockGenerator_Tag sgt && (sgt.tradeTag.Equals("TechHediff") || sgt.tradeTag.Equals("Bionic") || sgt.tradeTag.Equals("ImplantEmpireCommon") || sgt.tradeTag.Equals("ImplantEmpireRoyal")))
                    {
                        for (int i = 0; i < Math.Floor(HVB_Mod.settings.bionicsForSaleMultiplier) - 1; i++)
                        {
                            Faction makingFaction = parms.makingFaction;
                            int forTile;
                            if (parms.tile != null)
                            {
                                forTile = parms.tile.Value;
                            } else if (Find.AnyPlayerHomeMap != null) {
                                forTile = Find.AnyPlayerHomeMap.Tile;
                            } else if (Find.CurrentMap != null) {
                                forTile = Find.CurrentMap.Tile;
                            } else {
                                forTile = -1;
                            }
                            foreach (Thing thing in sgt.GenerateThings(forTile, makingFaction))
                            {
                                if (!thing.def.tradeability.TraderCanSell())
                                {
                                    Log.Error(string.Concat(new object[]
                                    {
                                    parms.traderDef,
                                    " generated carrying ",
                                    thing,
                                    " which can't be sold by traders. Ignoring..."
                                    }));
                                } else {
                                    thing.PostGeneratedForTrader(parms.traderDef, forTile, makingFaction);
                                    __result.Add(thing);
                                }
                            }
                        }
                    }
                }
            }
        }
        //psycasters cannot be generated with psychic foil barriers. Instead, the foil barrier gets replaced by one of the other skull 'replacements'.
        public static void HVB_InstallPartPrefix(Pawn pawn, ref ThingDef partDef)
        {
            if (pawn.GetMainPsylinkSource() != null)
            {
                if (partDef == HVBThingDefOf.HVB_PsychicFoilBarrier)
                {
                    if (ModsConfig.IdeologyActive && Rand.Value <= 0.25f && !pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_Gaucrown))
                    {
                        partDef = HVBThingDefOf.HVB_Gaucrown;
                    } else if (Rand.Value <= 0.83f) {
                        if (!pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_HardheadProtector))
                        {
                            partDef = HVBThingDefOf.HVB_HardheadProtector;
                        } else if (!pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_PanoptesSkull)) {
                            partDef = HVBThingDefOf.HVB_PanoptesSkull;
                        }
                    } else if (!pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_PanoptesSkull)) {
                        partDef = HVBThingDefOf.HVB_PanoptesSkull;
                    }
                }
            }
        }
        /*Has several effects. In order of appearance:
         * -cleans up rare, shitty instances where pawngen will slap an AddOn on a missing part. No! Get it out of there!
         * -adds certain hediffs that the ordinary GenerateTechHediffs process would not be able to dynamically assign to pawns on its own, because those hediffs lack "itemized forms" that it would detect when scanning for options to install on a pawn.
         *   (you can see a vanilla example in how the addition of peg legs to pawns is hardcoded, whereas no actual body part replacement with an itemized form needs this treatment)
         *   >psycasters have a 10% chance to be psychically trepanned. Non-psycasters have a 0.4% chance instead
         *   >pawns eligible for Imperial implants can gain one warlock bionic, or possibly the Archotech Cerebropearl if their psylink level is really high (but even then, its chance will be diluted by all the warlock schlep)
         *     Does not assign more than one warlock bionic to any given pawn because it would make their health suck to such an extreme degree
         *   >pawns who don't need to speak so good (neither traders nor possessed of a royal title) can have a tracheal intubation if their breathing is less than healthy. It's likelier the worse their breathing is
         *   >Pawns who aren't faction leaders and aren't of pre-industrial factions have a tiny chance to be neurally resocialized.*/
        public static void HVB_GenerateTechHediffsForPostfix(Pawn pawn)
        {
            for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
            {
                if (pawn.health.hediffSet.hediffs[i] is HediffWithComps hwc && hwc.Part != null && pawn.health.hediffSet.PartIsMissing(hwc.Part))
                {
                    foreach (HediffComp hc in hwc.comps)
                    {
                        if (hc is HediffComp_AddOn)
                        {
                            BodyPartRecord part = hwc.Part;
                            while (pawn.health.hediffSet.PartIsMissing(part))
                            {
                                if (part.parent != null)
                                {
                                    part = part.parent;
                                    if (!pawn.health.hediffSet.PartIsMissing(part))
                                    {
                                        hwc.Part = part;
                                        break;
                                    }
                                } else {
                                    pawn.health.hediffSet.hediffs.RemoveAt(i);
                                    hwc.PostRemoved();
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            if (pawn.RaceProps.Humanlike)
            {
                if (pawn.GetMainPsylinkSource() != null)
                {
                    if (Rand.Value <= 0.1f)
                    {
                        HautsBionics.HVB_DoSurgery(pawn, HVBDefOf.HVB_PerformPsychicTrepanation);
                    }
                    if (Rand.Value <= 0.17f && pawn.kindDef.techHediffsTags != null && pawn.kindDef.techHediffsTags.Contains("ImplantEmpireRoyal"))
                    {
                        int max = pawn.kindDef.techHediffsMaxAmount;
                        foreach (Hediff h in pawn.health.hediffSet.hediffs)
                        {
                            if (h is Hediff_Implant)
                            {
                                max--;
                            }
                        }
                        if (max > 0 || (max == 0 && Rand.Value <= 0.5f))
                        {
                            List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefs.Where((ThingDef x) => x.isTechHediff && x.techHediffsTags != null && x.techHediffsTags.Contains("HVB_Warlock") && (!pawn.WorkTagIsDisabled(WorkTags.Violent) || !x.violentTechHediff) && (pawn.kindDef.techHediffsDisallowTags == null || !pawn.kindDef.techHediffsDisallowTags.Any((string tag) => x.techHediffsTags.Contains(tag)))).ToList();
                            if ((float)pawn.GetPsylinkLevel() / (float)pawn.GetMaxPsylinkLevel() >= 0.8f)
                            {
                                List<ThingDef> allDefs2 = DefDatabase<ThingDef>.AllDefs.Where((ThingDef x) => x.isTechHediff && x.techHediffsTags != null && x.techHediffsTags.Contains("HVB_SuperPsychic") && (!pawn.WorkTagIsDisabled(WorkTags.Violent) || !x.violentTechHediff) && (pawn.kindDef.techHediffsDisallowTags == null || !pawn.kindDef.techHediffsDisallowTags.Any((string tag) => x.techHediffsTags.Contains(tag)))).ToList();
                                allDefs.AddRange(allDefs2);
                            }
                            if (allDefs.Any<ThingDef>())
                            {
                                ThingDef thingDef = allDefs.RandomElement<ThingDef>();
                                IEnumerable<RecipeDef> source = from x in DefDatabase<RecipeDef>.AllDefs
                                                                where x.IsIngredient(thingDef) && pawn.def.AllRecipes.Contains(x)
                                                                select x;
                                if (source.Any<RecipeDef>())
                                {
                                    RecipeDef recipeDef = source.RandomElement<RecipeDef>();
                                    if (recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).Any<BodyPartRecord>())
                                    {
                                        recipeDef.Worker.ApplyOnPawn(pawn, recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).RandomElement<BodyPartRecord>(), null, new List<Thing>(), null);
                                        return;
                                    }
                                } else {
                                    CompProperties_UseEffectInstallImplant compProperties = thingDef.GetCompProperties<CompProperties_UseEffectInstallImplant>();
                                    if (compProperties != null)
                                    {
                                        List<BodyPartRecord> partsWithDef = pawn.RaceProps.body.GetPartsWithDef(compProperties.bodyPart);
                                        pawn.health.AddHediff(compProperties.hediffDef, partsWithDef.NullOrEmpty<BodyPartRecord>() ? null : partsWithDef.RandomElement<BodyPartRecord>(), null, null);
                                    }
                                }
                            }
                        }
                    }
                } else if (Rand.Value <= 0.004f) {
                    HautsBionics.HVB_DoSurgery(pawn, HVBDefOf.HVB_PerformPsychicTrepanation);
                }
                if (pawn.trader == null && !pawn.kindDef.trader && pawn.kindDef.titleRequired == null && pawn.kindDef.minTitleRequired == null && (pawn.kindDef.royalTitleChance <= 0f || pawn.kindDef.titleSelectOne == null))
                {
                    if (Rand.Value <= (1 - pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing)))
                    {
                        HautsBionics.HVB_DoSurgery(pawn, HVBDefOf.HVB_InstallTrachealIntubation);
                    }
                    float resocChance = 0f;
                    if (pawn.Faction != null)
                    {
                        if (pawn.Faction.leader == pawn)
                        {
                            resocChance = 0f;
                        } else if (pawn.Faction.def.techLevel == TechLevel.Industrial) {
                            resocChance = 0.005f;
                        } else if (pawn.Faction.def.techLevel > TechLevel.Industrial) {
                            resocChance = 0.02f;
                        }
                    } else {
                        resocChance = 0.0025f;
                    }
                    if (Rand.Value <= resocChance)
                    {
                        HautsBionics.HVB_DoSurgery(pawn, HVBDefOf.HVB_InstallNeuralResocialization);
                    }
                }
            }
        }
        //ensures that RestorePart also gets rid of any add-ons
        public static void HVB_RestorePartPostfix(Pawn_HealthTracker __instance, BodyPartRecord part)
        {
            Pawn pawn = GetInstanceField(typeof(Pawn_HealthTracker), __instance, "pawn") as Pawn;
            if (!PawnGenerator.IsBeingGenerated(pawn))
            {
                for (int i = __instance.hediffSet.hediffs.Count - 1; i >= 0; i--)
                {
                    Hediff hediff = __instance.hediffSet.hediffs[i];
                    if (hediff.Part == part && hediff.TryGetComp<HediffComp_AddOn>() != null)
                    {
                        __instance.hediffSet.hediffs.RemoveAt(i);
                        hediff.PostRemoved();
                    }
                }
            }
        }
        //localized damage reduction effect of center mass laminar and hardhead protector. That's right, they are hardcoded
        public static void HVB_GetPostArmorDamagePostfix(ref float __result, Pawn pawn, BodyPartRecord part)
        {
            if ((part.IsInGroup(BodyPartGroupDefOf.FullHead) || part.IsInGroup(BodyPartGroupDefOf.UpperHead)) && pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_HardheadProtector))
            {
                __result *= 0.7f;
            }
            if (part.IsInGroup(BodyPartGroupDefOf.Torso) && pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_CenterMassLaminar))
            {
                __result *= 0.8f;
            }
        }
        //cogno-censor prevents the issuing of slights or insults, at the expense of a mood penalty
        public static bool HVB_TryInteractWithPrefix(ref bool __result, Pawn_InteractionsTracker __instance, ref InteractionDef intDef)
        {
            Pawn pawn = GetInstanceField(typeof(Pawn_InteractionsTracker), __instance, "pawn") as Pawn;
            if ((intDef == InteractionDefOf.Insult || intDef == DefDatabase<InteractionDef>.GetNamed("Slight")) && pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_CognoCensor))
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(HVBDefOf.HVB_CognoCensorship);
                __result = true;
                return false;
            }
            return true;
        }
        /*tempered heart's chance to stop a mental state (doesn't work on fleeing the map, as Strategic Advances to the Rear are not really mental breaks)
         * also, 10% chance on any mental break to cause neural resocialization to break down. If this happens to you and you didn't do it on purpose, how.*/
        public static void HVB_TryStartMentalStatePostfix(ref bool __result, MentalStateHandler __instance, MentalStateDef stateDef)
        {
            Pawn pawn = GetInstanceField(typeof(MentalStateHandler), __instance, "pawn") as Pawn;
            if (__result && stateDef != MentalStateDefOf.PanicFlee)
            {
                if (stateDef != MentalStateDefOf.SocialFighting && Rand.Chance(0.15f) && pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_TemperedHeart))
                {
                    if (pawn.MentalState != null)
                    {
                        pawn.MentalState.RecoverFromState();
                        return;
                    }
                }
                if (Rand.Value <= 0.1f && pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_NeuralResocialization))
                {
                    pawn.health.RemoveHediff(pawn.health.hediffSet.GetFirstHediffOfDef(HVBDefOf.HVB_NeuralResocialization));
                    if (PawnUtility.ShouldSendNotificationAbout(pawn))
                    {
                        Messages.Message("HVB_ResocBroke".Translate().CapitalizeFirst().Formatted(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true).Resolve(), pawn, MessageTypeDefOf.NegativeHealthEvent, true);
                    }
                }
            }
        }
        //purifier jaw prevents any food thoughts other than "that was a PERSON"
        public static bool HVB_ThoughtsFromIngestingPrefix(ref List<FoodUtility.ThoughtFromIngesting> __result, Pawn ingester, Thing foodSource, ThingDef foodDef)
        {
            if (ingester.health.hediffSet.HasHediff(HVBDefOf.HVB_PurifierJaw) && FoodUtility.GetMeatSourceCategoryFromCorpse(foodSource) != MeatSourceCategory.Humanlike)
            {
                List<FoodUtility.ThoughtFromIngesting> ingestThoughts = new List<FoodUtility.ThoughtFromIngesting>();
                __result = ingestThoughts;
                return false;
            }
            return true;
        }
        //archotech neutralizer provides immunity to all diseases
        public static void HVB_AnyHediffMakesFullyImmuneToPostfix(ref bool __result, ImmunityHandler __instance, ref Hediff sourceHediff)
        {
            if (__instance.pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_ArchotechNeutralizer))
            {
                sourceHediff = __instance.pawn.health.hediffSet.GetFirstHediffOfDef(HVBDefOf.HVB_ArchotechNeutralizer);
                __result = true;
            }
        }
        //Earthshaker effects
        public static void HVB_LandingEffectsPostfix(PawnFlyer __instance)
        {
            if (__instance.FlyingPawn != null)
            {
                Pawn pawn = __instance.FlyingPawn;
                if (pawn.abilities != null)
                {
                    foreach (RimWorld.Ability a in pawn.abilities.abilities)
                    {
                        if (a.def == HVBDefOf.HVB_ToggleEarthshaker)
                        {
                            CompAbilityEffect_Toggle caet = a.CompOfType<CompAbilityEffect_Toggle>();
                            if (caet != null && !caet.enabled)
                            {
                                return;
                            }
                        }
                    }
                }
                float earthshakerCount = 0f;
                float bonusRadius = 0f;
                foreach (Hediff h in pawn.health.hediffSet.hediffs)
                {
                    if (h is HediffWithComps hwc)
                    {
                        HediffComp_Earthshaker es = hwc.TryGetComp<HediffComp_Earthshaker>();
                        if (es != null)
                        {
                            earthshakerCount += es.Props.shockwavePower;
                            bonusRadius += es.Props.bonusRadiusPerSeverity * hwc.Severity;
                        }
                    }
                }
                if (earthshakerCount > 0f)
                {
                    DamageInfo dinfo = new DamageInfo(DamageDefOf.Crush, 35f * pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving), 0f, -1f, pawn);
                    float radius = (3f * pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving)) + bonusRadius;
                    foreach (Thing thing in GenRadial.RadialDistinctThingsAround(__instance.Position, __instance.Map, radius, true))
                    {
                        if (thing is Pawn p)
                        {
                            p.stances.stunner.StunFor((int)(250 * earthshakerCount), pawn);
                        }
                        else if (thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Plant)
                        {
                            thing.TakeDamage(dinfo);
                        }
                    }
                    GenExplosion.DoExplosion(__instance.Position, __instance.Map, radius, DamageDefOf.Smoke, null, -1, -1f, null, null, null, null, null, 0f, 1, null, null, 255, false, null, 0f, 1, 0f, false, null, null, null, true, 1f, 0f, true, null, 1f);
                }
            }
        }
        /*effects of gravship landing on various bionics
         * -Hediff_ImplantGravNausea: as name indicates, get grav nausea. Could theoretically be a DME, but that would actually be infinitesimally less performant because of stuff that iterates thru hediffs' DMEs
         * -GravitonPart: either adds the specified hediff to the pawn, or sets the GravitonPart hediff itself to max severity*/
        public static void HVBLandingEndedPrefix(WorldComponent_GravshipController __instance)
        {
            Gravship gship = GetInstanceField(typeof(WorldComponent_GravshipController), __instance, "gravship") as Gravship;
            if (gship != null)
            {
                List<Pawn> toAddTo = new List<Pawn>();
                foreach (Pawn pawn in gship.Pawns)
                {
                    for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
                    {
                        Hediff h = pawn.health.hediffSet.hediffs[i];
                        if (h is Hediff_ImplantGravNausea)
                        {
                            toAddTo.Add(pawn);
                        }
                        GravitonPart gp = h.def.GetModExtension<GravitonPart>();
                        if (gp != null) {
                            if (gp.hediff != null)
                            {
                                BodyPartRecord bpr = gp.addToSameBodyPart ? h.Part : null;
                                Hediff hediff = HediffMaker.MakeHediff(gp.hediff, pawn, bpr);
                                hediff.Severity = gp.hediff.initialSeverity * gp.severityMultiplier;
                                pawn.health.AddHediff(hediff, bpr);
                            } else {
                                h.Severity = h.def.maxSeverity;
                            }
                        }
                    }
                }
                foreach (Pawn p in toAddTo)
                {
                    p.health.AddHediff(HediffDefOf.GravNausea, null, null, null);
                }
            }
        }
        //psilocap filter overrides the normal function of psilocap ingestion. It might be better to do this as a __state prefix/postfix that sets chanceBreakdown to 0 and restores it afterward...
        public static bool HVB_DoIngestionOutcomeSpecialPrefix(IngestionOutcomeDoer_Psilocap __instance, Pawn pawn, Thing ingested)
        {
            if (pawn.health.hediffSet.HasHediff(HVBDefOf.HVB_PsilocapFilter))
            {
                if (Rand.Value < __instance.chanceInspiration)
                {
                    pawn.mindState.inspirationHandler.TryStartInspiration(InspirationDefOf.Inspired_Creativity, "LetterInspirationBeginPsilocap".Translate(), true);
                }
                return false;
            }
            return true;
        }

        //not a patch, just a method that gets reused multiple times up above
        public static void HVB_DoSurgery(Pawn pawn, RecipeDef recipeDef)
        {
            if (recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).Any<BodyPartRecord>() && (recipeDef.addsHediff == null || pawn.health.hediffSet.PainTotal + recipeDef.addsHediff.stages[recipeDef.addsHediff.StageAtSeverity(recipeDef.addsHediff.initialSeverity)].painOffset < pawn.GetStatValue(StatDefOf.PainShockThreshold)))
            {
                recipeDef.Worker.ApplyOnPawn(pawn, recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).RandomElement<BodyPartRecord>(), null, new List<Thing>(), null);
            }
        }
    }
}
