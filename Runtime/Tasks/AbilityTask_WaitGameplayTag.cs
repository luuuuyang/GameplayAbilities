using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    public delegate void WaitGameplayTagDelegate();

    public class AbilityTask_WaitGameplayTagAdded : AbilityTask_WaitGameplayTag
    {
        public WaitGameplayTagDelegate Added;

        public static AbilityTask_WaitGameplayTagAdded WaitGameplayTagAdd(GameplayAbility owningAbility, GameplayTag tag, GameObject optionalExternalTarget, bool onlyTriggerOnce)
        {
            AbilityTask_WaitGameplayTagAdded task = NewAbilityTask<AbilityTask_WaitGameplayTagAdded>(owningAbility);
            task.Tag = tag;
            task.SetExternalTarget(optionalExternalTarget);
            task.OnlyTriggerOnce = onlyTriggerOnce;

            return task;
        }

        protected override void Activate()
        {
            AbilitySystemComponent asc = TargetASC;
            if (asc != null && asc.HasMatchingGameplayTag(Tag))
            {
                if (ShouldBroadcastAbilityTaskDelegates)
                {
                    Added?.Invoke();
                }

                if (OnlyTriggerOnce)
                {
                    EndTask();
                    return;
                }
            }

            base.Activate();
        }

        public override void GameplayTagCallback(GameplayTag tag, int newCount)
        {
            if (newCount == 1)
            {
                if (ShouldBroadcastAbilityTaskDelegates)
                {
                    Added?.Invoke();
                }

                if (OnlyTriggerOnce)
                {
                    EndTask();
                }
            }
        }
    }

    public class AbilityTask_WaitGameplayTagRemoved : AbilityTask_WaitGameplayTag
    {
        public WaitGameplayTagDelegate Removed;

        public static AbilityTask_WaitGameplayTagRemoved WaitGameplayTagRemove(GameplayAbility owningAbility, GameplayTag tag, GameObject optionalExternalTarget, bool onlyTriggerOnce)
        {
            AbilityTask_WaitGameplayTagRemoved task = NewAbilityTask<AbilityTask_WaitGameplayTagRemoved>(owningAbility);
            task.Tag = tag;
            task.SetExternalTarget(optionalExternalTarget);
            task.OnlyTriggerOnce = onlyTriggerOnce;

            return task;
        }

        protected override void Activate()
        {
            AbilitySystemComponent asc = TargetASC;
            if (asc != null && !asc.HasMatchingGameplayTag(Tag))
            {
                if (ShouldBroadcastAbilityTaskDelegates)
                {
                    Removed?.Invoke();
                }

                if (OnlyTriggerOnce)
                {
                    EndTask();
                    return;
                }
            }

            base.Activate();
        }

        public override void GameplayTagCallback(GameplayTag tag, int newCount)
        {
            if (newCount == 0)
            {
                if (ShouldBroadcastAbilityTaskDelegates)
                {
                    Removed?.Invoke();
                }

                if (OnlyTriggerOnce)
                {
                    EndTask();
                }
            }
        }
    }
}
