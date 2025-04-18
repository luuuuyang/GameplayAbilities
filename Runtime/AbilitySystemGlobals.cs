using UnityEngine;
using System.Collections.Generic;
using GameplayTags;

namespace GameplayAbilities
{
	public class AbilitySystemGlobals
	{
		public static AbilitySystemGlobals Instance
		{
			get
			{
				if (instance == null)
				{
					//ensure that only one thread can execute
					lock (typeof(AbilitySystemGlobals))
					{
						if (instance == null)
						{
							instance = new();
							// TODO
						}
					}
				}
				return instance;
			}
		}

		private static AbilitySystemGlobals instance;

		public bool IgnoreAbilitySystemCooldowns;
		public bool IgnoreAbilitySystemCosts;

		public bool ShouldIgnoreCooldowns => IgnoreAbilitySystemCooldowns;
		public bool ShouldIgnoreCosts => IgnoreAbilitySystemCosts;

		public GameplayTag ActivateFailTagsBlockedTag;
		public GameplayTag ActivateFailTagsMissingTag;
		public GameplayTag ActivateFailCooldownTag;
		public GameplayTag ActivateFailCostTag;

		public static AbilitySystemComponent GetAbilitySystemComponentFromActor(GameObject actor, bool lookForComponent = true)
		{
			if (actor == null)
			{
				return null;
			}

			if (lookForComponent)
			{
				return actor.GetComponent<AbilitySystemComponent>();
			}

			return null;
		}

		public bool ShouldAllowGameplayModEvaluationChannels()
		{
			return GameplayAbilitiesDeveloperSettings.GetOrCreateSettings().AllowGameplayModEvaluationChannels;
		}

		public bool IsGameplayModEvaluationChannelValid(GameplayModEvaluationChannel channel)
		{
			bool allowChannels = ShouldAllowGameplayModEvaluationChannels();
			return allowChannels ? !string.IsNullOrEmpty(GetGameplayModEvaluationChannelAliases(channel)) : channel == GameplayModEvaluationChannel.Channel0;
		}

		public string GetGameplayModEvaluationChannelAliases(GameplayModEvaluationChannel channel)
		{
			return GetGameplayModEvaluationChannelAliases((int)channel);
		}

		public string GetGameplayModEvaluationChannelAliases(int index)
		{
			GameplayAbilitiesDeveloperSettings developerSettings = GameplayAbilitiesDeveloperSettings.GetOrCreateSettings();
			Debug.Assert(index >= 0 && index < developerSettings.GameplayModEvaluationChannelAliases.Length);
			return developerSettings.GameplayModEvaluationChannelAliases[index];
		}

		public virtual void GlobalPreGameplayEffectSpecApply(GameplayEffectSpec spec, AbilitySystemComponent abilitySystemComponent)
		{

		}

		public virtual void SetCurrentAppliedGE(in GameplayEffectSpec spec)
		{

		}

		public virtual GameplayAbilityActorInfo AllocAbilityActorInfo()
		{
			return new GameplayAbilityActorInfo();
		}

		public virtual GameplayEffectContext AllocGameplayEffectContext()
		{
			return new GameplayEffectContext();
		}
	}
}