using GameplayTags;
using System;
using UnityEngine;

namespace GameplayAbilities
{
	public enum GameplayModOp
	{
		Additive,
		Multiply,
		Divide,
		Override,
		[HideInInspector]
		Max
	}

	[Serializable]
	public class GameplayModifierInfo
	{
		public GameplayAttribute Attribute = new();
		public GameplayModOp ModifierOp = GameplayModOp.Additive;
		public GameplayEffectModifierMagnitude ModifierMagnitude = new();
		public GameplayModEvaluationChannelSettings EvaluationChannelSettings;
		public GameplayTagRequirements SourceTags;
		public GameplayTagRequirements TargetTags;

		public static bool operator ==(GameplayModifierInfo a, GameplayModifierInfo b)
		{
			return a.Attribute == b.Attribute && a.ModifierOp == b.ModifierOp && a.ModifierMagnitude == b.ModifierMagnitude && a.EvaluationChannelSettings == b.EvaluationChannelSettings && a.SourceTags == b.SourceTags && a.TargetTags == b.TargetTags;
		}

		public static bool operator !=(GameplayModifierInfo a, GameplayModifierInfo b)
		{
			return !(a == b);
		}
	}
}