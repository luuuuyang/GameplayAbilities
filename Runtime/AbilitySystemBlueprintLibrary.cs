using System;
using System.Collections.Generic;
using GameplayTags;
using UnityEngine;
using UnityEngine.Events;

namespace GameplayAbilities
{
    public delegate void OnGameplayTagChangedEventWrapperSignature(in GameplayTag tag, int tagCount);

    public class GameplayTagChangedEventWrapperSpec
    {
        public WeakReference<AbilitySystemComponent> AbilitySystemComponent;
        public OnGameplayTagChangedEventWrapperSignature OnGameplayTagChangedEventWrapperDelegate;
        public GameplayTagEventType TagListeningPolicy;
        public Dictionary<GameplayTag, UnityEvent<GameplayTag, int>> DelegateBindings;

        public GameplayTagChangedEventWrapperSpec(AbilitySystemComponent abilitySystemComponent, OnGameplayTagChangedEventWrapperSignature onGameplayTagChangedEventWrapperDelegate, GameplayTagEventType tagListeningPolicy)
        {
            AbilitySystemComponent = new WeakReference<AbilitySystemComponent>(abilitySystemComponent);
            OnGameplayTagChangedEventWrapperDelegate = onGameplayTagChangedEventWrapperDelegate;
            TagListeningPolicy = tagListeningPolicy;
            DelegateBindings = new Dictionary<GameplayTag, UnityEvent<GameplayTag, int>>();
        }
    }

    public record GameplayTagChangedEventWrapperSpecHandle
    {
        public GameplayTagChangedEventWrapperSpecHandle()
        {

        }

        public GameplayTagChangedEventWrapperSpecHandle(GameplayTagChangedEventWrapperSpec data)
        {
            Data = data;
        }

        public GameplayTagChangedEventWrapperSpec Data;
    }

    public static class AbilitySystemBlueprintLibrary
    {
        public static AbilitySystemComponent GetAbilitySystemComponent(GameObject actor)
        {
            return AbilitySystemGlobals.GetAbilitySystemComponentFromActor(actor);
        }

        public static void SendGameplayEventToActor(GameObject actor, GameplayTag eventTag, GameplayEventData payload)
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

        public static GameplayTagChangedEventWrapperSpecHandle BindEventWrapperToGameplayTagChanged(
            AbilitySystemComponent abilitySystemComponent,
            GameplayTag tag,
            OnGameplayTagChangedEventWrapperSignature gameplayTagChangedEventWrapperDelegate,
            bool executeImmediatelyIfTagApplied = true,
            GameplayTagEventType tagListeningPolicy = GameplayTagEventType.NewOrRemoved)
        {
            if (abilitySystemComponent == null)
            {
                return new GameplayTagChangedEventWrapperSpecHandle();
            }

            GameplayTagChangedEventWrapperSpec tagBindingSpec = new(abilitySystemComponent, gameplayTagChangedEventWrapperDelegate, tagListeningPolicy);
            GameplayTagChangedEventWrapperSpecHandle tagBindHandle = new(tagBindingSpec);

            UnityEvent<GameplayTag, int> delegateHandle = abilitySystemComponent.RegisterGameplayTagEvent(tag, tagListeningPolicy);
            delegateHandle.AddListener((gameplayTag, gameplayTagCount) =>
            {
                ProcessGameplayTagChangedEventWrapper(gameplayTag, gameplayTagCount, gameplayTagChangedEventWrapperDelegate);
            });

            tagBindingSpec.DelegateBindings.Add(tag, delegateHandle);

            if (executeImmediatelyIfTagApplied)
            {
                int gameplayTagCount = abilitySystemComponent.GetGameplayTagCount(tag);
                if (gameplayTagCount > 0)
                {
                    gameplayTagChangedEventWrapperDelegate(tag, gameplayTagCount);
                }
            }

            return tagBindHandle;
        }

        public static GameplayTagChangedEventWrapperSpecHandle BindEventWrapperToAnyGameplayTagsChanged(
            AbilitySystemComponent abilitySystemComponent,
            in List<GameplayTag> tags,
            OnGameplayTagChangedEventWrapperSignature gameplayTagChangedEventWrapperDelegate,
            bool executeImmediatelyIfTagApplied = true,
            GameplayTagEventType tagListeningPolicy = GameplayTagEventType.NewOrRemoved)
        {
            if (abilitySystemComponent == null)
            {
                return new GameplayTagChangedEventWrapperSpecHandle();
            }

            GameplayTagChangedEventWrapperSpec tagBindSpec = new(abilitySystemComponent, gameplayTagChangedEventWrapperDelegate, tagListeningPolicy);
            GameplayTagChangedEventWrapperSpecHandle tagBindHandle = new(tagBindSpec);

            tagBindSpec.DelegateBindings.EnsureCapacity(tags.Count);

            foreach (GameplayTag tag in tags)
            {
                UnityEvent<GameplayTag, int> delegateHandle = abilitySystemComponent.RegisterGameplayTagEvent(tag, tagListeningPolicy);
                delegateHandle.AddListener((gameplayTag, gameplayTagCount) =>
                {
                    ProcessGameplayTagChangedEventWrapper(gameplayTag, gameplayTagCount, gameplayTagChangedEventWrapperDelegate);
                });

                tagBindSpec.DelegateBindings.Add(tag, delegateHandle);
            }

            if (executeImmediatelyIfTagApplied)
            {
                foreach (GameplayTag tag in tags)
                {
                    int gameplayTagCount = abilitySystemComponent.GetGameplayTagCount(tag);
                    if (gameplayTagCount > 0)
                    {
                        gameplayTagChangedEventWrapperDelegate(tag, gameplayTagCount);
                    }
                }
            }

            return tagBindHandle;
        }

        public static GameplayTagChangedEventWrapperSpecHandle BindEventWrapperToAnyGameplayTagContainerChanged(
            AbilitySystemComponent abilitySystemComponent,
            in GameplayTagContainer tagContainer,
            OnGameplayTagChangedEventWrapperSignature gameplayTagChangedEventWrapperDelegate,
            bool executeImmediatelyIfTagApplied = true,
            GameplayTagEventType tagListeningPolicy = GameplayTagEventType.NewOrRemoved)
        {
            List<GameplayTag> tags = new();
            tagContainer.GetGameplayTagArray(tags);

            return BindEventWrapperToAnyGameplayTagsChanged(abilitySystemComponent, tags, gameplayTagChangedEventWrapperDelegate, executeImmediatelyIfTagApplied, tagListeningPolicy);
        }

        public static void UnbindAllGameplayTagChangedEventWrappersForHandle(GameplayTagChangedEventWrapperSpecHandle handle)
        {
            GameplayTagChangedEventWrapperSpec gameplayTagChangedEventData = handle.Data;
            if (gameplayTagChangedEventData == null)
            {
                return;
            }

            if (gameplayTagChangedEventData.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
            {
                foreach (var delegateBindingIterator in gameplayTagChangedEventData.DelegateBindings)
                {
                    delegateBindingIterator.Value.RemoveAllListeners();
                }

                gameplayTagChangedEventData.DelegateBindings.Clear();
            }
        }

        public static void UnbindGameplayTagChangedEventWrappersForHandle(GameplayTag tag, GameplayTagChangedEventWrapperSpecHandle handle)
        {
            GameplayTagChangedEventWrapperSpec gameplayTagChangedEventData = handle.Data;
            if (gameplayTagChangedEventData == null)
            {
                return;
            }

            if (gameplayTagChangedEventData.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
            {
                foreach (var delegateBindingIterator in gameplayTagChangedEventData.DelegateBindings)
                {
                    GameplayTag boundTag = delegateBindingIterator.Key;
                    if (!boundTag.MatchesTagExact(tag))
                    {
                        continue;
                    }

                    delegateBindingIterator.Value.RemoveAllListeners();
                    gameplayTagChangedEventData.DelegateBindings.Remove(boundTag);
                }
            }
        }

        private static void ProcessGameplayTagChangedEventWrapper(in GameplayTag gameplayTag, int gameplayTagCount, OnGameplayTagChangedEventWrapperSignature gameplayTagChangedEventWrapperDelegate)
        {
            gameplayTagChangedEventWrapperDelegate(gameplayTag, gameplayTagCount);
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
