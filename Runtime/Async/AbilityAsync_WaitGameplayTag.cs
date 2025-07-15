using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    public abstract class AbilityAsync_WaitGameplayTag : AbilityAsync
    {
        public delegate void AsyncWaitGameplayTagDelegate();

        protected int TargetCount = 1;
        protected GameplayTag Tag;
        protected bool OnlyTriggerOnce;

        protected override void Activate()
        {
            base.Activate();

            AbilitySystemComponent asc = AbilitySystemComponent;
            if (asc != null && ShouldBroadcastDelegates)
            {
                asc.RegisterGameplayTagEvent(Tag).AddListener(GameplayTagCallback);
            }
        }

        public override void EndAction()
        {
            AbilitySystemComponent asc = AbilitySystemComponent;
            if (asc != null)
            {
                asc.UnregisterGameplayTagEvent(GameplayTagCallback, Tag);
            }

            base.EndAction();
        }

        protected virtual void GameplayTagCallback(GameplayTag tag, int newCount)
        {
            if (newCount == TargetCount)
            {
                if (ShouldBroadcastDelegates)
                {
                    BroadcastDelegates();
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

        public virtual void BroadcastDelegates()
        {

        }
    }

    public class AbilityAsync_WaitGameplayTagAdded : AbilityAsync_WaitGameplayTag
    {
        public AsyncWaitGameplayTagDelegate Added;

        public static AbilityAsync_WaitGameplayTagAdded WaitGameplayTagAddToActor(GameObject targetActor, GameplayTag tag, bool onlyTriggerOnce = false)
        {
            AbilityAsync_WaitGameplayTagAdded async = new()
            {
                AbilityActor = targetActor,
                Tag = tag,
                OnlyTriggerOnce = onlyTriggerOnce,
                TargetCount = 1,
            };

            return async;
        }

        public override void BroadcastDelegates()
        {
            Added?.Invoke();
        }
    }

    public class AbilityAsync_WaitGameplayTagRemoved : AbilityAsync_WaitGameplayTag
    {
        public AsyncWaitGameplayTagDelegate Removed;

        public static AbilityAsync_WaitGameplayTagRemoved WaitGameplayTagRemoveFromActor(GameObject targetActor, GameplayTag tag, bool onlyTriggerOnce = false)
        {
            AbilityAsync_WaitGameplayTagRemoved async = new()
            {
                AbilityActor = targetActor,
                Tag = tag,
                OnlyTriggerOnce = onlyTriggerOnce,
                TargetCount = 0,
            };

            return async;
        }

        public override void BroadcastDelegates()
        {
            Removed?.Invoke();
        }
    }
}
