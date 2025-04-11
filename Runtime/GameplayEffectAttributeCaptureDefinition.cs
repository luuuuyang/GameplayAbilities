using System;
using System.Reflection;
using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace GameplayAbilities
{
	public enum GameplayEffectAttributeCaptureSource
	{
		Source,
		Target
	}

	[Serializable]
	public struct GameplayEffectAttributeCaptureDefinition
	{
		public GameplayAttribute AttributeToCapture;
		public GameplayEffectAttributeCaptureSource AttributeSource;
		public bool Snapshot;

		// public GameplayEffectAttributeCaptureDefinition()
		// {
		// 	AttributeToCapture = null;
		// 	AttributeSource = GameplayEffectAttributeCaptureSource.Source;
		// 	Snapshot = false;
		// }
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

		public static bool operator ==(GameplayEffectAttributeCaptureDefinition a, GameplayEffectAttributeCaptureDefinition b)
		{
			if (a.AttributeToCapture != b.AttributeToCapture)
			{
				return false;
			}
			if (a.AttributeSource != b.AttributeSource)
			{
				return false;
			}
			if (a.Snapshot != b.Snapshot)
			{
				return false;
			}
			return true;
		}

		public static bool operator !=(GameplayEffectAttributeCaptureDefinition a, GameplayEffectAttributeCaptureDefinition b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(AttributeToCapture, AttributeSource, Snapshot);
		}

		public override string ToString()
		{
			return $"Attribute: {AttributeToCapture}, Capture: {AttributeSource}, Snapshot: {Snapshot}";
		}
	}
}