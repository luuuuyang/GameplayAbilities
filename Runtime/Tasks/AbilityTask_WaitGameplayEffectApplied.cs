using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    public class AbilityTask_WaitGameplayEffectApplied : AbilityTask
    {
        public GameplayTargetDataFilterHandle Filter;
        public GameplayTagRequirements SourceTagRequirements;
        public GameplayTagRequirements TargetTagRequirements;
        public GameplayTagQuery SourceTagQuery;
        public GameplayTagQuery TargetTagQuery;
        public bool TriggerOnce;
        public bool ListenForPeriodicEffects;

        protected bool RegisteredCallback;
        protected bool UseExternalOwner;
        protected AbilitySystemComponent ExternalOwner;

        protected AbilitySystemComponent ASC
        {
            get
            {
                if (UseExternalOwner)
                {
                    return ExternalOwner;
                }

                if (AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
                {
                    return abilitySystemComponent;
                }

                return null;
            }
        }

        protected override void Activate()
        {
            if (ASC != null)
            {
                RegisterDelegate();
            }
        }

        protected override void OnDestroy(bool abilityEnded)
        {
            if (ASC != null)
            {
                RemoveDelegate();
            }

            base.OnDestroy(abilityEnded);
        }

        public void OnApplyGameplayEffectCallback(AbilitySystemComponent target, in GameplayEffectSpec specApplied, ActiveGameplayEffectHandle activeHandle)
        {
            bool passedComparison = false;

            GameObject avatarActor = target ? target.AvatarActor : null;

            if (!Filter.FilterPassesForActor(avatarActor))
            {
                return;
            }

            if (!SourceTagRequirements.RequirementsMet(specApplied.CapturedSourceTags.AggregatedTags))
            {
                return;
            }

            if (!TargetTagRequirements.RequirementsMet(specApplied.CapturedTargetTags.AggregatedTags))
            {
                return;
            }

            if (!SourceTagQuery.IsEmpty())
            {
                if (!SourceTagQuery.Matches(specApplied.CapturedSourceTags.AggregatedTags))
                {
                    return;
                }
            }

            if (!TargetTagQuery.IsEmpty())
            {
                if (!TargetTagQuery.Matches(specApplied.CapturedTargetTags.AggregatedTags))
                {
                    return;
                }
            }

            GameplayEffectSpecHandle specHandle = new(new GameplayEffectSpec(specApplied));

            BroadcastDelegate(avatarActor, specHandle, activeHandle);

            if (TriggerOnce)
            {
                EndTask();
            }
        }

        protected virtual void BroadcastDelegate(GameObject avatar, GameplayEffectSpecHandle specHandle, ActiveGameplayEffectHandle activeHandle)
        {

        }

        protected virtual void RegisterDelegate()
        {

        }

        protected virtual void RemoveDelegate()
        {
            
        }

        public void SetExternalActor(GameObject actor)
        {
            Filter.Filter.InitializeFilterContext(actor);
        }

        public void SetExternalOwner(GameObject actor)
        {
            if (actor != null)
            {
                UseExternalOwner = true;
                ExternalOwner = AbilitySystemGlobals.GetAbilitySystemComponentFromActor(actor);
            }
        }
    }
}
