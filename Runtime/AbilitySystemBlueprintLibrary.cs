using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    public static class AbilitySystemBlueprintLibrary
    {
        public static AbilitySystemComponent GetAbilitySystemComponent(GameObject actor)
        {
            return AbilitySystemGlobals.GetAbilitySystemComponentFromActor(actor);
        }

        public static void SendGameplayEventToActor(GameObject actor, GameplayTag eventTag, in GameplayEventData payload)
        {
            if (actor != null)
            {
                AbilitySystemComponent abilitySystemComponent = GetAbilitySystemComponent(actor);
                if (abilitySystemComponent != null)
                {
                    abilitySystemComponent.HandleGameplayEvent(eventTag, payload);
                }
                else
                {
                    Debug.LogError($"UAbilitySystemBlueprintLibrary::SendGameplayEventToActor: Invalid ability system component retrieved from Actor %s. EventTag was %s");
                }
            }
        }
    }
}
