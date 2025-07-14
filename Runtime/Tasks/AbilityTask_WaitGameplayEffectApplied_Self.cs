using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    public delegate void GameplayEffectAppliedSelfDelegate(GameObject source, GameplayEffectSpecHandle specHandle, ActiveGameplayEffectHandle activeHandle);

    public class AbilityTask_WaitGameplayEffectApplied_Self : AbilityTask_WaitGameplayEffectApplied
    {
        public GameplayEffectAppliedSelfDelegate OnApplied;

        public static AbilityTask_WaitGameplayEffectApplied_Self WaitForGameplayEffectAppliedToSelf(GameplayAbility owningAbility, in GameplayTargetDataFilterHandle filter, GameplayTagRequirements sourceTagRequirements, GameplayTagRequirements targetTagRequirements, bool triggerOnce = false, GameObject optionalExternalOwner = null, bool listenForPeriodicEffects = false)
        {
            AbilityTask_WaitGameplayEffectApplied_Self task = NewAbilityTask<AbilityTask_WaitGameplayEffectApplied_Self>(owningAbility);
            task.Filter = filter;
            task.SourceTagRequirements = sourceTagRequirements;
            task.TargetTagRequirements = targetTagRequirements;
            task.TriggerOnce = triggerOnce;
            task.SetExternalActor(optionalExternalOwner);
            task.ListenForPeriodicEffects = listenForPeriodicEffects;

            return task;
        }

        public static AbilityTask_WaitGameplayEffectApplied_Self WaitForGameplayEffectAppliedToSelf_Query(GameplayAbility owningAbility, in GameplayTargetDataFilterHandle filter, GameplayTagQuery sourceTagQuery, GameplayTagQuery targetTagQuery, bool triggerOnce = false, GameObject optionalExternalOwner = null, bool listenForPeriodicEffects = false)
        {
            AbilityTask_WaitGameplayEffectApplied_Self task = NewAbilityTask<AbilityTask_WaitGameplayEffectApplied_Self>(owningAbility);
            task.Filter = filter;
            task.SourceTagQuery = sourceTagQuery;
            task.TargetTagQuery = targetTagQuery;
            task.TriggerOnce = triggerOnce;
            task.SetExternalActor(optionalExternalOwner);
            task.ListenForPeriodicEffects = listenForPeriodicEffects;

            return task;
        }

        protected override void BroadcastDelegate(GameObject avatar, GameplayEffectSpecHandle specHandle, ActiveGameplayEffectHandle activeHandle)
        {
            if (ShouldBroadcastAbilityTaskDelegates)
            {
                OnApplied?.Invoke(avatar, specHandle, activeHandle);
            }
        }

        protected override void RegisterDelegate()
        {
            ASC.OnGameplayEffectAppliedDelegateToSelf += OnApplyGameplayEffectCallback;
            if (ListenForPeriodicEffects)
            {
                ASC.OnPeriodicGameplayEffectExecutedDelegateOnSelf += OnApplyGameplayEffectCallback;
            }
        }

        protected override void RemoveDelegate()
        {
            ASC.OnGameplayEffectAppliedDelegateToSelf -= OnApplyGameplayEffectCallback;
            if (ListenForPeriodicEffects)
            {
                ASC.OnPeriodicGameplayEffectExecutedDelegateOnSelf -= OnApplyGameplayEffectCallback;
            }
        }
    }
}
