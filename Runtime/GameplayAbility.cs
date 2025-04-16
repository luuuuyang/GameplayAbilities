using System;
using System.Collections.Generic;
using GameplayTags;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayAbilities
{
	public delegate void OnGameplayAbilityEnded(GameplayAbility ability);
	public delegate void OnGameplayAbilityCancelled();

	[Serializable]
	public struct AbilityTriggerData
	{
		public GameplayTag TriggerTag;
		public GameplayAbilityTriggerSource TriggerSource;
	}

	[CreateAssetMenu(fileName = "GameplayAbility", menuName = "GameplayAbilities/GameplayAbility")]
	public class GameplayAbility : ScriptableObject
	{
		[FoldoutGroup("Tags")]
		[LabelText("AssetTags (Default AbilityTags)")]
		public GameplayTagContainer AbilityTags = new();

		[FoldoutGroup("Tags")]
		[SerializeField]
		protected GameplayTagContainer CancelAbilitiesWithTags = new();

		[FoldoutGroup("Tags")]
		[SerializeField]
		protected GameplayTagContainer BlockAbilitiesWithTags = new();

		[FoldoutGroup("Tags")]
		[SerializeField]
		protected GameplayTagContainer ActivationOwnedTags = new();

		[FoldoutGroup("Tags")]
		[SerializeField]
		protected GameplayTagContainer ActivationRequiredTags = new();

		[FoldoutGroup("Tags")]
		[SerializeField]
		protected GameplayTagContainer ActivationBlockedTags = new();

		[FoldoutGroup("Tags")]
		[SerializeField]
		protected GameplayTagContainer SourceRequiredTags = new();

		[FoldoutGroup("Tags")]
		[SerializeField]
		protected GameplayTagContainer SourceBlockedTags = new();

		[FoldoutGroup("Tags")]
		[SerializeField]
		protected GameplayTagContainer TargetRequiredTags = new();

		[FoldoutGroup("Tags")]
		[SerializeField]
		protected GameplayTagContainer TargetBlockedTags = new();

		public GameplayTagContainer AssetTags => AbilityTags;

		[FoldoutGroup("Advanced")]
		public GameplayAbilityReplicationPolicy ReplicationPolicy;

		[FoldoutGroup("Advanced")]
		public GameplayAbilityInstancingPolicy InstancingPolicy = GameplayAbilityInstancingPolicy.InstancedPerActor;

		[FoldoutGroup("Advanced")]
		public bool RetriggerInstancedAbility;

		[FoldoutGroup("Costs")]
		public GameplayEffect Cost;

		[FoldoutGroup("Triggers")]
		public List<AbilityTriggerData> AbilityTriggers = new();

		[FoldoutGroup("Cooldowns")]
		public GameplayEffect Cooldown;

		[HideInInspector]
		public bool bIsActive;

		[HideInInspector]
		public bool IsAbilityEnding;

		public bool IsBlockingOtherAbilities
		{
			get
			{
				if (InstancingPolicy != GameplayAbilityInstancingPolicy.NonInstanced)
				{
					return _IsBlockingOtherAbilities;
				}
				return true;
			}
			set { _IsBlockingOtherAbilities = value; }
		}
		private bool _IsBlockingOtherAbilities;

		protected bool IsCancelable;

		public GameplayAbilityActorInfo CurrentActorInfo;
		public GameplayAbilitySpecHandle CurrentSpecHandle;

		protected bool MarkPendingKillOnAbilityEnd;

		public event OnGameplayAbilityCancelled OnGameplayAbilityCanceled;
		public event OnGameplayAbilityEnded OnGameplayAbilityEnded;
		public event GameplayAbilityEndedDelegate OnGameplayAbilityEndedWithData;

		public virtual bool CanActivateAbility(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actorInfo, in GameplayTagContainer sourceTags, in GameplayTagContainer targetTags, out GameplayTagContainer optionalRelevantTags)
		{
			optionalRelevantTags = new GameplayTagContainer();

			if (actorInfo != null && !actorInfo.AvatarActor.TryGetTarget(out GameObject _))
			{
				return false;
			}

			if (!actorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
			{
				return false;
			}

			GameplayAbilitySpec spec = abilitySystemComponent.FindAbilitySpecFromHandle(handle);
			if (spec == null)
			{
				Debug.LogWarning($"CanActivateAbility {this} failed, called with invalid Handle");
				return false;
			}

			if (abilitySystemComponent.UserAbilityActivationInhibited)
			{
				Debug.Log($"{actorInfo.OwnerActor}: {spec.Ability} could not be activated due to UserAbilityActivationInhibited returning true");
				return false;
			}

			AbilitySystemGlobals abilitySystemGlobals = AbilitySystemGlobals.Instance;
			if (abilitySystemGlobals.ShouldIgnoreCooldowns && !CheckCooldown(handle, actorInfo, optionalRelevantTags))
			{
				return false;
			}

			if (abilitySystemGlobals.ShouldIgnoreCosts && !CheckCost(handle, actorInfo, optionalRelevantTags))
			{
				return false;
			}

			if (!DoesAbilitySatisfyTagRequirements(abilitySystemComponent, sourceTags, targetTags, optionalRelevantTags))
			{
				return false;
			}

			return true;
		}

		public virtual bool DoesAbilitySatisfyTagRequirements(in AbilitySystemComponent abilitySystemComponent, in GameplayTagContainer sourceTags, in GameplayTagContainer targetTags, GameplayTagContainer optionalRelevantTags)
		{
			optionalRelevantTags = new GameplayTagContainer();
			bool blocked = false;
			void CheckForBlocked(in GameplayTagContainer containerA, in GameplayTagContainer containerB)
			{
				if (containerA.IsEmpty() || containerB.IsEmpty() || !containerA.HasAny(containerB))
				{
					return;
				}

				if (optionalRelevantTags is not null)
				{
					if (!blocked)
					{
						GameplayTag blockedTag = AbilitySystemGlobals.Instance.ActivateFailTagsBlockedTag;
						optionalRelevantTags.AddTag(blockedTag);
					}

					optionalRelevantTags.AppendMatchingTags(containerA, containerB);
				}

				blocked = true;
			}

			bool missing = false;
			void CheckForRequired(in GameplayTagContainer tagsToCheck, in GameplayTagContainer requiredTags)
			{
				if (requiredTags.IsEmpty() || tagsToCheck.HasAll(requiredTags))
				{
					return;
				}

				if (optionalRelevantTags is not null)
				{
					if (!missing)
					{
						GameplayTag missingTag = AbilitySystemGlobals.Instance.ActivateFailTagsMissingTag;
						optionalRelevantTags.AddTag(missingTag);
					}

					GameplayTagContainer missingTags = new(requiredTags);
					missingTags.RemoveTags(tagsToCheck.GetGameplayTagParents());
					optionalRelevantTags.AppendTags(missingTags);
				}

				missing = true;
			}

			CheckForBlocked(abilitySystemComponent.GetBlockedAbilityTags(), AssetTags);
			CheckForBlocked(abilitySystemComponent.GetOwnedGameplayTags(), ActivationBlockedTags);
			if (sourceTags is not null)
			{
				CheckForBlocked(sourceTags, SourceBlockedTags);
			}
			if (targetTags is not null)
			{
				CheckForBlocked(targetTags, TargetBlockedTags);
			}

			CheckForRequired(abilitySystemComponent.GetOwnedGameplayTags(), ActivationBlockedTags);
			if (sourceTags is not null)
			{
				CheckForRequired(sourceTags, SourceRequiredTags);
			}
			if (targetTags is not null)
			{
				CheckForRequired(targetTags, TargetRequiredTags);
			}

			return !blocked && !missing;
		}

		public void CallActivateAbility(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actorInfo, OnGameplayAbilityEnded onGameplayAbilityEndedDelegate, in GameplayEventData triggerEventData)
		{
			PreActivate(handle, actorInfo, onGameplayAbilityEndedDelegate);
			ActivateAbility(handle, actorInfo, triggerEventData);
		}

		public virtual void PreActivate(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actorInfo, OnGameplayAbilityEnded onGameplayAbilityEndedDelegate)
		{
			actorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent comp);

			if (InstancingPolicy != GameplayAbilityInstancingPolicy.NonInstanced)
			{
				bIsActive = true;
				IsBlockingOtherAbilities = true;
				IsCancelable = true;
			}

			SetCurrentActorInfo(handle, actorInfo);

			comp.HandleChangeAbilityCanBeCanceled(AssetTags, this, true);
			comp.AddLooseGameplayTags(ActivationOwnedTags);

			if (onGameplayAbilityEndedDelegate != null)
			{
				OnGameplayAbilityEnded += onGameplayAbilityEndedDelegate;
			}

			comp.NotifyAbilityActivated(handle, this);

			comp.ApplyAbilityBlockAndCancelTags(AssetTags, this, true, BlockAbilitiesWithTags, true, CancelAbilitiesWithTags);

			GameplayAbilitySpec spec = comp.FindAbilitySpecFromHandle(handle);
			if (spec == null)
			{
				Debug.LogWarning("PreActivate called with a valid handle but no matching ability spec was found. Handle: %s ASC: %s. AvatarActor: %s");
				return;
			}

			if (spec.ActiveCount < byte.MaxValue)
			{
				spec.ActiveCount++;
			}
			else
			{
				Debug.LogWarning($"PreActivate {this} called when the Spec->ActiveCount ({spec.ActiveCount}) >= byte.MaxValue");
			}
		}

		public virtual void ActivateAbility(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actorInfo, in GameplayEventData triggerEventData)
		{

		}

		public virtual bool ShouldAbilityRespondToEvent(in GameplayAbilityActorInfo actorInfo, in GameplayEventData payload)
		{
			return true;
		}

		public virtual bool CommitAbility(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actorInfo, GameplayTagContainer optionalRelevantTags)
		{
			if (!CommitCheck(handle, actorInfo, optionalRelevantTags))
			{
				return false;
			}

			CommitExecute(handle, actorInfo);

			actorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent);
			abilitySystemComponent.NotifyAbilityCommit(this);

			return true;
		}

		public virtual bool CommitCheck(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actorInfo, GameplayTagContainer optionalRelevantTags)
		{
			bool validHandle = handle.IsValid();
			bool validActorInfoPieces = actorInfo != null && actorInfo.AbilitySystemComponent != null;

			actorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent);
			bool validSpecFound = validActorInfoPieces && abilitySystemComponent.FindAbilitySpecFromHandle(handle) != null;

			if (!validHandle || !validActorInfoPieces || !validSpecFound)
			{
				Debug.LogWarning($"GameplayAbility::CommitCheck provided an invalid handle or actor info or couldn't find ability spec: {this} Handle Valid: {validHandle} ActorInfo Valid: {validActorInfoPieces} Spec Not Found: {validSpecFound}");
				return false;
			}

			AbilitySystemGlobals abilitySystemGlobals = AbilitySystemGlobals.Instance;

			if (!abilitySystemGlobals.IgnoreAbilitySystemCooldowns && !CheckCooldown(handle, actorInfo, optionalRelevantTags))
			{
				return false;
			}

			if (!abilitySystemGlobals.IgnoreAbilitySystemCosts && !CheckCost(handle, actorInfo, optionalRelevantTags))
			{
				return false;
			}

			return true;
		}

		public virtual bool CheckCooldown(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actorInfo, GameplayTagContainer optionalRelevantTags)
		{
			if (actorInfo == null)
			{
				return true;
			}

			GameplayTagContainer cooldownTags = GetCooldownTags();
			if (cooldownTags is not null && !cooldownTags.IsEmpty())
			{
				if (actorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
				{
					if (abilitySystemComponent.HasAnyMatchingGameplayTags(cooldownTags))
					{
						if (optionalRelevantTags is not null)
						{
							GameplayTag failCooldownTag = AbilitySystemGlobals.Instance.ActivateFailCooldownTag;
							if (failCooldownTag.IsValid())
							{
								optionalRelevantTags.AddTag(failCooldownTag);
							}

							optionalRelevantTags.AppendMatchingTags(abilitySystemComponent.GetOwnedGameplayTags(), cooldownTags);
						}

						return false;
					}
				}
			}
			return true;
		}

		public GameplayTagContainer GetCooldownTags()
		{
			return Cooldown.GrantedTags;
		}

		public virtual bool CheckCost(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actorInfo, GameplayTagContainer optionalRelevantTags)
		{
			GameplayEffect costGE = Cost;
			if (costGE != null)
			{
				if (actorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
				{
					if (!abilitySystemComponent.CanApplyAttributeModifiers(costGE, GetAbilityLevel(handle, actorInfo), MakeEffectContext(handle, actorInfo)))
					{
						GameplayTag costTag = AbilitySystemGlobals.Instance.ActivateFailCostTag;

						if (optionalRelevantTags is not null && costTag.IsValid())
						{
							optionalRelevantTags.AddTag(costTag);
						}
						return false;
					}
				}
			}
			return true;
		}

		public GameplayTagContainer GetCostTags()
		{
			return Cost.GrantedTags;
		}

		public virtual void CommitExecute(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actorInfo)
		{
			ApplyCooldown(handle, actorInfo);

			ApplyCost(handle, actorInfo);
		}

		public virtual bool CanBeCanceled()
		{
			if (InstancingPolicy != GameplayAbilityInstancingPolicy.NonInstanced)
			{
				return IsCancelable;
			}

			return true;
		}

		public virtual void CancelAbility(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
		{
			if (CanBeCanceled())
			{
				OnGameplayAbilityCanceled?.Invoke();

				bool wasCancelled = true;
				EndAbility(handle, actorInfo, wasCancelled);
			}
		}

		public virtual void EndAbility(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actorInfo, bool wasCancelled)
		{
			if (IsEndAbilityValid(handle, actorInfo))
			{
				if (InstancingPolicy != GameplayAbilityInstancingPolicy.NonInstanced)
				{
					IsAbilityEnding = true;
				}

				if (bIsActive == false && InstancingPolicy != GameplayAbilityInstancingPolicy.NonInstanced)
				{
					return;
				}

				TimerManager.Instance.ClearAllTimersForObject(this);

				OnGameplayAbilityEnded?.Invoke(this);
				OnGameplayAbilityEnded = null;

				OnGameplayAbilityEndedWithData?.Invoke(new AbilityEndedData(this, handle, wasCancelled));
				OnGameplayAbilityEndedWithData = null;

				if (InstancingPolicy != GameplayAbilityInstancingPolicy.NonInstanced)
				{
					bIsActive = false;
					IsAbilityEnding = false;
				}

				if (actorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
				{
					abilitySystemComponent.RemoveLooseGameplayTags(ActivationOwnedTags);

					if (CanBeCanceled())
					{
						abilitySystemComponent.HandleChangeAbilityCanBeCanceled(AssetTags, this, false);
					}

					if (IsBlockingOtherAbilities)
					{
						abilitySystemComponent.ApplyAbilityBlockAndCancelTags(AssetTags, this, false, BlockAbilitiesWithTags, false, CancelAbilitiesWithTags);
					}

					abilitySystemComponent.NotifyAbilityEnded(handle, this, wasCancelled);
				}
			}
		}

		public bool IsEndAbilityValid(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actorInfo)
		{
			if ((bIsActive == false || IsAbilityEnding == true) && InstancingPolicy != GameplayAbilityInstancingPolicy.NonInstanced)
			{
				Debug.LogWarning($"IsEndAbilityValid returning false on Ability {this} due to EndAbility being called multiple times");
				return false;
			}

			if (actorInfo == null || !actorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilityComp))
			{
				Debug.LogWarning($"IsEndAbilityValid returning false on Ability {this} due to AbilitySystemComponent being invalid");
				return false;
			}

			GameplayAbilitySpec spec = abilityComp.FindAbilitySpecFromHandle(handle);
			bool isSpecActive = spec is not null ? spec.IsActive : IsActive();

			if (!isSpecActive)
			{
				Debug.LogWarning($"IsEndAbilityValid returning false on Ability {this} due to spec not being active");
				return false;
			}

			return true;
		}

		public virtual void ApplyCost(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
		{
			GameplayEffect costGE = Cost;
			if (costGE != null)
			{
				ApplyGameplayEffectToOwner(handle, actorInfo, costGE, GetAbilityLevel(handle, actorInfo));
			}
		}

		public virtual float GetCooldownTimeRemaining(in GameplayAbilityActorInfo actorInfo)
		{
			if (actorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent ASC))
			{
				GameplayTagContainer cooldownTags = GetCooldownTags();
				if (cooldownTags is not null && cooldownTags.Count > 0)
				{
					GameplayEffectQuery query = GameplayEffectQuery.MakeQuery_MatchAnyOwningTags(cooldownTags);
					List<float> durations = ASC.GetActiveEffectsTimeRemaining(query);
					if (durations.Count > 0)
					{
						durations.Sort();
						return durations[^1];
					}
				}
			}

			return 0;
		}

		public virtual void ApplyCooldown(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
		{
			GameplayEffect cooldownGE = Cooldown;
			if (cooldownGE != null)
			{
				ApplyGameplayEffectToOwner(handle, actorInfo, cooldownGE, GetAbilityLevel(handle, actorInfo));
			}
		}
		
		protected virtual void EndAbility()
		{
			Debug.Assert(CurrentActorInfo != null);

			const bool wasCancelled = false;
			EndAbility(CurrentSpecHandle, CurrentActorInfo, wasCancelled);
		}

		protected ActiveGameplayEffectHandle ApplyGameplayEffectToOwner(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo, GameplayEffect gameplayEffect, int gameplayEffectLevel, int stacks = 1)
		{
			if (gameplayEffect != null)
			{
				GameplayEffectSpecHandle specHandle = MakeOutgoingGameplayEffectSpec(handle, actorInfo, gameplayEffect, gameplayEffectLevel);
				if (specHandle.IsValid())
				{
					specHandle.Data.StackCount = stacks;
					return ApplyGameplayEffectSpecToOwner(handle, actorInfo, specHandle);
				}
			}

			return new ActiveGameplayEffectHandle();
		}

		protected ActiveGameplayEffectHandle ApplyGameplayEffectSpecToOwner(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actorInfo, GameplayEffectSpecHandle specHandle)
		{
			if (specHandle.IsValid())
			{
				if (actorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
				{
					return abilitySystemComponent.ApplyGameplayEffectSpecToSelf(specHandle.Data);
				}
			}
			return new ActiveGameplayEffectHandle();
		}

		protected List<ActiveGameplayEffectHandle> ApplyGameplayEffectToTarget(GameplayAbilityTargetDataHandle target, GameplayEffect gameplayEffectClass, float gameplayEffectLevel = 1, int stacks = 1)
		{
			return ApplyGameplayEffectToTarget(CurrentSpecHandle, CurrentActorInfo, target, gameplayEffectClass, gameplayEffectLevel, stacks);
		}

		protected List<ActiveGameplayEffectHandle> ApplyGameplayEffectToTarget(in GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actorInfo, in GameplayAbilityTargetDataHandle target, GameplayEffect gameplayEffectClass, float gameplayEffectLevel, int stacks = 1)
		{
			List<ActiveGameplayEffectHandle> effectHandles = new();

			if (gameplayEffectClass == null)
			{
				Debug.LogError($"ApplyGameplayEffectToTarget called on ability {name} with no GameplayEffect.");
			}
			else
			{
				GameplayEffectSpecHandle specHandle = MakeOutgoingGameplayEffectSpec(handle, actorInfo, gameplayEffectClass, gameplayEffectLevel);
				if (specHandle.Data != null)
				{
					specHandle.Data.StackCount = stacks;
					effectHandles.AddRange(ApplyGameplayEffectSpecToTarget(handle, actorInfo, specHandle, target));
				}
				else
				{
					Debug.LogWarning($"GameplayAbility::ApplyGameplayEffectToTarget failed to create valid spec handle. Ability: {name}");
				}
			}

			return effectHandles;
		}

		protected List<ActiveGameplayEffectHandle> ApplyGameplayEffectSpecToTarget(in GameplayEffectSpecHandle specHandle, GameplayAbilityTargetDataHandle targetData)
		{
			return ApplyGameplayEffectSpecToTarget(CurrentSpecHandle, CurrentActorInfo, specHandle, targetData);
		}

		protected List<ActiveGameplayEffectHandle> ApplyGameplayEffectSpecToTarget(in GameplayAbilitySpecHandle abilityHandle, in GameplayAbilityActorInfo actorInfo, in GameplayEffectSpecHandle specHandle, in GameplayAbilityTargetDataHandle targetData)
		{
			List<ActiveGameplayEffectHandle> effectHandles = new();

			if (specHandle.IsValid())
			{
				foreach (GameplayAbilityTargetData data in targetData.Data)
				{
					if (data != null)
					{
						effectHandles.AddRange(data.ApplyGameplayEffectSpec(specHandle.Data));
					}
					else
					{
						Debug.LogWarning($"GameplayAbility::ApplyGameplayEffectSpecToTarget invalid target data passed in. Ability: {name}");
					}
				}
			}

			return effectHandles;
		}

		protected void RemoveGameplayEffectFromOwnerWithAssetTags(GameplayTagContainer withTags, int stacksToRemove = -1)
		{
			AbilitySystemComponent abilitySystemComponent = GetAbilitySystemComponentFromActorInfo_Ensured();
			if (abilitySystemComponent != null)
			{
				GameplayEffectQuery query = GameplayEffectQuery.MakeQuery_MatchAnyEffectTags(withTags);
				abilitySystemComponent.RemoveActiveEffects(query, stacksToRemove);
			}
		}

		protected void RemoveGameplayEffectFromOwnerWithGrantedTags(GameplayTagContainer withGrantedTags, int stacksToRemove = -1)
		{
			AbilitySystemComponent abilitySystemComponent = GetAbilitySystemComponentFromActorInfo_Ensured();
			if (abilitySystemComponent != null)
			{
				GameplayEffectQuery query = GameplayEffectQuery.MakeQuery_MatchAnyOwningTags(withGrantedTags);
				abilitySystemComponent.RemoveActiveEffects(query, stacksToRemove);
			}
		}

		protected void RemoveGameplayEffectFromOwnerWithHandle(ActiveGameplayEffectHandle handle, int stacksToRemove = -1)
		{
			AbilitySystemComponent abilitySystemComponent = GetAbilitySystemComponentFromActorInfo_Ensured();
			if (abilitySystemComponent != null)
			{
				abilitySystemComponent.RemoveActiveGameplayEffect(handle, stacksToRemove);
			}
		}

		public virtual GameplayEffectSpecHandle MakeOutgoingGameplayEffectSpec(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo, GameplayEffect gameplayEffect, float level)
		{
			if (actorInfo == null)
			{
				return new GameplayEffectSpecHandle();
			}

			actorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent);

			GameplayEffectSpecHandle newHandle = abilitySystemComponent.MakeOutgoingSpec(gameplayEffect, level, MakeEffectContext(handle, actorInfo));
			if (newHandle.IsValid())
			{
				GameplayAbilitySpec abilitySpec = abilitySystemComponent.FindAbilitySpecFromHandle(handle);
				ApplyAbilityTagsToGameplayEffectSpec(newHandle.Data, abilitySpec);

				if (abilitySpec != null)
				{
					newHandle.Data.SetByCallerTagMagnitudes = abilitySpec.SetByCallerTagMagnitudes;
				}
			}

			return newHandle;
		}

		public virtual GameplayEffectContextHandle MakeEffectContext(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actorInfo)
		{
			GameplayEffectContextHandle context = new GameplayEffectContextHandle(AbilitySystemGlobals.Instance.AllocGameplayEffectContext());
			context.SetAbility(this);

			if (actorInfo != null)
			{
				actorInfo.OwnerActor.TryGetTarget(out GameObject ownerActor);
				actorInfo.AvatarActor.TryGetTarget(out GameObject avatarActor);
				context.AddInstigator(ownerActor, avatarActor);

				if (actorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
				{
					GameplayAbilitySpec abilitySpec = abilitySystemComponent.FindAbilitySpecFromHandle(handle);
					if (abilitySpec is not null)
					{
						abilitySpec.SourceObject.TryGetTarget(out UnityEngine.Object sourceObject);
						context.AddSourceObject(sourceObject);
					}
				}
			}

			return context;
		}

		public virtual void ApplyAbilityTagsToGameplayEffectSpec(GameplayEffectSpec spec, GameplayAbilitySpec abilitySpec)
		{
			GameplayTagContainer capturedSourceTags = spec.CapturedSourceTags.SpecTags;

			capturedSourceTags.AppendTags(AssetTags);

			if (abilitySpec != null)
			{
				capturedSourceTags.AppendTags(abilitySpec.DynamicSpecSourceTags);

				if (abilitySpec.SourceObject.TryGetTarget(out UnityEngine.Object sourceObject))
				{
					if (sourceObject is IGameplayTagAssetInterface sourceObjAsTagInterface)
					{
						GameplayTagContainer sourceObjTags = new();
						sourceObjAsTagInterface.GetOwnedGameplayTags(sourceObjTags);

						capturedSourceTags.AppendTags(sourceObjTags);
					}
				}

				spec.MergeSetByCallerMagnitude(abilitySpec.SetByCallerTagMagnitudes);
			}
		}

		public virtual void OnGiveAbility(in GameplayAbilityActorInfo actorInfo, in GameplayAbilitySpec spec)
		{
			SetCurrentActorInfo(spec.Handle, actorInfo);

			if (actorInfo != null && actorInfo.AvatarActor != null)
			{
				OnAvatarSet(actorInfo, spec);
			}
		}

		public virtual void OnAvatarSet(GameplayAbilityActorInfo actorInfo, GameplayAbilitySpec spec)
		{

		}

		public virtual void SetCurrentActorInfo(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
		{
			CurrentActorInfo = actorInfo;
			CurrentSpecHandle = handle;
		}

		public int GetAbilityLevel()
		{
			if (CurrentActorInfo == null)
			{
				return 1;
			}

			return GetAbilityLevel(CurrentSpecHandle, CurrentActorInfo);
		}

		public int GetAbilityLevel(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
		{
			if (actorInfo != null)
			{
				if (actorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
				{
					GameplayAbilitySpec spec = abilitySystemComponent.FindAbilitySpecFromHandle(handle);
					if (spec is not null)
					{
						return spec.Level;
					}
				}
			}

			Debug.LogWarning($"UameplayAbility::GetAbilityLevel. Invalid AbilitySpecHandle {handle} for Ability {this}. Returning level 1.");
			return 1;
		}

		// Called when the ability is removed from an AbilitySystemComponent
		public virtual void OnRemoveAbility(GameplayAbilityActorInfo actorInfo, GameplayAbilitySpec spec)
		{

		}

		public bool IsActive()
		{
			if (InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerActor)
			{
				return bIsActive;
			}

			if (InstancingPolicy == GameplayAbilityInstancingPolicy.NonInstanced)
			{
				Debug.LogWarning($"GameplayAbility::IsActive() called on {this} NonInstanced ability, call IsActive on the Ability Spec instead");
			}

			return this != null;
		}

		public AbilitySystemComponent GetAbilitySystemComponentFromActorInfo()
		{
			Debug.Assert(CurrentActorInfo != null);

			CurrentActorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent);
			return abilitySystemComponent;
		}

		[Obsolete("Use GetAbilitySystemComponentFromActorInfo_Ensured instead")]
		public AbilitySystemComponent GetAbilitySystemComponentFromActorInfo_Checked()
		{
			if (CurrentActorInfo == null)
			{
				return null;
			}

			CurrentActorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent);
			Debug.Assert(abilitySystemComponent != null);

			return abilitySystemComponent;
		}

		public AbilitySystemComponent GetAbilitySystemComponentFromActorInfo_Ensured()
		{
			Debug.Assert(CurrentActorInfo != null);

			CurrentActorInfo.AbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent);
			Debug.Assert(abilitySystemComponent != null);

			return abilitySystemComponent;
		}
	}
}