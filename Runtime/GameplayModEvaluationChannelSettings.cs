using UnityEngine;

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
}
