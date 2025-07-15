using GameplayTags;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;
using System.Linq;
using System.Text;


namespace GameplayAbilities
{
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

    [Serializable]
    public record GameplayModEvaluationChannelSettings
    {
        [SerializeField]
        private GameplayModEvaluationChannel Channel;

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
    }

    public enum GameplayModOp
    {
        [LabelText("Add (Base)")]
        AddBase,

        [LabelText("Multiply (Additive)")]
        MultiplyAdditive,

        [LabelText("Divide (Additive)")]
        DivideAdditive,

        [LabelText("Multiply (Compound)")]
        MultiplyCompound = 4,

        [LabelText("Add (Final)")]
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
        public ActiveGameplayEffectHandle Handle;
        public bool IsValid;

        public GameplayModifierEvaluatedData()
        {
            ModifierOp = GameplayModOp.Additive;
            Magnitude = 0;
            IsValid = false;
        }

        public GameplayModifierEvaluatedData(in GameplayAttribute attribute, GameplayModOp modifierOp, float magnitude, ActiveGameplayEffectHandle handle = null)
        {
            Attribute = attribute;
            ModifierOp = modifierOp;
            Magnitude = magnitude;
            Handle = handle ?? new();
            IsValid = true;
        }

        public override string ToString()
        {
            return $"{Attribute} {ModifierOp} EvalMag: {Magnitude}";
        }
    }

    public record GameplayEffectContext
    {
        public WeakReference<GameObject> Instigator = new(null);
        public WeakReference<GameObject> EffectCauser = new(null);
        public WeakReference<UnityEngine.Object> SourceObject = new(null);
        public WeakReference<AbilitySystemComponent> InstigatorAbilitySystemComponent = new(null);
        public WeakReference<GameplayAbility> AbilityCdo = new(null);
        public WeakReference<GameplayAbility> AbilityInstanceNotReplicated = new(null);
        public List<WeakReference<GameObject>> Actors = new();
        public int AbilityLevel = 1;

        public GameplayEffectContext()
        {

        }

        public GameplayEffectContext(GameObject instigator, GameObject effectCauser)
        {
            AddInstigator(instigator, effectCauser);
        }

        public void AddInstigator(GameObject instigator, GameObject effectCauser)
        {
            Instigator.SetTarget(instigator);

            SetEffectCauser(effectCauser);

            InstigatorAbilitySystemComponent.SetTarget(null);

            if (Instigator.TryGetTarget(out GameObject target))
            {
                InstigatorAbilitySystemComponent.SetTarget(AbilitySystemGlobals.GetAbilitySystemComponentFromActor(target));
            }
        }

        public void SetEffectCauser(GameObject effectCauser)
        {
            EffectCauser.SetTarget(effectCauser);
        }

        public void SetAbility(GameplayAbility ability)
        {
            AbilityCdo.SetTarget(ability);
            AbilityInstanceNotReplicated.SetTarget(ability);
            AbilityLevel = ability.GetAbilityLevel();
        }

        public void AddSourceObject(UnityEngine.Object sourceObject)
        {
            SourceObject.SetTarget(sourceObject);
        }

        public void AddActors(List<WeakReference<GameObject>> actors, bool reset = false)
        {
            if (reset && Actors.Count > 0)
            {
                Actors.Clear();
            }

            Actors.AddRange(actors);
        }

        public void GetOwnedGameplayTags(GameplayTagContainer actorTagContainer, GameplayTagContainer specTagContainer)
        {
            if (InstigatorAbilitySystemComponent.TryGetTarget(out AbilitySystemComponent ASC))
            {
                ASC.GetOwnedGameplayTags(actorTagContainer);
            }
        }

        public virtual GameplayEffectContext Duplicate()
        {
            GameplayEffectContext newContext = new()
            {
                Instigator = Instigator,
                EffectCauser = EffectCauser,
                SourceObject = SourceObject,
                InstigatorAbilitySystemComponent = InstigatorAbilitySystemComponent,
                AbilityCdo = AbilityCdo,
                AbilityInstanceNotReplicated = AbilityInstanceNotReplicated,
                Actors = new List<WeakReference<GameObject>>(Actors),
                AbilityLevel = AbilityLevel
            };
            return newContext;
        }

        public override string ToString()
        {
            return Instigator.TryGetTarget(out GameObject target) ? target.name : "NONE";
        }
    }

    public record GameplayEffectContextHandle
    {
        private GameplayEffectContext Data;

        public GameplayEffectContextHandle()
        {

        }

        public GameplayEffectContextHandle(GameplayEffectContext data)
        {
            Data = data;
        }

        public bool IsValid => Data != null;

        public void GetOwnedGameplayTags(GameplayTagContainer actorTagContainer, GameplayTagContainer specTagContainer)
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

        public GameObject Instigator
        {
            get
            {
                if (IsValid && Data.Instigator.TryGetTarget(out GameObject instigator))
                {
                    return instigator;
                }
                return null;
            }
        }

        public AbilitySystemComponent InstigatorAbilitySystemComponent
        {
            get
            {
                if (IsValid && Data.InstigatorAbilitySystemComponent.TryGetTarget(out AbilitySystemComponent instigatorAbilitySystemComponent))
                {
                    return instigatorAbilitySystemComponent;
                }
                return null;
            }
        }

        public UnityEngine.Object SourceObject
        {
            get
            {
                if (IsValid && Data.SourceObject.TryGetTarget(out UnityEngine.Object sourceObject))
                {
                    return sourceObject;
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

        public void AddActors(List<WeakReference<GameObject>> actors, bool reset = false)
        {
            if (IsValid)
            {
                Data.AddActors(actors, reset);
            }
        }

        public List<WeakReference<GameObject>> Actors
        {
            get
            {
                if (IsValid)
                {
                    return Data.Actors;
                }

                return new List<WeakReference<GameObject>>();
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

        public override string ToString()
        {
            return IsValid ? Data.ToString() : "NONE";
        }
    }

    public struct GameplayEffectRemovalInfo
    {
        public bool PrematureRemoval;
        public int StackCount;
        public GameplayEffectContextHandle EffectContext;
        public ActiveGameplayEffect ActiveEffect;
    }

    public class GameplayCueParameters
    {
        public GameplayTag MatchedTagName;
        public GameplayTag OriginalTag;
        public GameplayTagContainer AggregatedSourceTags;
        public GameplayTagContainer AggregatedTargetTags;
        public WeakReference<GameObject> Instigator;
        public WeakReference<GameObject> EffectCauser;
    }

    public enum GameplayCueEventType
    {
        OnActive,
        WhileActive,
        Executed,
        Removed,
    }

    public class OnGivenActiveGameplayEffectRemoved : UnityEvent<ActiveGameplayEffect> { }
    public class OnActiveGameplayEffectRemoved_Info : UnityEvent<GameplayEffectRemovalInfo> { }
    public class OnActiveGameplayEffectStackChange : UnityEvent<ActiveGameplayEffectHandle, int, int> { }
    public class OnActiveGameplayEffectTimeChange : UnityEvent<ActiveGameplayEffectHandle, float, float> { }
    public class OnActiveGameplayEffectInhibitionChanged : UnityEvent<ActiveGameplayEffectHandle, bool> { }

    public class ActiveGameplayEffectEvents
    {
        public OnActiveGameplayEffectRemoved_Info OnEffectRemoved = new();
        public OnActiveGameplayEffectStackChange OnStackChanged = new();
        public OnActiveGameplayEffectTimeChange OnTimeChanged = new();
        public OnActiveGameplayEffectInhibitionChanged OnInhibitionChanged = new();
    }

    public class OnGameplayAttributeChange : UnityEvent<float, GameplayEffectModCallbackData> { }

    public struct OnAttributeChangeData
    {
        public GameplayAttribute Attribute;
        public float NewValue;
        public float OldValue;
        public GameplayEffectModCallbackData GEModData;
    }

    public class OnGameplayAttributeValueChange : UnityEvent<OnAttributeChangeData> { }

    public enum GameplayTagEventType
    {
        NewOrRemoved,
        AnyCountChange
    }

    [Serializable] public class OnGameplayEffectTagCountChanged : UnityEvent<GameplayTag, int> { }
    public delegate void DeferredTagChangeDelegate();

    public class GameplayTagCountContainer
    {
        public List<GameplayTag> GameplayTags = new();
        public List<GameplayTag> ParentTags = new();
        public GameplayTagContainer ExplicitGameplayTags => ExplicitTags;

        private Dictionary<GameplayTag, DelegateInfo> GameplayTagEventMap = new();
        private Dictionary<GameplayTag, int> GameplayTagCountMap = new();
        private Dictionary<GameplayTag, int> ExplicitTagCountMap = new();
        private OnGameplayEffectTagCountChanged OnAnyTagChangeDelegate;
        private GameplayTagContainer ExplicitTags = new();

        public class DelegateInfo
        {
            public OnGameplayEffectTagCountChanged OnNewOrRemove = new();
            public OnGameplayEffectTagCountChanged OnAnyChange = new();
        }

        public void Notify_StackCountChange(in GameplayTag tag)
        {
            GameplayTagContainer tagAndParentsContainer = new(tag.GetGameplayTagParents());
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
            if (!tag.IsValid())
            {
                return false;
            }

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
                info = new DelegateInfo();
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
            if (!tag.IsValid())
            {
                Debug.LogWarning($"GameplayTagCountContainer attempted to update tag map with an invalid Tag (None)!");
                return false;
            }

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
            GameplayTagContainer tagAndParentsContainer = new(tag.GetGameplayTagParents());
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
    public record GameplayTagRequirements
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

        public override string ToString()
        {
            StringBuilder str = new();

            if (RequireTags.Count > 0)
            {
                str.Append($"require: {RequireTags} ");
            }
            if (IgnoreTags.Count > 0)
            {
                str.Append($"ignore: {IgnoreTags} ");
            }
            if (!TagQuery.IsEmpty())
            {
                str.Append(TagQuery.Description);
            }

            return str.ToString();
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

    public record GameplayEffectSpecHandle
    {
        public GameplayEffectSpec Data;

        public GameplayEffectSpecHandle()
        {

        }

        public GameplayEffectSpecHandle(GameplayEffectSpec other)
        {
            Data = other;
        }

        public bool IsValid()
        {
            return Data != null;
        }
    }
}