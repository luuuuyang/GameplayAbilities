using System;
using System.Collections.Generic;
using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace GameplayAbilities
{
	public abstract class GameplayEffectCalculation : ScriptableObject
	{
#if ODIN_INSPECTOR
		[FoldoutGroup("Attributes")]
#endif
		public List<GameplayEffectAttributeCaptureDefinition> RelevantAttributesToCapture = new();
	}
}