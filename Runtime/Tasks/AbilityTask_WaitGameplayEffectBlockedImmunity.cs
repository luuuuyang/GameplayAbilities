using UnityEngine;

namespace GameplayAbilities
{
    public delegate void WaitGameplayEffectBlockedImmunityDelegate(GameplayEffectSpecHandle blockedSpec, ActiveGameplayEffectHandle immunityGameplayEffectHandle);

    public class AbilityTask_WaitGameplayEffectBlockedImmunity : AbilityTask
    {
        public WaitGameplayEffectBlockedImmunityDelegate Blocked;
        public GameplayTagRequirements SourceTagRequirements;
        public GameplayTagRequirements TargetTagRequirements;
        public bool TriggerOnce;
        public bool ListenForPeriodicEffects;

        protected AbilitySystemComponent ExternalOwner;
        protected bool RegisteredCallback;
        protected bool UseExternalOwner;

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

        public static AbilityTask_WaitGameplayEffectBlockedImmunity WaitGameplayEffectBlockedByImmunity(GameplayAbility owningAbility, GameplayTagRequirements sourceTagRequirements, GameplayTagRequirements targetTagRequirements, GameObject optionalExternalTarget = null, bool triggerOnce = false)
        {
            AbilityTask_WaitGameplayEffectBlockedImmunity task = NewAbilityTask<AbilityTask_WaitGameplayEffectBlockedImmunity>(owningAbility);
            task.SourceTagRequirements = sourceTagRequirements;
            task.TargetTagRequirements = targetTagRequirements;
            task.TriggerOnce = triggerOnce;
            task.SetExternalOwner(optionalExternalTarget);

            return task;
        }

        protected override void OnDestroy(bool abilityIsEnding)
        {
            if (ASC != null)
            {
                RemoveDelegate();
            }

            base.OnDestroy(abilityIsEnding);
        }

        protected void ImmunityCallback(in GameplayEffectSpec blockedSpec, in ActiveGameplayEffect immunityGE)
        {
            bool passedComparison = false;

            if (!SourceTagRequirements.RequirementsMet(blockedSpec.CapturedSourceTags.AggregatedTags))
            {
                return;
            }

            if (!TargetTagRequirements.RequirementsMet(blockedSpec.CapturedTargetTags.AggregatedTags))
            {
                return;
            }

            GameplayEffectSpecHandle specHandle = new(new GameplayEffectSpec(blockedSpec));

            if (ShouldBroadcastAbilityTaskDelegates)
            {
                Blocked?.Invoke(specHandle, immunityGE.Handle);
            }

            if (TriggerOnce)
            {
                EndTask();
            }
        }

        protected void RegisterDelegate()
        {
            AbilitySystemComponent asc = ASC;
            if (asc != null)
            {
                asc.OnImmunityBlockGameplayEffectDelegate += ImmunityCallback;
            }
        }

        protected void RemoveDelegate()
        {
            AbilitySystemComponent asc = ASC;
            if (asc != null)
            {
                asc.OnImmunityBlockGameplayEffectDelegate -= ImmunityCallback;
            }
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
