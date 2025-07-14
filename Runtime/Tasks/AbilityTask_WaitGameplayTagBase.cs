using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    public class AbilityTask_WaitGameplayTag : AbilityTask
    {
        public GameplayTag Tag;

        protected bool RegisteredCallback;
        protected AbilitySystemComponent OptionalExternalTarget;
        protected bool UseExternalTarget;
        protected bool OnlyTriggerOnce;

        protected AbilitySystemComponent TargetASC
        {
            get
            {
                if (UseExternalTarget)
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
            AbilitySystemComponent asc = TargetASC;
            if (asc != null)
            {
                var handle = asc.RegisterGameplayTagEvent(Tag);
                handle.AddListener(GameplayTagCallback);
                RegisteredCallback = true;
            }
        }

        public virtual void GameplayTagCallback(GameplayTag tag, int newCount)
        {

        }

        protected override void OnDestroy(bool abilityIsEnding)
        {
            AbilitySystemComponent asc = TargetASC;
            if (RegisteredCallback && asc != null)
            {
                asc.UnregisterGameplayTagEvent(GameplayTagCallback, Tag);
            }

            base.OnDestroy(abilityIsEnding);
        }

        public void SetExternalTarget(GameObject actor)
        {
            if (actor != null)
            {
                UseExternalTarget = true;
                OptionalExternalTarget = AbilitySystemGlobals.GetAbilitySystemComponentFromActor(actor);
            }
        }
    }
}

