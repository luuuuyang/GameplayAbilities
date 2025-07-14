using GameplayTags;

namespace GameplayAbilities
{
    public delegate void WaitAbilityCommitDelegate(GameplayAbility activatedAbility);

    public class AbilityTask_WaitAbilityCommit : AbilityTask
    {
        public WaitAbilityCommitDelegate OnCommit;
        public GameplayTag WithTag;
        public GameplayTag WithoutTag;
        public bool TriggerOnce;
        public GameplayTagQuery Query;

        protected override void Activate()
        {
            if (AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
            {
                abilitySystemComponent.AbilityCommittedCallbacks += OnAbilityCommit;
            }
        }

        protected void OnAbilityCommit(GameplayAbility activatedAbility)
        {
            GameplayTagContainer abilityTags = activatedAbility.AssetTags;

            if (WithTag.IsValid() && !abilityTags.HasTag(WithTag) || WithoutTag.IsValid() && abilityTags.HasTag(WithoutTag))
            {
                return;
            }

            if (!Query.IsEmpty())
            {
                if (!Query.Matches(abilityTags))
                {
                    return;
                }
            }

            if (ShouldBroadcastAbilityTaskDelegates)
            {
                OnCommit?.Invoke(activatedAbility);
            }

            if (TriggerOnce)
            {
                EndTask();
            }
        }

        protected override void OnDestroy(bool abilityIsEnding)
        {
            if (AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
            {
                abilitySystemComponent.AbilityCommittedCallbacks -= OnAbilityCommit;
            }

            base.OnDestroy(abilityIsEnding);
        }
    }
}
