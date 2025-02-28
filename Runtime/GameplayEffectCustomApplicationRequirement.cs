using System;
using UnityEngine;

namespace GameplayAbilities
{
    [Serializable]
    public abstract class GameplayEffectCustomApplicationRequirement
    {
        public abstract bool CanApplyGameplayEffect(in GameplayEffect gameplayEffect, in GameplayEffectSpec spec, AbilitySystemComponent ASC);
    }
}
