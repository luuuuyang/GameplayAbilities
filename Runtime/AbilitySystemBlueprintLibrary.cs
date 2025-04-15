using System;
using System.Collections.Generic;
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

        public static GameplayAbilityTargetDataHandle AbilityTargetDataFromActor(GameObject actor)
        {
            GameplayAbilityTargetData_ActorArray newData = new();
            newData.TargetActorArray.Add(new WeakReference<GameObject>(actor));
            GameplayAbilityTargetDataHandle handle = new(newData);
            return handle;
        }

        public static GameplayAbilityTargetDataHandle AbilityTargetDataFromActorArray(List<GameObject> actors, bool oneTargetPerHandle)
        {
            if (oneTargetPerHandle)
            {
                GameplayAbilityTargetDataHandle handle = new();
                for (int i = 0; i < actors.Count; i++)
                {
                    if (actors[i] != null)
                    {
                        GameplayAbilityTargetDataHandle tempHandle = AbilityTargetDataFromActor(actors[i]);
                        handle.AddRange(tempHandle);
                    }
                }
                return handle;
            }
            else
            {
                GameplayAbilityTargetData_ActorArray newData = new();
                foreach (GameObject actor in actors)
                {
                    newData.TargetActorArray.Add(new WeakReference<GameObject>(actor));
                }
                GameplayAbilityTargetDataHandle handle = new(newData);
                return handle;
            }
        }
    }
}
