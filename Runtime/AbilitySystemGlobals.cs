using UnityEngine;
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
					lock (typeof(AbilitySystemGlobals))
					{
						if (instance == null)
						{
							instance = new();
						}
					}
				}
				return instance;
			}
		}

		private static AbilitySystemGlobals instance;

		public bool IgnoreAbilitySystemCooldowns = true;
		public bool IgnoreAbilitySystemCosts = true;

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

		public bool ShouldUseTurnBasedTimerManager()
		{
			return GameplayAbilitiesDeveloperSettings.GetOrCreateSettings().UseTurnBasedTimerManager;
		}

		public bool IsGameplayEffectTimingTypeValid(GameplayEffectTimingType timingType)
		{
			return timingType == GameplayEffectTimingType.RealTime || (ShouldUseTurnBasedTimerManager() && timingType == GameplayEffectTimingType.TurnBased);
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