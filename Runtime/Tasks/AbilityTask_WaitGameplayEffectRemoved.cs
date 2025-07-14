using UnityEngine;

namespace GameplayAbilities
{
    public delegate void WaitGameplayEffectRemovedDelegate(in GameplayEffectRemovalInfo gameplayEffectRemovalInfo);

    public class AbilityTask_WaitGameplayEffectRemoved : AbilityTask
    {
        public WaitGameplayEffectRemovedDelegate OnRemoved;
        public WaitGameplayEffectRemovedDelegate InvalidHandle;
        public ActiveGameplayEffectHandle Handle;

        protected bool Registered;

        protected override void Activate()
        {
            GameplayEffectRemovalInfo emptyGameplayEffectRemovalInfo = new();

            if (!Handle.IsValid())
            {
                if (ShouldBroadcastAbilityTaskDelegates)
                {
                    InvalidHandle?.Invoke(emptyGameplayEffectRemovalInfo);
                }

                EndTask();
                return;
            }

            AbilitySystemComponent effectOwningAbilitySystemComponent = Handle.OwningAbilitySystemComponent;
            if (effectOwningAbilitySystemComponent != null)
            {
                OnActiveGameplayEffectRemoved_Info del = effectOwningAbilitySystemComponent.OnGameplayEffectRemoved_InfoDelegate(Handle);
                if (del != null)
                {
                    del.AddListener(OnGameplayEffectRemoved);
                    Registered = true;
                }
            }

            if (!Registered)
            {
                OnGameplayEffectRemoved(emptyGameplayEffectRemovalInfo);
            }
        }

        public static AbilityTask_WaitGameplayEffectRemoved WaitForGameplayEffectRemoved(GameplayAbility owningAbility, ActiveGameplayEffectHandle handle)
        {
            AbilityTask_WaitGameplayEffectRemoved task = NewAbilityTask<AbilityTask_WaitGameplayEffectRemoved>(owningAbility);
            task.Handle = handle;

            return task;
        }

        protected override void OnDestroy(bool abilityIsEnding)
        {
            AbilitySystemComponent effectOwningAbilitySystemComponent = Handle.OwningAbilitySystemComponent;
            if (effectOwningAbilitySystemComponent != null)
            {
                OnActiveGameplayEffectRemoved_Info del = effectOwningAbilitySystemComponent.OnGameplayEffectRemoved_InfoDelegate(Handle);
                if (del != null)
                {
                    del.RemoveListener(OnGameplayEffectRemoved);
                }
            }

            base.OnDestroy(abilityIsEnding);
        }

        protected void OnGameplayEffectRemoved(GameplayEffectRemovalInfo gameplayEffectRemovalInfo)
        {
            if (ShouldBroadcastAbilityTaskDelegates)
            {
                OnRemoved?.Invoke(gameplayEffectRemovalInfo);
            }

            EndTask();
        }
    }
}
