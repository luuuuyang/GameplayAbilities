using GameplayTags;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace GameplayAbilities
{
    using OnGameplayAttributeChange = UnityEvent<float, GameplayEffectModCallbackData>;
    
    using OnGameplayAttributeValueChange = UnityEvent<OnAttributeChangeData>;

    public enum GameplayModEvaluationChannel
    {
        Channel0,
        Channel1,
        Channel2,
        Channel3,
        Channel4,
        Channel5,
        Channel6,
        Channel7,
        Channel8,
        Channel9,
        [HideInInspector]
        ChannelMax
    }

    public struct GameplayModEvaluationChannelSettings
    {
        public GameplayModEvaluationChannel Channel;

        public GameplayModEvaluationChannel EvaluationChannel
        {
            get
            {
                if (AbilitySystemGlobals.Instance.IsGameplayModEvaluationChannelValid(Channel))
                {
                    return Channel;
                }
                return GameplayModEvaluationChannel.Channel0;
            }
        }

        public static bool operator ==(GameplayModEvaluationChannelSettings a, GameplayModEvaluationChannelSettings b)
        {
            return a.EvaluationChannel == b.EvaluationChannel;
        }

        public static bool operator !=(GameplayModEvaluationChannelSettings a, GameplayModEvaluationChannelSettings b)
        {
            return !(a == b);
        }
    }

    public enum GameplayModOp
    {
#if ODIN_INSPECTOR
        [LabelText("Add (Base)")]
#endif
        AddBase,
#if ODIN_INSPECTOR
        [LabelText("Multiply (Additive)")]
#endif
        MultiplyAdditive,
#if ODIN_INSPECTOR
        [LabelText("Divide (Additive)")]
#endif
        DivideAdditive,
#if ODIN_INSPECTOR
        [LabelText("Multiply (Compound)")]
#endif
        MultiplyCompound = 4,
#if ODIN_INSPECTOR
        [LabelText("Add (Final)")]
#endif
        AddFinal,
        [HideInInspector]
        [LabelText("Invalid")]
        Max,
        [HideInInspector]
        Additive = 0,
        [HideInInspector]
        Multiplicitive = 1,
        [HideInInspector]
        Division = 2,
        Override = 3,
    }

    public static class GameplayEffectUtilities
    {
        private static readonly float[] ModifierOpBiases = { 0, 1, 1, 0, 0, 0 };

        public static float GetModifierBiasByModifierOp(GameplayModOp modOp)
        {
            return ModifierOpBiases[(int)modOp];
        }

        public static float ComputeStackedModifierMagnitude(float baseComputedMagnitude, int stackCount, GameplayModOp modOp)
        {
            float operationBias = GetModifierBiasByModifierOp(modOp);

            stackCount = Mathf.Clamp(stackCount, 0, stackCount);

            float stackMag = baseComputedMagnitude;

            if (modOp != GameplayModOp.Override)
            {
                stackMag -= operationBias;
                stackMag *= stackCount;
                stackMag += operationBias;
            }

            return stackMag;
        }
    }

    public enum GameplayEffectStackingType
    {
        None,
        AggregateBySource,
        AggregateByTarget
    }

    public class GameplayModifierEvaluatedData
    {
        public GameplayAttribute Attribute;
        public GameplayModOp ModifierOp;
        public float Magnitude;

        public GameplayModifierEvaluatedData(in GameplayAttribute attribute, in GameplayModOp modifierOp, in float magnitude)
        {
            Attribute = attribute;
            ModifierOp = modifierOp;
            Magnitude = magnitude;
        }
    }

    public class GameplayEffectContext
    {
        public GameObject Instigator;
        public GameObject EffectCauser;
        public UnityEngine.Object SourceObject;
        public AbilitySystemComponent InstigatorAbilitySystemComponent;
        public GameplayAbility AbilityCdo;
        public GameplayAbility AbilityInstanceNotReplicated;
        public List<GameObject> Actors = new();
        public int AbilityLevel;

        public void AddInstigator(GameObject instigator, GameObject effectCauser)
        {
            Instigator = instigator;
            SetEffectCauser(effectCauser);

            InstigatorAbilitySystemComponent = null;
            InstigatorAbilitySystemComponent = AbilitySystemGlobals.GetAbilitySystemComponentFromActor(Instigator);
        }

        public void SetEffectCauser(GameObject effectCauser)
        {
            EffectCauser = effectCauser;
        }

        public void SetAbility(GameplayAbility ability)
        {
            AbilityCdo = ability;
            AbilityInstanceNotReplicated = ability;
            AbilityLevel = ability.GetAbilityLevel();
        }

        public void AddSourceObject(UnityEngine.Object sourceObject)
        {
            SourceObject = sourceObject;
        }

        public void AddActors(List<GameObject> actors, bool reset = false)
        {
            if (reset && Actors.Count > 0)
            {
                Actors.Clear();
            }

            Actors.AddRange(actors);
        }

        public void GetOwnedGameplayTags(GameplayTagContainer actor_tag_container, GameplayTagContainer spec_tag_container)
        {
            if (InstigatorAbilitySystemComponent != null)
            {
                InstigatorAbilitySystemComponent.GetOwnedGameplayTags(actor_tag_container);
            }
        }

        public virtual GameplayEffectContext Duplicate()
        {
            GameplayEffectContext newContext = new GameplayEffectContext();
            newContext.Instigator = Instigator;
            newContext.EffectCauser = EffectCauser;
            newContext.SourceObject = SourceObject;
            newContext.InstigatorAbilitySystemComponent = InstigatorAbilitySystemComponent;
            newContext.AbilityCdo = AbilityCdo;
            newContext.AbilityInstanceNotReplicated = AbilityInstanceNotReplicated;
            return newContext;
        }
    }

    public struct GameplayEffectContextHandle
    {
        private GameplayEffectContext Data;

        public GameplayEffectContextHandle(GameplayEffectContext data)
        {
            Data = data;
        }

        public readonly bool IsValid => Data != null;

        public readonly void GetOwnedGameplayTags(GameplayTagContainer actorTagContainer, GameplayTagContainer specTagContainer)
        {
            if (IsValid)
            {
                Data.GetOwnedGameplayTags(actorTagContainer, specTagContainer);
            }
        }

        public void AddInstigator(GameObject instigator, GameObject effectCauser)
        {
            if (IsValid)
            {
                Data.AddInstigator(instigator, effectCauser);
            }
        }

        public void SetAbility(in GameplayAbility gameplayAbility)
        {
            if (IsValid)
            {
                Data.SetAbility(gameplayAbility);
            }
        }

        public readonly AbilitySystemComponent InstigatorAbilitySystemComponent
        {
            get
            {
                if (IsValid)
                {
                    return Data.InstigatorAbilitySystemComponent;
                }
                return null;
            }
        }

        public readonly UnityEngine.Object SourceObject
        {
            get
            {
                if (IsValid)
                {
                    return Data.SourceObject;
                }
                return null;
            }
        }

        public void AddSourceObject(UnityEngine.Object sourceObject)
        {
            if (IsValid)
            {
                Data.AddSourceObject(sourceObject);
            }
        }

        public void AddActors(List<GameObject> actors, bool reset = false)
        {
            if (IsValid)
            {
                Data.AddActors(actors, reset);
            }
        }

        public GameplayEffectContextHandle Duplicate()
        {
            if (IsValid)
            {
                GameplayEffectContext newContext = Data.Duplicate();
                return new GameplayEffectContextHandle(newContext);
            }
            else
            {
                return new GameplayEffectContextHandle();
            }
        }

        public static bool operator ==(GameplayEffectContextHandle a, GameplayEffectContextHandle b)
        {
            if (a.IsValid != b.IsValid)
            {
                return false;
            }
            if (a.Data != b.Data)
            {
                return false;
            }
            return true;
        }

        public static bool operator !=(GameplayEffectContextHandle a, GameplayEffectContextHandle b)
        {
            return !(a == b);
        }
    }

    public struct GameplayEffectRemovalInfo
    {
        public bool PrematureRemoval;
        public int StackCount;
        public GameplayEffectContextHandle EffectContext;
        public ActiveGameplayEffect ActiveEffect;
    }

    public delegate void OnGivenActiveGameplayEffectRemoved(in ActiveGameplayEffect effect);
    public delegate void OnActiveGameplayEffectRemoved_Info(GameplayEffectRemovalInfo removalInfo);
    public delegate void OnActiveGameplayEffectStackChange(ActiveGameplayEffectHandle handle, int newStackCount, int previousStackCount);
    public delegate void OnActiveGameplayEffectTimeChange(ActiveGameplayEffectHandle handle, float newStartTime, float newDuration);
    public delegate void OnActiveGameplayEffectInhibitionChanged(ActiveGameplayEffectHandle handle, bool isInhibited);

    public class ActiveGameplayEffectEvents
    {
        public OnActiveGameplayEffectRemoved_Info OnEffectRemoved;
        public OnActiveGameplayEffectStackChange OnStackChanged;
        public OnActiveGameplayEffectTimeChange OnTimeChanged;
        public OnActiveGameplayEffectInhibitionChanged OnInhibitionChanged;
    }

    public struct OnAttributeChangeData
    {
        public GameplayAttribute Attribute;
        public float NewValue;
        public float OldValue;
        public GameplayEffectModCallbackData GEModData;
    }

    public enum GameplayTagEventType
    {
        NewOrRemoved,
        AnyCountChange
    }

    public delegate void OnGameplayEffectTagCountChanged(GameplayTag tag, int count_delta);
    public delegate void DeferredTagChangeDelegate();

    public class GameplayTagCountContainer
    {
        public Dictionary<GameplayTag, DelegateInfo> GameplayTagEventMap = new();

        public List<GameplayTag> GameplayTags = new();

        public List<GameplayTag> ParentTags = new();

        public Dictionary<GameplayTag, int> GameplayTagCountMap = new();

        public Dictionary<GameplayTag, int> ExplicitTagCountMap = new();

        public OnGameplayEffectTagCountChanged OnAnyTagChangeDelegate;

        public GameplayTagContainer ExplicitTags = new();

        public struct DelegateInfo
        {
            public OnGameplayEffectTagCountChanged OnNewOrRemove;
            public OnGameplayEffectTagCountChanged OnAnyChange;
        }

        public void Notify_StackCountChange(in GameplayTag tag)
        {
            GameplayTagContainer tagAndParentsContainer = tag.GetGameplayTagParents();
            foreach (GameplayTag curTag in tagAndParentsContainer)
            {
                if (GameplayTagEventMap.TryGetValue(curTag, out DelegateInfo delegateInfo))
                {
                    if (!GameplayTagCountMap.TryGetValue(curTag, out int tagCount))
                    {
                        GameplayTagCountMap.Add(curTag, 0);
                    }
                    delegateInfo.OnAnyChange?.Invoke(curTag, tagCount);
                }
            }
        }

        public void FillParentTags()
        {
            ExplicitTags.FillParentTags();
        }

        public bool HasMatchingGameplayTag(GameplayTag tag)
        {
            return GameplayTagCountMap.GetValueOrDefault(tag) > 0;
        }

        public bool HasAllMatchingGameplayTags(in GameplayTagContainer tagContainer)
        {
            if (tagContainer.Count == 0)
            {
                return true;
            }

            bool allMatch = true;
            foreach (GameplayTag tag in tagContainer)
            {
                if (GameplayTagCountMap.GetValueOrDefault(tag) <= 0)
                {
                    allMatch = false;
                    break;
                }
            }
            return allMatch;
        }

        public bool HasAnyMatchingGameplayTags(in GameplayTagContainer tagContainer)
        {
            if (tagContainer.Count == 0)
            {
                return false;
            }

            bool anyMatch = false;
            foreach (GameplayTag tag in tagContainer)
            {
                if (GameplayTagCountMap.GetValueOrDefault(tag) > 0)
                {
                    anyMatch = true;
                    break;
                }
            }
            return anyMatch;
        }

        public bool SetTagCount(in GameplayTag tag, int newCount)
        {
            int existingCount = ExplicitTagCountMap.GetValueOrDefault(tag);

            int countDelta = newCount - existingCount;
            if (countDelta != 0)
            {
                return UpdateTagMap_Internal(tag, countDelta);
            }

            return false;
        }

        public int GetTagCount(in GameplayTag tag)
        {
            return GameplayTagCountMap.GetValueOrDefault(tag);
        }

        public int GetExplicitTagCount(in GameplayTag tag)
        {
            return ExplicitTagCountMap.GetValueOrDefault(tag);
        }

        public void UpdateTagCount(in GameplayTagContainer container, in int countDelta)


        {
            if (countDelta != 0)
            {
                bool updateAny = false;
                List<DeferredTagChangeDelegate> deferredTagChangeDelegates = new();
                foreach (GameplayTag tag in container)
                {
                    updateAny |= UpdateTagMapDeferredParentRemoval_Internal(tag, countDelta, deferredTagChangeDelegates);
                }

                if (updateAny && countDelta < 0)
                {
                    ExplicitTags.FillParentTags();
                }

                foreach (DeferredTagChangeDelegate @delegate in deferredTagChangeDelegates)
                {
                    @delegate.Invoke();
                }
            }
        }

        public bool UpdateTagCount(in GameplayTag tag, int countDelta)
        {
            if (countDelta != 0)
            {
                return UpdateTagMap_Internal(tag, countDelta);
            }

            return false;
        }

        public bool UpdateTagCount_DeferredParentRemoval(in GameplayTag tag, int countDelta, List<DeferredTagChangeDelegate> deferredTagChangeDelegates)
        {
            if (countDelta != 0)
            {
                return UpdateTagMapDeferredParentRemoval_Internal(tag, countDelta, deferredTagChangeDelegates);
            }

            return false;
        }

        public OnGameplayEffectTagCountChanged RegisterGameplayTagEvent(in GameplayTag gameplayTag, GameplayTagEventType eventType = GameplayTagEventType.NewOrRemoved)
        {
            if (!GameplayTagEventMap.TryGetValue(gameplayTag, out DelegateInfo info))
            {
                GameplayTagEventMap.Add(gameplayTag, info);
            }

            if (eventType == GameplayTagEventType.NewOrRemoved)
            {
                return info.OnNewOrRemove;
            }

            return info.OnAnyChange;
        }

        public void Reset(bool resetCallbacks = true)
        {
            GameplayTagCountMap.Clear();
            ExplicitTagCountMap.Clear();
            ExplicitTags.Reset();

            if (resetCallbacks)
            {
                GameplayTagEventMap.Clear();
                OnAnyTagChangeDelegate = null;
            }
        }

        private bool UpdateTagMap_Internal(in GameplayTag tag, int countDelta)
        {
            if (!UpdateExplicitTags(tag, countDelta, false))
            {
                return false;
            }

            List<DeferredTagChangeDelegate> deferredTagChangeDelegates = new();
            bool significantChange = GatherTagChangeDelegates(tag, countDelta, deferredTagChangeDelegates);
            foreach (DeferredTagChangeDelegate @delegate in deferredTagChangeDelegates)
            {
                @delegate.Invoke();
            }

            return significantChange;
        }

        private bool UpdateTagMapDeferredParentRemoval_Internal(in GameplayTag tag, in int countDelta, List<DeferredTagChangeDelegate> deferredTagChangeDelegates)
        {
            if (!UpdateExplicitTags(tag, countDelta, true))
            {
                return false;
            }

            return GatherTagChangeDelegates(tag, countDelta, deferredTagChangeDelegates);
        }

        private bool UpdateExplicitTags(in GameplayTag tag, in int countDelta, in bool deferParentTagsOnRemove)
        {
            bool tagAlreadyExplicitlyExists = ExplicitTags.HasTagExact(tag);

            if (!tagAlreadyExplicitlyExists)
            {
                if (countDelta > 0)
                {
                    ExplicitTags.AddTag(tag);
                }
                else
                {
                    if (ExplicitTags.HasTag(tag))
                    {
                        Debug.LogWarning($"Attempted to remove tag: {tag} from tag count container, but it is not explicitly in the container!");
                    }
                    return false;
                }
            }

            if (!ExplicitTagCountMap.TryGetValue(tag, out int existingCount))
            {
                ExplicitTagCountMap.Add(tag, existingCount);
            }

            existingCount = Mathf.Max(existingCount + countDelta, 0);

            ExplicitTagCountMap[tag] = existingCount;

            if (existingCount <= 0)
            {
                ExplicitTags.RemoveTag(tag, deferParentTagsOnRemove);
            }

            return true;
        }

        private bool GatherTagChangeDelegates(in GameplayTag tag, in int countDelta, List<DeferredTagChangeDelegate> tagChangeDelegates)
        {
            GameplayTagContainer tagAndParentsContainer = tag.GetGameplayTagParents();
            bool createdSignificantChange = false;
            foreach (GameplayTag curTag in tagAndParentsContainer)
            {
                if (!GameplayTagCountMap.TryGetValue(curTag, out int tagCount))
                {
                    GameplayTagCountMap.Add(curTag, tagCount);
                }

                int oldCount = tagCount;

                int newTagCount = Mathf.Max(oldCount + countDelta, 0);
                tagCount = newTagCount;
                GameplayTagCountMap[curTag] = tagCount;

                bool significantChange = oldCount == 0 || newTagCount == 0;
                createdSignificantChange |= significantChange;
                if (significantChange)
                {
                    tagChangeDelegates.Add(() => OnAnyTagChangeDelegate?.Invoke(curTag, newTagCount));
                }

                if (GameplayTagEventMap.TryGetValue(curTag, out DelegateInfo delegateInfo))
                {
                    tagChangeDelegates.Add(() => delegateInfo.OnAnyChange?.Invoke(curTag, newTagCount));
                    if (significantChange)
                    {
                        tagChangeDelegates.Add(() => delegateInfo.OnNewOrRemove?.Invoke(curTag, newTagCount));
                    }
                }
            }

            return createdSignificantChange;
        }
    }

    [Serializable]
    public class GameplayTagRequirements
    {
        [LabelText("Must Have Tags")]
        public GameplayTagContainer RequireTags = new();

        [LabelText("Must Not Have Tags")]
        public GameplayTagContainer IgnoreTags = new();

        [LabelText("Query Must Match")]
        public GameplayTagQuery TagQuery = new();

        public bool RequirementsMet(in GameplayTagContainer container)
        {
            bool hasRequired = container.HasAll(RequireTags);
            bool hasIgnored = container.HasAny(IgnoreTags);
            bool matchQuery = TagQuery.IsEmpty() || TagQuery.Matches(container);

            return hasRequired && !hasIgnored && matchQuery;
        }

        public bool IsEmpty()
        {
            return RequireTags.IsEmpty() && IgnoreTags.IsEmpty() && TagQuery.IsEmpty();
        }

        public static bool operator ==(GameplayTagRequirements a, GameplayTagRequirements b)
        {
            return a.RequireTags == b.RequireTags && a.IgnoreTags == b.IgnoreTags && a.TagQuery == b.TagQuery;
        }

        public static bool operator !=(GameplayTagRequirements a, GameplayTagRequirements b)
        {
            return !(a == b);
        }

        public override string ToString()
        {
            string str = string.Empty;

            if (RequireTags.Count > 0)
            {
                str += $"require: {RequireTags} ";
            }
            if (IgnoreTags.Count > 0)
            {
                str += $"ignore: {IgnoreTags} ";
            }
            if (!TagQuery.IsEmpty())
            {
                str += TagQuery.Description;
            }

            return str;
        }

        public GameplayTagQuery ConvertTagFieldsToTagQuery()
        {
            bool hasRequiredTags = !RequireTags.IsEmpty();
            bool hasIgnoredTags = !IgnoreTags.IsEmpty();

            if (!hasRequiredTags && !hasIgnoredTags)
            {
                return new GameplayTagQuery();
            }

            GameplayTagQueryExpression requiredTagsQueryExpression = new GameplayTagQueryExpression().AllTagsMatch().AddTags(RequireTags);
            GameplayTagQueryExpression ignoreTagsQueryExpression = new GameplayTagQueryExpression().NoTagsMatch().AddTags(IgnoreTags);

            GameplayTagQueryExpression rootQueryExpression = new();
            if (hasRequiredTags && hasIgnoredTags)
            {
                rootQueryExpression = new GameplayTagQueryExpression().AllExprMatch().AddExpr(requiredTagsQueryExpression).AddExpr(ignoreTagsQueryExpression);
            }
            else if (hasRequiredTags)
            {
                rootQueryExpression = requiredTagsQueryExpression;
            }
            else if (hasIgnoredTags)
            {
                rootQueryExpression = ignoreTagsQueryExpression;
            }

            return GameplayTagQuery.BuildQuery(rootQueryExpression);
        }
    }

    public class TagContainerAggregator
    {
        private GameplayTagContainer CapturedActorTags = new();
        private GameplayTagContainer CapturedSpecTags = new();
        private GameplayTagContainer CachedAggregator = new();
        private bool CacheIsValid;

        public TagContainerAggregator()
        {
            CacheIsValid = false;
        }

        public TagContainerAggregator(TagContainerAggregator other)
        {
            CapturedActorTags = new GameplayTagContainer(other.CapturedActorTags);
            CapturedSpecTags = new GameplayTagContainer(other.CapturedSpecTags);
            CachedAggregator = new GameplayTagContainer(other.CachedAggregator);
            CacheIsValid = other.CacheIsValid;
        }

        public void CopyFrom(TagContainerAggregator other)
        {
            CapturedActorTags.CopyFrom(other.CapturedActorTags);
            CapturedSpecTags.CopyFrom(other.CapturedSpecTags);
            CachedAggregator.CopyFrom(other.CachedAggregator);
            CacheIsValid = other.CacheIsValid;
        }

        public GameplayTagContainer ActorTags
        {
            get
            {
                CacheIsValid = false;
                return CapturedActorTags;
            }
        }

        public GameplayTagContainer SpecTags
        {
            get
            {
                CacheIsValid = false;
                return CapturedSpecTags;
            }
        }

        public GameplayTagContainer AggregatedTags
        {
            get
            {
                if (!CacheIsValid)
                {
                    CacheIsValid = true;
                    CachedAggregator.Reset(CapturedActorTags.Count + CapturedSpecTags.Count);
                    CachedAggregator.AppendTags(CapturedActorTags);
                    CachedAggregator.AppendTags(CapturedSpecTags);
                }
                return CachedAggregator;
            }
        }
    }

    public struct GameplayEffectSpecHandle : ICloneable
    {
        public GameplayEffectSpec Data;

        public readonly bool IsValid()
        {
            return Data != null;
        }

        public object Clone()
        {
            return new GameplayEffectSpecHandle(Data);
        }

        public GameplayEffectSpecHandle(GameplayEffectSpec other)
        {
            Data = other;
        }

        public static bool operator ==(GameplayEffectSpecHandle a, GameplayEffectSpecHandle b)
        {
            bool bothValid = a.IsValid() && b.IsValid();
            bool bothInvalid = !a.IsValid() && !b.IsValid();
            return bothInvalid || (bothValid && a.Data == b.Data);
        }

        public static bool operator !=(GameplayEffectSpecHandle a, GameplayEffectSpecHandle b)
        {
            return !(a == b);
        }
    }
}