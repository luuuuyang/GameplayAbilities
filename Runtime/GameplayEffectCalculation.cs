using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameplayAbilities
{
	public abstract class GameplayEffectCalculation : ScriptableObject
	{
		[FoldoutGroup("Attributes")]
		[SerializeField]
		protected List<GameplayEffectAttributeCaptureDefinition> RelevantAttributesToCapture = new();

		public virtual List<GameplayEffectAttributeCaptureDefinition> AttributeCaptureDefinitions
		{
			get
			{
				return RelevantAttributesToCapture;
			}
		}
	}
}