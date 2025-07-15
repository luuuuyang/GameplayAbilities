using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    public class AbilityAsync_WaitGameplayEvent : AbilityAsync
    {
        public delegate void EventReceivedDelegate(GameplayEventData payload);
        public EventReceivedDelegate EventReceived;
        protected GameplayTag Tag;
        protected bool OnlyTriggerOnce;
        protected bool OnlyMatchExact;

        protected override void Activate()
        {
            base.Activate();

            AbilitySystemComponent asc = AbilitySystemComponent;
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
            else
            {
                EndAction();
            }
        }

        public override void EndAction()
        {
            AbilitySystemComponent asc = AbilitySystemComponent;
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

            base.EndAction();
        }

        public static AbilityAsync_WaitGameplayEvent WaitGameplayEventToActor(GameObject targetActor, GameplayTag eventTag, bool onlyTriggerOnce = false, bool onlyMatchExact = true)
        {
            AbilityAsync_WaitGameplayEvent async = new()
            {
                AbilityActor = targetActor,
                Tag = eventTag,
                OnlyTriggerOnce = onlyTriggerOnce,
                OnlyMatchExact = onlyMatchExact,
            };

            return async;
        }

        public virtual void GameplayEventCallback(in GameplayEventData payload)
        {
            GameplayEventContainerCallback(Tag, payload);
        }

        public virtual void GameplayEventContainerCallback(GameplayTag matchingTag, in GameplayEventData payload)
        {
            if (ShouldBroadcastDelegates)
            {
                GameplayEventData tempPayload = payload != null ? payload : new GameplayEventData();
                tempPayload.EventTag = matchingTag;
                EventReceived?.Invoke(tempPayload);

                if (OnlyTriggerOnce)
                {
                    EndAction();
                }
            }
            else
            {
                EndAction();
            }
        }
    }
}
