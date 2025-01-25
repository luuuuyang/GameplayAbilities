using System.Collections.Generic;
using UnityEngine;

namespace GameplayAbilities
{
    public class ImmunityGameplayEffectComponent : GameplayEffectComponent
    {
        public List<GameplayEffectQuery> ImmunityQueries;

        public override bool OnActiveGameplayEffectAdded(ActiveGameplayEffectsContainer activeGEContainer, ActiveGameplayEffect activeGE)
        {
            ActiveGameplayEffectHandle activeGEHandle = activeGE.Handle;
            AbilitySystemComponent ownerASC = activeGEContainer.Owner;

            GameplayEffectApplicationQuery boundQuery = ownerASC.GameplayEffectApplicationQueries[0];
            boundQuery += (in ActiveGameplayEffectsContainer activeGEContainer, in GameplayEffectSpec geSpecToConsider) => AllowGameplayEffectApplication(activeGEContainer, geSpecToConsider, activeGEHandle);

            activeGE.EventSet.OnEffectRemoved += (GameplayEffectRemovalInfo removalInfo) =>
            {
                if (ownerASC)
                {
                    List<GameplayEffectApplicationQuery> geAppQueries = ownerASC.GameplayEffectApplicationQueries;
                    geAppQueries.Remove(boundQuery);
                }
            };

            return true;
        }

        protected bool AllowGameplayEffectApplication(in ActiveGameplayEffectsContainer activeGEContainer, in GameplayEffectSpec geSpecToConsider, ActiveGameplayEffectHandle immunityActiveGEHandle)
        {
            AbilitySystemComponent ASC = activeGEContainer.Owner;
            if (ASC != immunityActiveGEHandle.OwningAbilitySystemComponent)
            {
                Debug.LogWarning("Something went wrong where an ActiveGameplayEffect jumped AbilitySystemComponents");
                return false;
            }

            ActiveGameplayEffect activeGE = ASC.GetActiveGameplayEffect(immunityActiveGEHandle);
            if (activeGE == null || activeGE.IsInhibited)
            {
                return true;
            }

            foreach (GameplayEffectQuery immunityQuery in ImmunityQueries)
            {
                if (!immunityQuery.IsEmpty() && immunityQuery.Matches(geSpecToConsider))
                {
                    ASC.OnImmunityBlockGameplayEffectDelegate?.Invoke(geSpecToConsider, activeGE);
                    return false;
                }
            }

            return true;
        }
    }
}
