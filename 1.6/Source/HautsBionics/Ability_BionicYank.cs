using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace HautsBionics
{
    /*Bionic Yank either pulls the caster to the target, or the target to the caster, depending on the target's nature. The mote is the graphical line drawn between the two, and it can be specified in XML since the Anomaly voidshard has a different look.
     * NPC pawns only use on pawns, not items or buildings. Doesn't go on cooldown if you just use it to pull an item to your location, since that's one of the most niche and trivial uses possible.*/
    public class CompProperties_AbilityMrFantastic : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityMrFantastic()
        {
            this.compClass = typeof(CompAbilityEffect_MrFantastic);
        }
        public ThingDef customMote;
    }
    public class CompAbilityEffect_MrFantastic : CompAbilityEffect
    {
        public new CompProperties_AbilityMrFantastic Props
        {
            get
            {
                return (CompProperties_AbilityMrFantastic)this.props;
            }
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return target.Pawn != null;
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.HasThing)
            {
                if (target.Thing is Plant p)
                {
                    if (!p.def.plant.IsTree)
                    {
                        return false;
                    }
                }
                else if (target.Thing.def.category != ThingCategory.Building && target.Thing.def.category != ThingCategory.Pawn && (target.Thing.def.category != ThingCategory.Item || !target.Thing.def.EverHaulable))
                {
                    return false;
                }
            }
            return base.Valid(target, throwMessages);
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Thing != null)
            {
                if (target.Thing.def.category != ThingCategory.Building && target.Thing.def.category != ThingCategory.Plant && MassUtility.FreeSpace(this.parent.pawn) >= target.Thing.GetStatValue(StatDefOf.Mass))
                {
                    int ticksToDisappear = (Mathf.Max(target.Thing.PositionHeld.DistanceTo(this.parent.pawn.PositionHeld), 1f) / HVBDefOf.HVB_GrabFlyer.pawnFlyer.flightSpeed).SecondsToTicks();
                    this.DoLink(this.parent.pawn, target.Thing, this.parent.pawn.Position, this.parent.pawn.Map, ticksToDisappear);
                    if (!(target.Thing is Pawn))
                    {
                        this.parent.ResetCooldown();
                    }
                }
                else
                {
                    this.DoLink(target.Thing, this.parent.pawn, target.Cell, target.Thing.Map, (Mathf.Max(target.Thing.PositionHeld.DistanceTo(this.parent.pawn.PositionHeld), 1f) / HVBDefOf.HVB_GrabFlyer.pawnFlyer.flightSpeed).SecondsToTicks());
                }
            }
        }
        private void DoLink(Thing other, Thing flyer, IntVec3 destination, Map map, int ticksToDisappear)
        {
            GrabFlyer gFlyer = GrabFlyer.MakeFlyer(HVBDefOf.HVB_GrabFlyer, flyer, other, this.Props.customMote, destination, null, null, false, null, null, default(LocalTargetInfo));
            GenSpawn.Spawn(gFlyer, destination, map, WipeMode.Vanish);
        }
    }
    /*It uses a distinct alternative to a PawnFlyer, which enables the mote to be drawn between the flyer and the other party (regardless of whether the other party is the caster or the target).
     * Instead of parabolically arcing the way jump fliers do, it just has the flier move in a visually straight line.*/
    public class GrabFlyer : Thing, IThingHolder
    {
        public Thing FlyingThing
        {
            get
            {
                if (this.innerContainer.InnerListForReading.Count <= 0)
                {
                    return null;
                }
                return this.innerContainer.InnerListForReading[0];
            }
        }
        public Pawn FlyingPawn
        {
            get
            {
                return this.FlyingThing as Pawn;
            }
        }
        public Thing CarriedThing
        {
            get
            {
                return this.carriedThing;
            }
        }
        public override Vector3 DrawPos
        {
            get
            {
                this.RecomputePosition();
                return this.effectivePos;
            }
        }
        private void RecomputePosition()
        {
            if (this.positionLastComputedTick == this.ticksFlying)
            {
                return;
            }
            this.positionLastComputedTick = this.ticksFlying;
            float num = (float)this.ticksFlying / (float)this.ticksFlightTime;
            float num2 = this.def.pawnFlyer.Worker.AdjustedProgress(num);
            this.effectiveHeight = this.def.pawnFlyer.Worker.GetHeight(num2);
            this.groundPos = Vector3.Lerp(this.startVec, this.DestinationPos, num2);
            Vector3 vector = Altitudes.AltIncVect * this.effectiveHeight;
            Vector3 vector2 = Vector3.forward * (this.def.pawnFlyer.heightFactor * this.effectiveHeight);
            this.effectivePos = this.groundPos + vector + vector2;
            base.Position = this.groundPos.ToIntVec3();
        }
        public ThingOwner GetDirectlyHeldThings()
        {
            return this.innerContainer;
        }
        public GrabFlyer()
        {
            this.innerContainer = new ThingOwner<Thing>(this);
        }
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Effecter effecter = this.flightEffecter;
            if (effecter != null)
            {
                effecter.Cleanup();
            }
            base.Destroy(mode);
        }
        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, this.GetDirectlyHeldThings());
        }
        public Vector3 DestinationPos
        {
            get
            {
                Thing flyingThing = this.FlyingThing;
                return GenThing.TrueCenter(this.destCell, flyingThing.Rotation, flyingThing.def.size, flyingThing.def.Altitude);
            }
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                float num = Mathf.Max(this.flightDistance, 1f) / this.def.pawnFlyer.flightSpeed;
                num = Mathf.Max(num, this.def.pawnFlyer.flightDurationMin);
                this.ticksFlightTime = num.SecondsToTicks();
                this.ticksFlying = 0;
            }
        }
        protected virtual void RespawnPawn()
        {
            Thing flyingThing = this.FlyingThing;
            this.LandingEffects();
            Thing thing;
            this.innerContainer.TryDrop(flyingThing, this.destCell, flyingThing.MapHeld, ThingPlaceMode.Direct, out thing, null, null, false);
            Pawn pawn = flyingThing as Pawn;
            if (((pawn != null) ? pawn.drafter : null) != null)
            {
                pawn.drafter.Drafted = this.pawnWasDrafted;
                pawn.drafter.FireAtWill = this.pawnCanFireAtWill;
            }
            flyingThing.Rotation = base.Rotation;
            if (this.carriedThing != null && this.innerContainer.TryDrop(this.carriedThing, this.destCell, flyingThing.MapHeld, ThingPlaceMode.Direct, out thing, null, null, false) && pawn != null)
            {
                this.carriedThing.DeSpawn(DestroyMode.Vanish);
                if (!pawn.carryTracker.TryStartCarry(this.carriedThing))
                {
                    Log.Error("Could not carry " + this.carriedThing.ToStringSafe<Thing>() + " after respawning flyer pawn.");
                }
            }
            if (pawn != null)
            {
                if (this.jobQueue != null)
                {
                    pawn.jobs.RestoreCapturedJobs(this.jobQueue, true);
                }
                pawn.jobs.CheckForJobOverride(0f);
                if (this.def.pawnFlyer.stunDurationTicksRange != IntRange.Zero)
                {
                    pawn.stances.stunner.StunFor(this.def.pawnFlyer.stunDurationTicksRange.RandomInRange, null, false, false, false);
                }
                if (this.triggeringAbility != null)
                {
                    RimWorld.Ability ability = pawn.abilities.GetAbility(this.triggeringAbility, false);
                    if (((ability != null) ? ability.comps : null) != null)
                    {
                        using (List<AbilityComp>.Enumerator enumerator = ability.comps.GetEnumerator())
                        {
                            while (enumerator.MoveNext())
                            {
                                ICompAbilityEffectOnJumpCompleted compAbilityEffectOnJumpCompleted;
                                if ((compAbilityEffectOnJumpCompleted = enumerator.Current as ICompAbilityEffectOnJumpCompleted) != null)
                                {
                                    compAbilityEffectOnJumpCompleted.OnJumpCompleted(this.startVec.ToIntVec3(), this.target);
                                }
                            }
                        }
                    }
                }
            }
        }
        private void LandingEffects()
        {
            SoundDef soundDef = this.soundLanding;
            if (soundDef != null)
            {
                soundDef.PlayOneShot(new TargetInfo(base.Position, base.Map, false));
            }
            FleckMaker.ThrowDustPuff(this.DestinationPos + Gen.RandomHorizontalVector(0.5f), base.Map, 2f);
        }
        protected override void Tick()
        {
            base.Tick();
            ThingDef thingDef = this.moteDef ?? ThingDefOf.Mote_PsychicLinkLine;
            if (this.mote == null || this.mote.Destroyed)
            {
                this.mote = MoteMaker.MakeInteractionOverlay(thingDef, this, this.other);
            }
            this.mote.Maintain();
        }
        protected override void TickInterval(int delta)
        {
            if (this.flightEffecter == null && this.flightEffecterDef != null)
            {
                this.flightEffecter = this.flightEffecterDef.Spawn();
                this.flightEffecter.Trigger(this, TargetInfo.Invalid, -1);
            }
            else
            {
                Effecter effecter = this.flightEffecter;
                if (effecter != null)
                {
                    effecter.EffectTick(this, TargetInfo.Invalid);
                }
            }
            if (this.ticksFlying >= this.ticksFlightTime)
            {
                this.RespawnPawn();
                this.Destroy(DestroyMode.Vanish);
            }
            else
            {
                if (this.IsHashIntervalTick(15, delta))
                {
                    this.CheckDestination();
                }
                this.innerContainer.DoTick();
            }
            this.ticksFlying += delta;
        }
        private void CheckDestination()
        {
            if (!JumpUtility.ValidJumpTarget(this.FlyingThing, base.Map, this.destCell))
            {
                int num = GenRadial.NumCellsInRadius(3.9f);
                for (int i = 0; i < num; i++)
                {
                    IntVec3 intVec = this.destCell + GenRadial.RadialPattern[i];
                    if (JumpUtility.ValidJumpTarget(this.FlyingThing, base.Map, intVec))
                    {
                        this.destCell = intVec;
                        return;
                    }
                }
            }
        }
        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            this.RecomputePosition();
            if (this.FlyingPawn != null)
            {
                this.FlyingPawn.DynamicDrawPhaseAt(phase, this.effectivePos, false);
            }
            else
            {
                Thing flyingThing = this.FlyingThing;
                if (flyingThing != null)
                {
                    flyingThing.DynamicDrawPhaseAt(phase, this.effectivePos, false);
                }
            }
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
        }
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            this.DrawShadow(this.groundPos, this.effectiveHeight);
            if (this.CarriedThing != null && this.FlyingPawn != null)
            {
                PawnRenderUtility.DrawCarriedThing(this.FlyingPawn, this.effectivePos, this.CarriedThing);
            }
        }
        private void DrawShadow(Vector3 drawLoc, float height)
        {
            Material shadowMaterial = this.def.pawnFlyer.ShadowMaterial;
            if (shadowMaterial == null)
            {
                return;
            }
            float num = Mathf.Lerp(1f, 0.6f, height);
            Vector3 vector = new Vector3(num, 1f, num);
            Matrix4x4 matrix4x = default(Matrix4x4);
            matrix4x.SetTRS(drawLoc, Quaternion.identity, vector);
            Graphics.DrawMesh(MeshPool.plane10, matrix4x, shadowMaterial, 0);
        }
        public static GrabFlyer MakeFlyer(ThingDef flyingDef, Thing thing, Thing other, ThingDef customMote, IntVec3 destCell, EffecterDef flightEffecterDef, SoundDef landingSound, bool flyWithCarriedThing = false, Vector3? overrideStartVec = null, RimWorld.Ability triggeringAbility = null, LocalTargetInfo target = default(LocalTargetInfo))
        {
            GrabFlyer pawnFlyer = (GrabFlyer)ThingMaker.MakeThing(flyingDef, null);
            pawnFlyer.startVec = overrideStartVec ?? thing.TrueCenter();
            pawnFlyer.Rotation = thing.Rotation;
            pawnFlyer.flightDistance = thing.Position.DistanceTo(destCell);
            pawnFlyer.destCell = destCell;
            pawnFlyer.flightEffecterDef = flightEffecterDef;
            pawnFlyer.soundLanding = landingSound;
            pawnFlyer.triggeringAbility = ((triggeringAbility != null) ? triggeringAbility.def : null);
            pawnFlyer.target = target;
            pawnFlyer.other = other;
            ThingDef thingDef = customMote ?? ThingDefOf.Mote_PsychicLinkLine;
            pawnFlyer.moteDef = thingDef;
            if (thing is Pawn pawn)
            {
                pawnFlyer.pawnWasDrafted = pawn.Drafted;
                if (pawn.drafter != null)
                {
                    pawnFlyer.pawnCanFireAtWill = pawn.drafter.FireAtWill;
                }
                if (pawn.CurJob != null)
                {
                    if (pawn.CurJob.def == JobDefOf.CastJump)
                    {
                        pawn.jobs.EndCurrentJob(JobCondition.Succeeded, true, true);
                    }
                    else
                    {
                        pawn.jobs.SuspendCurrentJob(JobCondition.InterruptForced, true, null);
                    }
                }
                pawnFlyer.jobQueue = pawn.jobs.CaptureAndClearJobQueue();
                if (flyWithCarriedThing && pawn.carryTracker.CarriedThing != null && pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out pawnFlyer.carriedThing, null))
                {
                    if (pawnFlyer.carriedThing.holdingOwner != null)
                    {
                        pawnFlyer.carriedThing.holdingOwner.Remove(pawnFlyer.carriedThing);
                    }
                    pawnFlyer.carriedThing.DeSpawn(DestroyMode.Vanish);
                }
            }
            if (thing.Spawned)
            {
                thing.DeSpawn(DestroyMode.WillReplace);
            }
            if (!pawnFlyer.innerContainer.TryAdd(thing, true))
            {
                Log.Error("Could not add " + thing.ToStringSafe<Thing>() + " to a flyer.");
                thing.Destroy(DestroyMode.Vanish);
            }
            if (pawnFlyer.carriedThing != null && !pawnFlyer.innerContainer.TryAdd(pawnFlyer.carriedThing, true))
            {
                Log.Error("Could not add " + pawnFlyer.carriedThing.ToStringSafe<Thing>() + " to a flyer.");
            }
            return pawnFlyer;
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<Vector3>(ref this.startVec, "startVec", default(Vector3), false);
            Scribe_Values.Look<IntVec3>(ref this.destCell, "destCell", default(IntVec3), false);
            Scribe_Values.Look<float>(ref this.flightDistance, "flightDistance", 0f, false);
            Scribe_Values.Look<bool>(ref this.pawnWasDrafted, "pawnWasDrafted", false, false);
            Scribe_Values.Look<bool>(ref this.pawnCanFireAtWill, "pawnCanFireAtWill", true, false);
            Scribe_Values.Look<int>(ref this.ticksFlightTime, "ticksFlightTime", 0, false);
            Scribe_Values.Look<int>(ref this.ticksFlying, "ticksFlying", 0, false);
            Scribe_Defs.Look<ThingDef>(ref this.moteDef, "moteDef");
            Scribe_Defs.Look<EffecterDef>(ref this.flightEffecterDef, "flightEffecterDef");
            Scribe_Defs.Look<SoundDef>(ref this.soundLanding, "soundLanding");
            Scribe_Defs.Look<RimWorld.AbilityDef>(ref this.triggeringAbility, "triggeringAbility");
            Scribe_References.Look<Thing>(ref this.carriedThing, "carriedThing", false);
            Scribe_References.Look<Thing>(ref this.other, "other", false);
            Scribe_Deep.Look<ThingOwner<Thing>>(ref this.innerContainer, "innerContainer", new object[] { this });
            Scribe_Deep.Look<JobQueue>(ref this.jobQueue, "jobQueue", Array.Empty<object>());
            Scribe_TargetInfo.Look(ref this.target, "target");
        }
        public MoteDualAttached mote;
        public ThingDef moteDef;
        private ThingOwner<Thing> innerContainer;
        protected Vector3 startVec;
        private IntVec3 destCell;
        private float flightDistance;
        private bool pawnWasDrafted;
        private bool pawnCanFireAtWill = true;
        protected int ticksFlightTime = 120;
        protected int ticksFlying;
        private JobQueue jobQueue;
        protected EffecterDef flightEffecterDef;
        protected SoundDef soundLanding;
        private Thing carriedThing;
        public Thing other;
        private LocalTargetInfo target;
        private RimWorld.AbilityDef triggeringAbility;
        private Effecter flightEffecter;
        private int positionLastComputedTick = -1;
        private Vector3 groundPos;
        private Vector3 effectivePos;
        private float effectiveHeight;
    }
    public class GrabFlyerWorker : PawnFlyerWorker
    {
        public GrabFlyerWorker(PawnFlyerProperties properties) : base(properties)
        {
        }
        public override float GetHeight(float t)
        {
            return 0.1f;
        }
    }
}
