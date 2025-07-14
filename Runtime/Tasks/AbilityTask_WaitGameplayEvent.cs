using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    public delegate void WaitGameplayEventDelegate(GameplayEventData payload);

    public class AbilityTask_WaitGameplayEvent : AbilityTask
    {
        public WaitGameplayEventDelegate EventReceived;
        public GameplayTag Tag;
        public AbilitySystemComponent OptionalExternalTarget;
        public bool UseExternalTarget;
        public bool OnlyTriggerOnce;
        public bool OnlyMatchExact;

        public AbilitySystemComponent TargetASC
        {
            get
            {
                if (UseExternalTarget)
                {
                    return OptionalExternalTarget;
                }

                if (AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent target))
                {
                    return target;
                }

                return null;
            }
        }

        protected override void Activate()
        {
            AbilitySystemComponent asc = TargetASC;
            if (asc != null)
            {
                if (OnlyMatchExact)
                {
                    asc.GenericGameplayEventCallbacks.Add(Tag, GameplayEventCallback);
                }
                else
                {
                    asc.AddGameplayEventTagContainerDelegate(new GameplayTagContainer(Tag), GameplayEventContainerCallback);
                }
            }

            base.Activate();
        }

        protected override void OnDestroy(bool abilityIsEnding)
        {
            AbilitySystemComponent asc = TargetASC;
            if (asc != null)
            {
                if (OnlyMatchExact)
                {
                    asc.GenericGameplayEventCallbacks.TryGetValue(Tag, out GameplayEventMulticastDelegate @delegate);
                    if (@delegate != null)
                    {
                        @delegate -= GameplayEventCallback;
                    }
                }
                else
                {
                    asc.RemoveGameplayEventTagContainerDelegate(new GameplayTagContainer(Tag), GameplayEventContainerCallback);
                }
            }
        }

        public static AbilityTask_WaitGameplayEvent WaitGameplayEvent(GameplayAbility owningAbility, GameplayTag tag, GameObject optionalExternalTarget = null, bool onlyTriggerOnce = false, bool onlyMatchExact = true)
        {
            AbilityTask_WaitGameplayEvent task = NewAbilityTask<AbilityTask_WaitGameplayEvent>(owningAbility);
            task.Tag = tag;
            task.SetExternalTarget(optionalExternalTarget);
            task.OnlyTriggerOnce = onlyTriggerOnce;
            task.OnlyMatchExact = onlyMatchExact;

            return task;
        }

        public virtual void GameplayEventCallback(in GameplayEventData payload)
        {
            GameplayEventContainerCallback(Tag, payload);
        }

        public virtual void GameplayEventContainerCallback(GameplayTag matchingTag, in GameplayEventData payload)
        {
            if (ShouldBroadcastAbilityTaskDelegates)
            {
                GameplayEventData tempPayload = payload != null ? payload : new GameplayEventData();
                tempPayload.EventTag = matchingTag;
                EventReceived?.Invoke(tempPayload);
            }

            if (OnlyTriggerOnce)
            {
                EndTask();
            }
        }

        public void SetExternalTarget(GameObject actor)
        {
            if (actor != null)
            {
                UseExternalTarget = true;
                OptionalExternalTarget = AbilitySystemGlobals.GetAbilitySystemComponentFromActor(actor);
            }
        }
    }
}
