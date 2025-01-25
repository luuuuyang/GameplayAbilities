using UnityEngine;

namespace GameplayAbilities
{
    public abstract class GameplayEffectCustomApplicationRequirement
    {
        public bool CanApplyGameplayEffect(in GameplayEffect gameplayEffect, in GameplayEffectSpec spec, AbilitySystemComponent ASC)
        {
            return true;
        }
    }
}
