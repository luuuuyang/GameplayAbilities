namespace GameplayAbilities
{
    public delegate void WaitGameplayEffectStackChangeDelegate(ActiveGameplayEffectHandle handle, int newCount, int oldCount);

    public class AbilityTask_WaitGameplayEffectStackChange : AbilityTask
    {
        public WaitGameplayEffectStackChangeDelegate OnChange;
        public WaitGameplayEffectStackChangeDelegate InvalidHandle;
        public ActiveGameplayEffectHandle Handle;

        protected bool Registered;

        protected override void Activate()
        {
            if (!Handle.IsValid())
            {
                if (ShouldBroadcastAbilityTaskDelegates)
                {
                    InvalidHandle?.Invoke(Handle, 0, 0);
                }

                EndTask();
                return;
            }

            AbilitySystemComponent effectOwningAbilitySystemComponent = Handle.OwningAbilitySystemComponent;
            if (effectOwningAbilitySystemComponent != null)
            {
                OnActiveGameplayEffectStackChange del = effectOwningAbilitySystemComponent.OnGameplayEffectStackChangeDelegate(Handle);
                if (del != null)
                {
                    del.AddListener(OnGameplayEffectStackChange);
                    Registered = true;
                }
            }
        }

        public static AbilityTask_WaitGameplayEffectStackChange WaitForStackChange(GameplayAbility owningAbility, ActiveGameplayEffectHandle handle)
        {
            AbilityTask_WaitGameplayEffectStackChange task = NewAbilityTask<AbilityTask_WaitGameplayEffectStackChange>(owningAbility);
            task.Handle = handle;

            return task;
        }

        protected override void OnDestroy(bool abilityIsEnding)
        {
            AbilitySystemComponent effectOwningAbilitySystemComponent = Handle.OwningAbilitySystemComponent;
            if (effectOwningAbilitySystemComponent != null)
            {
                OnActiveGameplayEffectStackChange del = effectOwningAbilitySystemComponent.OnGameplayEffectStackChangeDelegate(Handle);
                if (del != null)
                {
                    del.RemoveListener(OnGameplayEffectStackChange);
                }
            }

            base.OnDestroy(abilityIsEnding);
        }

        public void OnGameplayEffectStackChange(ActiveGameplayEffectHandle handle, int newCount, int oldCount)
        {
            if (ShouldBroadcastAbilityTaskDelegates)
            {
                OnChange?.Invoke(handle, newCount, oldCount);
            }
        }
    }
}
