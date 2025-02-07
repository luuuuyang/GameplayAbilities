namespace GameplayAbilities
{
	public struct GameplayAbilitySpecHandle
	{
		public int Handle;
		private static int GHandle = 1;

		public GameplayAbilitySpecHandle(int handle = GameplayEffectConstants.IndexNone)
		{
			Handle = handle;
		}

		public readonly bool IsValid()
		{
			return Handle != GameplayEffectConstants.IndexNone;
		}

		public void GenerateNewHandle()
		{
			Handle = GHandle++;
		}

		public static bool operator ==(GameplayAbilitySpecHandle a, GameplayAbilitySpecHandle b)
		{
			return a.Handle == b.Handle;
		}

		public static bool operator !=(GameplayAbilitySpecHandle a, GameplayAbilitySpecHandle b)
		{
			return a.Handle != b.Handle;
		}
	}
}