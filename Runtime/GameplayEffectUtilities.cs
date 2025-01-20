using UnityEngine;

namespace GameplayAbilities
{
	public static class GameplayEffectUtilities
	{
		private static readonly float[] modifier_op_biases = { 0, 1, 1, 0 };

		public static float GetModifierBiasByModifierOp(GameplayModOp mod_op)
		{
			return modifier_op_biases[(int)mod_op];
		}

		public static float ComputeStackedModifierMagnitude(float base_computed_magnitude, int stack_count, GameplayModOp mod_op)
		{
			float operation_bias = GetModifierBiasByModifierOp(mod_op);
			stack_count = Mathf.Clamp(stack_count, 0, int.MaxValue);
			var stack_mag = base_computed_magnitude;

			if (mod_op != GameplayModOp.Override)
			{
				stack_mag -= operation_bias;
				stack_mag *= stack_count;
				stack_mag += operation_bias;
			}

			return stack_mag;
		}
	}
}
