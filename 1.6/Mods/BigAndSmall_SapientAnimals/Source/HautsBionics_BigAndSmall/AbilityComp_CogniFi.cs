using BigAndSmall;
using RimWorld;
using Verse;

namespace HautsBionics_BigAndSmall
{
    public class HautsBionics_BigAndSmall
    {

    }
    public class CompProperties_AbilityCogniFi : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityCogniFi()
        {
            this.compClass = typeof(CompAbilityEffect_CogniFi);
        }
    }
    //an ability comp that mimicks what the cogni-fi item does
    public class CompAbilityEffect_CogniFi : CompAbilityEffect
    {
        public new CompProperties_AbilityCogniFi Props
        {
            get
            {
                return (CompProperties_AbilityCogniFi) this.props;
            }
        }
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return base.CanApplyOn(target, dest) && target.Pawn != null && HumanlikeAnimalGenerator.reverseLookupHumanlikeAnimals.ContainsKey(target.Pawn.def);
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = target.Pawn;
            if (pawn == null)
            {
                return;
            }
            if (HumanlikeAnimalGenerator.reverseLookupHumanlikeAnimals.ContainsKey(pawn.def))
            {
                pawn.SwapAnimalToSapientVersion();
                return;
            }
            Log.Warning(string.Format("Tried to swap {0} to a sapient version, but no sapient version found for {1}.", pawn.Name, pawn.def.defName));
        }
    }
}
