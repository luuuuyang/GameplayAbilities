using UnityEngine;

namespace GameplayAbilities
{
    public class ChanceToApplyGameplayEffectComponent : GameplayEffectComponent
    {
        public ScalableFloat ChanceToApplyToTarget;

        public override bool CanGameplayEffectApply(in ActiveGameplayEffectsContainer activeGEContainer, in GameplayEffectSpec GESpec)
        {
            float calculatedChanceToApplyToTarget = ChanceToApplyToTarget.GetValueAtLevel(GESpec.Level);

            if (calculatedChanceToApplyToTarget < 1.0f - float.Epsilon && Random.value > calculatedChanceToApplyToTarget)
            {
                return false;
            }

            return true;
        }
    }
}
