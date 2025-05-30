using GameplayTags;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System.Linq;
using UnityEngine.Events;

namespace GameplayAbilities
{
	public delegate void AbilityFailedDelegate(in GameplayAbility ability, in GameplayTagContainer failureReason);
	public delegate void AbilityEnded(in GameplayAbility ability);
	public delegate void ImmunityBlockGE(in GameplayEffectSpec blockedSpec, in ActiveGameplayEffect immunityGameplayEffect);
	public delegate bool GameplayEffectApplicationQuery(in ActiveGameplayEffectsContainer activeGEContainer, in GameplayEffectSpec geSpecToConsider);

	[DefaultExecutionOrder(-1)]
	public class AbilitySystemComponent : MonoBehaviour, IGameplayTagAssetInterface
	{
		public delegate void OnGameplayEffectAppliedDelegate(AbilitySystemComponent instigator, in GameplayEffectSpec spec, ActiveGameplayEffectHandle handle);
		public GameplayTagCountContainer GameplayTagCountContainer = new();
		public GameplayTagCountContainer BlockedAbilityTags = new();
		public ActiveGameplayEffectsContainer ActiveGameplayEffects = new();
		public GameplayAbilitySpecContainer ActivatableAbilities = new();
		public GameplayAbilityActorInfo AbilityActorInfo;
		[HideInInspector]
		public bool SuppressGrantAbility;
		[HideInInspector]
		public List<AttributeSet> SpawnedAttributes = new();
		public List<GameplayEffectApplicationQuery> GameplayEffectApplicationQueries = new();

		public ImmunityBlockGE OnImmunityBlockGameplayEffectDelegate;
		public OnGameplayEffectAppliedDelegate OnGameplayEffectAppliedToSelfDelegate;
		public OnGameplayEffectAppliedDelegate OnGameplayEffectAppliedToTargetDelegate;
		public OnGameplayEffectAppliedDelegate OnActiveGameplayEffectAddedDelegateToSelf;
		public OnGameplayEffectAppliedDelegate OnPeriodicGameplayEffectExecutedDelegateOnSelf;
		public OnGameplayEffectAppliedDelegate OnPeriodicGameplayEffectExecutedDelegateOnTarget;

		public GenericAbilityDelegate AbilityActivatedCallbacks;
		public AbilityEnded AbilityEndedCallbacks;
		public GameplayAbilityEndedDelegate OnAbilityEnded;
		public GenericAbilityDelegate AbilityCommittedCallbacks;
		public AbilityFailedDelegate AbilityFailedCallbacks;

		private Dictionary<GameplayTag, GameplayEventMulticastDelegate> GenericGameplayEventCallbacks = new();
		private List<KeyValuePair<GameplayTagContainer, GameplayEventTagMulticastDelegate>> GameplayEventTagContainerDelegates = new();

		private float OutgoingDuration;
		private float IncomingDuration;

		public static readonly Lazy<FieldInfo> OutgoingDurationProperty = new(
		() => typeof(AbilitySystemComponent).GetField(
			nameof(OutgoingDuration),
			BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new MissingFieldException(nameof(AbilitySystemComponent), nameof(OutgoingDuration))
		);

		public static readonly Lazy<FieldInfo> IncomingDurationProperty = new(
			() => typeof(AbilitySystemComponent).GetField(
				nameof(IncomingDuration),
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new MissingFieldException(nameof(AbilitySystemComponent), nameof(IncomingDuration))
		);

		// 使用 Lazy<T> 实现线程安全的延迟初始化
		public static readonly Lazy<GameplayEffectAttributeCaptureDefinition> OutgoingDurationCapture
			= new(() => new GameplayEffectAttributeCaptureDefinition(
				OutgoingDurationProperty.Value,
				GameplayEffectAttributeCaptureSource.Source,
				true
			));

		public static readonly Lazy<GameplayEffectAttributeCaptureDefinition> IncomingDurationCapture
			= new(() => new GameplayEffectAttributeCaptureDefinition(
				IncomingDurationProperty.Value,
				GameplayEffectAttributeCaptureSource.Target,
				false
			));

		[HideInInspector]
		public bool UserAbilityActivationInhibited;

		[HideInInspector]
		public GameObject OwnerActor;

		[HideInInspector]
		public GameObject AvatarActor;

		private GameplayTagContainer InternalTryActivateAbilityFailureTags = new();

		protected Dictionary<GameplayTag, List<GameplayAbilitySpecHandle>> GameplayEventTriggerdAbilities = new();
		protected Dictionary<GameplayTag, List<GameplayAbilitySpecHandle>> OwnedTagTriggeredAbilities = new();
		protected float AbilityLastActivatedTime;
		// protected Dictionary<GameplayAbilitySpecHandle, 

		// private readonly object abilityLock = new object();

		private void Awake()
		{
			ActiveGameplayEffects.RegisterWithOwner(this);

			AbilityActorInfo = AbilitySystemGlobals.Instance.AllocAbilityActorInfo();
			InitAbilityActorInfo(gameObject, gameObject);
		}

		protected virtual void Start()
		{

		}

		public virtual void InitAbilityActorInfo(GameObject inOwnerActor, GameObject inAvatarActor)
		{
			Debug.Assert(AbilityActorInfo != null);

			AbilityActorInfo.AvatarActor.TryGetTarget(out GameObject avatarActor);
			bool avatarChanged = inAvatarActor != avatarActor;

			AbilityActorInfo.InitFromActor(inOwnerActor, inAvatarActor, this);

			OwnerActor = inOwnerActor;
			AvatarActor = inAvatarActor;

			if (avatarChanged)
			{
				foreach (GameplayAbilitySpec spec in ActivatableAbilities.Items)
				{
					if (spec.Ability != null)
					{
						if (spec.Ability.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerActor)
						{
							GameplayAbility abilityInstance = spec.GetPrimaryInstance();
							if (abilityInstance != null)
							{
								abilityInstance.OnAvatarSet(AbilityActorInfo, spec);
							}
						}
						else
						{
							spec.Ability.OnAvatarSet(AbilityActorInfo, spec);
						}
					}
				}
			}
		}

		public T GetSet<T>() where T : AttributeSet
		{
			return GetAttributeSubobject(typeof(T)) as T;
		}

		public T GetSetChecked<T>() where T : AttributeSet
		{
			return GetAttributeSubobjectChecked(typeof(T)) as T;
		}

		public T AddSet<T>() where T : AttributeSet
		{
			return AddAttributeSetSubobject(ScriptableObject.CreateInstance<T>());
		}

		public AttributeSet InitStats(Type attributes)
		{
			AttributeSet attributeObj = null;

			if (attributes != null)
			{
				attributeObj = GetOrCreateAttributeSubobject(attributes);
			}

			return attributeObj;
		}

		public ActiveGameplayEffectHandle ApplyGameplayEffectToSelf(in GameplayEffect gameplayEffect, float level, in GameplayEffectContextHandle context)
		{
			if (gameplayEffect == null)
			{
				return new ActiveGameplayEffectHandle();
			}

			GameplayEffectSpec spec = new(gameplayEffect, context, level);
			return ApplyGameplayEffectSpecToSelf(spec);
		}

		public ActiveGameplayEffectHandle ApplyGameplayEffectSpecToSelf(in GameplayEffectSpec spec)
		{
			if (!spec.Def.CanApply(ActiveGameplayEffects, spec))
			{
				return new ActiveGameplayEffectHandle();
			}

			foreach (GameplayModifierInfo mod in spec.Def.Modifiers)
			{
				if (!mod.Attribute.IsValid())
				{
					Debug.LogWarning($"{spec.Def} has a null modifier attribute.");
					return new ActiveGameplayEffectHandle();
				}
			}

			bool treatAsInfiniteDuration = spec.Def.DurationPolicy == GameplayEffectDurationType.Infinite;

			ActiveGameplayEffectHandle myHandle = new(GameplayEffectConstants.IndexNone);
			bool foundExistingStackableGE = false;

			ActiveGameplayEffect appliedEffect = null;
			GameplayEffectSpec ourCopySpec = null;
			GameplayEffectSpec stackSpec = null;
			{
				if (spec.Def.DurationPolicy != GameplayEffectDurationType.Instant || treatAsInfiniteDuration)
				{
					appliedEffect = ActiveGameplayEffects.ApplyGameplayEffectSpec(spec, ref foundExistingStackableGE);
					if (appliedEffect == null)
					{
						return new ActiveGameplayEffectHandle();
					}

					myHandle = appliedEffect.Handle;
					ourCopySpec = appliedEffect.Spec;

					Debug.Log($"Applied {ourCopySpec.Def.name}");
					foreach (GameplayModifierInfo modifier in spec.Def.Modifiers)
					{
						float magnitude = 0;
						modifier.ModifierMagnitude.AttemptCalculateMagnitude(spec, ref magnitude);
						Debug.Log($"{modifier.Attribute.AttributeName}: {modifier.ModifierOp} {magnitude}");
					}
				}

				if (ourCopySpec == null)
				{
					stackSpec = spec;
					ourCopySpec = stackSpec;

					AbilitySystemGlobals.Instance.GlobalPreGameplayEffectSpecApply(ourCopySpec, this);
					ourCopySpec.CaptureAttributeDataFromTarget(this);
				}

				if (treatAsInfiniteDuration)
				{
					ourCopySpec.SetDuration(GameplayEffectConstants.InfiniteDuration, true);
				}
			}

			AbilitySystemGlobals.Instance.SetCurrentAppliedGE(ourCopySpec);

			if (treatAsInfiniteDuration)
			{

			}
			else if (spec.Def.DurationPolicy == GameplayEffectDurationType.Instant)
			{
				ExecuteGameplayEffect(ourCopySpec);
			}

			spec.Def.OnApplied(ActiveGameplayEffects, ourCopySpec);

			AbilitySystemComponent instigator = spec.EffectContext.InstigatorAbilitySystemComponent;

			OnGameplayEffectAppliedToSelf(instigator, ourCopySpec, myHandle);

			if (instigator)
			{
				instigator.OnGameplayEffectAppliedToTarget(this, ourCopySpec, myHandle);
			}

			return myHandle;
		}

		public void GetAllActiveGameplayEffectSpecs(List<GameplayEffectSpec> outSpecCopies)
		{
			ActiveGameplayEffects.GetAllActiveGameplayEffectSpecs(outSpecCopies);
		}

		public List<ActiveGameplayEffectHandle> GetActiveEffects(in GameplayEffectQuery query)
		{
			return ActiveGameplayEffects.GetActiveEffects(query);
		}

		public List<ActiveGameplayEffectHandle> GetActiveEffectsWithAllTags(GameplayTagContainer tags)
		{
			return GetActiveEffects(GameplayEffectQuery.MakeQuery_MatchAllEffectTags(tags));
		}

		public void NotifyTagMap_StackCountChange(in GameplayTagContainer container)
		{
			foreach (GameplayTag tag in container)
			{
				GameplayTagCountContainer.Notify_StackCountChange(tag);
			}
		}

		public ActiveGameplayEffectHandle ApplyGameplayEffectToTarget(GameplayEffect gameplayEffect, AbilitySystemComponent target, float level = GameplayEffectConstants.InvalidLevel, GameplayEffectContextHandle context = null)
		{
			Debug.Assert(gameplayEffect);
			if (context is not { IsValid: true })
			{
				context = MakeEffectContext();
			}

			GameplayEffectSpec spec = new(gameplayEffect, context, level);
			return ApplyGameplayEffectSpecToTarget(spec, target);
		}

		public ActiveGameplayEffectHandle ApplyGameplayEffectSpecToTarget(in GameplayEffectSpec spec, AbilitySystemComponent target)
		{
			ActiveGameplayEffectHandle returnHandle = new();

			if (target != null)
			{
				returnHandle = target.ApplyGameplayEffectSpecToSelf(spec);
			}

			return returnHandle;
		}

		[Obsolete("Use SetActiveGameplayEffectInhibit with a MoveTemp(ActiveGEHandle) so it's clear the Handle is no longer valid. Check (then use) the returned FActiveGameplayEffectHandle to continue your operation.")]
		public virtual void InhibitActiveGameplayEffect(ActiveGameplayEffectHandle activeGEHandle, bool inhibit)
		{
			ActiveGameplayEffectHandle continuationHandle = SetActiveGameplayEffectInhibit(activeGEHandle, inhibit);
			Debug.Assert(continuationHandle.IsValid(), $"InhibitActiveGameplayEffect invalidated the incoming ActiveGEHandle. Update your code to SetActiveGameplayEffectInhibit so it's clear the incoming handle can be invalidated.");
		}

		public virtual ActiveGameplayEffectHandle SetActiveGameplayEffectInhibit(ActiveGameplayEffectHandle activeGEHandle, bool inhibit)
		{
			ActiveGameplayEffect activeGE = ActiveGameplayEffects.GetActiveGameplayEffect(activeGEHandle);
			if (activeGE is null)
			{
				Debug.LogError($"InhibitActiveGameplayEffect received bad Active GameplayEffect Handle: {activeGEHandle}");
				return new ActiveGameplayEffectHandle();
			}

			if (activeGE.IsInhibited != inhibit)
			{
				activeGE.IsInhibited = inhibit;

				if (inhibit)
				{
					ActiveGameplayEffects.RemoveActiveGameplayEffectGrantedTagsAndModifiers(activeGE);
				}
				else
				{
					ActiveGameplayEffects.AddActiveGameplayEffectGrantedTagsAndModifiers(activeGE);
				}

				if (!activeGE.IsPendingRemove)
				{
					activeGE.EventSet.OnInhibitionChanged?.Invoke(activeGEHandle, activeGE.IsInhibited);
				}

				if (activeGE.IsPendingRemove)
				{
					return new ActiveGameplayEffectHandle();
				}
			}

			return activeGEHandle;
		}

		public bool RemoveActiveGameplayEffect(ActiveGameplayEffectHandle handle, int stacksToRemove = -1)
		{
			return ActiveGameplayEffects.RemoveActiveGameplayEffect(handle, stacksToRemove);
		}

		public void UpdateActiveGameplayEffectSetByCallerMagnitude(ActiveGameplayEffectHandle activeHandle, GameplayTag setByCallerTag, float magnitude)
		{
			ActiveGameplayEffects.UpdateActiveGameplayEffectSetByCallerMagnitude(activeHandle, setByCallerTag, magnitude);
		}

		public void UpdateActiveGameplayEffectSetByCallerMagnitudes(ActiveGameplayEffectHandle activeHandle, in Dictionary<GameplayTag, float> newSetByCallerValues)
		{
			ActiveGameplayEffects.UpdateActiveGameplayEffectSetByCallerMagnitudes(activeHandle, newSetByCallerValues);
		}

		public ActiveGameplayEffect GetActiveGameplayEffect(in ActiveGameplayEffectHandle handle)
		{
			return ActiveGameplayEffects.GetActiveGameplayEffect(handle);
		}

		public ActiveGameplayEffectEvents GetActiveEffectEventSet(in ActiveGameplayEffectHandle handle)
		{
			ActiveGameplayEffect activeEffect = ActiveGameplayEffects.GetActiveGameplayEffect(handle);
			return activeEffect?.EventSet;
		}

		public OnGivenActiveGameplayEffectRemoved OnAnyGameplayEffectRemovedDelegate()
		{
			return ActiveGameplayEffects.OnActiveGameplayEffectRemovedDelegate;
		}

		public OnActiveGameplayEffectRemoved_Info OnGameplayEffectRemoved_InfoDelegate(ActiveGameplayEffectHandle handle)
		{
			ActiveGameplayEffect activeEffect = ActiveGameplayEffects.GetActiveGameplayEffect(handle);
			return activeEffect?.EventSet.OnEffectRemoved;
		}

		public OnActiveGameplayEffectStackChange OnGameplayEffectStackChangeDelegate(ActiveGameplayEffectHandle handle)
		{
			ActiveGameplayEffect activeEffect = ActiveGameplayEffects.GetActiveGameplayEffect(handle);
			return activeEffect?.EventSet.OnStackChanged;
		}

		public OnActiveGameplayEffectTimeChange OnGameplayEffectTimeChangeDelegate(ActiveGameplayEffectHandle handle)
		{
			ActiveGameplayEffect activeEffect = ActiveGameplayEffects.GetActiveGameplayEffect(handle);
			return activeEffect?.EventSet.OnTimeChanged;
		}

		public OnActiveGameplayEffectInhibitionChanged OnGameplayEffectInhibitionChangedDelegate(ActiveGameplayEffectHandle handle)
		{
			ActiveGameplayEffect activeEffect = ActiveGameplayEffects.GetActiveGameplayEffect(handle);
			return activeEffect?.EventSet.OnInhibitionChanged;
		}

		public void BlockAbilitiesWithTags(GameplayTagContainer tags)
		{
			BlockedAbilityTags.UpdateTagCount(tags, 1);
		}

		public void UnblockAbilitiesWithTags(GameplayTagContainer tags)
		{
			BlockedAbilityTags.UpdateTagCount(tags, -1);
		}

		public void ExecutePeriodicEffect(ActiveGameplayEffectHandle handle)
		{
			ActiveGameplayEffects.ExecutePeriodicGameplayEffect(handle);
		}

		public void ExecuteGameplayEffect(GameplayEffectSpec spec)
		{
			Debug.Assert(spec.Duration == GameplayEffectConstants.InstantApplication || spec.Period == GameplayEffectConstants.NoPeriod);

			Debug.Log($"Executed {spec.Def.name}");
			foreach (GameplayModifierInfo modifier in spec.Def.Modifiers)
			{
				float magnitude = 0;
				modifier.ModifierMagnitude.AttemptCalculateMagnitude(spec, ref magnitude);
				Debug.Log($"{modifier.Attribute.AttributeName}: {modifier.ModifierOp} {magnitude}");
			}

			ActiveGameplayEffects.ExecuteActiveEffectsFrom(spec);
		}

		public void CheckDurationExpired(ActiveGameplayEffectHandle handle)
		{
			ActiveGameplayEffects.CheckDuration(handle);
		}

		public GameplayEffect GetGameplayEffectDefForHandle(ActiveGameplayEffectHandle handle)
		{
			ActiveGameplayEffect activeGE = ActiveGameplayEffects.GetActiveGameplayEffect(handle);
			if (activeGE is not null)
			{
				return activeGE.Spec.Def;
			}

			return null;
		}

		public void SetActiveGameplayEffectLevel(ActiveGameplayEffectHandle handle, int newLevel)
		{
			ActiveGameplayEffects.SetActiveGameplayEffectLevel(handle, newLevel);
		}

		public void SetActiveGameplayEffectLevelUsingQuery(GameplayEffectQuery query, int newLevel)
		{
			List<ActiveGameplayEffectHandle> activeGameplayEffectHandles = ActiveGameplayEffects.GetActiveEffects(query);
			foreach (ActiveGameplayEffectHandle activeHandle in activeGameplayEffectHandles)
			{
				SetActiveGameplayEffectLevel(activeHandle, newLevel);
			}
		}

		#region GameplayTags 

		public bool HasMatchingGameplayTag(GameplayTag tagToCheck)
		{
			return GameplayTagCountContainer.HasMatchingGameplayTag(tagToCheck);
		}

		public bool HasAllMatchingGameplayTags(in GameplayTagContainer tagContainer)
		{
			return GameplayTagCountContainer.HasAllMatchingGameplayTags(tagContainer);
		}

		public bool HasAnyMatchingGameplayTags(in GameplayTagContainer tagContainer)
		{
			return GameplayTagCountContainer.HasAnyMatchingGameplayTags(tagContainer);
		}

		public void UpdateTagMap(in GameplayTagContainer container, int countDelta)
		{
			if (!container.IsEmpty())
			{
				UpdateTagMap_Internal(container, countDelta);
			}
		}

		public GameplayTagContainer GetBlockedAbilityTags()
		{
			return BlockedAbilityTags.ExplicitGameplayTags;
		}

		protected void UpdateTagMap_Internal(in GameplayTagContainer container, in int countDelta)
		{
			if (countDelta > 0)
			{
				foreach (GameplayTag tag in container)
				{
					if (GameplayTagCountContainer.UpdateTagCount(tag, countDelta))
					{
						OnTagUpdated(tag, true);
					}
				}
			}
			else if (countDelta < 0)
			{
				List<GameplayTag> removedTags = new();
				removedTags.Reserve(container.Count);
				List<DeferredTagChangeDelegate> deferredTagChangeDelegates = new();

				foreach (GameplayTag tag in container)
				{
					if (GameplayTagCountContainer.UpdateTagCount_DeferredParentRemoval(tag, countDelta, deferredTagChangeDelegates))
					{
						removedTags.Add(tag);
					}
				}

				if (removedTags.Count > 0)
				{
					GameplayTagCountContainer.FillParentTags();
				}

				foreach (DeferredTagChangeDelegate @delegate in deferredTagChangeDelegates)
				{
					@delegate.Invoke();
				}

				foreach (GameplayTag tag in removedTags)
				{
					OnTagUpdated(tag, false);
				}
			}
		}

		public virtual void OnTagUpdated(in GameplayTag tag, bool tagExists)
		{

		}

		public void AddLooseGameplayTags(GameplayTagContainer gameplayTags, int count = 1)
		{
			UpdateTagMap(gameplayTags, count);
		}

		public void RemoveLooseGameplayTags(GameplayTagContainer gameplayTags, int count = -1)
		{
			UpdateTagMap(gameplayTags, count);
		}


		public OnGameplayEffectTagCountChanged RegisterGameplayTagEvent(GameplayTag tag, GameplayTagEventType eventType = GameplayTagEventType.NewOrRemoved)
		{
			return GameplayTagCountContainer.RegisterGameplayTagEvent(tag, eventType);
		}

		public bool UnregisterGameplayTagEvent(UnityAction<GameplayTag, int> @delegate, GameplayTag tag, GameplayTagEventType eventType = GameplayTagEventType.NewOrRemoved)
		{
			GameplayTagCountContainer.RegisterGameplayTagEvent(tag, eventType).RemoveListener(@delegate);
			return true;
		}

		public void GetOwnedGameplayTags(GameplayTagContainer tagContainer)
		{
			tagContainer.Reset();
			tagContainer.AppendTags(GetOwnedGameplayTags());
		}

		public GameplayTagContainer GetOwnedGameplayTags()
		{
			return GameplayTagCountContainer.ExplicitGameplayTags;
		}

		public int GetTagCount(GameplayTag tagToCheck)
		{
			return GameplayTagCountContainer.GetTagCount(tagToCheck);
		}

		#endregion

		public void CaptureAttributeForGameplayEffect(GameplayEffectAttributeCaptureSpec captureSpec)
		{
			GameplayAttribute attributeToCapture = captureSpec.BackingDefinition.AttributeToCapture;
			if (attributeToCapture.IsValid() && GetAttributeSubobject(attributeToCapture.GetAttributeSetClass()) != null)
			{
				ActiveGameplayEffects.CaptureAttributeForGameplayEffect(captureSpec);
			}
		}

		public GameplayEffectSpecHandle MakeOutgoingSpec(GameplayEffect gameplayEffect, float level, GameplayEffectContextHandle context)
		{
			if (!context.IsValid)
			{
				context = MakeEffectContext();
			}

			if (gameplayEffect)
			{
				GameplayEffectSpec newSpec = new(gameplayEffect, context, level);
				return new GameplayEffectSpecHandle(newSpec);
			}

			return new GameplayEffectSpecHandle(null);
		}

		public bool CanApplyAttributeModifiers(GameplayEffect gameplayEffect, float level, GameplayEffectContextHandle effectContext)
		{
			return ActiveGameplayEffects.CanApplyAttributeModifiers(gameplayEffect, level, effectContext);
		}

		public void OnMagnitudeDependencyChange(ActiveGameplayEffectHandle handle, Aggregator changed_aggregator)
		{
			ActiveGameplayEffects.OnMagnitudeDependencyChange(handle, changed_aggregator);
		}

		public void OnAttributeAggregatorDirty(Aggregator aggregator, GameplayAttribute attribute, bool fromRecursiveCall = false)
		{
			ActiveGameplayEffects.OnAttributeAggregatorDirty(aggregator, attribute, fromRecursiveCall);
		}

		public virtual void OnGameplayEffectDurationChange(ActiveGameplayEffect activeEffect)
		{

		}

		public void OnGameplayEffectAppliedToSelf(AbilitySystemComponent source, in GameplayEffectSpec specApplied, ActiveGameplayEffectHandle activeHandle)
		{
			OnGameplayEffectAppliedToSelfDelegate?.Invoke(source, specApplied, activeHandle);
		}

		public void OnGameplayEffectAppliedToTarget(AbilitySystemComponent target, in GameplayEffectSpec specApplied, ActiveGameplayEffectHandle activeHandle)
		{
			OnGameplayEffectAppliedToTargetDelegate?.Invoke(target, specApplied, activeHandle);
		}

		public void OnActiveGameplayEffectAddedToSelf(AbilitySystemComponent self, in GameplayEffectSpec specExecuted, ActiveGameplayEffectHandle activeHandle)
		{
			OnActiveGameplayEffectAddedDelegateToSelf?.Invoke(self, specExecuted, activeHandle);
		}

		public void OnPeriodicGameplayEffectExecutedOnTarget(AbilitySystemComponent target, in GameplayEffectSpec specExecuted, ActiveGameplayEffectHandle activeHandle)
		{
			OnPeriodicGameplayEffectExecutedDelegateOnTarget?.Invoke(target, specExecuted, activeHandle);
		}

		public void OnPeriodicGameplayEffectExecutedOnSelf(AbilitySystemComponent target, in GameplayEffectSpec specExecuted, ActiveGameplayEffectHandle activeHandle)
		{
			OnPeriodicGameplayEffectExecutedDelegateOnSelf?.Invoke(target, specExecuted, activeHandle);
		}

		public AttributeSet GetAttributeSubobject(Type attributeClass)
		{
			foreach (AttributeSet set in SpawnedAttributes)
			{
				if (set != null && set.GetType() == attributeClass)
				{
					return set;
				}
			}
			return null;
		}

		public AttributeSet GetAttributeSubobjectChecked(Type attributeClass)
		{
			AttributeSet set = GetAttributeSubobject(attributeClass);
			Debug.Assert(set != null);
			return set;
		}

		public AttributeSet GetOrCreateAttributeSubobject(Type attributeClass)
		{
			AttributeSet myAttributes = null;
			if (attributeClass != null)
			{
				myAttributes = GetAttributeSubobject(attributeClass);
				if (myAttributes == null)
				{
					AttributeSet attributes = ScriptableObject.CreateInstance(attributeClass) as AttributeSet;
					AddSpawnedAttribute(attributes);
					myAttributes = attributes;
				}
			}

			return myAttributes;
		}

		public T AddAttributeSetSubobject<T>(T attributeSet) where T : AttributeSet
		{
			AddSpawnedAttribute(attributeSet);
			return attributeSet;
		}

		public void AddSpawnedAttribute(AttributeSet attribute)
		{
			if (attribute == null)
			{
				return;
			}

			if (!SpawnedAttributes.Contains(attribute))
			{
				attribute.OwningActor = gameObject;
				SpawnedAttributes.Add(attribute);
			}
		}

		public void RemoveSpawnedAttribute(AttributeSet attribute)
		{
			if (SpawnedAttributes.Remove(attribute))
			{
				attribute.OwningActor = null;
			}
		}

		public bool HasAttributeSetForAttribute(GameplayAttribute attribute)
		{
			return attribute.IsValid() && GetAttributeSubobject(attribute.GetAttributeSetClass()) != null;
		}

		public virtual void HandleChangeAbilityCanBeCanceled(in GameplayTagContainer abilityTags, GameplayAbility requestingAbility, bool canBeCancelled)
		{

		}

		public virtual void NotifyAbilityCommit(GameplayAbility ability)
		{
			AbilityCommittedCallbacks?.Invoke(ability);

		}

		public virtual void NotifyAbilityActivated(GameplayAbilitySpecHandle handle, GameplayAbility ability)
		{
			AbilityActivatedCallbacks?.Invoke(ability);
		}

		public virtual void NotifyAbilityFailed(GameplayAbilitySpecHandle handle, GameplayAbility ability, in GameplayTagContainer failureReason)
		{
			AbilityFailedCallbacks?.Invoke(ability, failureReason);
		}

		public virtual void NotifyAbilityEnded(GameplayAbilitySpecHandle handle, GameplayAbility ability, bool wasCancelled)
		{
			Debug.Assert(ability != null);
			GameplayAbilitySpec spec = FindAbilitySpecFromHandle(handle);
			if (spec == null)
			{
				return;
			}

			Debug.Log($"{name}: Ended [{handle}] {(spec.GetPrimaryInstance() ? spec.GetPrimaryInstance().name : ability.name)}. Level: {spec.Level}. WasCancelled: {wasCancelled}");

			Debug.Assert(spec.ActiveCount > 0, $"NotifyAbilityEnded called when the Spec->ActiveCount <= 0 for ability {ability.name}");
			if (spec.ActiveCount > 0)
			{
				spec.ActiveCount--;
			}
			else
			{
				Debug.LogWarning($"NotifyAbilityEnded called when the Spec->ActiveCount <= 0 for ability {ability.name}");
			}

			AbilityEndedCallbacks?.Invoke(ability);
			OnAbilityEnded?.Invoke(new AbilityEndedData(ability, handle, wasCancelled));

			spec = FindAbilitySpecFromHandle(handle);
			if (spec == null)
			{
				Debug.LogError($"NotifyAbilityEnded({ability}): {handle} lost its active handle halfway through the function.");
				return;
			}

			if (ability.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerExecution)
			{
				if (ability.ReplicationPolicy != GameplayAbilityReplicationPolicy.ReplicateNo)
				{
					spec.ReplicatedInstances.Remove(ability);
				}
				else
				{
					spec.NonReplicatedInstances.Remove(ability);
				}
			}

			if (spec.RemoveAfterActivation && !spec.IsActive)
			{
				ClearAbility(handle);
			}
		}

		public GameplayAbility CreateNewInstanceOfAbility(GameplayAbilitySpec spec, in GameplayAbility ability)
		{
			Debug.Assert(ability != null);

			GameplayAbility abilityInstance = Instantiate(ability);
			Debug.Assert(abilityInstance != null);

			spec.NonReplicatedInstances.Add(abilityInstance);

			return abilityInstance;
		}

		public GameplayAbilitySpec FindAbilitySpecFromHandle(GameplayAbilitySpecHandle handle)
		{
			foreach (var spec in ActivatableAbilities.Items)
			{
				if (spec.Handle == handle)
				{
					return spec;
				}
			}
			return null;
		}

		public List<GameplayAbilitySpec> FindAbilitySpecsFromGEHandle(ActiveGameplayEffectHandle activeGEHandle)
		{
			List<GameplayAbilitySpec> foundSpecs = new();

			void GatherGAsByGEHandle(List<GameplayAbilitySpec> abilitiesToConsider)
			{
				foreach (var GASpec in abilitiesToConsider)
				{
					if (GASpec.GameplayEffectHandle == activeGEHandle)
					{
						if (!GASpec.PendingRemove)
						{
							foundSpecs.Add(GASpec);
						}
					}
				}
			}

			GatherGAsByGEHandle(GetActivatableAbilities());


			return foundSpecs;
		}

		public GameplayAbilitySpec FindAbilitySpecFromClass(GameplayAbility abilityClass)
		{
			foreach (var spec in ActivatableAbilities.Items)
			{
				if (spec.Ability == null)
				{
					continue;
				}

				if (spec.Ability == abilityClass)
				{
					return spec;
				}
			}

			return null;
		}

		public void ApplyAbilityBlockAndCancelTags(GameplayTagContainer abilityTags, GameplayAbility requestingAbility, bool enableBlockTags, in GameplayTagContainer blockTags, bool executeCancelTags, in GameplayTagContainer cancelTags)
		{
			if (enableBlockTags)
			{
				BlockAbilitiesWithTags(blockTags);
			}
			else
			{
				UnblockAbilitiesWithTags(blockTags);
			}

			if (executeCancelTags)
			{
				CancelAbilities(cancelTags, null, requestingAbility);
			}
		}

		public void CancelAbility(GameplayAbility ability)
		{
			// lock (abilityLock)
			// {

			// }
			for (int i = 0; i < ActivatableAbilities.Items.Count; i++)
			{
				GameplayAbilitySpec spec = ActivatableAbilities.Items[i];
				if (spec.Ability == ability)
				{
					CancelAbilitySpec(spec, null);
				}
			}
		}

		public void CancelAbilities(in GameplayTagContainer withTags, in GameplayTagContainer withoutTags, GameplayAbility ignore)
		{
			for (int i = 0; i < ActivatableAbilities.Items.Count; i++)
			{
				GameplayAbilitySpec spec = ActivatableAbilities.Items[i];
				if (!spec.IsActive || spec.Ability == null)
				{
					continue;
				}

				GameplayTagContainer abilityTags = spec.Ability.AssetTags;
				bool withTagPass = withTags is null || abilityTags.HasAny(withTags);
				bool withoutTagPass = withoutTags is null || !abilityTags.HasAny(withoutTags);

				if (withTagPass && withoutTagPass)
				{
					CancelAbilitySpec(spec, ignore);
				}
			}
		}

		public virtual GameplayEffectContextHandle MakeEffectContext()
		{
			GameplayEffectContextHandle context = new(AbilitySystemGlobals.Instance.AllocGameplayEffectContext());

			if (AbilityActorInfo != null)
			{
				AbilityActorInfo.OwnerActor.TryGetTarget(out GameObject ownerActor);
				AbilityActorInfo.AvatarActor.TryGetTarget(out GameObject avatarActor);
				context.AddInstigator(ownerActor, avatarActor);
			}
			else
			{
				Debug.LogWarning("Unable to make effect context because AbilityActorInfo is not valid.");
			}

			return context;
		}

		public float GetNumericAttributeBase(in GameplayAttribute attribute)
		{
			if (attribute.IsSystemAttribute())
			{
				return 0;
			}

			return ActiveGameplayEffects.GetAttributeBaseValue(attribute);
		}

		public float GetNumericAttribute(in GameplayAttribute attribute)
		{
			if (attribute.IsSystemAttribute())
			{
				return 0;
			}

			AttributeSet attributeSetOrNull = GetAttributeSubobject(attribute.GetAttributeSetClass());
			if (attributeSetOrNull == null)
			{
				return 0;
			}

			return attribute.GetNumericValue(attributeSetOrNull);
		}

		public float GetNumericAttributeChecked(in GameplayAttribute attribute)
		{
			if (attribute.IsSystemAttribute())
			{
				return 0;
			}

			AttributeSet attributeSetOrNull = GetAttributeSubobject(attribute.GetAttributeSetClass());
			return attribute.GetNumericValueChecked(attributeSetOrNull);
		}

		public void SetNumericAttributeBase(in GameplayAttribute attribute, float newValue)
		{
			ActiveGameplayEffects.SetAttributeBaseValue(attribute, newValue);
		}

		public void SetNumericAttribute_Internal(in GameplayAttribute attribute, float newValue)
		{
			AttributeSet attributeSet = GetAttributeSubobjectChecked(attribute.GetAttributeSetClass());
			attribute.SetNumericValueChecked(newValue, attributeSet);
		}

		public GameplayTagContainer GetGameplayEffectSourceTagsFromHandle(ActiveGameplayEffectHandle handle)
		{
			return ActiveGameplayEffects.GetGameplayEffectSourceTagsFromHandle(handle);
		}

		public GameplayTagContainer GetGameplayEffectTargetTagsFromHandle(ActiveGameplayEffectHandle handle)
		{
			return ActiveGameplayEffects.GetGameplayEffectTargetTagsFromHandle(handle);
		}

		public GameplayAbilitySpecHandle GiveAbility(in GameplayAbilitySpec spec)
		{
			if (spec.Ability == null)
			{
				Debug.LogError("GiveAbility called with an invalid Ability Class.");
				return new GameplayAbilitySpecHandle();
			}

			ActivatableAbilities.Items.Add(spec);
			GameplayAbilitySpec ownedSpec = ActivatableAbilities.Items.Last();

			if (ownedSpec.Ability.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerActor)
			{
				CreateNewInstanceOfAbility(ownedSpec, ownedSpec.Ability);
			}

			OnGiveAbility(ownedSpec);

			Debug.Log($"{this.name}: GiveAbility {ownedSpec.Ability} [{ownedSpec.Handle}] Level: {ownedSpec.Level} Source: {ownedSpec.SourceObject}");
			return ownedSpec.Handle;
		}

		public GameplayAbilitySpecHandle GiveAbility(GameplayAbility abilityClass, int level = 0)
		{
			GameplayAbilitySpec abilitySpec = BuildAbilitySpecFromClass(abilityClass, level);

			if (abilitySpec.Ability == null)
			{
				Debug.LogError("GiveAbility called with an invalid Ability Class.");
				return new GameplayAbilitySpecHandle();
			}

			return GiveAbility(abilitySpec);
		}

		public GameplayAbilitySpecHandle GiveAbilityAndActivateOnce(GameplayAbilitySpec spec, in GameplayEventData gameplayEventData = null)
		{
			if (spec.Ability == null)
			{
				Debug.LogError("GiveAbilityAndActivateOnce called with an invalid Ability Class.");
				return new GameplayAbilitySpecHandle();
			}

			if (spec.Ability.InstancingPolicy == GameplayAbilityInstancingPolicy.NonInstanced)
			{
				Debug.LogError("GiveAbilityAndActivateOnce called with a non-instanced ability. This ability will not be activated.");
				return new GameplayAbilitySpecHandle();
			}

			spec.ActivateOnce = true;

			GameplayAbilitySpecHandle addedAbilityHandle = GiveAbility(spec);

			GameplayAbilitySpec foundSpec = FindAbilitySpecFromHandle(addedAbilityHandle);

			if (foundSpec != null)
			{
				foundSpec.RemoveAfterActivation = true;

				if (!InternalTryActivateAbility(addedAbilityHandle, null, null, gameplayEventData))
				{
					ClearAbility(addedAbilityHandle);

					return new GameplayAbilitySpecHandle();
				}
			}
			else if (gameplayEventData != null)
			{

			}

			return addedAbilityHandle;
		}

		public GameplayAbilitySpecHandle GiveAbilityAndActivateOnce(GameplayAbility abilityClass, int level = 0)
		{
			GameplayAbilitySpec abilitySpec = BuildAbilitySpecFromClass(abilityClass, level);

			if (abilitySpec.Ability == null)
			{
				Debug.LogError("GiveAbilityAndActivateOnce called with an invalid Ability Class.");
				return new GameplayAbilitySpecHandle();
			}

			return GiveAbilityAndActivateOnce(abilitySpec);
		}

		public void SetRemoveAbilityOnEnd(GameplayAbilitySpecHandle abilitySpecHandle)
		{
			GameplayAbilitySpec foundSpec = FindAbilitySpecFromHandle(abilitySpecHandle);
			if (foundSpec != null)
			{
				if (foundSpec.IsActive)
				{
					foundSpec.RemoveAfterActivation = true;
				}
				else
				{
					ClearAbility(abilitySpecHandle);
				}
			}
		}

		public void ClearAbility(in GameplayAbilitySpecHandle handle)
		{
			for (int i = 0; i < ActivatableAbilities.Items.Count; i++)
			{
				Debug.Assert(ActivatableAbilities.Items[i].Handle.IsValid());
				if (ActivatableAbilities.Items[i].Handle == handle)
				{
					OnRemoveAbility(ActivatableAbilities.Items[i]);
					ActivatableAbilities.Items.RemoveAt(i);
					CheckForClearedAbilities();
					return;
				}
			}
		}

		protected virtual void OnRemoveAbility(GameplayAbilitySpec spec)
		{
			if (spec.Ability == null)
			{
				return;
			}

			Debug.Log($"{this.name}: Removing Ability [{spec.Handle}] {spec.Ability} Level: {spec.Level}");

			List<GameplayAbility> instances = spec.AbilityInstances;
			foreach (GameplayAbility instance in instances)
			{
				if (instance != null)
				{
					if (instance.IsActive())
					{
						bool wasCancelled = false;
						instance.EndAbility(instance.CurrentSpecHandle, instance.CurrentActorInfo, wasCancelled);
					}
					else
					{
						if (instance.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerExecution)
						{

						}
					}
				}
			}

			GameplayAbility primaryInstance = spec.GetPrimaryInstance();
			if (primaryInstance != null)
			{
				primaryInstance.OnRemoveAbility(AbilityActorInfo, spec);
			}
			else
			{
				if (spec.IsActive)
				{
					if (spec.Ability.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerExecution)
					{
						instances = spec.AbilityInstances;
						foreach (GameplayAbility instance in instances)
						{
							Debug.Assert(instance.IsAbilityEnding, $"All instances of {spec.Ability} on {this.name} should have been ended by now. Maybe it was retriggered from OnEndAbility (bad)?");
						}

						spec.RemoveAfterActivation = true;
						return;
					}
				}
				else if (spec.Ability.InstancingPolicy == GameplayAbilityInstancingPolicy.NonInstanced)
				{
					const bool wasCancelled = false;
					spec.Ability.EndAbility(spec.Handle, AbilityActorInfo, wasCancelled);
				}
				else
				{
					Debug.LogWarning($"We should never have an instanced Gameplay Ability that is still active by this point. All instances should have EndAbility called just before here.");
				}

				spec.Ability.OnRemoveAbility(AbilityActorInfo, spec);
			}

			if (spec.GameplayEffectHandle.IsValid())
			{
				AbilitySystemComponent sourceASC = spec.GameplayEffectHandle.OwningAbilitySystemComponent;
				if (sourceASC != null)
				{
					ActiveGameplayEffect sourceActiveGE = sourceASC.ActiveGameplayEffects.GetActiveGameplayEffect(spec.GameplayEffectHandle);
					if (sourceActiveGE != null)
					{
						sourceActiveGE.GrantedAbilityHandles.Remove(spec.Handle);
					}
				}
			}

			spec.ReplicatedInstances.Clear();
			spec.NonReplicatedInstances.Clear();
		}

		protected virtual void CheckForClearedAbilities()
		{
			foreach (var triggered in GameplayEventTriggerdAbilities)
			{
				for (int i = 0; i < triggered.Value.Count; i++)
				{
					GameplayAbilitySpec spec = FindAbilitySpecFromHandle(triggered.Value[i]);

					if (spec == null)
					{
						triggered.Value.RemoveAt(i);
						i--;
					}
				}
			}

			foreach (var triggered in OwnedTagTriggeredAbilities)
			{
				bool removedTrigger = false;
				for (int i = 0; i < triggered.Value.Count; i++)
				{
					GameplayAbilitySpec spec = FindAbilitySpecFromHandle(triggered.Value[i]);

					if (spec == null)
					{
						triggered.Value.RemoveAt(i);
						i--;
						removedTrigger = true;
					}
				}

				if (removedTrigger && triggered.Value.Count == 0)
				{
					OnGameplayEffectTagCountChanged countChangedEvent = RegisterGameplayTagEvent(triggered.Key);
					if (countChangedEvent != null)
					{
						// countChangedEvent.Remove(MonitoredTagChangedDelegateHandle);
					}
				}
			}

			foreach (var activeGE in ActiveGameplayEffects)
			{
				foreach (GameplayAbilitySpecDef abilitySpec in activeGE.Spec.GrantedAbilitySpecs)
				{
					if (abilitySpec.AssignedHandle.IsValid() && FindAbilitySpecFromHandle(abilitySpec.AssignedHandle) == null)
					{
						Debug.Log($"::CheckForClearedAbilities is clearing AssignedHandle {abilitySpec.AssignedHandle} from GE {activeGE} / {activeGE.Handle}");
						abilitySpec.AssignedHandle = new GameplayAbilitySpecHandle();
					}
				}
			}
		}

		public virtual GameplayAbilitySpec BuildAbilitySpecFromClass(GameplayAbility abilityClass, int level = 0)
		{
			if (abilityClass == null)
			{
				Debug.LogError("BuildAbilitySpecFromClass called with an invalid Ability Class.");
				return new GameplayAbilitySpec();
			}

			GameplayAbility abilityCDO = abilityClass;

			return new GameplayAbilitySpec(abilityClass, level);
		}

		protected virtual void OnGiveAbility(GameplayAbilitySpec spec)
		{
			if (spec.Ability == null)
			{
				return;
			}

			GameplayAbility specAbility = spec.Ability;
			bool instancedPerActor = specAbility.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerActor;
			if (instancedPerActor && specAbility.ReplicationPolicy == GameplayAbilityReplicationPolicy.ReplicateNo)
			{
				if (spec.NonReplicatedInstances.Count == 0)
				{
					CreateNewInstanceOfAbility(spec, specAbility);
				}
			}

			if (spec.GameplayEffectHandle.IsValid())
			{
				AbilitySystemComponent sourceASC = spec.GameplayEffectHandle.OwningAbilitySystemComponent;
				Debug.Assert(sourceASC != null, $"OnGiveAbility Spec '{spec}' GameplayEffectHandle had invalid Owning Ability System Component");
				if (sourceASC != null)
				{
					ActiveGameplayEffect sourceActiveGE = sourceASC.ActiveGameplayEffects.GetActiveGameplayEffect(spec.GameplayEffectHandle);
					Debug.Assert(sourceActiveGE != null, $"OnGiveAbility Spec '{spec}' GameplayEffectHandle was not active on Owning Ability System Component '{sourceASC.name}'");
					if (sourceActiveGE != null)
					{
						sourceActiveGE.GrantedAbilityHandles.AddUnique(spec.Handle);
					}
				}
			}

			foreach (AbilityTriggerData triggerData in specAbility.AbilityTriggers)
			{
				GameplayTag eventTag = triggerData.TriggerTag;

				var triggeredAbilityMap = triggerData.TriggerSource == GameplayAbilityTriggerSource.GameplayEvent ? GameplayEventTriggerdAbilities : OwnedTagTriggeredAbilities;

				if (triggeredAbilityMap.ContainsKey(eventTag))
				{
					triggeredAbilityMap[eventTag].Add(spec.Handle);
				}
				else
				{
					List<GameplayAbilitySpecHandle> triggers = new()
					{
						spec.Handle
					};
					triggeredAbilityMap.Add(eventTag, triggers);
				}

				if (triggerData.TriggerSource != GameplayAbilityTriggerSource.GameplayEvent)
				{
					OnGameplayEffectTagCountChanged countChangedEvent = RegisterGameplayTagEvent(eventTag);

					if (countChangedEvent != null)
					{
						// countChangedEvent.Add(OnGameplayEffectTagCountChanged);
					}
				}
			}

			GameplayAbility primaryInstance = instancedPerActor ? spec.GetPrimaryInstance() : null;

			if (primaryInstance != null)
			{
				primaryInstance.OnGiveAbility(AbilityActorInfo, spec);
			}
			else
			{
				spec.Ability.OnGiveAbility(AbilityActorInfo, spec);
			}
		}

		protected virtual void MonitoredTagChanged(GameplayTag tag, int newCount)
		{
			int triggerCount = 0;
			if (OwnedTagTriggeredAbilities.ContainsKey(tag))
			{
				List<GameplayAbilitySpecHandle> triggeredAbilityHandles = OwnedTagTriggeredAbilities[tag];

				foreach (GameplayAbilitySpecHandle abilityHandle in triggeredAbilityHandles)
				{
					GameplayAbilitySpec spec = FindAbilitySpecFromHandle(abilityHandle);
					if (spec == null)
					{
						continue;
					}

					if (spec.Ability != null)
					{
						List<AbilityTriggerData> abilityTriggers = spec.Ability.AbilityTriggers;
						foreach (AbilityTriggerData triggerData in abilityTriggers)
						{
							GameplayTag eventTag = triggerData.TriggerTag;

							if (eventTag == tag)
							{
								if (newCount > 0)
								{
									GameplayEventData eventData = new()
									{
										EventMagnitude = newCount,
										EventTag = eventTag,
										Instigator = this.gameObject,
									};
									eventData.Target = eventData.Instigator;

									InternalTryActivateAbility(spec.Handle, null, null, eventData);
								}
								else if (newCount == 0 && triggerData.TriggerSource == GameplayAbilityTriggerSource.OwnedTagPresent)
								{
									CancelAbilitySpec(spec, null);
								}
							}
						}
					}
				}
			}
		}

		public void GetAllAbilities(out List<GameplayAbilitySpecHandle> abilityHandles)
		{
			abilityHandles = new List<GameplayAbilitySpecHandle>(ActivatableAbilities.Items.Count);

			foreach (GameplayAbilitySpec spec in ActivatableAbilities.Items)
			{
				abilityHandles.Add(spec.Handle);
			}
		}

		public void FindAllAbilitiesWithTags(out List<GameplayAbilitySpecHandle> abilityHandles, GameplayTagContainer tags, bool exactMatch = true)
		{
			abilityHandles = new List<GameplayAbilitySpecHandle>();

			foreach (GameplayAbilitySpec currentSpec in ActivatableAbilities.Items)
			{
				if (currentSpec.Ability == null)
				{
					continue;
				}

				GameplayAbility abilityInstance = currentSpec.GetPrimaryInstance();

				if (abilityInstance == null)
				{
					abilityInstance = currentSpec.Ability;
				}

				if (abilityInstance != null)
				{
					if (exactMatch)
					{
						if (abilityInstance.AssetTags.HasAll(tags))
						{
							abilityHandles.Add(currentSpec.Handle);
						}
					}
					else
					{
						if (abilityInstance.AssetTags.HasAny(tags))
						{
							abilityHandles.Add(currentSpec.Handle);
						}
					}
				}
			}
		}

		public void FindAllAbilitiesMatchingQuery(out List<GameplayAbilitySpecHandle> abilityHandles, GameplayTagQuery query)
		{
			abilityHandles = new List<GameplayAbilitySpecHandle>();

			foreach (GameplayAbilitySpec currentSpec in ActivatableAbilities.Items)
			{
				GameplayAbility abilityInstance = currentSpec.GetPrimaryInstance();

				if (abilityInstance == null)
				{
					abilityInstance = currentSpec.Ability;
				}

				if (abilityInstance != null)
				{
					if (abilityInstance.AssetTags.MatchesQuery(query))
					{
						abilityHandles.Add(currentSpec.Handle);
					}
				}
			}
		}

		public List<GameplayAbilitySpec> GetActivatableAbilities()
		{
			return ActivatableAbilities.Items;
		}

		public bool TryActivateAbility(GameplayAbilitySpecHandle abilityToActivate)
		{
			GameplayTagContainer failureTags = new();

			GameplayAbilitySpec spec = FindAbilitySpecFromHandle(abilityToActivate);

			if (spec == null)
			{
				Debug.LogWarning("TryActivateAbility called with invalid Handle");
				return false;
			}

			if (spec.RemoveAfterActivation)
			{
				return false;
			}

			GameplayAbility ability = spec.Ability;

			if (ability == null)
			{
				Debug.LogWarning("TryActivateAbility called with invalid Ability");
				return false;
			}

			GameplayAbilityActorInfo actorInfo = AbilityActorInfo;

			if (actorInfo == null || actorInfo.OwnerActor == null || actorInfo.AvatarActor == null)
			{
				return false;
			}

			return InternalTryActivateAbility(abilityToActivate);
		}

		public bool InternalTryActivateAbility(GameplayAbilitySpecHandle handle, GameplayAbility outInstancedAbility = null, OnGameplayAbilityEnded onGameplayAbilityEndedDelegate = null, GameplayEventData triggerEventData = null)
		{
			if (!handle.IsValid())
			{
				Debug.LogWarning($"InternalTryActivateAbility called with invalid Handle! ASC: {name}. AvatarActor: {AvatarActor}");
				return false;
			}

			GameplayAbilitySpec spec = FindAbilitySpecFromHandle(handle);
			if (spec == null)
			{
				Debug.LogWarning($"InternalTryActivateAbility called with a valid handle but no matching ability was found. Handle: {handle}. ASC: {name}. AvatarActor: {AvatarActor}");
				return false;
			}

			GameplayAbilityActorInfo actorInfo = AbilityActorInfo;
			if (actorInfo == null || actorInfo.OwnerActor == null || actorInfo.AvatarActor == null)
			{
				return false;
			}

			GameplayAbility ability = spec.Ability;
			if (ability == null)
			{
				Debug.LogWarning($"InternalTryActivateAbility called with invalid Ability");
				return false;
			}

			GameplayAbility instancedAbility = spec.GetPrimaryInstance();
			GameplayAbility abilitySource = instancedAbility != null ? instancedAbility : ability;

			if (triggerEventData != null)
			{
				if (!abilitySource.ShouldAbilityRespondToEvent(actorInfo, triggerEventData))
				{
					Debug.Log($"{this.name}: Can't activate {ability} because ShouldAbilityRespondToEvent was false.");

					NotifyAbilityFailed(handle, abilitySource, InternalTryActivateAbilityFailureTags);
					return false;
				}
			}

			{
				GameplayTagContainer sourceTags = triggerEventData?.InstigatorTags;
				GameplayTagContainer targetTags = triggerEventData?.TargetTags;

				if (!abilitySource.CanActivateAbility(handle, actorInfo, sourceTags, targetTags, out InternalTryActivateAbilityFailureTags))
				{
					if (InternalTryActivateAbilityFailureTags.IsEmpty())
					{
						InternalTryActivateAbilityFailureTags.AddTag(GameplayAbilitiesDeveloperSettings.GetOrCreateSettings().ActivateFailCanActivateAbilityTag);
					}

					NotifyAbilityFailed(handle, abilitySource, InternalTryActivateAbilityFailureTags);
					return false;
				}
			}

			if (ability.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerActor)
			{
				if (spec.IsActive)
				{
					if (ability.RetriggerInstancedAbility && instancedAbility)
					{
						Debug.Log($"{this.name}: Ending {instancedAbility} prematurely to retrigger.");

						const bool wasCancelled = false;
						instancedAbility.EndAbility(handle, actorInfo, wasCancelled);
					}
					else
					{
						Debug.LogWarning($"Can't activate instanced per actor ability {ability.name} when their is already a currently active instance for this actor.");
						return false;
					}
				}
			}

			if (ability.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerActor && !instancedAbility)
			{
				Debug.LogWarning($"InternalTryActivateAbility called but instanced ability is missing! Ability: {ability.name}");
				return false;
			}

			if (ability.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerExecution)
			{
				instancedAbility = CreateNewInstanceOfAbility(spec, ability);
				instancedAbility.CallActivateAbility(handle, actorInfo, onGameplayAbilityEndedDelegate, triggerEventData);
			}
			else
			{
				abilitySource.CallActivateAbility(handle, actorInfo, onGameplayAbilityEndedDelegate, triggerEventData);
			}

			if (instancedAbility)
			{
				if (outInstancedAbility != null)
				{
					outInstancedAbility = instancedAbility;
				}
			}

			AbilityLastActivatedTime = Time.time;

			Debug.Log($"{this.name}: Activated [{handle}] {abilitySource}. Level: {spec.Level}");
			return true;
		}

		public void CancelAbilityHandle(in GameplayAbilitySpecHandle abilityHandle)
		{
			for (int i = 0; i < ActivatableAbilities.Items.Count; i++)
			{
				GameplayAbilitySpec spec = ActivatableAbilities.Items[i];
				if (spec.Handle == abilityHandle)
				{
					CancelAbilitySpec(spec, null);
					return;
				}
			}
		}

		public void CancelAbilitySpec(GameplayAbilitySpec spec, GameplayAbility ignore)
		{
			GameplayAbilityActorInfo actorInfo = AbilityActorInfo;

			if (spec.Ability.InstancingPolicy != GameplayAbilityInstancingPolicy.NonInstanced)
			{
				List<GameplayAbility> abilitiesToCancel = spec.AbilityInstances;
				foreach (GameplayAbility instanceAbility in abilitiesToCancel)
				{
					if (instanceAbility != null && instanceAbility != ignore)
					{
						instanceAbility.CancelAbility(spec.Handle, actorInfo);
					}
				}
			}
			else
			{
				spec.Ability.CancelAbility(spec.Handle, actorInfo);
			}
		}

		public void SetUserAbilityActivationInhibited(bool newInhibit)
		{
			if (newInhibit && UserAbilityActivationInhibited)
			{
				Debug.LogWarning("Call to SetUserAbilityActivationInhibited(true) when UserAbilityActivationInhibited was already true");
			}

			UserAbilityActivationInhibited = newInhibit;
		}

		public int HandleGameplayEvent(GameplayTag eventTag, in GameplayEventData payload)
		{
			int triggerCount = 0;
			GameplayTag currentTag = eventTag;

			while (currentTag.IsValid())
			{
				if (GameplayEventTriggerdAbilities.ContainsKey(currentTag))
				{
					List<GameplayAbilitySpecHandle> triggeredAbilityHandles = GameplayEventTriggerdAbilities[currentTag];

					foreach (GameplayAbilitySpecHandle abilityHandle in triggeredAbilityHandles)
					{
						if (TriggerAbilityFromGameplayEvent(abilityHandle, AbilityActorInfo, currentTag, payload, this))
						{
							triggerCount++;
						}
					}
				}

				currentTag = currentTag.RequestDirectParent();
			}

			if (GenericGameplayEventCallbacks.TryGetValue(eventTag, out GameplayEventMulticastDelegate @delegate))
			{
				@delegate.Invoke(payload);
			}

			List<KeyValuePair<GameplayTagContainer, GameplayEventTagMulticastDelegate>> localGameplayEventTagContainerDelegates = GameplayEventTagContainerDelegates.ToList();
			foreach (KeyValuePair<GameplayTagContainer, GameplayEventTagMulticastDelegate> searchPair in localGameplayEventTagContainerDelegates)
			{
				if (searchPair.Key.IsEmpty() || eventTag.MatchesAny(searchPair.Key))
				{
					searchPair.Value.Invoke(eventTag, payload);
				}
			}

			return triggerCount;
		}

		public bool TriggerAbilityFromGameplayEvent(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo, GameplayTag eventTag, in GameplayEventData payload, AbilitySystemComponent component)
		{
			GameplayAbilitySpec spec = FindAbilitySpecFromHandle(handle);
			if (spec == null)
			{
				Debug.Assert(spec != null, $"Failed to find gameplay ability spec {eventTag}");
				return false;
			}

			GameplayAbility instancedAbility = spec.GetPrimaryInstance();
			GameplayAbility ability = instancedAbility != null ? instancedAbility : spec.Ability;

			if (ability == null)
			{
				return false;
			}

			if (payload == null)
			{
				return false;
			}

			GameplayEventData tempEventData = payload;
			tempEventData.EventTag = eventTag;

			return InternalTryActivateAbility(handle, null, null, tempEventData);
		}

		[Obsolete("Use GetGameplayAttributeValueChangeDelegate instead")]
		public OnGameplayAttributeChange RegisterGameplayAttributeEvent(GameplayAttribute attribute)
		{
			return ActiveGameplayEffects.RegisterGameplayAttributeEvent(attribute);
		}

		public OnGameplayAttributeValueChange GetGameplayAttributeValueChangeDelegate(GameplayAttribute attribute)
		{
			return ActiveGameplayEffects.GetGameplayAttributeValueChangeDelegate(attribute);
		}

		public int RemoveActiveEffectsWithTags(GameplayTagContainer tags)
		{
			return RemoveActiveEffects(GameplayEffectQuery.MakeQuery_MatchAnyEffectTags(tags));
		}

		public int RemoveActiveEffectsWithSourceTags(GameplayTagContainer tags)
		{
			return RemoveActiveEffects(GameplayEffectQuery.MakeQuery_MatchAnySourceSpecTags(tags));
		}

		public int RemoveActiveEffectsWithAppliedTags(GameplayTagContainer tags)
		{
			return RemoveActiveEffects(GameplayEffectQuery.MakeQuery_MatchAnyOwningTags(tags));
		}

		public int RemoveActiveEffectsWithGrantedTags(GameplayTagContainer tags)
		{
			return RemoveActiveEffects(GameplayEffectQuery.MakeQuery_MatchAnyOwningTags(tags));
		}

		public virtual int RemoveActiveEffects(in GameplayEffectQuery query, int stacksToRemove = -1)
		{
			return ActiveGameplayEffects.RemoveActiveEffects(query, stacksToRemove);
		}

		public List<float> GetActiveEffectsTimeRemaining(in GameplayEffectQuery query)
		{
			return ActiveGameplayEffects.GetActiveEffectsTimeRemaining(query);
		}

		public int GetGameplayEffectCount(GameplayEffect sourceGameplayEffect, AbilitySystemComponent optionalInstigatorFilterComponent, bool enforceOnGoingCheck = true)
		{
			int count = 0;

			if (sourceGameplayEffect != null)
			{
				GameplayEffectQuery query = new()
				{
					CustomMatchDelegate = (in ActiveGameplayEffect curEffect) =>
					{
						bool matches = false;

						if (curEffect.Spec.Def != null && curEffect.Spec.Def == sourceGameplayEffect)
						{
							if (optionalInstigatorFilterComponent != null)
							{
								matches = optionalInstigatorFilterComponent == curEffect.Spec.EffectContext.InstigatorAbilitySystemComponent;
							}
							else
							{
								matches = true;
							}
						}
						return matches;
					}
				};

				count = ActiveGameplayEffects.GetActiveEffectCount(query, enforceOnGoingCheck);
			}

			return count;
		}

		#region Debug

		public struct AbilitySystemComponentDebugInfo
		{
			public bool ShowAttributes;
			public bool ShowGameplayEffects;
			public bool ShowAbilities;
		}

		public virtual void Debug_Internal(AbilitySystemComponentDebugInfo info)
		{
			string debugTitle = string.Empty;
			if (info.ShowAbilities)
			{
				debugTitle += $"ABILITIES ";
			}
			if (info.ShowAttributes)
			{
				debugTitle += $"ATTRIBUTES ";
			}
			if (info.ShowGameplayEffects)
			{
				debugTitle += $"GAMEPLAYEFFECTS ";
			}
			Debug.Log(debugTitle);

			GameplayTagContainer ownedTags = new();
			GetOwnedGameplayTags(ownedTags);

			string tagsStrings = string.Empty;
			int tagCount = 1;
			int numTags = ownedTags.Count;
			foreach (GameplayTag tag in ownedTags)
			{
				tagsStrings += $"{tag} ({GetTagCount(tag)}) ";
				if (tagCount++ < numTags)
				{
					tagsStrings += ", ";
				}
			}
			Debug.Log($"Owned Tags: {tagsStrings}");

			if (BlockedAbilityTags.ExplicitGameplayTags.Count > 0)
			{
				string blockedTagsStrings = string.Empty;
				int blockedTagCount = 1;
				foreach (GameplayTag tag in BlockedAbilityTags.ExplicitGameplayTags)
				{
					blockedTagsStrings += $"{tag} ({BlockedAbilityTags.GetTagCount(tag)})";
					if (blockedTagCount++ < numTags)
					{
						blockedTagsStrings += ", ";
					}
				}
				Debug.Log($"BlockedAbilitiesTags: {blockedTagsStrings}");
			}
			else
			{
				Debug.Log($"BlockedAbilitiesTags: ");
			}

			HashSet<GameplayAttribute> drawAttributes = new();

			if (info.ShowAttributes)
			{
				foreach (KeyValuePair<GameplayAttribute, Aggregator> it in ActiveGameplayEffects.AttributeAggregatorMap)
				{
					GameplayAttribute attribute = it.Key;
					Aggregator aggregator = it.Value;
					if (aggregator != null)
					{
						AggregatorEvaluateParameters emptyParams = new();

						Dictionary<GameplayModEvaluationChannel, List<AggregatorMod>[]> modMap = new();
						aggregator.EvaluateQualificationForAllMods(emptyParams);
						aggregator.GetAllAggregatorMods(modMap);

						if (modMap.Count == 0)
						{
							continue;
						}

						float finalValue = GetNumericAttribute(attribute);
						float baseValue = aggregator.BaseValue;

						string attributeString = $"{attribute} ({finalValue:F2}) ";
						if (Mathf.Abs(finalValue - baseValue) > 1E-4f)
						{
							attributeString += $" (Base: {baseValue:F2})";
						}

						Debug.Log(attributeString);

						drawAttributes.Add(attribute);

						foreach (KeyValuePair<GameplayModEvaluationChannel, List<AggregatorMod>[]> curMapElement in modMap)
						{
							GameplayModEvaluationChannel channel = curMapElement.Key;
							List<AggregatorMod>[] modArrays = curMapElement.Value;

							string channelNameString = AbilitySystemGlobals.Instance.GetGameplayModEvaluationChannelAliases(channel);
							for (int modOpIdx = 0; modOpIdx < (int)GameplayModOp.Max; modOpIdx++)
							{
								List<AggregatorMod> curModArray = modArrays[modOpIdx];
								foreach (AggregatorMod mod in curModArray)
								{
									bool isActivelyModifyingAttribute = mod.Qualifies;

									ActiveGameplayEffect activeGE = ActiveGameplayEffects.GetActiveGameplayEffect(mod.ActiveHandle);
									string srcName = activeGE is not null ? activeGE.Spec.Def.name : string.Empty;

									if (!isActivelyModifyingAttribute)
									{
										if (mod.SourceTagReqs is not null)
										{
											srcName += $" SourceTags: [{mod.SourceTagReqs}] ";
										}
										if (mod.TargetTagReqs is not null)
										{
											srcName += $" TargetTags: [{mod.TargetTagReqs}] ";
										}
									}

									Debug.Log($"   {channelNameString} {(GameplayModOp)modOpIdx}\t {mod.EvaluatedMagnitude:F2} - {srcName}");
								}
							}
						}
					}
				}
			}

			if (info.ShowGameplayEffects)
			{
				foreach (ActiveGameplayEffect activeGE in ActiveGameplayEffects)
				{
					string durationStr = "Infinite Duration ";
					if (activeGE.Duration > 0)
					{
						durationStr = $"Duration:{activeGE.Duration:F2}. Remaining: {activeGE.GetTimeRemaining(TimerManager.Instance.GetTimeSeconds()):F2} (Start: {activeGE.StartWorldTime:F2}) {(activeGE.DurationHandle.IsValid() ? "Valid Handle" : "Invalid Handle")}";
						if (activeGE.DurationHandle.IsValid())
						{
							durationStr += $"(Local Duration: {TimerManager.Instance.GetTimerRemaining(activeGE.DurationHandle):F2})";
						}
					}
					if (activeGE.Period > 0)
					{
						durationStr += $"(Period: {activeGE.Period:F2})";
					}

					string stackString = string.Empty;
					if (activeGE.Spec.StackCount > 1)
					{
						if (activeGE.Spec.Def.StackingType == GameplayEffectStackingType.AggregateBySource)
						{
							stackString = $"(Stack: {activeGE.Spec.StackCount}. From: {activeGE.Spec.EffectContext.InstigatorAbilitySystemComponent.AvatarActor.name}) ";
						}
						else
						{
							stackString = $"(Stack: {activeGE.Spec.StackCount}) ";
						}
					}

					string LevelString = string.Empty;
					if (activeGE.Spec.Level > 1)
					{
						LevelString = $"(Level: {activeGE.Spec.Level:F2})";
					}

					Debug.Log($"{activeGE.Spec.Def.name} ({durationStr}) {stackString} {LevelString}");

					GameplayTagContainer grantedTags = new();
					activeGE.Spec.GetAllGrantedTags(grantedTags);
					if (grantedTags.Count > 0)
					{
						Debug.Log($"Granted Tags: {grantedTags}");
					}

					for (int modIdx = 0; modIdx < activeGE.Spec.Modifiers.Count; modIdx++)
					{
						if (activeGE.Spec.Def == null)
						{
							Debug.Log("null def! (Backwards campat?)");
							continue;
						}

						ModifierSpec modSpec = activeGE.Spec.Modifiers[modIdx];
						GameplayModifierInfo modInfo = activeGE.Spec.Def.Modifiers[modIdx];


						Debug.Log($"Mod: {modInfo.Attribute}. {modInfo.ModifierOp}. {modSpec.EvaluatedMagnitude:F2}");
					}
				}
			}

			if (info.ShowAttributes)
			{
				foreach (AttributeSet set in SpawnedAttributes)
				{
					if (set == null)
					{
						continue;
					}

					List<GameplayAttribute> attributes = new();
					AttributeSet.GetAttributesFromSetClass(set.GetType(), attributes);
					foreach (GameplayAttribute attribute in attributes)
					{
						if (drawAttributes.Contains(attribute))
						{
							continue;
						}

						if (attribute.IsValid())
						{
							float value = GetNumericAttribute(attribute);
							Debug.Log($"{attribute} ({value:F2})");
						}
					}
				}
			}

			if (info.ShowAbilities)
			{
				foreach (GameplayAbilitySpec abilitySpec in ActivatableAbilities.Items)
				{
					if (abilitySpec.Ability == null)
					{
						continue;
					}

					string statusText = string.Empty;

					GameplayAbility abilitySource = abilitySpec.GetPrimaryInstance();
					if (abilitySource == null)
					{
						abilitySource = abilitySpec.Ability;
					}

					if (abilitySpec.IsActive)
					{
						statusText = $" (Active {abilitySpec.ActiveCount})";
					}
					else if (abilitySource.AssetTags.HasAny(BlockedAbilityTags.ExplicitGameplayTags))
					{
						statusText = $" (TagBlocked)";
					}
					else if (!abilitySource.CanActivateAbility(abilitySpec.Handle, AbilityActorInfo, null, null, out GameplayTagContainer failureTags))
					{
						statusText = $" (CantActivate {failureTags})";

						float cooldown = abilitySpec.Ability.GetCooldownTimeRemaining(AbilityActorInfo);
						if (cooldown > 0)
						{
							statusText += $"   Cooldown: {cooldown:F2}\n";
						}
					}

					Debug.Log($"{abilitySpec.Ability.name} {statusText}");
				}
			}
		}

		public void DebugCyclicAggregatorBroadcasts(Aggregator aggregator)
		{
			ActiveGameplayEffects.DebugCyclicAggregatorBroadcasts(aggregator);
		}

		#endregion
	}
}