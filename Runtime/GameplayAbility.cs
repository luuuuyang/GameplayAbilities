using GameplayTags;

namespace GameplayAbilities
{
	public delegate void OnGameplayAbilityCanceled();

	public delegate void OnGameplayAbilityEnded(GameplayAbility ability);

	public class GameplayAbility
	{
		public GameplayTagContainer CancelAbilitiesWithTags;
		public GameplayTagContainer BlockAbilitiesWithTags;
		public GameplayTagContainer ActivationOwnedTags;
		public GameplayTagContainer ActivationRequiredTags;
		public GameplayTagContainer ActivationBlockedTags;
		public GameplayTagContainer SourceRequiredTags;
		public GameplayTagContainer SourceBlockedTags;
		public GameplayTagContainer TargetRequiredTags;
		public GameplayTagContainer TargetBlockedTags;

		public GameplayTagContainer AbilityTags;

		public GameplayAbilityInstancingPolicy InstancingPolicy;
		public bool RetriggerInstancedAbility;

		public GameplayEffect Cost;
		public GameplayEffect Cooldown;

		public bool IsActive;
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

		public bool IsCancelable;

		public GameplayAbilityActorInfo CurrentActorInfo;
		public GameplayAbilitySpecHandle CurrentSpecHandle;

		public bool MarkPendingKillOnAbilityEnd;

		public event OnGameplayAbilityCanceled OnGameplayAbilityCanceled;
		public event OnGameplayAbilityEnded OnGameplayAbilityEnded;
		public event GameplayAbilityEndedDelegate OnGameplayAbilityEndedWithData;

		public virtual bool CanActivateAbility(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actor_info, in GameplayTagContainer source_tags, in GameplayTagContainer target_tags, out GameplayTagContainer optional_relevant_tags)
		{
			optional_relevant_tags = new GameplayTagContainer();
			return true;
		}

		public void CallActivateAbility(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actor_info, OnGameplayAbilityEnded on_gameplay_ability_ended_delegate, in GameplayEventData trigger_event_data)
		{
			PreActivate(handle, actor_info, on_gameplay_ability_ended_delegate);
			ActivateAbility(handle, actor_info, trigger_event_data);
		}

		// Do boilerplate init stuff and then call ActivateAbility
		public void PreActivate(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actor_info, OnGameplayAbilityEnded on_gameplay_ability_ended)
		{
			var comp = actor_info.AbilitySystemComponent;

			if (InstancingPolicy != GameplayAbilityInstancingPolicy.NonInstanced)
			{
				IsActive = true;
				IsBlockingOtherAbilities = true;
				IsCancelable = true;
			}

			SetCurrentActorInfo(handle, actor_info);

			comp.HandleChangeAbilityCanBeCanceled(AbilityTags, this, true);
			comp.AddLooseGameplayTags(ActivationOwnedTags);

			OnGameplayAbilityEnded += on_gameplay_ability_ended;

			comp.NotifyAbilityActivated(handle, this);
			comp.ApplyAbilityBlockAndCancelTags(AbilityTags, this, true, BlockAbilitiesWithTags, true, CancelAbilitiesWithTags);

			var spec = comp.FindAbilitySpecFromHandle(handle);
			spec.ActiveCount += 1;
		}

		public void ActivateAbility(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actor_info, in GameplayEventData trigger_event_data)
		{

		}

		public bool CommitAbiltiy(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info, GameplayEventData trigger_event_data)
		{
			if (!CommitCheck(handle, actor_info))
			{
				return false;
			}
			CommitExecute(handle, actor_info, trigger_event_data);
			actor_info.AbilitySystemComponent.NotifyAbilityCommit(this);
			return true;
		}

		public bool CommitCheck(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info)
		{
			if (!AbilitySystemGlobals.Instance.IgnoreAbilitySystemCooldowns && !CheckCooldown(handle, actor_info))
			{
				return false;
			}
			if (!AbilitySystemGlobals.Instance.IgnoreAbilitySystemCosts && !CheckCost(handle, actor_info))
			{
				return false;
			}
			return true;
		}

		public bool CheckCooldown(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info)
		{
			var cooldown_tags = GetCooldownTags();
			if (cooldown_tags.Count > 0)
			{
				var ability_system_component = actor_info.AbilitySystemComponent;
				if (ability_system_component != null && ability_system_component.HasAnyMatchingGameplayTags(cooldown_tags))
				{
					return false;
				}
			}
			return true;
		}

		public GameplayTagContainer GetCooldownTags()
		{
			return Cooldown.GrantedTags;
		}

		public bool CheckCost(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info)
		{
			var ability_system_component = actor_info.AbilitySystemComponent;
			if (ability_system_component != null && !ability_system_component.CanApplyAttributeModifiers(Cost, GetAbilityLevelForNonInstanced(handle, actor_info), MakeEffectContext(handle, actor_info)))
			{
				return false;
			}
			return true;
		}

		public GameplayTagContainer GetCostTags()
		{
			return Cost.GrantedTags;
		}

		public void CommitExecute(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info, GameplayEventData trigger_event_data)
		{
			ApplyCooldown(handle, actor_info);
			ApplyCost(handle, actor_info);
		}

		public bool CanBeCanceled()
		{
			return InstancingPolicy != GameplayAbilityInstancingPolicy.NonInstanced ? true : IsCancelable;
		}

		public virtual void CancelAbility(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info)
		{
			if (CanBeCanceled())
			{
				OnGameplayAbilityCanceled?.Invoke();
				EndAbility(handle, actor_info, true);
			}
		}

		public void EndAbility(GameplayAbilitySpecHandle handle, in GameplayAbilityActorInfo actor_info, bool was_cancelled)
		{
			if (IsEndAbilityValid(handle, actor_info))
			{
				if (InstancingPolicy != GameplayAbilityInstancingPolicy.NonInstanced)
				{
					IsAbilityEnding = true;
				}

				OnGameplayAbilityEnded?.Invoke(this);
				OnGameplayAbilityEnded = null;

				OnGameplayAbilityEndedWithData?.Invoke(new AbilityEndedData(this, handle, was_cancelled));
				OnGameplayAbilityEndedWithData = null;

				if (InstancingPolicy != GameplayAbilityInstancingPolicy.NonInstanced)
				{
					IsActive = false;
					IsAbilityEnding = false;
				}
				var ability_system_component = actor_info.AbilitySystemComponent;
				if (ability_system_component != null)
				{
					ability_system_component.RemoveLooseGameplayTags(ActivationOwnedTags);
					// TODO: extend variable overrides
					if (CanBeCanceled())
					{
						ability_system_component.HandleChangeAbilityCanBeCanceled(AbilityTags, this, false);
					}

					if (IsBlockingOtherAbilities)
					{
						ability_system_component.ApplyAbilityBlockAndCancelTags(AbilityTags, this, false, BlockAbilitiesWithTags, false, CancelAbilitiesWithTags);
					}

					ability_system_component.NotifyAbilityEnd(handle, this, was_cancelled);
				}
			}
		}

		public bool IsEndAbilityValid(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info)
		{
			var ability_comp = actor_info.AbilitySystemComponent;
			if (ability_comp == null)
			{
				return false;
			}
			var spec = ability_comp.FindAbilitySpecFromHandle(handle);
			var isSpecActive = (spec is not null ? spec.IsActive() : IsActive);
			if (!isSpecActive)
			{
				return false;
			}
			return true;
		}

		public void ApplyCost(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info)
		{
			ApplyGameplayEffectToOwner(handle, actor_info, Cost, GetAbilityLevelForNonInstanced(handle, actor_info));
		}

		public void ApplyCooldown(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info)
		{
			ApplyGameplayEffectToOwner(handle, actor_info, Cooldown, GetAbilityLevelForNonInstanced(handle, actor_info));
		}

		public void ApplyGameplayEffectToOwner(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info, GameplayEffect gameplay_effect, int gameplay_effect_level, int stacks = 1)
		{
			var spec_handle = MakeOutgoingGameplayEffectSpec(handle, actor_info, gameplay_effect, gameplay_effect_level);
			spec_handle.Data.StackCount = stacks;
			ApplyGameplayEffectSpecToOwner(handle, actor_info, spec_handle);
		}

		public void ApplyGameplayEffectSpecToOwner(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info, GameplayEffectSpecHandle spec_handle)
		{
			var ability_system_component = actor_info.AbilitySystemComponent;
			if (ability_system_component != null)
			{
				ability_system_component.ApplyGameplayEffectSpecToSelf(spec_handle.Data);
			}
		}

		public GameplayEffectSpecHandle MakeOutgoingGameplayEffectSpec(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info, GameplayEffect gameplay_effect, int level)
		{
			var ability_system_component = actor_info.AbilitySystemComponent;
			var new_handle = ability_system_component.MakeOutgoingSpec(gameplay_effect, level, MakeEffectContext(handle, actor_info));
			var ability_spec = ability_system_component.FindAbilitySpecFromHandle(handle);
			ApplyAbilityTagsToGameplayEffectSpec(new_handle.Data, ability_spec);
			return new_handle;
		}

		public GameplayEffectContextHandle MakeEffectContext(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info)
		{
			var context = new GameplayEffectContextHandle();
			context.AddInstigator(actor_info.OwnerActor, actor_info.AvatarActor);

			context.SetAbility(this);

			var ability_spec = actor_info.AbilitySystemComponent.FindAbilitySpecFromHandle(handle);
			if (ability_spec is not null)
			{
				context.AddSourceObject(ability_spec.SourceObject);
			}

			return context;
		}

		public void ApplyAbilityTagsToGameplayEffectSpec(GameplayEffectSpec spec, GameplayAbilitySpec ability_spec)
		{
			var captured_source_tags = spec.CapturedSourceTags.SpecTags;
			captured_source_tags.AppendTags(AbilityTags);
		}


		public void OnGiveAbility(GameplayAbilityActorInfo actor_info, GameplayAbilitySpec spec)
		{
			if (actor_info.AvatarActor != null)
			{
				OnAvatarSet(actor_info, spec);
			}
		}

		public void OnAvatarSet(GameplayAbilityActorInfo actor_info, GameplayAbilitySpec spec)
		{

		}

		public void SetCurrentActorInfo(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info)
		{
			CurrentActorInfo = actor_info;
			CurrentSpecHandle = handle;
		}

		public int GetAbilityLevel()
		{
			return GetAbilityLevelForNonInstanced(CurrentSpecHandle, CurrentActorInfo);
		}

		public int GetAbilityLevelForNonInstanced(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actor_info)
		{
			var ability_system_component = actor_info.AbilitySystemComponent;
			if (ability_system_component != null)
			{
				var spec = ability_system_component.FindAbilitySpecFromHandle(handle);
				if (spec is not null)
				{
					return spec.Level;
				}
			}
			return 1;
		}

		// Called when the ability is removed from an AbilitySystemComponent
		public void OnRemoveAbility(GameplayAbilityActorInfo actor_info, GameplayAbilitySpec spec)
		{

		}
	}
}