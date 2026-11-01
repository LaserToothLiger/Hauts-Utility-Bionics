using HautsFramework;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace HautsBionics
{
    /*Yank works by throwing out a projectile (the hand) which must remain visually attached to the launcher (caster) via the rope visual as a MoteDualAttached (customMoteLine, which is passed onto the projectile's unique class by the ability so we can have arms of different colors w different abilities).
     * Theoretically, you could vary the hand graphic (customMoteOnVictim), but I've decided they all look the same. Importantly, whatever that mote is should have the EXACT same graphicData as the projectile to maintain visual continuity. (maybe later make a 'closed' hand graphic for customMoteOnVictim?)
     * comp also endows the projectile with knowledge of the Ability that begat it, so that it can, on impact, reset the ability's cd if warranted by the target*/
    public class CompProperties_AbilityLaunchGraspProjectile : CompProperties_AbilityAiTargetingDistanceRange
    {
        public CompProperties_AbilityLaunchGraspProjectile()
        {
            this.compClass = typeof(CompAbilityEffect_LaunchGraspProjectile);
        }
        public ThingDef customMoteLine;
        public ThingDef customMoteOnVictim;
        public ThingDef projectileDef;
    }
    public class CompAbilityEffect_LaunchGraspProjectile : CompAbilityEffect_AiTargetingDistanceRange
    {
        public new CompProperties_AbilityLaunchGraspProjectile Props
        {
            get
            {
                return (CompProperties_AbilityLaunchGraspProjectile)this.props;
            }
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
                } else if (target.Thing.def.category != ThingCategory.Building && target.Thing.def.category != ThingCategory.Pawn && (target.Thing.def.category != ThingCategory.Item || !target.Thing.def.EverHaulable)) {
                    return false;
                }
            }
            return base.Valid(target, throwMessages);
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (this.Props.projectileDef != null)
            {
                Pawn pawn = this.parent.pawn;
                Projectile p = ((Projectile)GenSpawn.Spawn(this.Props.projectileDef, pawn.Position, pawn.Map, WipeMode.Vanish));
                if (p is Projectile_GraspHand pgh)
                {
                    pgh.customMoteLine = this.Props.customMoteLine;
                    pgh.customMoteOnVictim = this.Props.customMoteOnVictim;
                    pgh.ability = this.parent;
                }
                p.Launch(pawn, pawn.DrawPos, target, target, ProjectileHitFlags.IntendedTarget, true, null, null);
            }
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return target.Pawn != null;
        }
    }
    public class Projectile_GraspHand : Projectile
    {
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            this.CreateOrMaintainArm();
        }
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            if (hitThing != null && hitThing == this.intendedTarget && this.launcher != null && this.launcher.Spawned && hitThing.Spawned && this.launcher.Map == hitThing.Map && this.launcher is Pawn p)
            {
                if (hitThing != null)
                {
                    if (hitThing.def.category != ThingCategory.Building && hitThing.def.category != ThingCategory.Plant && MassUtility.FreeSpace(p) >= hitThing.GetStatValue(StatDefOf.Mass))
                    {
                        int ticksToDisappear = (Mathf.Max(hitThing.PositionHeld.DistanceTo(p.PositionHeld), 1f) / HVBDefOf.HVB_GrabFlyer.pawnFlyer.flightSpeed).SecondsToTicks();
                        this.DoLink(hitThing, p, false, p.Position, p.Map, ticksToDisappear);
                        if (this.ability != null && !(hitThing is Pawn))
                        {
                            this.ability.ResetCooldown();
                        }
                    } else {
                        IntVec3 destination = hitThing.Position;
                        if (!hitThing.Position.Walkable(hitThing.Map))//NO WALLHOPPING
                        {
                            if (p.Position.x > destination.x)
                            {
                                IntVec3 replacement = new IntVec3(destination.x + 1, destination.y, destination.z);
                                if (replacement.Walkable(hitThing.Map))
                                {
                                    destination = replacement;
                                }
                            }
                            if (p.Position.z > destination.z)
                            {
                                IntVec3 replacement = new IntVec3(destination.x, destination.y, destination.z + 1);
                                if (replacement.Walkable(hitThing.Map))
                                {
                                    destination = replacement;
                                }
                            }
                        }
                        this.DoLink(p, hitThing, true, destination, hitThing.Map, (Mathf.Max(hitThing.PositionHeld.DistanceTo(p.PositionHeld), 1f) / HVBDefOf.HVB_GrabFlyer.pawnFlyer.flightSpeed).SecondsToTicks());
                    }
                }
            }
            base.Impact(hitThing, blockedByShield);
        }
        private void DoLink(Thing flyer, Thing other, bool flyerIsCaster, IntVec3 destination, Map map, int ticksToDisappear)
        {
            GrabFlyer gFlyer = GrabFlyer.MakeFlyer(HVBDefOf.HVB_GrabFlyer, flyer, other, flyerIsCaster, this.customMoteLine, this.customMoteOnVictim, destination, null, null, false, null, null, default(LocalTargetInfo));
            GenSpawn.Spawn(gFlyer, destination, map, WipeMode.Vanish);
        }
        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            this.CreateOrMaintainArm();
        }
        public void CreateOrMaintainArm()
        {
            ThingDef thingDef = this.customMoteLine ?? ThingDefOf.Mote_PsychicLinkLine;
            if (this.mote == null || this.mote.Destroyed)
            {
                this.mote = MoteMaker.MakeInteractionOverlay(thingDef, this, this.launcher);
            }
            this.mote.Maintain();
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look<ThingDef>(ref this.customMoteLine, "customMoteLine");
            Scribe_Defs.Look<ThingDef>(ref this.customMoteOnVictim, "customMoteOnVictim");
            Scribe_References.Look<Ability>(ref this.ability, "ability", false);
        }
        public ThingDef customMoteLine;
        public ThingDef customMoteOnVictim;
        public MoteDualAttached mote;
        public Ability ability;
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
        protected virtual void RespawnPawn(bool dropAtFlyerPosition = false)
        {
            Thing flyingThing = this.FlyingThing;
            this.LandingEffects();
            Thing thing;
            this.innerContainer.TryDrop(flyingThing, dropAtFlyerPosition ? base.Position : this.destCell, flyingThing.MapHeld, ThingPlaceMode.Direct, out thing, null, null, false);
            Pawn pawn = flyingThing as Pawn;
            if (((pawn != null) ? pawn.drafter : null) != null)
            {
                pawn.drafter.Drafted = this.pawnWasDrafted;
                pawn.drafter.FireAtWill = this.pawnCanFireAtWill;
            }
            flyingThing.Rotation = base.Rotation;
            if (this.carriedThing != null && this.innerContainer.TryDrop(this.carriedThing, dropAtFlyerPosition ? base.Position : this.destCell, flyingThing.MapHeld, ThingPlaceMode.Direct, out thing, null, null, false) && pawn != null)
            {
                this.carriedThing.DeSpawn(DestroyMode.Vanish);
                if (!pawn.carryTracker.TryStartCarry(this.carriedThing))
                {
                    Log.Error("Could not carry " + this.carriedThing.ToStringSafe<Thing>() + " after respawning GrabFlyer.");
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
            ThingDef thingDef2 = this.moteOnVictimDef ?? ThingDefOf.Mote_TempRoof;
            if (this.moteOnVictim == null || this.moteOnVictim.Destroyed)
            {
                this.moteOnVictim = MoteMaker.MakeAttachedOverlay(this.flyerIsCaster ? this.other : this, thingDef2, Vector3.zero, 1f, -1f);
            }
            if (this.moteOnVictim != null)
            {
                this.moteOnVictim.exactRotation = this.DrawPos.AngleToFlat(this.other.DrawPos) + 90f;
                this.moteOnVictim.link1.UpdateDrawPos();
                this.moteOnVictim.exactPosition = this.moteOnVictim.link1.LastDrawPos;
            }
            this.moteOnVictim.Maintain();
        }
        protected override void TickInterval(int delta)
        {
            if (this.flightEffecter == null && this.flightEffecterDef != null)
            {
                this.flightEffecter = this.flightEffecterDef.Spawn();
                this.flightEffecter.Trigger(this, TargetInfo.Invalid, -1);
            } else {
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
            } else {
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
            if (this.other != null && this.other.Spawned)
            {
                this.destCell = this.other.Position;
                if (!this.destCell.Walkable(other.Map))//NO WALLHOPPING ... 2!
                {
                    if (this.Position.x > this.destCell.x)
                    {
                        IntVec3 replacement = new IntVec3(this.destCell.x + 1, this.destCell.y, this.destCell.z);
                        if (replacement.Walkable(other.Map))
                        {
                            this.destCell = replacement;
                        }
                    }
                    if (this.Position.z > this.destCell.z)
                    {
                        IntVec3 replacement = new IntVec3(this.destCell.x, this.destCell.y, this.destCell.z + 1);
                        if (replacement.Walkable(other.Map))
                        {
                            this.destCell = replacement;
                        }
                    }
                }
            }
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
        public static GrabFlyer MakeFlyer(ThingDef flyingDef, Thing thing, Thing other, bool flyerIsCaster, ThingDef customMoteLine, ThingDef customMoteOnVictim, IntVec3 destCell, EffecterDef flightEffecterDef, SoundDef landingSound, bool flyWithCarriedThing = false, Vector3? overrideStartVec = null, RimWorld.Ability triggeringAbility = null, LocalTargetInfo target = default(LocalTargetInfo))
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
            pawnFlyer.flyerIsCaster = flyerIsCaster;
            pawnFlyer.moteDef = customMoteLine ?? ThingDefOf.Mote_PsychicLinkLine;
            pawnFlyer.moteOnVictimDef = customMoteOnVictim ?? ThingDefOf.Mote_TempRoof;
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
                    } else {
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
            Scribe_Defs.Look<ThingDef>(ref this.moteOnVictimDef, "moteOnVictimDef");
            Scribe_Values.Look<bool>(ref this.flyerIsCaster, "flyerIsCaster", false, false);
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
        public Mote moteOnVictim;
        public ThingDef moteOnVictimDef;
        public bool flyerIsCaster;
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
