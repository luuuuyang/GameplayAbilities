using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    public enum WaitAttributeChangeComparison
    {
        None,
        GreaterThan,
        LessThan,
        GreaterThanOrEqualTo,
        LessThanOrEqualTo,
        NotEqualTo,
        ExactlyEqualTo,
        Max,
    }

    public delegate void WaitAttributeChangeDelegate();

    public class AbilityTask_WaitAttributeChange : AbilityTask
    {
        public WaitAttributeChangeDelegate OnChanged;
        public GameplayTag WithTag;
        public GameplayTag WithoutTag;
        public GameplayAttribute Attribute;
        public WaitAttributeChangeComparison ComparisonType;
        public float ComparisonValue;
        public bool TriggerOnce;

        protected AbilitySystemComponent ExternalOwner;

        protected override void Activate()
        {

        }

        public void OnAttributeChanged(in OnAttributeChangeData callbackData)
        {
            float newValue = callbackData.NewValue;
            GameplayEffectModCallbackData data = callbackData.GEModData;

            if (data == null)
            {
                if (WithTag.IsValid())
                {
                    return;
                }
            }
            else
            {
                if (WithTag.IsValid() && !data.EffectSpec.CapturedSourceTags.AggregatedTags.HasTag(WithTag) ||
                    WithoutTag.IsValid() && data.EffectSpec.CapturedSourceTags.AggregatedTags.HasTag(WithoutTag))
                {
                    return;
                }
            }

            bool passedComparison = true;
            switch (ComparisonType)
            {
                case WaitAttributeChangeComparison.ExactlyEqualTo:
                    passedComparison = newValue == ComparisonValue;
                    break;
                case WaitAttributeChangeComparison.GreaterThan:
                    passedComparison = newValue > ComparisonValue;
                    break;
                case WaitAttributeChangeComparison.GreaterThanOrEqualTo:
                    passedComparison = newValue >= ComparisonValue;
                    break;
                case WaitAttributeChangeComparison.LessThan:
                    passedComparison = newValue < ComparisonValue;
                    break;
                case WaitAttributeChangeComparison.LessThanOrEqualTo:
                    passedComparison = newValue <= ComparisonValue;
                    break;
                case WaitAttributeChangeComparison.NotEqualTo:
                    passedComparison = newValue != ComparisonValue;
                    break;
                default:
                    break;
            }
            if (passedComparison)
            {
                if (ShouldBroadcastAbilityTaskDelegates)
                {
                    OnChanged?.Invoke();
                }
                if (TriggerOnce)
                {
                    EndTask();
                }
            }
        }

        public static AbilityTask_WaitAttributeChange WaitForAttributeChange(GameplayAbility owningAbility, GameplayAttribute attribute, GameplayTag withTag, GameplayTag withoutTag, bool triggerOnce = true, GameObject optionalExternalOwner = null)
        {
            AbilityTask_WaitAttributeChange newTask = NewAbilityTask<AbilityTask_WaitAttributeChange>(owningAbility);
            newTask.WithTag = withTag;
            newTask.WithoutTag = withoutTag;
            newTask.Attribute = attribute;
            newTask.ComparisonType = WaitAttributeChangeComparison.None;
            newTask.TriggerOnce = triggerOnce;
            newTask.ExternalOwner = optionalExternalOwner ? AbilitySystemGlobals.GetAbilitySystemComponentFromActor(optionalExternalOwner) : null;

            return newTask;
        }

        public static AbilityTask_WaitAttributeChange WaitForAttributeChangeWithComparison(GameplayAbility owningAbility, GameplayAttribute attribute, GameplayTag withTag, GameplayTag withoutTag, WaitAttributeChangeComparison comparisonType, float comparisonValue, bool triggerOnce = true, GameObject optionalExternalOwner = null)
        {
            AbilityTask_WaitAttributeChange newTask = NewAbilityTask<AbilityTask_WaitAttributeChange>(owningAbility);
            newTask.WithTag = withTag;
            newTask.WithoutTag = withoutTag;
            newTask.Attribute = attribute;
            newTask.ComparisonType = comparisonType;
            newTask.ComparisonValue = comparisonValue;
            newTask.TriggerOnce = triggerOnce;
            newTask.ExternalOwner = optionalExternalOwner ? AbilitySystemGlobals.GetAbilitySystemComponentFromActor(optionalExternalOwner) : null;

            return newTask;
        }
    }
}
