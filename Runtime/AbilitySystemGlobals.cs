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

		//# Whether the game should allow the usage of gameplay mod evaluation channels or not
		public bool AllowGameplayModEvaluationChannels;

		public List<GameplayModEvaluationChannel> GameplayModEvaluationChannelAliases;

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

		public bool IsGameplayModEvaluationChannelValid(GameplayModEvaluationChannel channel)
		{
			return AllowGameplayModEvaluationChannels ? GameplayModEvaluationChannelAliases.Contains(channel) : channel == GameplayModEvaluationChannel.Channel0;
		}

		public virtual void GlobalPreGameplayEffectSpecApply(GameplayEffectSpec spec, AbilitySystemComponent abilitySystemComponent)
		{

		}

		public virtual void SetCurrentAppliedGE(in GameplayEffectSpec spec)
		{

		}
	}
}