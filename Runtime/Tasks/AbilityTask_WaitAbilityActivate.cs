using GameplayTags;

namespace GameplayAbilities
{
    public delegate void WaitAbilityActivateDelegate(GameplayAbility activatedAbility);

    public class AbilityTask_WaitAbilityActivate : AbilityTask
    {
        public WaitAbilityActivateDelegate OnActivate;
        public GameplayTag WithTag;
        public GameplayTag WithoutTag;
        public bool IncludeTriggeredAbilities;
        public bool TriggerOnce;
        public GameplayTagRequirements TagRequirements;
        public GameplayTagQuery Query;

        protected override void Activate()
        {
            if (AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
            {
                abilitySystemComponent.AbilityActivatedCallbacks += OnAbilityActivate;
            }
        }

        protected void OnAbilityActivate(GameplayAbility activatedAbility)
        {
            if (!IncludeTriggeredAbilities && activatedAbility.IsTriggered())
            {
                return;
            }

            GameplayTagContainer abilityTags = activatedAbility.AssetTags;

            if (TagRequirements.IsEmpty())
            {
                if (WithTag.IsValid() && !abilityTags.HasTag(WithTag) || WithoutTag.IsValid() && abilityTags.HasTag(WithoutTag))
                {
                    return;
                }
            }
            else
            {
                if (!TagRequirements.RequirementsMet(abilityTags))
                {
                    return;
                }
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
                OnActivate?.Invoke(activatedAbility);
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
                abilitySystemComponent.AbilityActivatedCallbacks -= OnAbilityActivate;
            }

            base.OnDestroy(abilityIsEnding);
        }
    }
}
