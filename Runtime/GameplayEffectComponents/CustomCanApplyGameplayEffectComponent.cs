using System.Collections.Generic;
using UnityEngine;

namespace GameplayAbilities
{
    public class CustomCanApplyGameplayEffectComponent : GameplayEffectComponent
    {
        public List<GameplayEffectCustomApplicationRequirement> ApplicationRequirements = new();

        public override bool CanGameplayEffectApply(in ActiveGameplayEffectsContainer activeGEContainer, in GameplayEffectSpec GESpec)
        {
            foreach (GameplayEffectCustomApplicationRequirement appReq in ApplicationRequirements)
            {
                if (appReq != null && !appReq.CanApplyGameplayEffect(GESpec.Def, GESpec, activeGEContainer.Owner))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
