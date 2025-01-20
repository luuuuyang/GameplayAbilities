namespace GameplayAbilities
{
	public struct GameplayAbilitySpecHandle
	{
		public int Handle;

		public GameplayAbilitySpecHandle(int handle = GameplayEffectConstants.IndexNone)
		{
			Handle = handle;
		}

		public bool IsValid()
		{
			return Handle != GameplayEffectConstants.IndexNone;
		}

		private static int GHandle = 1;

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