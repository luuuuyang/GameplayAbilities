using GameplayTags;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Reflection;
using System.Linq;

namespace GameplayAbilities
{
	public delegate void AbilityFailedDelegate(in GameplayAbility ability, in GameplayTagContainer failureReason);
	public delegate void AbilityEnded(in GameplayAbility ability);
	public delegate void ImmunityBlockGE(in GameplayEffectSpec blockedSpec, in ActiveGameplayEffect immunityGameplayEffect);
	public delegate bool GameplayEffectApplicationQuery(in ActiveGameplayEffectsContainer activeGEContainer, in GameplayEffectSpec geSpecToConsider);

	public class AbilitySystemComponent : MonoBehaviour, IGameplayTagAssetInterface
	{
		public delegate void OnGameplayEffectAppliedDelegate(AbilitySystemComponent instigator, in GameplayEffectSpec spec, ActiveGameplayEffectHandle handle);
		public GameplayTagCountContainer GameplayTagCountContainer = new();
		public GameplayTagCountContainer BlockedAbilityTags = new();
		public ActiveGameplayEffectsContainer ActiveGameplayEffects = new();
		public GameplayAbilitySpecContainer ActivatableAbilities = new();
		public GameplayAbilityActorInfo AbilityActorInfo = new();
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

		public bool UserAbilityActivationInhibited;

		private GameplayTagContainer InternalTryActivateAbilityFailureTags = new();

		protected Dictionary<GameplayTag, List<GameplayAbilitySpecHandle>> GameplayEventTriggerdAbilities = new();
		protected Dictionary<GameplayTag, List<GameplayAbilitySpecHandle>> OwnedTagTriggeredAbilities = new();
		protected float AbilityLastActivatedTime;

		private void Awake()
		{
			ActiveGameplayEffects.RegisterWithOwner(this);
		}

		protected virtual void Start()
		{
			InitAbilityActorInfo(gameObject, gameObject);
		}

		public virtual void InitAbilityActorInfo(GameObject ownerActor, GameObject avatarActor)
		{
			bool avatarChanged = avatarActor != AbilityActorInfo.AvatarActor;

			AbilityActorInfo.InitFromActor(ownerActor, avatarActor, this);

			if (avatarChanged)
			{
				foreach (var spec in ActivatableAbilities.Items)
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

		public ActiveGameplayEffectHandle ApplyGameplayEffectToSelf(in GameplayEffect gameplay_effect, float level, in GameplayEffectContextHandle context)
		{
			if (gameplay_effect == null)
			{
				return new ActiveGameplayEffectHandle();
			}

			GameplayEffectSpec spec = new(gameplay_effect, context, level);
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

					Debug.Log($"Applied {ourCopySpec.Def}");
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

		public void NotifyTagMap_StackCountChange(in GameplayTagContainer container)
		{
			foreach (GameplayTag tag in container)
			{
				GameplayTagCountContainer.Notify_StackCountChange(tag);
			}
		}

		public ActiveGameplayEffectHandle ApplyGameplayEffectToTarget(GameplayEffect gameplayEffect, AbilitySystemComponent target, float level = GameplayEffectConstants.InvalidLevel, GameplayEffectContextHandle context = new())
		{
			if (!context.IsValid)
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

		public void InhibitActiveGameplayEffect(ActiveGameplayEffectHandle activeGEHandle, bool inhibit)
		{
			ActiveGameplayEffect activeGE = ActiveGameplayEffects.GetActiveGameplayEffect(activeGEHandle);
			if (activeGE is null)
			{
				Debug.LogError($"InhibitActiveGameplayEffect received bad Active GameplayEffect Handle: {activeGEHandle}");
				return;
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

				activeGE.EventSet.OnInhibitionChanged?.Invoke(activeGEHandle, activeGE.IsInhibited);
			}
		}

		public bool RemoveActiveGameplayEffect(ActiveGameplayEffectHandle handle, int stacksToRemove = -1)
		{
			return ActiveGameplayEffects.RemoveActiveGameplayEffect(handle, stacksToRemove);
		}

		public ActiveGameplayEffect GetActiveGameplayEffect(in ActiveGameplayEffectHandle handle)
		{
			return ActiveGameplayEffects.GetActiveGameplayEffect(handle);
		}

		public ActiveGameplayEffectEvents GetActiveEffectEventSet(in ActiveGameplayEffectHandle handle)
		{
			ActiveGameplayEffect activeEffect = ActiveGameplayEffects.GetActiveGameplayEffect(handle);
			return activeEffect != null ? activeEffect.EventSet : null;
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

			Debug.Log($"Executed {spec.Def}");
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

		#region GameplayTags
		public bool HasAnyMatchingGameplayTags(GameplayTagContainer tag_container)
		{
			return GameplayTagCountContainer.HasAnyMatchingGameplayTags(tag_container);
		}

		public void UpdateTagMap(in GameplayTagContainer container, int count_delta)
		{
			if (!container.IsEmpty())
			{
				UpdateTagMap_Internal(container, count_delta);
			}
		}

		public GameplayTagContainer GetBlockedAbilityTags()
		{
			return BlockedAbilityTags.ExplicitTags;
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

		public bool UnregisterGameplayTagEvent(OnGameplayEffectTagCountChanged @delegate, GameplayTag tag, GameplayTagEventType eventType = GameplayTagEventType.NewOrRemoved)
		{
			var de = GameplayTagCountContainer.RegisterGameplayTagEvent(tag, eventType);
			de -= @delegate;
			return de == null;
		}

		public void GetOwnedGameplayTags(GameplayTagContainer tagContainer)
		{
			tagContainer.Reset();
			tagContainer.AppendTags(GameplayTagCountContainer.ExplicitTags);
		}

		public GameplayTagContainer GetOwnedGameplayTags()
		{
			return GameplayTagCountContainer.ExplicitTags;
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

		public void OnAttributeAggregatorDirty(Aggregator aggregator, GameplayAttribute attribute, bool from_recursive_call)
		{
			ActiveGameplayEffects.OnAttributeAggregatorDirty(aggregator, attribute, from_recursive_call);
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
				if (set.GetType() == attributeClass)
				{
					return set;
				}
			}
			return null;
		}

		public AttributeSet GetAttributeSubobjectChecked(Type attributeClass)
		{
			AttributeSet set = GetAttributeSubobject(attributeClass);
			Assert.IsNotNull(set);
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
				SpawnedAttributes.Add(attribute);
			}
		}

		public void RemoveSpawnedAttribute(AttributeSet attribute)
		{
			if (SpawnedAttributes.Remove(attribute))
			{
			}
		}

		public bool HasAttributeSetForAttribute(GameplayAttribute attribute)
		{
			return attribute.IsValid() && GetAttributeSubobject(attribute.GetAttributeSetClass()) != null;
		}

		public virtual void HandleChangeAbilityCanBeCanceled(GameplayTagContainer ability_tags, GameplayAbility requesting_ability, bool can_be_cancelled)
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

		public virtual void NotifyAbilityEnd(GameplayAbilitySpecHandle handle, GameplayAbility ability, bool wasCancelled)
		{
			Debug.Assert(ability != null);
			GameplayAbilitySpec spec = FindAbilitySpecFromHandle(handle);
			if (spec == null)
			{
				return;
			}

			Debug.Log($"{this.name}: Ended [{handle}] {(spec.GetPrimaryInstance() ? spec.GetPrimaryInstance().name : ability.name)}. Level: {spec.Level}. WasCancelled: {wasCancelled}");

			Debug.Assert(spec.ActiveCount > 0, $"NotifyAbilityEnded called when the Spec->ActiveCount <= 0 for ability {ability.name}");
			if (spec.ActiveCount > 0)
			{
				spec.ActiveCount--;
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
				spec.NonReplicatedInstances.Remove(ability);
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

		public void CancelAbilities(in GameplayTagContainer withTags, in GameplayTagContainer withoutTags, GameplayAbility ignore)
		{
			foreach (GameplayAbilitySpec spec in ActivatableAbilities.Items)
			{
				if (!spec.IsActive || spec.Ability == null)
				{
					continue;
				}

				GameplayTagContainer abilityTags = spec.Ability.AssetTags;
				bool withTagPass = withoutTags == null || abilityTags.HasAny(withoutTags);
				bool withoutTagPass = withTags == null || !abilityTags.HasAny(withTags);

				if (withTagPass && withoutTagPass)
				{
					CancelAbilitySpec(spec, ignore);
				}
			}
		}

		public GameplayEffectContextHandle MakeEffectContext()
		{
			GameplayEffectContextHandle context = new(new GameplayEffectContext());
			context.AddInstigator(AbilityActorInfo.OwnerActor, AbilityActorInfo.AvatarActor);
			return context;
		}

		public float GetNumericAttributeBase(in GameplayAttribute attribute)
		{
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

		public void SetNumericAttribute_Internal(GameplayAttribute attribute, float newValue)
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

		public virtual void OnGiveAbility(GameplayAbilitySpec spec)
		{
			if (spec.Ability == null)
			{
				return;
			}

			GameplayAbility specAbility = spec.Ability;
			bool instancedPerActor = specAbility.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerActor;
			if (instancedPerActor)
			{
				if (spec.ReplicatedInstances.Count == 0)
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
										Instigator = this
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

		public void GetAllAbilities(List<GameplayAbilitySpecHandle> abilityHandles)
		{
			abilityHandles.Clear();

			foreach (GameplayAbilitySpec spec in ActivatableAbilities.Items)
			{
				abilityHandles.Add(spec.Handle);
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
				Debug.LogWarning($"InternalTryActivateAbility called with invalid Handle! ASC: {this.name}. AvatarActor: {AbilityActorInfo.AvatarActor.name}");
				return false;
			}

			GameplayAbilitySpec spec = FindAbilitySpecFromHandle(handle);
			if (spec == null)
			{
				Debug.LogWarning($"InternalTryActivateAbility called with a valid handle but no matching ability was found. Handle: {handle}. ASC: {this.name}. AvatarActor: {AbilityActorInfo.AvatarActor.name}");
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
			GameplayAbility abilitySource = instancedAbility ?? ability;

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
				GameplayTagContainer sourceTags = triggerEventData != null ? triggerEventData.InstigatorTags : null;
				GameplayTagContainer targetTags = triggerEventData != null ? triggerEventData.TargetTags : null;

				if (!abilitySource.CanActivateAbility(handle, actorInfo, sourceTags, targetTags, out InternalTryActivateAbilityFailureTags))
				{
					if (InternalTryActivateAbilityFailureTags.IsEmpty())
					{
						InternalTryActivateAbilityFailureTags.AddTag(GameplayAbilitiesDeveloperSettings.instance.ActivateFailCanActivateAbilityTag);
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
				}
				else
				{
					Debug.Log($"Can't activate instanced per actor ability {ability} when their is already a currently active instance for this actor.");
					return false;
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
			foreach (GameplayAbilitySpec spec in ActivatableAbilities.Items)
			{
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
	}
}