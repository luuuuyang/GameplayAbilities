using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    public delegate void GameplayEffectAppliedTargetDelegate(GameObject target, GameplayEffectSpecHandle specHandle, ActiveGameplayEffectHandle activeHandle);

    public class AbilityTask_WaitGameplayEffectApplied_Target : AbilityTask_WaitGameplayEffectApplied
    {
        public GameplayEffectAppliedTargetDelegate OnApplied;

        public static AbilityTask_WaitGameplayEffectApplied_Target WaitForGameplayEffectAppliedToTarget(GameplayAbility owningAbility, in GameplayTargetDataFilterHandle filter, GameplayTagRequirements sourceTagRequirements, GameplayTagRequirements targetTagRequirements, bool triggerOnce = false, GameObject optionalExternalOwner = null, bool listenForPeriodicEffects = false)
        {
            AbilityTask_WaitGameplayEffectApplied_Target task = NewAbilityTask<AbilityTask_WaitGameplayEffectApplied_Target>(owningAbility);
            task.Filter = filter;
            task.SourceTagRequirements = sourceTagRequirements;
            task.TargetTagRequirements = targetTagRequirements;
            task.TriggerOnce = triggerOnce;
            task.SetExternalActor(optionalExternalOwner);
            task.ListenForPeriodicEffects = listenForPeriodicEffects;

            return task;
        }

        public static AbilityTask_WaitGameplayEffectApplied_Target WaitForGameplayEffectAppliedToTarget_Query(GameplayAbility owningAbility, in GameplayTargetDataFilterHandle filter, GameplayTagQuery sourceTagQuery, GameplayTagQuery targetTagQuery, bool triggerOnce = false, GameObject optionalExternalOwner = null, bool listenForPeriodicEffects = false)
        {
            AbilityTask_WaitGameplayEffectApplied_Target task = NewAbilityTask<AbilityTask_WaitGameplayEffectApplied_Target>(owningAbility);
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
            ASC.OnGameplayEffectAppliedDelegateToTarget += OnApplyGameplayEffectCallback;
            if (ListenForPeriodicEffects)
            {
                ASC.OnPeriodicGameplayEffectExecutedDelegateOnTarget += OnApplyGameplayEffectCallback;
            }
        }

        protected override void RemoveDelegate()
        {
            ASC.OnGameplayEffectAppliedDelegateToTarget -= OnApplyGameplayEffectCallback;
            if (ListenForPeriodicEffects)
            {
                ASC.OnPeriodicGameplayEffectExecutedDelegateOnTarget -= OnApplyGameplayEffectCallback;
            }
        }
    }
}
