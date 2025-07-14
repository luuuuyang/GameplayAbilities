using GameplayTags;

namespace GameplayAbilities
{
    public delegate void WaitGameplayTagCountDelegate(int tagCount);

    public class AbilityTask_WaitGameplayTagCountChanged : AbilityTask
    {
        protected WaitGameplayTagCountDelegate TagCountChanged;
        protected GameplayTag Tag;
        protected AbilitySystemComponent OptionalExternalTarget;

        protected AbilitySystemComponent TargetASC
        {
            get
            {
                if (OptionalExternalTarget != null)
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
            base.Activate();

            AbilitySystemComponent asc = TargetASC;
            if (asc != null)
            {
                var handle = asc.RegisterGameplayTagEvent(Tag, GameplayTagEventType.AnyCountChange);
                handle.AddListener(GameplayTagCallback);
            }
        }

        protected override void OnDestroy(bool abilityIsEnding)
        {
            AbilitySystemComponent asc = TargetASC;
            if (asc != null)
            {
                asc.UnregisterGameplayTagEvent(GameplayTagCallback, Tag, GameplayTagEventType.AnyCountChange);
            }

            base.OnDestroy(abilityIsEnding);
        }

        protected void GameplayTagCallback(GameplayTag tag, int newCount)
        {
            if (ShouldBroadcastAbilityTaskDelegates)
            {
                TagCountChanged?.Invoke(newCount);
            }
        }
    }
}

