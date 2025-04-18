using System;
using System.Reflection;

namespace GameplayAbilities
{
	public enum GameplayEffectAttributeCaptureSource
	{
		Source,
		Target
	}

	[Serializable]
	public record GameplayEffectAttributeCaptureDefinition
	{
		public GameplayAttribute AttributeToCapture;
		public GameplayEffectAttributeCaptureSource AttributeSource;
		public bool Snapshot;

		public GameplayEffectAttributeCaptureDefinition()
		{
			AttributeToCapture = null;
			AttributeSource = GameplayEffectAttributeCaptureSource.Source;
			Snapshot = false;
		}

		public GameplayEffectAttributeCaptureDefinition(GameplayAttribute attribute, GameplayEffectAttributeCaptureSource source, bool snapshot)
		{
			AttributeToCapture = attribute;
			AttributeSource = source;
			Snapshot = snapshot;
		}

		public GameplayEffectAttributeCaptureDefinition(FieldInfo fieldInfo, GameplayEffectAttributeCaptureSource source, bool snapshot)
		{
			AttributeToCapture = new GameplayAttribute(fieldInfo);
			AttributeSource = source;
			Snapshot = snapshot;
		}

		public override string ToString()
		{
			return $"Attribute: {AttributeToCapture}, Capture: {AttributeSource}, Snapshot: {Snapshot}";
		}
	}
}