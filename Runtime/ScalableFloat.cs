using System;
using UnityEngine;

namespace GameplayAbilities
{
	[Serializable]
	public struct ScalableFloat
	{
		public float Value;
		public AnimationCurve AnimationCurve;

		public ScalableFloat(float initialValue = 0)
		{
			Value = initialValue;
			AnimationCurve = null;
		}

		public readonly float GetValueAtLevel(float level)
		{
			EvaluateCurveAtLevel(level, out float outFloat);
			return outFloat;
		}

		public readonly bool EvaluateCurveAtLevel(float level, out float outFloat)
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
	}
}