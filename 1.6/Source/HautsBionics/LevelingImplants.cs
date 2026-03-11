using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HautsBionics
{
    /*In unmodded RimWorld, Hediff_Level hediffs are added via using an item e.g. psylinks or various mechanitor implants).
     * These recipes are for adding Hediff_Levels via bionic surgery. You can install them like normal the first time, and then continue installing them to gain levels (up to their max level, obv). You can also remove one level at a time.
     * Recipe_InstallArtificialLevellingBodyPart is currently unused by anything. You used to have to do surgery for mechanityrant vertebral links, but testing revealed: that was stupid.
     * The recipe worker is still here in case I find a use for it later.*/
    public class Recipe_InstallLevellingImplant : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(recipe.addsHediff);
            if (h != null && h is Hediff_Level hl && hl.level >= hl.def.maxSeverity)
            {
                return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => hl.level < hl.def.maxSeverity);
            }
            return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => !pawn.health.hediffSet.hediffs.Any((Hediff x) => x.Part == record && !recipe.CompatibleWithHediff(x.def)));
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
                Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(this.recipe.addsHediff, false);
                if (firstHediffOfDef == null)
                {
                    pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
                }
                else
                {
                    ((Hediff_Level)firstHediffOfDef).ChangeLevel(1);
                }
                if (!PawnGenerator.IsBeingGenerated(pawn) && this.IsViolationOnPawn(pawn, part, Faction.OfPlayer))
                {
                    base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
                }
                if (ModsConfig.IdeologyActive)
                {
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.InstalledProsthetic, billDoer.Named(HistoryEventArgsNames.Doer)), true);
                }
            }
        }
    }
    public class Recipe_InstallArtificialLevellingBodyPart : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(recipe.addsHediff);
            if (h != null && h is Hediff_Level hl)
            {
                return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, (BodyPartRecord record) => hl.level < hl.def.maxSeverity);
            }
            return MedicalRecipesUtility.GetFixedPartsToApplyOn(recipe, pawn, delegate (BodyPartRecord record)
            {
                IEnumerable<Hediff> source = from x in pawn.health.hediffSet.hediffs
                                             where x.Part == record
                                             select x;
                return (source.Count<Hediff>() != 1 || source.First<Hediff>().def != recipe.addsHediff) && (record.parent == null || pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null).Contains(record.parent)) && (!pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(record) || pawn.health.hediffSet.HasDirectlyAddedPartFor(record));
            });
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(this.recipe.addsHediff, false);
            bool flag = !PawnGenerator.IsBeingGenerated(pawn) && this.IsViolationOnPawn(pawn, part, Faction.OfPlayer);
            if (billDoer != null)
            {
                if (base.CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[]
                {
                    billDoer,
                    pawn
                });
                if (MedicalRecipesUtility.IsClean(pawn, part) && flag && part.def.spawnThingOnRemoved != null)
                {
                    ThoughtUtility.GiveThoughtsForPawnOrganHarvested(pawn, billDoer);
                }
                if (flag)
                {
                    base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
                }
                if (ModsConfig.IdeologyActive)
                {
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.InstalledProsthetic, billDoer.Named(HistoryEventArgsNames.Doer)), true);
                }
            }
            if (firstHediffOfDef == null)
            {
                if (pawn.Map != null)
                {
                    MedicalRecipesUtility.RestorePartAndSpawnAllPreviousParts(pawn, part, pawn.Position, pawn.Map);
                }
                else
                {
                    pawn.health.RestorePart(part, null, true);
                }
                pawn.health.AddHediff(this.recipe.addsHediff, part, null, null);
            }
            else
            {
                ((Hediff_Level)firstHediffOfDef).ChangeLevel(1);
            }
        }
    }
    public class Recipe_RemoveLevellingImplant : Recipe_RemoveImplant
    {
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (pawn.story != null)
            {
                foreach (Trait t in pawn.story.traits.allTraits)
                {
                    if (t.def.HasModExtension<CannotRemoveBionicsFrom>())
                    {
                        return;
                    }
                }
            }
            MedicalRecipesUtility.IsClean(pawn, part);
            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, new object[]
                {
                    billDoer,
                    pawn
                });
                if (!pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null).Contains(part))
                {
                    return;
                }
                Hediff hediff = pawn.health.hediffSet.hediffs.FirstOrDefault((Hediff x) => x.def == this.recipe.removesHediff);
                if (hediff != null)
                {
                    if (hediff.def.spawnThingOnRemoved != null)
                    {
                        GenSpawn.Spawn(hediff.def.spawnThingOnRemoved, billDoer.Position, billDoer.Map, WipeMode.Vanish);
                    }
                    ((Hediff_Level)hediff).ChangeLevel(-1);
                }
            }
            if (this.IsViolationOnPawn(pawn, part, Faction.OfPlayer))
            {
                base.ReportViolation(pawn, billDoer, pawn.HomeFaction, -70, null);
            }
        }
    }
}
