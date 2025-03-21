using UnityEngine;
using GameplayTags;

namespace GameplayAbilities
{
	public class GameplayModMagnitudeCalculation : GameplayEffectCalculation
	{
		public virtual float CalculateBaseMagnitude_Implementation(in GameplayEffectSpec spec)
		{
			return 0f;
		}

		protected bool GetCapturedAttributeMagnitude(in GameplayEffectAttributeCaptureDefinition def, in GameplayEffectSpec spec, in AggregatorEvaluateParameters evaluationParameters, out float magnitude)
		{
			magnitude = 0;
			GameplayEffectAttributeCaptureSpec captureSpec = spec.CapturedRelevantAttributes.FindCaptureSpecByDefinition(def, true);

			if (captureSpec is null)
			{
				Debug.LogError($"GetCapturedAttributeMagnitude unable to get capture spec.");
				return false;
			}

			if (!captureSpec.AttemptCalculateAttributeMagnitude(evaluationParameters, ref magnitude))
			{
				Debug.LogError($"GetCapturedAttributeMagnitude unable to calculate attribute magnitude.");
				return false;
			}

			return true;
		}

		protected float GetCapturedAttributeMagnitude(in GameplayEffectSpec effectSpec, GameplayAttribute attribute, in GameplayTagContainer sourceTags, in GameplayTagContainer targetTags)
		{
			float magnitude = 0;

			foreach (GameplayEffectAttributeCaptureDefinition currentCapture in RelevantAttributesToCapture)
			{
				if (currentCapture.AttributeToCapture == attribute)
				{
                    AggregatorEvaluateParameters evaluationParameters = new()
                    {
                        SourceTags = sourceTags,
                        TargetTags = targetTags
                    };

                    GetCapturedAttributeMagnitude(currentCapture, effectSpec, evaluationParameters, out magnitude);

					break;
				}
			}

			return magnitude;
		}

		protected float GetSetByCallerMagnitudeByTag(in GameplayEffectSpec effectSpec, in GameplayTag tag)
		{	
			return effectSpec.GetSetByCallerMagnitude(tag, true, 0f);
		}

		protected float GetSetByCallerMagnitudeByName(in GameplayEffectSpec effectSpec, string magnitudeName)
		{
			return effectSpec.GetSetByCallerMagnitude(magnitudeName, true, 0f);
		}

		protected GameplayTagContainer GetSourceAggregatorTags(in GameplayEffectSpec effectSpec)
		{
			GameplayTagContainer tags = effectSpec.CapturedSourceTags.AggregatedTags;

			if (tags is not null)
			{
				return tags;
			}
			
			return new GameplayTagContainer();
		}

		protected GameplayTagContainer GetSourceActorTags(in GameplayEffectSpec effectSpec)
		{
			return effectSpec.CapturedSourceTags.ActorTags;
		}

		protected GameplayTagContainer GetSourceSpecTags(in GameplayEffectSpec effectSpec)
		{
			return effectSpec.CapturedSourceTags.SpecTags;
		}

		protected GameplayTagContainer GetTargetAggregatorTags(in GameplayEffectSpec effectSpec)
		{
			GameplayTagContainer tags = effectSpec.CapturedTargetTags.AggregatedTags;

			if (tags is not null)
			{
				return tags;
			}
			
			return new GameplayTagContainer();
		}

		protected GameplayTagContainer GetTargetActorTags(in GameplayEffectSpec effectSpec)
		{
			return effectSpec.CapturedTargetTags.ActorTags;
		}

		protected GameplayTagContainer GetTargetSpecTags(in GameplayEffectSpec effectSpec)
		{
			return effectSpec.CapturedTargetTags.SpecTags;
		}
	}
}
