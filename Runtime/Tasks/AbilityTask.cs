using System;
using UnityEngine;

namespace GameplayAbilities
{
    public enum AbilityTaskWaitState : byte
    {
        WaitingOnGameplayAbility,
        WaitingOnUser,
        WaitingOnAvatar,
    }

    public abstract class AbilityTask : GameplayTask
    {
        public GameplayAbility Ability;
        public WeakReference<AbilitySystemComponent> AbilitySystemComponent;

        public bool ShouldBroadcastAbilityTaskDelegates
        {
            get
            {
                bool shouldBroadcast = Ability != null && Ability.IsActive();

                // config
                bool AbilitytaskwarnIfBroadcastSuppress = false;
                if (!shouldBroadcast && AbilitytaskwarnIfBroadcastSuppress)
                {
                    Debug.LogWarning("AbilityTask: ShouldBroadcastAbilityTaskDelegates: Ability is not active, but ShouldBroadcastAbilityTaskDelegates is true");
                }

                return shouldBroadcast;
            }
        }

        public void SetAbilitySystemComponent(AbilitySystemComponent abilitySystemComponent)
        {
            AbilitySystemComponent = new WeakReference<AbilitySystemComponent>(abilitySystemComponent);
        }

        public static T NewAbilityTask<T>(GameplayAbility owningAbility, string instanceName = null) where T : AbilityTask, new()
        {
            T newTask = new();
            newTask.InitTask(owningAbility, (owningAbility as IGameplayTaskOwnerInterface).GameplayTaskDefaultPriority);

            newTask.InstanceName = instanceName;

            return newTask;
        }
    }
}
