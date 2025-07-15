using System.Collections.Generic;
using GameplayTags;
using UnityEngine;
using UnityEngine.Events;

namespace GameplayAbilities
{
    public delegate void AsyncWaitGameplayTagQueryDelegate();

    public class AbilityAsync_WaitGameplayTagQuery : AbilityAsync
    {
        public AsyncWaitGameplayTagQueryDelegate Triggered;
        protected GameplayTagQuery TagQuery;
        protected WaitGameplayTagQueryTriggerCondition TriggerCondition = WaitGameplayTagQueryTriggerCondition.WhenTrue;
        protected bool OnlyTriggerOnce;
        protected bool RegisteredCallback;
        protected bool QueryState;
        protected GameplayTagContainer TargetTags;
        protected Dictionary<GameplayTag, UnityAction<GameplayTag, int>> TagHandleMap;

        protected override void Activate()
        {
            base.Activate();

            AbilitySystemComponent asc = ShouldBroadcastDelegates ? AbilitySystemComponent : null;
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
            else
            {
                EndAction();
            }
        }

        public static AbilityAsync_WaitGameplayTagQuery WaitGameplayTagQueryOnActor
        (
            GameObject targetActor,
            in GameplayTagQuery tagQuery,
            in WaitGameplayTagQueryTriggerCondition triggerCondition = WaitGameplayTagQueryTriggerCondition.WhenTrue,
            in bool onlyTriggerOnce = false
        )
        {
            AbilityAsync_WaitGameplayTagQuery async = new()
            {
                AbilityActor = targetActor,
                TagQuery = tagQuery,
                TriggerCondition = triggerCondition,
                OnlyTriggerOnce = onlyTriggerOnce,
            };

            return async;
        }

        protected void UpdateTargetTags(GameplayTag tag, int newCount)
        {
            if (ShouldBroadcastDelegates)
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
            else
            {
                EndAction();
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
                    EndAction();
                }
            }
        }

        public override void EndAction()
        {
            AbilitySystemComponent asc = AbilitySystemComponent;
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

            base.EndAction();
        }
    }
}
