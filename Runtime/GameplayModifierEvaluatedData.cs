namespace GameplayAbilities
{
	public class GameplayModifierEvaluatedData
	{
		public GameplayAttribute Attribute;
		public GameplayModOp ModifierOp;
		public float Magnitude;

		public GameplayModifierEvaluatedData(GameplayAttribute attribute, GameplayModOp modifier_op, float magnitude)
		{
			Attribute = attribute;
			ModifierOp = modifier_op;
			Magnitude = magnitude;
		}
	}
}
