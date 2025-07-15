using UnityEngine;

namespace GameplayAbilities
{
    public class AbilityAsync_WaitGameplayEffectApplied : AbilityAsync
    {
        public delegate void OnAppliedDelegate(GameObject source, GameplayEffectSpecHandle specHandle, ActiveGameplayEffectHandle activeHandle);
        public OnAppliedDelegate OnApplied;

        protected GameplayTargetDataFilterHandle Filter;
        protected GameplayTagRequirements SourceTagRequirements;
        protected GameplayTagRequirements TargetTagRequirements;
        protected bool TriggerOnce;
        protected bool ListenForPeriodicEffects;
        protected bool Locked;

        protected override void Activate()
        {
            base.Activate();

            AbilitySystemComponent asc = AbilitySystemComponent;
            if (asc != null)
            {
                asc.OnGameplayEffectAppliedDelegateToSelf += OnApplyGameplayEffectCallback;
                if (ListenForPeriodicEffects)
                {
                    asc.OnPeriodicGameplayEffectExecutedDelegateOnSelf += OnApplyGameplayEffectCallback;
                }
            }
            else
            {
                EndAction();
            }
        }

        public static AbilityAsync_WaitGameplayEffectApplied WaitForGameplayEffectAppliedToActor(GameObject targetActor, GameplayTargetDataFilterHandle sourceFilter, GameplayTagRequirements sourceTagRequirements, GameplayTagRequirements targetTagRequirements, bool triggerOnce = false, bool listenForPeriodicEffects = false)
        {
            AbilityAsync_WaitGameplayEffectApplied async = new()
            {
                AbilityActor = targetActor,
                Filter = sourceFilter,
                SourceTagRequirements = sourceTagRequirements,
                TargetTagRequirements = targetTagRequirements,
                TriggerOnce = triggerOnce,
                ListenForPeriodicEffects = listenForPeriodicEffects,
            };

            return async;
        }

        public void OnApplyGameplayEffectCallback(AbilitySystemComponent target, in GameplayEffectSpec specApplied, ActiveGameplayEffectHandle activeHandle)
        {
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

            if (Locked)
            {
                return;
            }

            GameplayEffectSpecHandle specHandle = new(new GameplayEffectSpec(specApplied));

            if (ShouldBroadcastDelegates)
            {
                Locked = true;
                OnApplied?.Invoke(avatarActor, specHandle, activeHandle);

                if (TriggerOnce)
                {
                    EndAction();
                }
            }
            else
            {
                EndAction();
            }
        }

        public override void EndAction()
        {
            AbilitySystemComponent asc = AbilitySystemComponent;
            if (asc != null)
            {
                asc.OnPeriodicGameplayEffectExecutedDelegateOnSelf -= OnApplyGameplayEffectCallback;
                asc.OnGameplayEffectAppliedDelegateToSelf -= OnApplyGameplayEffectCallback;
            }

            base.EndAction();
        }
    }
}
