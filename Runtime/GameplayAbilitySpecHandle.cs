namespace GameplayAbilities
{
	public record GameplayAbilitySpecHandle
	{
		private int Handle = -1;

		private static int GHandle = 0;

		public bool IsValid()
		{
			return Handle != -1;
		}

		public void GenerateNewHandle()
		{
			Handle = GHandle++;
		}

		public override string ToString()
		{
			return IsValid() ? Handle.ToString() : "Invalid";
		}
	}
}