namespace GameplayAbilities
{
	public struct GameplayEffectSpecHandle
	{
		public GameplayEffectSpec Data;

		public bool IsValid()
		{
			return Data != null;
		}

		public GameplayEffectSpecHandle(GameplayEffectSpec other)
		{
			Data = other;
		}
	}
}