using System.Collections.Generic;
using GameplayTags;
using UnityEngine;
using UnityEngine.Events;

namespace GameplayAbilities
{
    public delegate void WaitGameplayTagQueryDelegate();

    public enum WaitGameplayTagQueryTriggerCondition : byte
    {
        WhenTrue,
        WhenFalse,
    }

    public class AbilityTask_WaitGameplayTagQuery : AbilityTask
    {
        protected GameplayTagQuery TagQuery;
        protected bool RegisteredCallback;
        protected bool QueryState;
        protected GameplayTagContainer TargetTags;
        protected Dictionary<GameplayTag, UnityAction<GameplayTag, int>> TagHandleMap;
        protected WaitGameplayTagQueryDelegate Triggered;
        protected AbilitySystemComponent OptionalExternalTarget;
        protected WaitGameplayTagQueryTriggerCondition TriggerCondition = WaitGameplayTagQueryTriggerCondition.WhenTrue;
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

                if (AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
                {
                    return abilitySystemComponent;
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
                QueryState = TriggerCondition != WaitGameplayTagQueryTriggerCondition.WhenTrue;

                List<GameplayTag> queryTags = new(TagQuery.TagDictionary);

                foreach (GameplayTag tag in queryTags)
                {
                    if (!TagHandleMap.ContainsKey(tag))
                    {
                        UpdateTargetTags(tag, asc.GetTagCount(tag));
                        asc.RegisterGameplayTagEvent(tag).AddListener(UpdateTargetTags);
                        TagHandleMap.Add(tag, UpdateTargetTags);
                    }
                }

                EvaluateTagQuery();

                RegisteredCallback = true;
            }
        }

        public void SetExternalTarget(GameObject actor)
        {
            if (actor != null)
            {
                UseExternalTarget = true;
                OptionalExternalTarget = AbilitySystemGlobals.GetAbilitySystemComponentFromActor(actor);
            }
        }

        public static AbilityTask_WaitGameplayTagQuery WaitGameplayTagQuery(GameplayAbility owningAbility, in GameplayTagQuery tagQuery, in GameObject optionalExternalTarget, in WaitGameplayTagQueryTriggerCondition triggerCondition, in bool onlyTriggerOnce)
        {
            AbilityTask_WaitGameplayTagQuery task = NewAbilityTask<AbilityTask_WaitGameplayTagQuery>(owningAbility);
            task.TagQuery = tagQuery;
            task.SetExternalTarget(optionalExternalTarget);
            task.TriggerCondition = triggerCondition;
            task.OnlyTriggerOnce = onlyTriggerOnce;

            return task;
        }

        protected void UpdateTargetTags(GameplayTag tag, int newCount)
        {
            if (newCount <= 0)
            {
                TargetTags.RemoveTag(tag);
            }
            else
            {
                TargetTags.AddTag(tag);
            }

            if (RegisteredCallback)
            {
                EvaluateTagQuery();
            }
        }

        protected void EvaluateTagQuery()
        {
            if (TagQuery.IsEmpty())
            {
                return;
            }

            bool matchesQuery = TagQuery.Matches(TargetTags);
            bool stateChanged = matchesQuery != QueryState;
            QueryState = matchesQuery;

            bool triggerDelegate = false;
            if (stateChanged)
            {
                if (TriggerCondition == WaitGameplayTagQueryTriggerCondition.WhenTrue && QueryState)
                {
                    triggerDelegate = true;
                }
                else if (TriggerCondition == WaitGameplayTagQueryTriggerCondition.WhenFalse && !QueryState)
                {
                    triggerDelegate = true;
                }
            }

            if (triggerDelegate)
            {
                Triggered?.Invoke();
                if (OnlyTriggerOnce)
                {
                    EndTask();
                }
            }
        }

        protected override void OnDestroy(bool abilityIsEnding)
        {
            AbilitySystemComponent asc = RegisteredCallback ? TargetASC : null;
            if (asc != null)
            {
                foreach (KeyValuePair<GameplayTag, UnityAction<GameplayTag, int>> pair in TagHandleMap)
                {
                    if (pair.Value != null)
                    {
                        asc.UnregisterGameplayTagEvent(pair.Value, pair.Key);
                    }
                }
            }

            TagHandleMap.Clear();
            TargetTags.Reset();

            base.OnDestroy(abilityIsEnding);
        }
    }
}
