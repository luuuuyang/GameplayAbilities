using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayAbilities
{
    [LabelText("Immunity (Prevent Other GEs)")]
    public class ImmunityGameplayEffectComponent : GameplayEffectComponent
    {
        public List<GameplayEffectQuery> ImmunityQueries;

        public override bool OnActiveGameplayEffectAdded(ActiveGameplayEffectsContainer activeGEContainer, ActiveGameplayEffect activeGE)
        {
            ActiveGameplayEffectHandle activeGEHandle = activeGE.Handle;
            AbilitySystemComponent ownerASC = activeGEContainer.Owner;

            bool boundQuery(in ActiveGameplayEffectsContainer activeGEContainer, in GameplayEffectSpec GESpecToConsider) => AllowGameplayEffectApplication(activeGEContainer, GESpecToConsider, activeGEHandle);
            ownerASC.GameplayEffectApplicationQueries.Add(boundQuery);

            activeGE.EventSet.OnEffectRemoved.AddListener(removalInfo =>
            {
                if (ownerASC)
                {
                    List<GameplayEffectApplicationQuery> GEAppQueries = ownerASC.GameplayEffectApplicationQueries;
                    GEAppQueries.Remove(boundQuery);
                }
            });

            return true;
        }

        protected bool AllowGameplayEffectApplication(in ActiveGameplayEffectsContainer activeGEContainer, in GameplayEffectSpec GESpecToConsider, ActiveGameplayEffectHandle immunityActiveGEHandle)
        {
            AbilitySystemComponent ASC = activeGEContainer.Owner;
            if (ASC != immunityActiveGEHandle.OwningAbilitySystemComponent)
            {
                Debug.LogWarning("Something went wrong where an ActiveGameplayEffect jumped AbilitySystemComponents");
                return false;
            }

            ActiveGameplayEffect activeGE = ASC.GetActiveGameplayEffect(immunityActiveGEHandle);
            if (activeGE is null || activeGE.IsInhibited)
            {
                return true;
            }

            foreach (GameplayEffectQuery immunityQuery in ImmunityQueries)
            {
                if (!immunityQuery.IsEmpty() && immunityQuery.Matches(GESpecToConsider))
                {
                    ASC.OnImmunityBlockGameplayEffectDelegate?.Invoke(GESpecToConsider, activeGE);
                    return false;
                }
            }

            return true;
        }
    }
}
