using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayAbilities
{
	[InlineProperty]
	[Serializable]
	public class ScalableFloat
	{
		[HorizontalGroup]
		[HideLabel]
		public float Value;

		[HorizontalGroup]
		[HideLabel]
		public AnimationCurve AnimationCurve;

		public ScalableFloat()
		{
			Value = 0;
			AnimationCurve = null;
		}

		public ScalableFloat(float initialValue)
		{
			Value = initialValue;
			AnimationCurve = null;
		}

		public float GetValueAtLevel(float level)
		{
			EvaluateCurveAtLevel(level, out float outFloat);
			return outFloat;
		}

		public bool EvaluateCurveAtLevel(float level, out float outFloat)
		{
			if (AnimationCurve != null && AnimationCurve.length > 0)
			{
				outFloat = Value * AnimationCurve.Evaluate(level);
				return true;
			}
			else
			{
				outFloat = Value;
				return false;
			}
		}

		public static bool operator ==(ScalableFloat a, ScalableFloat b)
		{
			return a.Value == b.Value && a.AnimationCurve == b.AnimationCurve;
		}

		public static bool operator !=(ScalableFloat a, ScalableFloat b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}
	}
}