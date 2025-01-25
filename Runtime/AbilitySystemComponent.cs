using GameplayTags;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Reflection;

namespace GameplayAbilities
{
	public delegate void ImmunityBlockGE(in GameplayEffectSpec blockedSpec, in ActiveGameplayEffect immunityGameplayEffect);
	public delegate bool GameplayEffectApplicationQuery(in ActiveGameplayEffectsContainer activeGEContainer, in GameplayEffectSpec geSpecToConsider);

	public class AbilitySystemComponent : MonoBehaviour
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

		public GameplayEffectSpec ApplyGameplayEffectSpec(GameplayEffectSpec spec)
		{
			return spec;
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
			Assert.IsTrue(spec.Duration == GameplayEffectConstants.InstantApplication || spec.Period == GameplayEffectConstants.NoPeriod);

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

		public void UpdateTagMap_Internal(in GameplayTagContainer container, in int count_delta)
		{
			if (count_delta > 0)
			{
				foreach (GameplayTag tag in container)
				{
					if (GameplayTagCountContainer.UpdateTagCount(tag, count_delta))
					{
						OnTagUpdated(tag, true);
					}
				}
			}
			else if (count_delta < 0)
			{
				List<GameplayTag> removed_tags = new();
				removed_tags.Reserve(container.Count);
				List<DeferredTagChangeDelegate> deferred_tag_change_delegates = new();

				foreach (GameplayTag tag in container)
				{
					if (GameplayTagCountContainer.UpdateTagCount_DeferredParentRemoval(tag, count_delta, deferred_tag_change_delegates))
					{
						removed_tags.Add(tag);
					}
				}

				if (removed_tags.Count > 0)
				{
					GameplayTagCountContainer.FillParentTags();
				}

				foreach (DeferredTagChangeDelegate @delegate in deferred_tag_change_delegates)
				{
					@delegate.Invoke();
				}

				foreach (GameplayTag tag in removed_tags)
				{
					OnTagUpdated(tag, false);
				}
			}
		}

		public virtual void OnTagUpdated(in GameplayTag tag, bool tag_exists)
		{

		}

		public void AddLooseGameplayTags(GameplayTagContainer gameplay_tags, int count = 1)
		{
			UpdateTagMap(gameplay_tags, count);
		}

		public void RemoveLooseGameplayTags(GameplayTagContainer gameplay_tags, int count = -1)
		{
			UpdateTagMap(gameplay_tags, count);
		}

		public OnGameplayEffectTagCountChanged RegisterGameplayTagEvent(GameplayTag tag, GameplayTagEventType eventType)
		{
			return GameplayTagCountContainer.RegisterGameplayTagEvent(tag, eventType);
		}

		public bool UnregisterGameplayTagEvent(OnGameplayEffectTagCountChanged @delegate, GameplayTag tag, GameplayTagEventType eventType)
		{
			var de = GameplayTagCountContainer.RegisterGameplayTagEvent(tag, eventType);
			de -= @delegate;
			return de == null;
		}

		public void GetOwnedGameplayTags(GameplayTagContainer tag_container)
		{
			tag_container.Reset();
			tag_container.AppendTags(GameplayTagCountContainer.ExplicitTags);
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

		public void NotifyAbilityCommit(GameplayAbility ability)
		{

		}

		public void NotifyAbilityActivated(GameplayAbilitySpecHandle handle, GameplayAbility ability)
		{

		}

		public void NotifyAbilityEnd(GameplayAbilitySpecHandle handle, GameplayAbility ability, bool was_cancelled)
		{

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

		public void ApplyAbilityBlockAndCancelTags(GameplayTagContainer AbilityTags, GameplayAbility RequestingAbility, bool EnableBlockTags, GameplayTagContainer BlockTags, bool bExecuteCancelTags, GameplayTagContainer CancelTags)
		{

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

	}
}