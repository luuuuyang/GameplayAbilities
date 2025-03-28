using GameplayTags;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;


#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace GameplayAbilities
{
	using OnGameplayAttributeChange = UnityEvent<float, GameplayEffectModCallbackData>;

	using OnGameplayAttributeValueChange = UnityEvent<OnAttributeChangeData>;

	public enum GameplayEffectMagnitudeCalculation
	{
		ScalableFloat,
		AttributeBased,
		CustomCalculationClass,
		SetByCaller
	}

	public enum AttributeBasedFloatCalculationType
	{
		AttributeMagnitude,
		AttributeBaseValue,
		AttributeBonusMagnitude,
		AttributeMagnitudeEvaluatedUpToChannel
	}

	public static class GameplayEffectConstants
	{
		public const float InfiniteDuration = -1;
		public const float InstantApplication = 0;
		public const float NoPeriod = 0;
		public const float InvalidLevel = -1;
		public const int IndexNone = -1;
	}

	[Serializable]
	public struct AttributeBasedFloat
	{
		public ScalableFloat Coefficient;
		public ScalableFloat PreMultiplyAdditiveValue;
		public ScalableFloat PostMultiplyAdditiveValue;
		public GameplayEffectAttributeCaptureDefinition BackingAttribute;
		public AnimationCurve AttributeCurve;
		public AttributeBasedFloatCalculationType AttributeCalculationType;
		public GameplayModEvaluationChannel FinalChannel;
		public GameplayTagContainer SourceTagFilter;
		public GameplayTagContainer TargetTagFilter;

		public AttributeBasedFloat(float coefficient = 1, float preMultiplyAdditiveValue = 0, float postMultiplyAdditiveValue = 0, AttributeBasedFloatCalculationType attributeCalculationType = AttributeBasedFloatCalculationType.AttributeMagnitude, GameplayModEvaluationChannel finalChannel = GameplayModEvaluationChannel.Channel0)
		{
			Coefficient = new ScalableFloat(coefficient);
			PreMultiplyAdditiveValue = new ScalableFloat(preMultiplyAdditiveValue);
			PostMultiplyAdditiveValue = new ScalableFloat(postMultiplyAdditiveValue);
			BackingAttribute = new GameplayEffectAttributeCaptureDefinition();
			AttributeCurve = null;
			AttributeCalculationType = attributeCalculationType;
			FinalChannel = finalChannel;
			SourceTagFilter = new GameplayTagContainer();
			TargetTagFilter = new GameplayTagContainer();
		}

		public readonly float CalculateMagnitude(in GameplayEffectSpec relevantSpec)
		{
			GameplayEffectAttributeCaptureSpec captureSpec = relevantSpec.CapturedRelevantAttributes.FindCaptureSpecByDefinition(BackingAttribute, true);

			float attributeValue = 0;

			if (AttributeCalculationType == AttributeBasedFloatCalculationType.AttributeBaseValue)
			{
				captureSpec.AttemptCalculateAttributeBaseValue(ref attributeValue);
			}
			else
			{
				AggregatorEvaluateParameters evaluationParameters = new AggregatorEvaluateParameters();
				evaluationParameters.SourceTags = relevantSpec.CapturedSourceTags.AggregatedTags;
				evaluationParameters.TargetTags = relevantSpec.CapturedTargetTags.AggregatedTags;
				evaluationParameters.AppliedSourceTagFilter = SourceTagFilter;
				evaluationParameters.AppliedTargetTagFilter = TargetTagFilter;

				if (AttributeCalculationType == AttributeBasedFloatCalculationType.AttributeMagnitude)
				{
					captureSpec.AttemptCalculateAttributeMagnitude(evaluationParameters, ref attributeValue);
				}
				else if (AttributeCalculationType == AttributeBasedFloatCalculationType.AttributeBonusMagnitude)
				{
					captureSpec.AttemptCalculateAttributeBonusMagnitude(evaluationParameters, ref attributeValue);
				}
				else if (AttributeCalculationType == AttributeBasedFloatCalculationType.AttributeMagnitudeEvaluatedUpToChannel)
				{
					bool requestingValidChannel = AbilitySystemGlobals.Instance.IsGameplayModEvaluationChannelValid(FinalChannel);
					GameplayModEvaluationChannel channelToUse = requestingValidChannel ? FinalChannel : GameplayModEvaluationChannel.Channel0;
					captureSpec.AttemptCalculateAttributeMagnitudeEvaluatedUpToChannel(evaluationParameters, channelToUse, ref attributeValue);
				}
			}

			if (AttributeCurve != null)
			{
				attributeValue = AttributeCurve.Evaluate(attributeValue);
			}

			float specLevel = relevantSpec.Level;
			return Coefficient.GetValueAtLevel(specLevel) * (attributeValue + PreMultiplyAdditiveValue.GetValueAtLevel(specLevel)) + PostMultiplyAdditiveValue.GetValueAtLevel(specLevel);
		}

		public static bool operator ==(AttributeBasedFloat a, AttributeBasedFloat b)
		{
			if (a.Coefficient != b.Coefficient ||
				a.PreMultiplyAdditiveValue != b.PreMultiplyAdditiveValue ||
				a.PostMultiplyAdditiveValue != b.PostMultiplyAdditiveValue ||
				a.BackingAttribute != b.BackingAttribute ||
				a.AttributeCurve != b.AttributeCurve ||
				a.AttributeCalculationType != b.AttributeCalculationType ||
				a.FinalChannel != b.FinalChannel)
			{
				return false;
			}

			if (a.SourceTagFilter.Count != b.SourceTagFilter.Count ||
				!a.SourceTagFilter.HasAllExact(b.SourceTagFilter))
			{
				return false;
			}

			if (a.TargetTagFilter.Count != b.TargetTagFilter.Count ||
				!a.TargetTagFilter.HasAllExact(b.TargetTagFilter))
			{
				return false;
			}

			return true;
		}

		public static bool operator !=(AttributeBasedFloat a, AttributeBasedFloat b)
		{
			return !(a == b);
		}
	}

	[Serializable]
	public class CustomCalculationBasedFloat
	{
		public GameplayModMagnitudeCalculation CalculationClassMagnitude;
		public ScalableFloat Coefficient;
		public ScalableFloat PreMultiplyAdditiveValue;
		public ScalableFloat PostMultiplyAdditiveValue;
		public AnimationCurve FinalLookupCurve;

		public CustomCalculationBasedFloat()
		{
			Coefficient = new ScalableFloat(1);
			PreMultiplyAdditiveValue = new ScalableFloat(0);
			PostMultiplyAdditiveValue = new ScalableFloat(0);
		}

		public float CalculateMagnitude(in GameplayEffectSpec relevantSpec)
		{
			GameplayModMagnitudeCalculation calcCDO = CalculationClassMagnitude;

			float customBaseValue = calcCDO.CalculateBaseMagnitude_Implementation(relevantSpec);

			float specLvl = relevantSpec.Level;

			float finalValue = Coefficient.GetValueAtLevel(specLvl) * (customBaseValue + PreMultiplyAdditiveValue.GetValueAtLevel(specLvl)) + PostMultiplyAdditiveValue.GetValueAtLevel(specLvl);
			if (FinalLookupCurve != null)
			{
				finalValue = FinalLookupCurve.Evaluate(finalValue);
			}

			return finalValue;
		}

		public static bool operator ==(CustomCalculationBasedFloat a, CustomCalculationBasedFloat b)
		{
			return a.CalculationClassMagnitude == b.CalculationClassMagnitude && a.Coefficient == b.Coefficient && a.PreMultiplyAdditiveValue == b.PreMultiplyAdditiveValue && a.PostMultiplyAdditiveValue == b.PostMultiplyAdditiveValue;
		}

		public static bool operator !=(CustomCalculationBasedFloat a, CustomCalculationBasedFloat b)
		{
			return !(a == b);
		}
	}

	[Serializable]
	public struct SetByCallerFloat
	{
		public string DataName;
		public GameplayTag DataTag;
	}

	[Serializable]
	public class GameplayEffectModifierMagnitude
	{
		public GameplayEffectMagnitudeCalculation MagnitudeCalculationType;

#if ODIN_INSPECTOR
		[ShowIf("MagnitudeCalculationType", GameplayEffectMagnitudeCalculation.ScalableFloat)]
#endif
		public ScalableFloat ScalableFloatMagnitude;

#if ODIN_INSPECTOR
		[ShowIf("MagnitudeCalculationType", GameplayEffectMagnitudeCalculation.AttributeBased)]
#endif
		public AttributeBasedFloat AttributeBasedMagnitude;

#if ODIN_INSPECTOR
		[ShowIf("MagnitudeCalculationType", GameplayEffectMagnitudeCalculation.CustomCalculationClass)]
#endif
		public CustomCalculationBasedFloat CustomMagnitude;

#if ODIN_INSPECTOR
		[ShowIf("MagnitudeCalculationType", GameplayEffectMagnitudeCalculation.SetByCaller)]
#endif
		public SetByCallerFloat SetByCallerMagnitude;

		public GameplayEffectModifierMagnitude()
		{
			MagnitudeCalculationType = GameplayEffectMagnitudeCalculation.ScalableFloat;
		}

		public GameplayEffectModifierMagnitude(in ScalableFloat value)
		{
			MagnitudeCalculationType = GameplayEffectMagnitudeCalculation.ScalableFloat;
			ScalableFloatMagnitude = value;
		}

		public GameplayEffectModifierMagnitude(in AttributeBasedFloat value)
		{
			MagnitudeCalculationType = GameplayEffectMagnitudeCalculation.AttributeBased;
			AttributeBasedMagnitude = value;
		}

		public GameplayEffectModifierMagnitude(in SetByCallerFloat value)
		{
			MagnitudeCalculationType = GameplayEffectMagnitudeCalculation.SetByCaller;
			SetByCallerMagnitude = value;
		}

		public static bool operator ==(GameplayEffectModifierMagnitude a, GameplayEffectModifierMagnitude b)
		{
			if (a.MagnitudeCalculationType != b.MagnitudeCalculationType)
			{
				return false;
			}

			switch (a.MagnitudeCalculationType)
			{
				case GameplayEffectMagnitudeCalculation.ScalableFloat:
					if (a.ScalableFloatMagnitude != b.ScalableFloatMagnitude)
					{
						return false;
					}
					break;
				case GameplayEffectMagnitudeCalculation.AttributeBased:
					if (a.AttributeBasedMagnitude != b.AttributeBasedMagnitude)
					{
						return false;
					}
					break;
				case GameplayEffectMagnitudeCalculation.CustomCalculationClass:
					if (a.CustomMagnitude != b.CustomMagnitude)
					{
						return false;
					}
					break;
				case GameplayEffectMagnitudeCalculation.SetByCaller:
					if (a.SetByCallerMagnitude.DataName != b.SetByCallerMagnitude.DataName)
					{
						return false;
					}
					break;
			}

			return true;
		}

		public static bool operator !=(GameplayEffectModifierMagnitude a, GameplayEffectModifierMagnitude b)
		{
			return !(a == b);
		}

		public bool CanCalculateMagnitude(in GameplayEffectSpec relevantSpec)
		{
			List<GameplayEffectAttributeCaptureDefinition> reqCaptureDefs = new();

			GetAttributeCaptureDefinitions(reqCaptureDefs);

			return relevantSpec.HasValidCapturedAttributes(reqCaptureDefs);
		}

		public bool AttemptCalculateMagnitude(in GameplayEffectSpec relevantSpec, ref float calculatedMagnitude, bool warnIfSetByCallerFail = true, float defaultSetByCaller = 0)
		{
			bool canCalc = CanCalculateMagnitude(relevantSpec);
			if (canCalc)
			{
				switch (MagnitudeCalculationType)
				{
					case GameplayEffectMagnitudeCalculation.ScalableFloat:
						calculatedMagnitude = ScalableFloatMagnitude.GetValueAtLevel(relevantSpec.Level);
						break;
					case GameplayEffectMagnitudeCalculation.AttributeBased:
						calculatedMagnitude = AttributeBasedMagnitude.CalculateMagnitude(relevantSpec);
						break;
					case GameplayEffectMagnitudeCalculation.CustomCalculationClass:
						calculatedMagnitude = CustomMagnitude.CalculateMagnitude(relevantSpec);
						break;
					case GameplayEffectMagnitudeCalculation.SetByCaller:
						if (SetByCallerMagnitude.DataTag.IsValid())
						{
							calculatedMagnitude = relevantSpec.GetSetByCallerMagnitude(SetByCallerMagnitude.DataTag, warnIfSetByCallerFail, defaultSetByCaller);
						}
						else
						{
							calculatedMagnitude = relevantSpec.GetSetByCallerMagnitude(SetByCallerMagnitude.DataName, warnIfSetByCallerFail, defaultSetByCaller);
						}
						break;
					default:
						Debug.LogError($"Unknown MagnitudeCalculationType {MagnitudeCalculationType} in AttemptCalculateMagnitude");
						calculatedMagnitude = 0;
						break;
				}
			}
			else
			{
				calculatedMagnitude = 0;
			}

			return canCalc;
		}

		public bool AttemptRecalculateMagnitudeFromDependentAggregatorChange(in GameplayEffectSpec relevantSpec, ref float calculatedMagnitude, in Aggregator changedAggregator)
		{
			List<GameplayEffectAttributeCaptureDefinition> reqCaptureDefs = new();

			GetAttributeCaptureDefinitions(reqCaptureDefs);

			foreach (GameplayEffectAttributeCaptureDefinition captureDef in reqCaptureDefs)
			{
				if (captureDef.Snapshot == false)
				{
					GameplayEffectAttributeCaptureSpec captureSpec = relevantSpec.CapturedRelevantAttributes.FindCaptureSpecByDefinition(captureDef, true);
					if (captureSpec != null && captureSpec.ShouldRefreshLinkedAggregator(changedAggregator))
					{
						return AttemptCalculateMagnitude(relevantSpec, ref calculatedMagnitude);
					}
				}
			}

			return false;
		}

		public void GetAttributeCaptureDefinitions(List<GameplayEffectAttributeCaptureDefinition> captureDefs)
		{
			captureDefs.Clear();
			switch (MagnitudeCalculationType)
			{
				case GameplayEffectMagnitudeCalculation.AttributeBased:
					captureDefs.Add(AttributeBasedMagnitude.BackingAttribute);
					break;
				case GameplayEffectMagnitudeCalculation.CustomCalculationClass:
					// captureDefs.Add(CustomMagnitude.CalculationClassMagnitude.CaptureAttribute);
					break;
			}
		}

		public bool GetStaticMagnitudeIfPossible(float level, out float magnitude, in string contextString = null)
		{
			magnitude = 0;

			if (MagnitudeCalculationType == GameplayEffectMagnitudeCalculation.ScalableFloat)
			{
				magnitude = ScalableFloatMagnitude.GetValueAtLevel(level);
				return true;
			}

			return false;
		}
	}

	public enum GameplayEffectScopedModifierAggregatorType
	{
		CapturedAttributeBacked,
		Transient
	}

	[Serializable]
	public class GameplayEffectExecutionScopedModifierInfo
	{
		public GameplayEffectAttributeCaptureDefinition CaptureAttribute;
		[HideInInspector]
		public GameplayTag TransientAggregatorIdentifier;
		[HideInInspector]
		public GameplayEffectScopedModifierAggregatorType AggregatorType;
		public GameplayModOp ModifierOp;
		public GameplayEffectModifierMagnitude ModifierMagnitude;
		public GameplayModEvaluationChannelSettings EvaluationChannelSettings;
		public GameplayTagRequirements SourceTags;
		public GameplayTagRequirements TargetTags;

		public GameplayEffectExecutionScopedModifierInfo()
		{
			AggregatorType = GameplayEffectScopedModifierAggregatorType.CapturedAttributeBacked;
			ModifierOp = GameplayModOp.Additive;
		}

		public GameplayEffectExecutionScopedModifierInfo(in GameplayEffectAttributeCaptureDefinition captureDef)
		{
			CaptureAttribute = captureDef;
			AggregatorType = GameplayEffectScopedModifierAggregatorType.CapturedAttributeBacked;
			ModifierOp = GameplayModOp.Additive;
		}

		public GameplayEffectExecutionScopedModifierInfo(in GameplayTag transientAggregatorIdentifier)
		{
			TransientAggregatorIdentifier = transientAggregatorIdentifier;
			AggregatorType = GameplayEffectScopedModifierAggregatorType.Transient;
			ModifierOp = GameplayModOp.Additive;
		}
	}

	[Serializable]
	public struct ConditionalGameplayEffect
	{
		public GameplayEffect Effect;
		public GameplayTagContainer RequiredSourceTags;

		public readonly bool CanApply(in GameplayTagContainer sourceTags, float sourceLevel)
		{
			return sourceTags.HasAll(RequiredSourceTags);
		}

		public readonly GameplayEffectSpecHandle CreateSpec(GameplayEffectContextHandle effectContext, float sourceLevel)
		{
			return Effect != null ? new GameplayEffectSpecHandle(new GameplayEffectSpec(Effect, effectContext, sourceLevel)) : new GameplayEffectSpecHandle();
		}

		public static bool operator ==(ConditionalGameplayEffect a, ConditionalGameplayEffect b)
		{
			return a.Effect == b.Effect && a.RequiredSourceTags == b.RequiredSourceTags;
		}

		public static bool operator !=(ConditionalGameplayEffect a, ConditionalGameplayEffect b)
		{
			return !(a == b);
		}
	}

	[Serializable]
	public struct GameplayEffectExecutionDefinition
	{
		public GameplayEffectExecutionCalculation CalculationClass;

#if ODIN_INSPECTOR
		[ShowIf("@CalculationClass != null && CalculationClass.RequiresPassedInTags")]
#endif
		public GameplayTagContainer PassedInTags;

#if ODIN_INSPECTOR
		[ShowIf("@CalculationClass != null && (CalculationClass.RelevantAttributesToCapture.Count > 0 || CalculationClass.InvalidScopedModifierAttributes.Count > 0 || CalculationClass.ValidTransientAggregatorIdentifiers.Count > 0)")]
#endif
		public List<GameplayEffectExecutionScopedModifierInfo> CalculationModifiers;

		public List<ConditionalGameplayEffect> ConditionalGameplayEffects;

		public readonly void GetAttributeCaptureDefinitions(List<GameplayEffectAttributeCaptureDefinition> captureDefs)
		{
			captureDefs.Clear();

			foreach (GameplayEffectExecutionScopedModifierInfo curScopedMod in CalculationModifiers)
			{
				List<GameplayEffectAttributeCaptureDefinition> scopedModMagDefs = new();
				curScopedMod.ModifierMagnitude.GetAttributeCaptureDefinitions(scopedModMagDefs);

				captureDefs.AddRange(scopedModMagDefs);
			}
		}
	}

	[Serializable]
	public class GameplayModifierInfo
	{
#if ODIN_INSPECTOR
		[HideLabel]
#endif
		public GameplayAttribute Attribute = new();
		public GameplayModOp ModifierOp = GameplayModOp.Additive;
		public GameplayEffectModifierMagnitude ModifierMagnitude = new();
		public GameplayModEvaluationChannelSettings EvaluationChannelSettings;
		public GameplayTagRequirements SourceTags;
		public GameplayTagRequirements TargetTags;

		public static bool operator ==(GameplayModifierInfo a, GameplayModifierInfo b)
		{
			return a.Attribute == b.Attribute && a.ModifierOp == b.ModifierOp && a.ModifierMagnitude == b.ModifierMagnitude && a.EvaluationChannelSettings == b.EvaluationChannelSettings && a.SourceTags == b.SourceTags && a.TargetTags == b.TargetTags;
		}

		public static bool operator !=(GameplayModifierInfo a, GameplayModifierInfo b)
		{
			return !(a == b);
		}
	}

	[Serializable]
	public class InheritedTagContainer
	{
#if ODIN_INSPECTOR
		[ReadOnly]
#endif
		public GameplayTagContainer CombinedTags = new();

		public GameplayTagContainer Added = new();

		public GameplayTagContainer Removed = new();

		public void UpdateInheritedTagProperties(in InheritedTagContainer parent)
		{
			CombinedTags.Reset();

			if (parent is not null)
			{
				foreach (GameplayTag tag in parent.CombinedTags)
				{
					if (!tag.MatchesAny(Removed))
					{
						CombinedTags.AddTag(tag);
					}
				}
			}

			foreach (GameplayTag tag in Added)
			{
				if (!Removed.HasTagExact(tag))
				{
					CombinedTags.AddTag(tag);
				}
			}
		}

		public void ApplyTo(GameplayTagContainer applyToContainer)
		{
			if (applyToContainer.IsEmpty() && !CombinedTags.IsEmpty())
			{
				applyToContainer.CopyFrom(CombinedTags);
			}
			else
			{
				GameplayTagContainer removesThatApply = Removed.Filter(applyToContainer);
				GameplayTagContainer removeOverridesAdd = Added.FilterExact(Removed);
				removesThatApply.AppendTags(removeOverridesAdd);

				applyToContainer.AppendTags(Added);
				applyToContainer.RemoveTags(removesThatApply);
			}
		}

		public void AddTag(in GameplayTag tagToAdd)
		{
			CombinedTags.AddTag(tagToAdd);
		}

		public void RemoveTag(in GameplayTag tagToRemove)
		{
			CombinedTags.RemoveTag(tagToRemove);
		}

		public static bool operator ==(InheritedTagContainer a, InheritedTagContainer b)
		{
			return a.CombinedTags == b.CombinedTags && a.Added == b.Added && a.Removed == b.Removed;
		}

		public static bool operator !=(InheritedTagContainer a, InheritedTagContainer b)
		{
			return !(a == b);
		}
	}

	public enum GameplayEffectDurationType
	{
		Instant,
		Infinite,
		HasDuration,
		/*HasTurn*/
	}

	public enum GameplayEffectStackingDurationPolicy
	{
		RefreshOnSuccessfulApplication,
		NeverRefresh
	}

	public enum GameplayEffectStackingPeriodPolicy
	{
		ResetOnSuccessfulApplication,
		NeverReset
	}

	public enum GameplayEffectStackingExpirationPolicy
	{
		ClearEntireStack,
		RemoveSingleStackAndRefreshDuration,
		RefreshDuration
	}

	public enum GameplayEffectPeriodInhibitionRemovedPolicy
	{
		NeverReset,
		ResetPeriod,
		ExecuteAndResetPeriod
	}

	public class ModifierSpec
	{
		public float EvaluatedMagnitude;
	}

	public class GameplayEffectModifiedAttribute : ICloneable
	{
		public GameplayAttribute Attribute;
		public float TotalMagnitude;

		public GameplayEffectModifiedAttribute()
		{
			TotalMagnitude = 0;
		}

		public GameplayEffectModifiedAttribute(GameplayAttribute attribute, float totalMagnitude)
		{
			Attribute = attribute;
			TotalMagnitude = totalMagnitude;
		}

		public object Clone()
		{
			return new GameplayEffectModifiedAttribute(Attribute, TotalMagnitude);
		}
	}

	public class GameplayEffectAttributeCaptureSpec : ICloneable
	{
		public GameplayEffectAttributeCaptureDefinition BackingDefinition;
		public Aggregator AttributeAggregator;

		public GameplayEffectAttributeCaptureSpec(GameplayEffectAttributeCaptureDefinition definition)
		{
			BackingDefinition = definition;
			AttributeAggregator = new Aggregator();
		}

		public object Clone()
		{
			return new GameplayEffectAttributeCaptureSpec(BackingDefinition);
		}

		public bool HasValidCapture()
		{
			return AttributeAggregator is not null;
		}

		public bool AttemptCalculateAttributeBaseValue(ref float base_value)
		{
			if (AttributeAggregator is not null)
			{
				base_value = AttributeAggregator.BaseValue;
				return true;
			}
			return false;
		}

		public bool AttemptCalculateAttributeMagnitude(AggregatorEvaluateParameters eval_params, ref float magnitude)
		{
			if (AttributeAggregator is not null)
			{
				magnitude = AttributeAggregator.Evaluate(eval_params);
				return true;
			}
			return false;
		}

		public bool AttemptCalculateAttributeBonusMagnitude(AggregatorEvaluateParameters evalParams, ref float magnitude)
		{
			if (AttributeAggregator is not null)
			{
				magnitude = AttributeAggregator.EvaluateBonus(evalParams);
				return true;
			}
			return false;
		}

		public bool AttemptCalculateAttributeMagnitudeEvaluatedUpToChannel(AggregatorEvaluateParameters evalParams, GameplayModEvaluationChannel finalChannel, ref float magnitude)
		{
			if (AttributeAggregator is not null)
			{
				magnitude = AttributeAggregator.EvaluateToChannel(evalParams, finalChannel);
				return true;
			}

			return false;
		}

		public bool ShouldRefreshLinkedAggregator(Aggregator changed_aggregator)
		{
			return BackingDefinition.Snapshot == false && (changed_aggregator is null || changed_aggregator == AttributeAggregator);
		}

		public void RegisterLinkedAggregatorCallbacks(ActiveGameplayEffectHandle handle)
		{
			if (BackingDefinition.Snapshot == false)
			{
				AttributeAggregator.AddDependent(handle);
			}
		}

		public void UnregisterLinkedAggregatorCallbacks(ActiveGameplayEffectHandle handle)
		{
			AttributeAggregator.RemoveDependent(handle);
		}

		public bool AttemptAddAggregatorModsToAggregator(Aggregator aggregatorToAddTo)
		{
			aggregatorToAddTo.AddModsFrom(AttributeAggregator);
			return true;
		}
	}

	public class GameplayEffectAttributeCaptureSpecContainer
	{
		private List<GameplayEffectAttributeCaptureSpec> SourceAttributes;
		private List<GameplayEffectAttributeCaptureSpec> TargetAttributes;
		private bool HasNonSnapshottedAttributes;

		public GameplayEffectAttributeCaptureSpecContainer()
		{
			SourceAttributes = new();
			TargetAttributes = new();
			HasNonSnapshottedAttributes = false;
		}

		public GameplayEffectAttributeCaptureSpecContainer(in GameplayEffectAttributeCaptureSpecContainer other)
		{
			SourceAttributes = other.SourceAttributes.DeepCopy();
			TargetAttributes = other.TargetAttributes.DeepCopy();
			HasNonSnapshottedAttributes = other.HasNonSnapshottedAttributes;
		}

		public void CopyFrom(in GameplayEffectAttributeCaptureSpecContainer other)
		{
			SourceAttributes = other.SourceAttributes.DeepCopy();
			TargetAttributes = other.TargetAttributes.DeepCopy();
			HasNonSnapshottedAttributes = other.HasNonSnapshottedAttributes;
		}

		public GameplayEffectAttributeCaptureSpec FindCaptureSpecByDefinition(GameplayEffectAttributeCaptureDefinition definition, bool only_include_valid_capture)
		{
			bool source_attribute = definition.AttributeSource == GameplayEffectAttributeCaptureSource.Source;
			List<GameplayEffectAttributeCaptureSpec> attribute_array = source_attribute ? SourceAttributes : TargetAttributes;

			GameplayEffectAttributeCaptureSpec matching_spec = attribute_array.Find((element) => element.BackingDefinition == definition);

			if (matching_spec is not null && only_include_valid_capture && !matching_spec.HasValidCapture())
			{
				matching_spec = null;
			}

			return matching_spec;
		}

		public bool HasValidCapturedAttributes(in List<GameplayEffectAttributeCaptureDefinition> capture_defs_to_check)
		{
			bool has_valid = true;

			foreach (GameplayEffectAttributeCaptureDefinition cur_def in capture_defs_to_check)
			{
				GameplayEffectAttributeCaptureSpec capture_spec = FindCaptureSpecByDefinition(cur_def, true);
				if (capture_spec is null)
				{
					has_valid = false;
					break;
				}
			}

			return has_valid;
		}

		public void CaptureAttributes(AbilitySystemComponent ability_system_component, GameplayEffectAttributeCaptureSource capture_source)
		{
			if (ability_system_component != null)
			{
				bool source_component = capture_source == GameplayEffectAttributeCaptureSource.Source;
				List<GameplayEffectAttributeCaptureSpec> attribute_array = source_component ? SourceAttributes : TargetAttributes;
				foreach (GameplayEffectAttributeCaptureSpec cur_capture_spec in attribute_array)
				{
					ability_system_component.CaptureAttributeForGameplayEffect(cur_capture_spec);
				}
			}
		}

		public void AddCaptureDefinition(GameplayEffectAttributeCaptureDefinition capture_definition)
		{
			bool source_attribute = capture_definition.AttributeSource == GameplayEffectAttributeCaptureSource.Source;
			List<GameplayEffectAttributeCaptureSpec> attribute_array = source_attribute ? SourceAttributes : TargetAttributes;
			if (!attribute_array.Exists((element) => element.BackingDefinition == capture_definition))
			{
				attribute_array.Add(new GameplayEffectAttributeCaptureSpec(capture_definition));
			}
		}

		public void RegisterLinkedAggregatorCallbacks(ActiveGameplayEffectHandle handle)
		{
			foreach (GameplayEffectAttributeCaptureSpec captureSpec in SourceAttributes)
			{
				captureSpec.RegisterLinkedAggregatorCallbacks(handle);
			}

			foreach (GameplayEffectAttributeCaptureSpec captureSpec in TargetAttributes)
			{
				captureSpec.RegisterLinkedAggregatorCallbacks(handle);
			}
		}

		public void UnregisterLinkedAggregatorCallbacks(ActiveGameplayEffectHandle handle)
		{
			foreach (GameplayEffectAttributeCaptureSpec captureSpec in SourceAttributes)
			{
				captureSpec.UnregisterLinkedAggregatorCallbacks(handle);
			}

			foreach (GameplayEffectAttributeCaptureSpec captureSpec in TargetAttributes)
			{
				captureSpec.UnregisterLinkedAggregatorCallbacks(handle);
			}
		}
	}

	public class GameplayEffectSpec
	{
		public GameplayEffect Def;
		public List<GameplayEffectModifiedAttribute> ModifiedAttributes = new();
		public GameplayEffectAttributeCaptureSpecContainer CapturedRelevantAttributes = new();
		public List<GameplayEffectSpecHandle> TargetEffectSpecs = new();
		public float Duration;
		public float Period;
		public TagContainerAggregator CapturedSourceTags = new();
		public TagContainerAggregator CapturedTargetTags = new();
		public GameplayTagContainer DynamicGrantedTags = new();
		public GameplayTagContainer DynamicAssetTags = new();
		public List<ModifierSpec> Modifiers = new();
		public int StackCount;
		public bool CompletedSourceAttributeCapture;
		public bool CompletedTargetAttributeCapture;
		public bool DurationLocked;
		public List<GameplayAbilitySpecDef> GrantedAbilitySpecs = new();
		public Dictionary<string, float> SetByCallerNameMagnitudes = new();
		public Dictionary<GameplayTag, float> SetByCallerTagMagnitudes = new();
		public float Level;
		public GameplayEffectContextHandle EffectContext;

		public GameplayEffectSpec()
		{
			Def = null;
			Duration = GameplayEffectConstants.InstantApplication;
			Period = GameplayEffectConstants.NoPeriod;
			StackCount = 1;
			DurationLocked = false;
			Level = GameplayEffectConstants.InvalidLevel;
		}

		public GameplayEffectSpec(in GameplayEffect def, in GameplayEffectContextHandle effectContext, float level = GameplayEffectConstants.InvalidLevel)
		{
			Def = def;
			Duration = GameplayEffectConstants.InstantApplication;
			Period = GameplayEffectConstants.NoPeriod;
			StackCount = 1;
			DurationLocked = false;

			Initialize(def, effectContext, level);
		}

		public GameplayEffectSpec(in GameplayEffectSpec other)
		{
			Def = other.Def;
			ModifiedAttributes = other.ModifiedAttributes.DeepCopy();
			CapturedRelevantAttributes.CopyFrom(other.CapturedRelevantAttributes);
			TargetEffectSpecs = other.TargetEffectSpecs.DeepCopy();
			Duration = other.Duration;
			Period = other.Period;
			CapturedSourceTags.CopyFrom(other.CapturedSourceTags);
			CapturedTargetTags.CopyFrom(other.CapturedTargetTags);
			DynamicGrantedTags.CopyFrom(other.DynamicGrantedTags);
			DynamicAssetTags.CopyFrom(other.DynamicAssetTags);
			Modifiers = other.Modifiers;
			StackCount = other.StackCount;
			CompletedSourceAttributeCapture = other.CompletedSourceAttributeCapture;
			CompletedTargetAttributeCapture = other.CompletedTargetAttributeCapture;
			DurationLocked = other.DurationLocked;
			GrantedAbilitySpecs = other.GrantedAbilitySpecs;
			SetByCallerNameMagnitudes = new Dictionary<string, float>(other.SetByCallerNameMagnitudes);
			SetByCallerTagMagnitudes = new Dictionary<GameplayTag, float>(other.SetByCallerTagMagnitudes);
			EffectContext = other.EffectContext;
			Level = other.Level;
		}

		public void Initialize(in GameplayEffect def, in GameplayEffectContextHandle effectContext, float level)
		{
			Def = def;

			Level = level;
			SetContext(effectContext);
			SetLevel(level);

			Modifiers.Capacity = Def.Modifiers.Count;
			for (int i = 0; i < Def.Modifiers.Count; i++)
			{
				Modifiers.Add(new ModifierSpec());
			}

			SetupAttributeCaptureDefinitions();

			CapturedSourceTags.SpecTags.AppendTags(Def.AssetTags);

			CaptureDataFromSource();
		}

		public void InitializeFromLinkedSpec(in GameplayEffect def, in GameplayEffectSpec originalSpec)
		{
			GameplayEffectContextHandle expiringSpecContextHandle = originalSpec.EffectContext;
			GameplayEffectContextHandle newContextHandle = expiringSpecContextHandle.Duplicate();

			CapturedSourceTags.CopyFrom(originalSpec.CapturedSourceTags);
			CapturedSourceTags.SpecTags.RemoveTags(originalSpec.Def.AssetTags);

			Initialize(def, newContextHandle, originalSpec.Level);

			CopySetByCallerMagnitudes(originalSpec);
		}

		public void CopySetByCallerMagnitudes(in GameplayEffectSpec originalSpec)
		{
			SetByCallerNameMagnitudes = originalSpec.SetByCallerNameMagnitudes;
			SetByCallerTagMagnitudes = originalSpec.SetByCallerTagMagnitudes;
		}

		public void MergeSetByCallerMagnitude(in Dictionary<GameplayTag, float> magnitudes)
		{
			foreach (KeyValuePair<GameplayTag, float> pair in magnitudes)
			{
				SetByCallerTagMagnitudes.TryAdd(pair.Key, pair.Value);
			}
		}

		public void SetupAttributeCaptureDefinitions()
		{
			List<GameplayEffectAttributeCaptureDefinition> captureDefs = new();

			{
				captureDefs.Reset();
				Def.DurationMagnitude.GetAttributeCaptureDefinitions(captureDefs);
				foreach (GameplayEffectAttributeCaptureDefinition curDurationCaptureDef in captureDefs)
				{
					CapturedRelevantAttributes.AddCaptureDefinition(curDurationCaptureDef);
				}
			}

			for (int i = 0; i < Modifiers.Count; i++)
			{
				GameplayModifierInfo modDef = Def.Modifiers[i];
				ModifierSpec modSpec = Modifiers[i];

				captureDefs.Reset();
				modDef.ModifierMagnitude.GetAttributeCaptureDefinitions(captureDefs);

				foreach (GameplayEffectAttributeCaptureDefinition curCaptureDef in captureDefs)
				{
					CapturedRelevantAttributes.AddCaptureDefinition(curCaptureDef);
				}
			}

			foreach (GameplayEffectExecutionDefinition execCalc in Def.Executions)
			{
				captureDefs.Reset();
				execCalc.GetAttributeCaptureDefinitions(captureDefs);
				foreach (GameplayEffectAttributeCaptureDefinition curExecCaptureDef in captureDefs)
				{
					CapturedRelevantAttributes.AddCaptureDefinition(curExecCaptureDef);
				}
			}
		}

		public void SetContext(GameplayEffectContextHandle newEffectContext, bool skipRecaptureSourceActorTags = false)
		{
			var wasAlreadyInit = EffectContext.IsValid;
			EffectContext = newEffectContext;
			if (wasAlreadyInit)
			{
				CaptureDataFromSource(skipRecaptureSourceActorTags);
			}
		}

		public void GetAllGrantedTags(GameplayTagContainer container)
		{
			container.AppendTags(DynamicGrantedTags);
			if (Def != null)
			{
				container.AppendTags(Def.GrantedTags);
			}
		}

		public void GetAllAssetTags(GameplayTagContainer container)
		{
			container.AppendTags(DynamicAssetTags);
			if (Def != null)
			{
				container.AppendTags(Def.AssetTags);
			}
		}

		public void SetByCallerMagnitude(string dataName, float magnitude)
		{
			if (!string.IsNullOrEmpty(dataName))
			{
				SetByCallerNameMagnitudes.TryAdd(dataName, magnitude);
			}
		}

		public void SetLevel(float level)
		{
			Level = level;
			if (Def != null)
			{
				float defCalcDuration = 0;
				if (AttemptCalculateDurationFromDef(ref defCalcDuration))
				{
					SetDuration(defCalcDuration, false);
				}

				Period = Def.Period.GetValueAtLevel(level);
			}
		}

		public void SetDuration(float new_duration, bool lock_duration)
		{
			if (!DurationLocked)
			{
				Duration = new_duration;
				DurationLocked = lock_duration;
			}
		}

		public void CaptureDataFromSource(bool skip_recapture_source_actor_tags = false)
		{
			if (!skip_recapture_source_actor_tags)
			{
				RecaptureSourceActorTags();
			}

			CapturedRelevantAttributes.CaptureAttributes(EffectContext.InstigatorAbilitySystemComponent, GameplayEffectAttributeCaptureSource.Source);

			float defCalcDuration = 0;
			if (AttemptCalculateDurationFromDef(ref defCalcDuration))
			{
				SetDuration(defCalcDuration, false);
			}

			CompletedSourceAttributeCapture = true;
		}

		public void RecaptureSourceActorTags()
		{
			CapturedSourceTags.ActorTags.Reset();
			EffectContext.GetOwnedGameplayTags(CapturedSourceTags.ActorTags, CapturedSourceTags.SpecTags);
		}

		public GameplayEffectModifiedAttribute AddModifiedAttribute(in GameplayAttribute attribute)
		{
			GameplayEffectModifiedAttribute modified_attribute = new GameplayEffectModifiedAttribute
			{
				Attribute = attribute
			};
			ModifiedAttributes.Add(modified_attribute);
			return modified_attribute;
		}

		public GameplayEffectModifiedAttribute GetModifiedAttribute(in GameplayAttribute attribute)
		{
			foreach (GameplayEffectModifiedAttribute modified_attribute in ModifiedAttributes)
			{
				if (modified_attribute.Attribute == attribute)
				{
					return modified_attribute;
				}
			}
			return null;
		}

		public float GetModifierMagnitude(int modifierIdx, bool factorInStackCount)
		{
			Debug.Assert(Modifiers.IsValidIndex(modifierIdx) && Def.Modifiers.IsValidIndex(modifierIdx));

			float singleEvaluatedMagnitude = Modifiers[modifierIdx].EvaluatedMagnitude;

			float modMagnitude = singleEvaluatedMagnitude;
			if (factorInStackCount)
			{
				modMagnitude = GameplayEffectUtilities.ComputeStackedModifierMagnitude(singleEvaluatedMagnitude, StackCount, Def.Modifiers[modifierIdx].ModifierOp);
			}

			return modMagnitude;
		}

		public void CalculateModifierMagnitudes()
		{
			for (int modIdx = 0; modIdx < Modifiers.Count; modIdx++)
			{
				GameplayModifierInfo modDef = Def.Modifiers[modIdx];
				ModifierSpec modSpec = Modifiers[modIdx];

				if (modDef.ModifierMagnitude.AttemptCalculateMagnitude(this, ref modSpec.EvaluatedMagnitude) == false)
				{
					modSpec.EvaluatedMagnitude = 0;
					Debug.LogWarning($"Modifier on spec: {this} was asked to CalculateMagnitude and failed, falling back to 0.");
				}
			}
		}

		public float CalculateModifiedDuration()
		{
			Aggregator durationAgg = new();

			GameplayEffectAttributeCaptureSpec outgoingCaptureSpec = CapturedRelevantAttributes.FindCaptureSpecByDefinition(AbilitySystemComponent.OutgoingDurationCapture.Value, true);
			if (outgoingCaptureSpec != null)
			{
				outgoingCaptureSpec.AttemptAddAggregatorModsToAggregator(durationAgg);
			}

			GameplayEffectAttributeCaptureSpec incomingCaptureSpec = CapturedRelevantAttributes.FindCaptureSpecByDefinition(AbilitySystemComponent.IncomingDurationCapture.Value, true);
			if (incomingCaptureSpec != null)
			{
				incomingCaptureSpec.AttemptAddAggregatorModsToAggregator(durationAgg);
			}

			AggregatorEvaluateParameters @params = new()
			{
				SourceTags = CapturedSourceTags.AggregatedTags,
				TargetTags = CapturedTargetTags.AggregatedTags
			};

			return durationAgg.EvaluateWithBase(Duration, @params);
		}

		public void CaptureAttributeDataFromTarget(AbilitySystemComponent target_ability_system_component)
		{
			CapturedRelevantAttributes.CaptureAttributes(target_ability_system_component, GameplayEffectAttributeCaptureSource.Target);
		}

		public bool AttemptCalculateDurationFromDef(ref float defDuration)
		{
			bool calculatedDuration = true;

			GameplayEffectDurationType durType = Def.DurationPolicy;
			if (durType == GameplayEffectDurationType.Infinite)
			{
				defDuration = GameplayEffectConstants.InfiniteDuration;
			}
			else if (durType == GameplayEffectDurationType.Instant)
			{
				defDuration = GameplayEffectConstants.InstantApplication;
			}
			else
			{
				calculatedDuration = Def.DurationMagnitude.AttemptCalculateMagnitude(this, ref defDuration, false, 1);
			}

			return calculatedDuration;
		}

		public bool HasValidCapturedAttributes(in List<GameplayEffectAttributeCaptureDefinition> captureDefsToCheck)
		{
			return CapturedRelevantAttributes.HasValidCapturedAttributes(captureDefsToCheck);
		}

		public void SetSetByCallerMagnitude(string dataName, float magnitude)
		{
			if (!string.IsNullOrEmpty(dataName))
			{
				SetByCallerNameMagnitudes.TryAdd(dataName, magnitude);
			}
		}

		public void SetSetByCallerMagnitude(GameplayTag dataTag, float magnitude)
		{
			if (dataTag.IsValid())
			{
				SetByCallerTagMagnitudes.TryAdd(dataTag, magnitude);
			}
		}

		public float GetSetByCallerMagnitude(string dataName, bool warnIfNotFound, float defaultIfNotFound)
		{
			float magnitude = defaultIfNotFound;

			if (!string.IsNullOrEmpty(dataName))
			{
				if (!SetByCallerNameMagnitudes.TryGetValue(dataName, out magnitude))
				{
					if (warnIfNotFound)
					{
						Debug.LogError($"GameplayEffectSpec::GetMagnitude called for Data {dataName} on Def {Def} when magnitude had not yet been set by caller.");
					}
				}
			}

			return magnitude;
		}

		public float GetSetByCallerMagnitude(GameplayTag dataTag, bool warnIfNotFound, float defaultIfNotFound)
		{
			float magnitude = defaultIfNotFound;

			if (dataTag.IsValid())
			{
				if (!SetByCallerTagMagnitudes.TryGetValue(dataTag, out magnitude))
				{
					if (warnIfNotFound)
					{
						Debug.LogError($"GameplayEffectSpec::GetMagnitude called for Data {dataTag} on Def {Def} when magnitude had not yet been set by caller.");
					}
				}
			}

			return magnitude;
		}
	}

	public class ActiveGameplayEffect
	{
		public ActiveGameplayEffectHandle Handle;
		public GameplayEffectSpec Spec;
		public List<GameplayAbilitySpecHandle> GrantedAbilityHandles;
		public bool IsInhibited;
		public float StartWorldTime;
		public bool IsPendingRemove;
		public TimerHandle PeriodHandle;
		public TimerHandle DurationHandle;
		public ActiveGameplayEffectEvents EventSet = new();

		public float Duration => Spec.Duration;

		public float Period => Spec.Period;

		public float EndTime => Duration == GameplayEffectConstants.InfiniteDuration ? -1 : Duration + StartWorldTime;

		public ActiveGameplayEffect(ActiveGameplayEffectHandle handle, in GameplayEffectSpec spec)
		{
			Handle = handle;
			Spec = spec;
		}

		public float GetTimeRemaining(float worldTime)
		{
			return Duration == GameplayEffectConstants.InfiniteDuration ? -1 : Duration - (worldTime - StartWorldTime);
		}
	}

	public delegate bool ActiveGameplayEffectQueryCustomMatch(in ActiveGameplayEffect effect);

	[Serializable]
	public class GameplayEffectQuery
	{
		public GameplayTagQuery OwningTagQuery = new();
		public GameplayTagQuery EffectTagQuery = new();
		public GameplayTagQuery SourceTagQuery = new();
		public GameplayTagQuery SourceAggregateTagQuery = new();
		public GameplayAttribute ModifyingAttribute = new();
		public UnityEngine.Object EffectSource;
		public GameplayEffect EffectDefinition;
		public ActiveGameplayEffectQueryCustomMatch CustomMatchDelegate;
		public List<ActiveGameplayEffectHandle> IgnoreHandles = new();

		public bool IsEmpty()
		{
			return
				OwningTagQuery.IsEmpty() &&
				EffectTagQuery.IsEmpty() &&
				SourceAggregateTagQuery.IsEmpty() &&
				SourceTagQuery.IsEmpty() &&
				!ModifyingAttribute.IsValid() &&
				EffectSource == null &&
				EffectDefinition == null;
		}

		public bool Matches(in ActiveGameplayEffect effect)
		{
			if (IgnoreHandles.Contains(effect.Handle))
			{
				return false;
			}

			if (CustomMatchDelegate != null)
			{
				if (!CustomMatchDelegate(effect))
				{
					return false;
				}
			}

			return Matches(effect.Spec);
		}

		public bool Matches(in GameplayEffectSpec spec)
		{
			if (spec.Def == null)
			{
				return false;
			}

			if (OwningTagQuery.IsEmpty() == false)
			{
				GameplayTagContainer targetTags = new();
				targetTags.Reset();

				spec.GetAllAssetTags(targetTags);
				spec.GetAllGrantedTags(targetTags);

				if (OwningTagQuery.Matches(targetTags) == false)
				{
					return false;
				}
			}

			if (EffectTagQuery.IsEmpty() == false)
			{
				GameplayTagContainer GETags = new();
				GETags.Reset();

				spec.GetAllAssetTags(GETags);

				if (EffectTagQuery.Matches(GETags) == false)
				{
					return false;
				}
			}

			if (SourceAggregateTagQuery.IsEmpty() == false)
			{
				GameplayTagContainer sourceAggregateTags = spec.CapturedSourceTags.AggregatedTags;
				if (SourceAggregateTagQuery.Matches(sourceAggregateTags) == false)
				{
					return false;
				}
			}

			if (SourceTagQuery.IsEmpty() == false)
			{
				GameplayTagContainer sourceSpecTags = spec.CapturedSourceTags.SpecTags;
				if (SourceTagQuery.Matches(sourceSpecTags) == false)
				{
					return false;
				}
			}

			if (ModifyingAttribute.IsValid())
			{
				bool effectModifiesThisAttribute = false;

				for (int i = 0; i < spec.Modifiers.Count; i++)
				{
					GameplayModifierInfo modDef = spec.Def.Modifiers[i];
					ModifierSpec modSpec = spec.Modifiers[i];

					if (modDef.Attribute == ModifyingAttribute)
					{
						effectModifiesThisAttribute = true;
						break;
					}
				}

				if (!effectModifiesThisAttribute)
				{
					return false;
				}
			}

			if (EffectSource != null)
			{
				if (spec.EffectContext.SourceObject != EffectSource)
				{
					return false;
				}
			}

			if (EffectDefinition != null)
			{
				if (spec.Def != EffectDefinition)
				{
					return false;
				}
			}

			return true;
		}

		public static GameplayEffectQuery MakeQuery_MatchAnyOwningTags(GameplayTagContainer tags)
		{
			GameplayEffectQuery outQuery = new()
			{
				OwningTagQuery = GameplayTagQuery.MakeQuery_MatchAnyTags(tags)
			};
			return outQuery;
		}

		public static GameplayEffectQuery MakeQuery_MatchAllOwningTags(GameplayTagContainer tags)
		{
			GameplayEffectQuery outQuery = new()
			{
				OwningTagQuery = GameplayTagQuery.MakeQuery_MatchAllTags(tags)
			};
			return outQuery;
		}

		public static GameplayEffectQuery MakeQuery_MatchNoOwningTags(GameplayTagContainer tags)
		{
			GameplayEffectQuery outQuery = new()
			{
				OwningTagQuery = GameplayTagQuery.MakeQuery_MatchNoTags(tags)
			};
			return outQuery;
		}

		public static GameplayEffectQuery MakeQuery_MatchAnyEffectTags(GameplayTagContainer tags)
		{
			GameplayEffectQuery outQuery = new()
			{
				EffectTagQuery = GameplayTagQuery.MakeQuery_MatchAnyTags(tags)
			};
			return outQuery;
		}

		public static GameplayEffectQuery MakeQuery_MatchAllEffectTags(GameplayTagContainer tags)
		{
			GameplayEffectQuery outQuery = new()
			{
				EffectTagQuery = GameplayTagQuery.MakeQuery_MatchAllTags(tags)
			};
			return outQuery;
		}

		public static GameplayEffectQuery MakeQuery_MatchNoEffectTags(GameplayTagContainer tags)
		{
			GameplayEffectQuery outQuery = new()
			{
				EffectTagQuery = GameplayTagQuery.MakeQuery_MatchNoTags(tags)
			};
			return outQuery;
		}

		public static GameplayEffectQuery MakeQuery_MatchAnySourceSpecTags(GameplayTagContainer tags)
		{
			GameplayEffectQuery outQuery = new()
			{
				SourceTagQuery = GameplayTagQuery.MakeQuery_MatchAnyTags(tags)
			};
			return outQuery;
		}

		public static GameplayEffectQuery MakeQuery_MatchAllSourceSpecTags(GameplayTagContainer tags)
		{
			GameplayEffectQuery outQuery = new()
			{
				SourceTagQuery = GameplayTagQuery.MakeQuery_MatchAllTags(tags)
			};
			return outQuery;
		}

		public static GameplayEffectQuery MakeQuery_MatchNoSourceSpecTags(GameplayTagContainer tags)
		{
			GameplayEffectQuery outQuery = new()
			{
				SourceTagQuery = GameplayTagQuery.MakeQuery_MatchNoTags(tags)
			};
			return outQuery;
		}
	}

	public class ActiveGameplayEffectsContainer
	{
		public AbilitySystemComponent Owner;
		public OnGivenActiveGameplayEffectRemoved OnActiveGameplayEffectRemovedDelegate;
		private List<ActiveGameplayEffect> GameplayEffects_Internal = new();
		public Dictionary<GameplayAttribute, Aggregator> AttributeAggregatorMap = new();

		[Obsolete("use AttributeValueChangeDelegates")]
		private Dictionary<GameplayAttribute, OnGameplayAttributeChange> AttributeChangeDelegates = new();
		private Dictionary<GameplayAttribute, OnGameplayAttributeValueChange> AttributeValueChangeDelegates = new();

		private struct DebugExecutedGameplayEffectData
		{
			public string GameplayEffectName;
			public string ActivationState;
			public GameplayAttribute Attribute;
			public GameplayModOp ModifierOp;
			public float Magnitude;
			public int StackCount;
		}

		private List<DebugExecutedGameplayEffectData> DebugExecutedGameplayEffects = new();

		private GameplayEffectModCallbackData CurrentModCallbackData;

		public IEnumerator<ActiveGameplayEffect> GetEnumerator()
		{
			return GameplayEffects_Internal.GetEnumerator();
		}

		public void RegisterWithOwner(AbilitySystemComponent owner)
		{
			if (Owner != owner && owner != null)
			{
				Owner = owner;
			}
		}

		public ActiveGameplayEffect ApplyGameplayEffectSpec(in GameplayEffectSpec spec, ref bool foundExistingStackableGE)
		{
			if (!spec.Def)
			{
				Debug.LogWarning($"Tried to apply GE with no Def (context == {spec.EffectContext})");
				return null;
			}

			foundExistingStackableGE = false;

			ActiveGameplayEffect appliedActiveGE = null;
			ActiveGameplayEffect existingStackableGE = FindStackableActiveGameplayEffect(spec);

			bool setDuration = true;
			bool setPeriod = true;
			int startingStackCount = 0;
			int newStackCount = 0;

			if (existingStackableGE is not null)
			{
				foundExistingStackableGE = true;

				GameplayEffectSpec existingSpec = existingStackableGE.Spec;
				startingStackCount = existingSpec.StackCount;

				AbilitySystemGlobals.Instance.SetCurrentAppliedGE(existingSpec);

				if (existingSpec.StackCount == existingSpec.Def.StackLimitCount)
				{
					if (!HandleActiveGameplayEffectStackOverflow(existingStackableGE, existingSpec, spec))
					{
						return null;
					}
				}

				newStackCount = existingSpec.StackCount + spec.StackCount;
				if (existingSpec.Def.StackLimitCount > 0)
				{
					newStackCount = Mathf.Min(newStackCount, existingSpec.Def.StackLimitCount);
				}

				existingSpec.CapturedRelevantAttributes.UnregisterLinkedAggregatorCallbacks(existingStackableGE.Handle);

				existingStackableGE.Spec.StackCount = newStackCount;

				appliedActiveGE = existingStackableGE;

				GameplayEffect geDef = existingSpec.Def;

				if (geDef.StackDurationRefreshPolicy == GameplayEffectStackingDurationPolicy.NeverRefresh)
				{
					setDuration = false;
				}
				else
				{
					RestartActiveGameplayEffectDuration(existingStackableGE);
				}

				if (geDef.StackPeriodResetPolicy == GameplayEffectStackingPeriodPolicy.NeverReset)
				{
					setPeriod = false;
				}
			}
			else
			{
				ActiveGameplayEffectHandle newHandle = ActiveGameplayEffectHandle.GenerateNewHandle(Owner);

				appliedActiveGE = new ActiveGameplayEffect(newHandle, spec);

				GameplayEffects_Internal.Add(appliedActiveGE);
			}

			AbilitySystemGlobals.Instance.SetCurrentAppliedGE(appliedActiveGE.Spec);

			GameplayEffectSpec appliedEffectSpec = appliedActiveGE.Spec;
			AbilitySystemGlobals.Instance.GlobalPreGameplayEffectSpecApply(appliedEffectSpec, Owner);

			appliedEffectSpec.CapturedTargetTags.
			ActorTags.Reset();
			Owner.GetOwnedGameplayTags(appliedEffectSpec.CapturedTargetTags.ActorTags);

			appliedEffectSpec.CaptureAttributeDataFromTarget(Owner);
			appliedEffectSpec.CalculateModifierMagnitudes();

			{
				bool hasModifiedAttributes = appliedEffectSpec.ModifiedAttributes.Count > 0;
				bool hasDurationAndNoPeriod = appliedEffectSpec.Def.DurationPolicy == GameplayEffectDurationType.HasDuration
					&& appliedEffectSpec.Period == GameplayEffectConstants.NoPeriod;
				bool hasPeriodAndNoDuration = appliedEffectSpec.Def.DurationPolicy == GameplayEffectDurationType.Instant
					&& appliedEffectSpec.Period > GameplayEffectConstants.NoPeriod;
				bool shouldBuildModifiedAttributeList = !hasModifiedAttributes && (hasDurationAndNoPeriod || hasPeriodAndNoDuration);
				if (shouldBuildModifiedAttributeList)
				{
					int modifierIndex = -1;
					foreach (GameplayModifierInfo mod in appliedEffectSpec.Def.Modifiers)
					{
						modifierIndex++;

						float magnitude = 0;
						if (appliedEffectSpec.Modifiers.IsValidIndex(modifierIndex))
						{
							ModifierSpec modSpec = appliedEffectSpec.Modifiers[modifierIndex];
							magnitude = modSpec.EvaluatedMagnitude;
						}

						GameplayEffectModifiedAttribute modifiedAttribute = appliedEffectSpec.GetModifiedAttribute(mod.Attribute);
						if (modifiedAttribute is null)
						{
							modifiedAttribute = appliedEffectSpec.AddModifiedAttribute(mod.Attribute);
						}
						modifiedAttribute.TotalMagnitude += magnitude;
					}
				}
			}

			appliedEffectSpec.CapturedRelevantAttributes.RegisterLinkedAggregatorCallbacks(appliedActiveGE.Handle);

			float defCalcDuration = 0;
			if (appliedEffectSpec.AttemptCalculateDurationFromDef(ref defCalcDuration))
			{
				appliedEffectSpec.SetDuration(defCalcDuration, false);
			}
			else if (appliedEffectSpec.Def.DurationMagnitude.MagnitudeCalculationType == GameplayEffectMagnitudeCalculation.SetByCaller)
			{
				appliedEffectSpec.Def.DurationMagnitude.AttemptCalculateMagnitude(appliedEffectSpec, ref defCalcDuration);
				appliedEffectSpec.Duration = defCalcDuration;
			}

			float durationBaseValue = appliedEffectSpec.Duration;
			if (durationBaseValue > 0)
			{
				float finalDuration = appliedEffectSpec.CalculateModifiedDuration();

				if (finalDuration <= 0)
				{
					Debug.LogError($"GameplayEffect {appliedEffectSpec.Def} Duration was modified to {finalDuration}. Clamping to 0.1s duration.");
					finalDuration = 0.1f;
				}

				appliedEffectSpec.SetDuration(finalDuration, true);

				if (Owner && setDuration)
				{
					TimerDelegate @delegate = () =>
					{
						Owner.CheckDurationExpired(appliedActiveGE.Handle);
					};
					TimerManager.Instance.SetTimer(ref appliedActiveGE.DurationHandle, @delegate, finalDuration, false);
					if (!appliedActiveGE.DurationHandle.IsValid())
					{
						Debug.LogWarning($"Invalid Duration Handle after attempting to set duration for GE {appliedActiveGE} @ {finalDuration}");
						TimerManager.Instance.SetTimerForNextTick(@delegate);
					}
				}
			}

			if (setPeriod && Owner && appliedActiveGE.Period > GameplayEffectConstants.NoPeriod)
			{
				TimerDelegate @delegate = () =>
				{
					Owner.ExecutePeriodicEffect(appliedActiveGE.Handle);
				};

				if (appliedEffectSpec.Def.ExecutePeriodicEffectOnApplication)
				{
					TimerManager.Instance.SetTimerForNextTick(@delegate);
				}

				TimerManager.Instance.SetTimer(ref appliedActiveGE.PeriodHandle, @delegate, appliedEffectSpec.Period, true);
			}

			if (existingStackableGE != null)
			{
				OnStackCountChange(existingStackableGE, startingStackCount, newStackCount);
			}
			else
			{
				InternalOnActiveGameplayEffectAdded(appliedActiveGE);
			}

			return appliedActiveGE;
		}

		private void InternalOnActiveGameplayEffectAdded(ActiveGameplayEffect effect)
		{
			GameplayEffect effectDef = effect.Spec.Def;

			if (effectDef == null)
			{
				Debug.LogError("ActiveGameplayEffectsContainer serialized new GameplayEffect with NULL Def!");
				return;
			}

			Debug.Log($"Added {effectDef}");

			AddCustomMagnitudeExternalDependecies(effect);

			bool active = effectDef.OnAddedToActiveContainer(this, effect);

			effect.IsInhibited = true;
			Owner.InhibitActiveGameplayEffect(effect.Handle, !active);
		}

		public bool HandleActiveGameplayEffectStackOverflow(in ActiveGameplayEffect activeStackableGE, in GameplayEffectSpec oldSpec, in GameplayEffectSpec overflowingSpec)
		{
			GameplayEffect stackedGE = oldSpec.Def;
			bool allowOverflowApplication = !stackedGE.DenyOverflowApplication;

			foreach (GameplayEffect overflowEffect in stackedGE.OverflowEffects)
			{
				GameplayEffectSpec newGESpec = new();
				newGESpec.InitializeFromLinkedSpec(overflowEffect, overflowingSpec);
				Owner.ApplyGameplayEffectSpecToSelf(newGESpec);
			}

			if (!allowOverflowApplication && stackedGE.ClearStackOnOverflow)
			{
				Owner.RemoveActiveGameplayEffect(activeStackableGE.Handle);
			}

			return allowOverflowApplication;
		}

		public int GetNumGameplayEffects()
		{
			return GameplayEffects_Internal.Count;
		}

		public void ExecutePeriodicGameplayEffect(ActiveGameplayEffectHandle handle)
		{
			ActiveGameplayEffect activeEffect = GetActiveGameplayEffect(handle);
			if (activeEffect != null)
			{
				InternalExecutePeriodicGameplayEffect(activeEffect);
			}
		}

		public ActiveGameplayEffect GetActiveGameplayEffect(int index)
		{
			return GameplayEffects_Internal[index];
		}

		public bool RemoveActiveGameplayEffect(ActiveGameplayEffectHandle handle, int stacksToRemove = -1)
		{
			int numGameplayEffects = GetNumGameplayEffects();

			for (int i = 0; i < numGameplayEffects; i++)
			{
				ActiveGameplayEffect effect = GetActiveGameplayEffect(i);
				if (effect.Handle == handle)
				{
					InternalRemoveActiveGameplayEffect(i, stacksToRemove, true);
					return true;
				}
			}

			return false;
		}

		private void InternalExecutePeriodicGameplayEffect(ActiveGameplayEffect activeEffect)
		{
			if (!activeEffect.IsInhibited)
			{
				activeEffect.Spec.ModifiedAttributes.Clear();

				ExecuteActiveEffectsFrom(activeEffect.Spec);

				AbilitySystemComponent sourceASC = activeEffect.Spec.EffectContext.InstigatorAbilitySystemComponent;
				Owner.OnPeriodicGameplayEffectExecutedOnSelf(sourceASC, activeEffect.Spec, activeEffect.Handle);
				if (sourceASC != null)
				{
					sourceASC.OnPeriodicGameplayEffectExecutedOnTarget(Owner, activeEffect.Spec, activeEffect.Handle);
				}
			}
		}

		public bool InternalRemoveActiveGameplayEffect(int index, int stacksToRemove, bool prematureRemoval)
		{
			if (index < GetNumGameplayEffects())
			{
				ActiveGameplayEffect effect = GetActiveGameplayEffect(index);
				if (effect.IsPendingRemove)
				{
					return true;
				}

				Debug.Log($"Removing: {effect}. NumToRemove: {stacksToRemove}");
				foreach (GameplayModifierInfo modifier in effect.Spec.Def.Modifiers)
				{
					float magnitude = 0;
					modifier.ModifierMagnitude.AttemptCalculateMagnitude(effect.Spec, ref magnitude);
					Debug.Log($"{modifier.Attribute} {modifier.ModifierOp} {magnitude}");
				}

				GameplayEffectRemovalInfo gameplayEffectRemovalInfo;
				gameplayEffectRemovalInfo.ActiveEffect = effect;
				gameplayEffectRemovalInfo.StackCount = effect.Spec.StackCount;
				gameplayEffectRemovalInfo.PrematureRemoval = prematureRemoval;
				gameplayEffectRemovalInfo.EffectContext = effect.Spec.EffectContext;

				if (stacksToRemove > 0 && effect.Spec.StackCount > stacksToRemove)
				{
					int startingStackCount = effect.Spec.StackCount;
					effect.Spec.StackCount = startingStackCount - stacksToRemove;
					OnStackCountChange(effect, startingStackCount, effect.Spec.StackCount);
					return false;
				}

				InternalOnActiveGameplayEffectRemoved(effect, gameplayEffectRemovalInfo);

				if (effect.DurationHandle.IsValid())
				{
					TimerManager.Instance.ClearTimer(effect.DurationHandle);
				}
				if (effect.PeriodHandle.IsValid())
				{
					TimerManager.Instance.ClearTimer(effect.PeriodHandle);
				}

				effect.Handle.RemoveFromGlobalMap();

				bool modifiedArray = false;

				GameplayEffects_Internal.RemoveAt(index);

				modifiedArray = true;
				return modifiedArray;
			}

			Debug.LogWarning($"InternalRemoveActiveGameplayEffect called with invalid index: {index}");
			return false;
		}

		public void InternalOnActiveGameplayEffectRemoved(ActiveGameplayEffect effect, in GameplayEffectRemovalInfo gameplayEffectRemovalInfo)
		{
			effect.IsPendingRemove = true;

			if (effect.Spec.Def)
			{
				if (!effect.IsInhibited)
				{
					RemoveActiveGameplayEffectGrantedTagsAndModifiers(effect);
				}

				RemoveCustomMagnitudeExternalDependecies(effect);
			}
			else
			{
				Debug.LogWarning($"InternalOnActiveGameplayEffectRemoved called with no GameplayEffect: {effect.Handle}");
			}

			effect.EventSet.OnEffectRemoved?.Invoke(gameplayEffectRemovalInfo);

			OnActiveGameplayEffectRemovedDelegate?.Invoke(effect);
		}

		public void AddCustomMagnitudeExternalDependecies(ActiveGameplayEffect effect)
		{

		}

		public void RemoveCustomMagnitudeExternalDependecies(ActiveGameplayEffect effect)
		{

		}

		public void RestartActiveGameplayEffectDuration(ActiveGameplayEffect activeGameplayEffect)
		{
			activeGameplayEffect.StartWorldTime = GetWorldTime();

			OnDurationChange(activeGameplayEffect);
		}

		public ActiveGameplayEffect FindStackableActiveGameplayEffect(in GameplayEffectSpec spec)
		{
			ActiveGameplayEffect stackableGE = null;
			GameplayEffect GEDef = spec.Def;
			GameplayEffectStackingType stackingType = GEDef.StackingType;

			if (stackingType != GameplayEffectStackingType.None && GEDef.DurationPolicy != GameplayEffectDurationType.Instant)
			{
				AbilitySystemComponent sourceASC = spec.EffectContext.InstigatorAbilitySystemComponent;
				foreach (ActiveGameplayEffect activeEffect in GameplayEffects_Internal)
				{
					if (activeEffect.Spec.Def == spec.Def && (stackingType == GameplayEffectStackingType.AggregateByTarget || sourceASC && sourceASC == activeEffect.Spec.EffectContext.InstigatorAbilitySystemComponent))
					{
						stackableGE = activeEffect;
						break;
					}
				}
			}

			return stackableGE;
		}

		public void ExecuteActiveEffectsFrom(GameplayEffectSpec spec)
		{
			if (!Owner)
			{
				return;
			}

			GameplayEffectSpec specToUse = spec;

			specToUse.CapturedTargetTags.AggregatedTags.Reset();
			Owner.GetOwnedGameplayTags(specToUse.CapturedTargetTags.AggregatedTags);

			specToUse.CalculateModifierMagnitudes();

			bool modifierSuccessfullyExecuted = false;

			for (int i = 0; i < specToUse.Modifiers.Count; i++)
			{
				GameplayModifierInfo modDef = specToUse.Def.Modifiers[i];
				GameplayModifierEvaluatedData evalData = new(modDef.Attribute, modDef.ModifierOp, specToUse.GetModifierMagnitude(i, true));
				modifierSuccessfullyExecuted |= InternalExecuteMod(specToUse, evalData);
			}

			List<GameplayEffectSpecHandle> conditionalEffectSpecs = new();

			foreach (GameplayEffectExecutionDefinition curExecDef in specToUse.Def.Executions)
			{
				bool runConditionalEffects = true;

				GameplayEffectCustomExecutionParams executionParams = new GameplayEffectCustomExecutionParams();
				GameplayEffectCustomExecutionOutput executionOutput = new GameplayEffectCustomExecutionOutput();
				// curExecDef.CalculationClass.Execute(executionParams, out executionOutput);

				runConditionalEffects = executionOutput.ShouldTriggerConditionalGameplayEffects;

				List<GameplayModifierEvaluatedData> outModifiers = executionOutput.OutputModifiers;

				bool applyStackCountToEmittedMods = !executionOutput.IsStackCountHandledManually;
				int specStackCount = specToUse.StackCount;

				foreach (GameplayModifierEvaluatedData curExecMod in outModifiers)
				{
					if (applyStackCountToEmittedMods && specStackCount > 1)
					{
						curExecMod.Magnitude = GameplayEffectUtilities.ComputeStackedModifierMagnitude(curExecMod.Magnitude, specStackCount, curExecMod.ModifierOp);
					}
					modifierSuccessfullyExecuted |= InternalExecuteMod(specToUse, curExecMod);
				}

				if (runConditionalEffects)
				{
					foreach (ConditionalGameplayEffect conditionalEffect in curExecDef.ConditionalGameplayEffects)
					{
						if (conditionalEffect.CanApply(specToUse.CapturedSourceTags.ActorTags, specToUse.Level))
						{
							GameplayEffectSpecHandle specHandle = conditionalEffect.CreateSpec(specToUse.EffectContext, specToUse.Level);
							if (specHandle.IsValid())
							{
								conditionalEffectSpecs.Add(specHandle);
							}
						}
					}
				}
			}

			foreach (GameplayEffectSpecHandle targetSpec in conditionalEffectSpecs)
			{
				if (targetSpec.IsValid())
				{
					Owner.ApplyGameplayEffectSpecToSelf(targetSpec.Data);
				}
			}

			spec.Def.OnExecuted(this, spec);
		}


		public bool InternalExecuteMod(GameplayEffectSpec spec, GameplayModifierEvaluatedData modEvalData)
		{
			Debug.Assert(Owner);

			bool executed = false;

			AttributeSet attributeSet = null;
			Type attributeSetType = modEvalData.Attribute.GetAttributeSetClass();
			if (attributeSetType != null && attributeSetType.IsSubclassOf(typeof(AttributeSet)))
			{
				attributeSet = Owner.GetAttributeSubobject(attributeSetType);
			}

			if (attributeSet != null)
			{
				GameplayEffectModCallbackData executeData = new()
				{
					EffectSpec = spec,
					EvaluatedData = modEvalData,
					Target = Owner
				};

				if (attributeSet.PreGameplayEffectExecute(executeData))
				{
					float oldValueOfProperty = Owner.GetNumericAttributeBase(modEvalData.Attribute);
					ApplyModToAttribute(modEvalData.Attribute, modEvalData.ModifierOp, modEvalData.Magnitude);

					GameplayEffectModifiedAttribute modifiedAttribute = spec.GetModifiedAttribute(modEvalData.Attribute);
					if (modifiedAttribute is null)
					{
						modifiedAttribute = spec.AddModifiedAttribute(modEvalData.Attribute);
					}
					modifiedAttribute.TotalMagnitude += modEvalData.Magnitude;

					attributeSet.PostGameplayEffectExecute(executeData);

					DebugExecutedGameplayEffectData debugData = new()
					{
						GameplayEffectName = spec.Def.ToString(),
						ActivationState = "INSTANT",
						Attribute = modEvalData.Attribute,
						Magnitude = Owner.GetNumericAttributeBase(modEvalData.Attribute) - oldValueOfProperty
					};
					DebugExecutedGameplayEffects.Add(debugData);

					executed = true;
				}
			}
			else
			{
				Debug.Log($"{Owner.name} does not have attribute {modEvalData.Attribute.GetName()}. Skipping modifer");
			}

			return executed;
		}

		public void ApplyModToAttribute(in GameplayAttribute attribute, GameplayModOp modifierOp, float modifierMagnitude, GameplayEffectModCallbackData modData = null)
		{
			CurrentModCallbackData = modData;
			float currentBase = GetAttributeBaseValue(attribute);
			float newBase = Aggregator.StaticExecModOnBaseValue(currentBase, modifierOp, modifierMagnitude);

			SetAttributeBaseValue(attribute, newBase);

			if (CurrentModCallbackData != null)
			{
				Debug.LogWarning($"ActiveGameplayEffectsContainer.ApplyModToAttribute CurrentModcallbackData was not consumed For attribute {attribute.AttributeName} on {Owner.name}");
				CurrentModCallbackData = null;
			}
		}

		public float GetAttributeBaseValue(GameplayAttribute attribute)
		{
			float baseValue = 0;
			if (Owner)
			{
				AttributeSet attributeSet = Owner.GetAttributeSubobject(attribute.GetAttributeSetClass());
				if (!attributeSet)
				{
					Debug.LogWarning($"ActiveGameplayEffectsContainer.SetAttributeBaseValue: Unable to get attribute set for attribute {attribute.AttributeName}");
					return baseValue;
				}

				AttributeAggregatorMap.TryGetValue(attribute, out Aggregator aggregator);

				if (GameplayAttribute.IsGameplayAttributeDataField(attribute.Property))
				{
					var type = attributeSet.GetType();
					var field = type.GetField(attribute.AttributeName.Split('.').Last(),
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
					GameplayAttributeData data = field.GetValue(attributeSet) as GameplayAttributeData;
					baseValue = data.BaseValue;
				}
				else if (aggregator != null)
				{
					baseValue = aggregator.BaseValue;
				}
				else
				{
					baseValue = Owner.GetNumericAttribute(attribute);
				}
			}
			else
			{
				Debug.LogWarning($"No Owner for ActiveGameplayEffectsContainer in GetAttributeBaseValue");
			}

			return baseValue;
		}

		public void SetAttributeBaseValue(GameplayAttribute attribute, float newBaseValue)
		{
			AttributeSet set = Owner.GetAttributeSubobject(attribute.GetAttributeSetClass());
			if (!set)
			{
				Debug.LogWarning($"ActiveGameplayEffectsContainer.SetAttributeBaseValue: Unable to get attribute set for attribute {attribute.AttributeName}");
				return;
			}

			float oldBaseValue = 0;

			set.PreAttributeBaseChange(attribute, newBaseValue);

			bool isGameplayAttributeDataField = GameplayAttribute.IsGameplayAttributeDataField(attribute.Property);
			if (isGameplayAttributeDataField)
			{
				var type = set.GetType();
				var field = type.GetField(attribute.AttributeName.Split('.').Last(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				GameplayAttributeData attributeData = field.GetValue(set) as GameplayAttributeData;

				if (attributeData != null)
				{
					oldBaseValue = attributeData.BaseValue;
					attributeData.BaseValue = newBaseValue;
				}
			}
			else
			{
				oldBaseValue = Owner.GetNumericAttribute(attribute);
			}

			if (AttributeAggregatorMap.TryGetValue(attribute, out Aggregator aggregator))
			{
				oldBaseValue = aggregator.BaseValue;
				aggregator.BaseValue = newBaseValue;
			}
			else
			{
				InternalUpdateNumericAttribute(attribute, newBaseValue, null);
			}

			set.PostAttributeBaseChange(attribute, oldBaseValue, newBaseValue);
		}

		public void InternalUpdateNumericAttribute(GameplayAttribute attribute, float newValue, in GameplayEffectModCallbackData modData, bool fromRecursiveCall = false)
		{
			float oldValue = Owner.GetNumericAttribute(attribute);
			Debug.Log($"InternalUpdateNumericAttribute {attribute.AttributeName} OldValue = {oldValue} newValue = {newValue}.");
			Owner.SetNumericAttribute_Internal(attribute, newValue);

			if (!fromRecursiveCall)
			{
				if (modData != null && CurrentModCallbackData != null)
				{
					Debug.LogWarning($"Had passed in ModData and cached CurrentModcallbackData in ActiveGameplayEffectsContainer::InternalUpdateNumericalAttribute. For attribute {attribute.AttributeName} on {Owner.name}.");
				}

				GameplayEffectModCallbackData dataToShare = modData ?? CurrentModCallbackData;
				if (dataToShare != null)
				{
					CurrentModCallbackData = dataToShare;
				}

				if (AttributeChangeDelegates.TryGetValue(attribute, out OnGameplayAttributeChange legacyDelegate))
				{
					legacyDelegate.Invoke(newValue, dataToShare);
				}

				if (AttributeValueChangeDelegates.TryGetValue(attribute, out OnGameplayAttributeValueChange newDelegate))
				{
					OnAttributeChangeData callbackData = new()
					{
						Attribute = attribute,
						NewValue = newValue,
						OldValue = oldValue,
						GEModData = dataToShare
					};
					newDelegate.Invoke(callbackData);
				}
			}
			CurrentModCallbackData = null;
		}

		public void AddActiveGameplayEffectGrantedTagsAndModifiers(ActiveGameplayEffect effect)
		{
			if (effect.Spec.Def == null)
			{
				Debug.LogError("AddActiveGameplayEffectGrantedTagsAndModifiers called with null Def!");
				return;
			}

			if (effect.Spec.Period <= GameplayEffectConstants.NoPeriod)
			{
				for (int i = 0; i < effect.Spec.Modifiers.Count; i++)
				{
					if (effect.Spec.Def.Modifiers.IsValidIndex(i) == false)
					{
						Debug.LogError($"Spec Modifiers[{i}] (max {effect.Spec.Def.Modifiers.Count}) is invalid with Def ({effect.Spec.Def}) modifiers (max {effect.Spec.Def.Modifiers.Count})");
						continue;
					}

					GameplayModifierInfo modInfo = effect.Spec.Def.Modifiers[i];

					if (!Owner || !Owner.HasAttributeSetForAttribute(modInfo.Attribute))
					{
						continue;
					}

					float evaluatedMagnitude = effect.Spec.GetModifierMagnitude(i, true);

					Aggregator aggregator = FindOrCreateAttributeAggregator(modInfo.Attribute);
					if (aggregator != null)
					{
						aggregator.AddAggregatorMod(evaluatedMagnitude, modInfo.ModifierOp, modInfo.EvaluationChannelSettings.EvaluationChannel, modInfo.SourceTags, modInfo.TargetTags, effect.Handle);
					}
				}
			}
			else
			{
				if (effect.Spec.Def.PeriodInhibitionPolicy != GameplayEffectPeriodInhibitionRemovedPolicy.NeverReset)
				{
					TimerDelegate timerDelegate = () =>
					{
						Owner.ExecutePeriodicEffect(effect.Handle);
					};

					if (effect.Spec.Def.PeriodInhibitionPolicy == GameplayEffectPeriodInhibitionRemovedPolicy.ExecuteAndResetPeriod)
					{
						TimerManager.Instance.SetTimerForNextTick(timerDelegate);
					}

					TimerManager.Instance.SetTimer(ref effect.PeriodHandle, timerDelegate, effect.Spec.Period, true);
				}
			}

			Owner.UpdateTagMap(effect.Spec.Def.GrantedTags, 1);
			Owner.UpdateTagMap(effect.Spec.DynamicGrantedTags, 1);

			Owner.BlockAbilitiesWithTags(effect.Spec.Def.BlockedAbilityTags);

			Owner.OnActiveGameplayEffectAddedDelegateToSelf?.Invoke(Owner, effect.Spec, effect.Handle);
		}

		public void RemoveActiveGameplayEffectGrantedTagsAndModifiers(in ActiveGameplayEffect effect)
		{
			if (effect.Spec.Period <= GameplayEffectConstants.NoPeriod)
			{
				foreach (GameplayModifierInfo mod in effect.Spec.Def.Modifiers)
				{
					if (mod.Attribute.IsValid())
					{
						if (AttributeAggregatorMap.TryGetValue(mod.Attribute, out Aggregator aggregator))
						{
							aggregator.RemoveAggregatorMod(effect.Handle);
						}
					}
				}
			}

			Owner.UpdateTagMap(effect.Spec.Def.AssetTags, -1);
			Owner.UpdateTagMap(effect.Spec.DynamicGrantedTags, -1);

			Owner.UnblockAbilitiesWithTags(effect.Spec.Def.BlockedAbilityTags);
		}


		public Aggregator FindOrCreateAttributeAggregator(GameplayAttribute attribute)
		{
			if (AttributeAggregatorMap.TryGetValue(attribute, out Aggregator attributeAggregator))
			{
				return attributeAggregator;
			}

			float currentBaseValueOfProperty = Owner.GetNumericAttributeBase(attribute);
			Debug.Log($"Creating new entry in AttributeAggregatorMap for {attribute.AttributeName}. CurrentValue: {currentBaseValueOfProperty}");

			Aggregator newAttributeAggregator = new(currentBaseValueOfProperty);

			if (attribute.IsSystemAttribute() == false)
			{
				newAttributeAggregator.OnDirty += (aggregator) => Owner.OnAttributeAggregatorDirty(aggregator, attribute, false);
				newAttributeAggregator.OnDirtyRecursive += (aggregator) => Owner.OnAttributeAggregatorDirty(aggregator, attribute, true);

				AttributeSet set = Owner.GetAttributeSubobject(attribute.GetAttributeSetClass());
				set.OnAttributeAggregatorCreated(attribute, newAttributeAggregator);
			}

			AttributeAggregatorMap.Add(attribute, newAttributeAggregator);
			return newAttributeAggregator;
		}

		public void CaptureAttributeForGameplayEffect(GameplayEffectAttributeCaptureSpec captureSpec)
		{
			Aggregator attributeAggregator = FindOrCreateAttributeAggregator(captureSpec.BackingDefinition.AttributeToCapture);

			if (captureSpec.BackingDefinition.Snapshot)
			{
				captureSpec.AttributeAggregator.TakeSnapshotOf(attributeAggregator);
			}
			else
			{
				captureSpec.AttributeAggregator = attributeAggregator;
			}
		}

		public float GetWorldTime()
		{
			return UnityEngine.Time.time;
		}

		public void CheckDuration(ActiveGameplayEffectHandle handle)
		{
			for (int activeGEIdx = 0; activeGEIdx < GameplayEffects_Internal.Count; activeGEIdx++)
			{
				ActiveGameplayEffect effect = GameplayEffects_Internal[activeGEIdx];
				if (effect.Handle == handle)
				{
					if (effect.IsPendingRemove)
					{
						break;
					}

					float duration = effect.Duration;
					float currentTime = GetWorldTime();

					int stacksToRemove = -2;
					bool refreshStartTime = false;
					bool refreshDurationTimer = false;
					bool checkForFinalPeriodicExec = false;

					if (duration > 0 && (effect.StartWorldTime + duration < currentTime || Mathf.Approximately(currentTime - duration - effect.StartWorldTime, 0)))
					{
						switch (effect.Spec.Def.StackExpirationPolicy)
						{
							case GameplayEffectStackingExpirationPolicy.ClearEntireStack:
								stacksToRemove = -1;
								checkForFinalPeriodicExec = true;
								break;
							case GameplayEffectStackingExpirationPolicy.RemoveSingleStackAndRefreshDuration:
								stacksToRemove = 1;
								checkForFinalPeriodicExec = effect.Spec.StackCount == 1;
								refreshStartTime = true;
								refreshDurationTimer = true;
								break;
							case GameplayEffectStackingExpirationPolicy.RefreshDuration:
								refreshStartTime = true;
								refreshDurationTimer = true;
								break;
						}
					}
					else
					{
						refreshDurationTimer = true;
					}

					if (checkForFinalPeriodicExec)
					{
						if (effect.PeriodHandle.IsValid() && TimerManager.Instance.TimerExists(effect.PeriodHandle))
						{
							float periodTimeRemaining = TimerManager.Instance.GetTimerRemaining(effect.PeriodHandle);
							if (periodTimeRemaining <= Mathf.Epsilon && !effect.IsInhibited)
							{
								InternalExecutePeriodicGameplayEffect(effect);

								if (effect.IsPendingRemove)
								{
									break;
								}
							}

							TimerManager.Instance.ClearTimer(effect.PeriodHandle);
						}
					}

					if (stacksToRemove >= -1)
					{
						InternalRemoveActiveGameplayEffect(activeGEIdx, stacksToRemove, false);
					}

					if (refreshStartTime)
					{
						RestartActiveGameplayEffectDuration(effect);
					}

					if (refreshDurationTimer)
					{
						TimerDelegate @delegate = () =>
						{
							Owner.CheckDurationExpired(effect.Handle);
						};

						float newTimerDuration = effect.StartWorldTime + duration - currentTime;
						TimerManager.Instance.SetTimer(ref effect.DurationHandle, @delegate, newTimerDuration, false);

						if (effect.DurationHandle.IsValid() == false)
						{
							Debug.LogWarning($"Failed to set new timer in CheckDuration. Timer trying to be set for: {newTimerDuration}. Removing GE instead");
							if (!effect.IsPendingRemove)
							{
								InternalRemoveActiveGameplayEffect(activeGEIdx, -1, false);
							}
							Debug.Assert(effect.IsPendingRemove);
						}
					}

					break;
				}
			}
		}

		public bool CanApplyAttributeModifiers(in GameplayEffect gameplayEffect, float level, in GameplayEffectContextHandle effectContext)
		{
			GameplayEffectSpec spec = new(gameplayEffect, effectContext, level);

			spec.CalculateModifierMagnitudes();

			for (int modIdx = 0; modIdx < spec.Modifiers.Count; modIdx++)
			{
				GameplayModifierInfo modDef = spec.Def.Modifiers[modIdx];
				ModifierSpec modSpec = spec.Modifiers[modIdx];

				if (modDef.ModifierOp == GameplayModOp.Additive)
				{
					if (!modDef.Attribute.IsValid())
					{
						continue;
					}
					AttributeSet set = Owner.GetAttributeSubobject(modDef.Attribute.GetAttributeSetClass());
					float currentValue = modDef.Attribute.GetNumericValueChecked(set);
					float costValue = modSpec.EvaluatedMagnitude;

					if (currentValue + costValue < 0)
					{
						return false;
					}
				}
			}

			return true;
		}

		public ActiveGameplayEffect GetActiveGameplayEffect(ActiveGameplayEffectHandle handle)
		{
			foreach (ActiveGameplayEffect effect in GameplayEffects_Internal)
			{
				if (effect.Handle == handle)
				{
					return effect;
				}
			}
			return null;
		}

		public List<ActiveGameplayEffectHandle> GetActiveEffects(in GameplayEffectQuery query)
		{
			List<ActiveGameplayEffectHandle> returnList = new();
			foreach (ActiveGameplayEffect effect in GameplayEffects_Internal)
			{
				if (!query.Matches(effect))
				{
					continue;
				}

				returnList.Add(effect.Handle);
			}
			return returnList;
		}

		public int RemoveActiveEffects(in GameplayEffectQuery query, int stacksToRemove)
		{
			int removedCount = 0;
			for (int i = GetNumGameplayEffects() - 1; i >= 0; i--)
			{
				ActiveGameplayEffect effect = GetActiveGameplayEffect(i);
				if (effect.IsPendingRemove == false && query.Matches(effect))
				{
					InternalRemoveActiveGameplayEffect(i, stacksToRemove, false);
					removedCount++;
				}
			}
			return removedCount;
		}

		public void OnStackCountChange(ActiveGameplayEffect activeEffect, int oldStackCount, int newStackCount)
		{
			if (oldStackCount != newStackCount)
			{
				UpdateAllAggregatorModMagnitudes(activeEffect);
			}

			if (activeEffect.Spec.Def)
			{
				Owner.NotifyTagMap_StackCountChange(activeEffect.Spec.Def.GrantedTags);
			}

			Owner.NotifyTagMap_StackCountChange(activeEffect.Spec.DynamicGrantedTags);

			activeEffect.EventSet.OnStackChanged?.Invoke(activeEffect.Handle, activeEffect.Spec.StackCount, oldStackCount);
		}

		public void OnDurationChange(ActiveGameplayEffect effect)
		{
			effect.EventSet.OnTimeChanged?.Invoke(effect.Handle, effect.StartWorldTime, effect.Duration);
			Owner.OnGameplayEffectDurationChange(effect);
		}

		public void UpdateAllAggregatorModMagnitudes(ActiveGameplayEffect activeEffect)
		{
			if (activeEffect.Spec.Period > GameplayEffectConstants.NoPeriod)
			{
				return;
			}

			if (activeEffect.IsInhibited)
			{
				return;
			}

			GameplayEffectSpec spec = activeEffect.Spec;

			if (spec.Def == null)
			{
				Debug.LogError($"UpdateAllAggregatorModMagnitudes called with no GameplayEffect def.");
				return;
			}

			HashSet<GameplayAttribute> attributesToUpdate = new();

			for (int modIdx = 0; modIdx < spec.Modifiers.Count; modIdx++)
			{
				GameplayModifierInfo modDef = spec.Def.Modifiers[modIdx];
				attributesToUpdate.Add(modDef.Attribute);
			}

			UpdateAggregatorModMagnitudes(attributesToUpdate, activeEffect);
		}

		public void OnMagnitudeDependencyChange(ActiveGameplayEffectHandle handle, in Aggregator changedAggregator)
		{
			if (handle.IsValid())
			{
				ActiveGameplayEffect activeEffect = GetActiveGameplayEffect(handle);
				if (activeEffect is not null)
				{
					GameplayEffectSpec spec = activeEffect.Spec;

					bool mustUpdateAttributeAggregators = !activeEffect.IsInhibited && spec.Period <= 0;

					HashSet<GameplayAttribute> attributesToUpdate = new();

					for (int modIdx = 0; modIdx < spec.Modifiers.Count; modIdx++)
					{
						GameplayModifierInfo modDef = spec.Def.Modifiers[modIdx];
						ModifierSpec modSpec = spec.Modifiers[modIdx];

						float recalculatedMagnitude = 0;
						if (modDef.ModifierMagnitude.AttemptRecalculateMagnitudeFromDependentAggregatorChange(spec, ref recalculatedMagnitude, changedAggregator))
						{
							modSpec.EvaluatedMagnitude = recalculatedMagnitude;

							if (mustUpdateAttributeAggregators)
							{
								attributesToUpdate.Add(modDef.Attribute);
							}
						}
					}

					UpdateAggregatorModMagnitudes(attributesToUpdate, activeEffect);
				}
			}
		}

		public void OnAttributeAggregatorDirty(Aggregator aggregator, GameplayAttribute attribute, bool fromRecursiveCall)
		{
			AggregatorEvaluateParameters evaluationParameters = new();

			float newValue = aggregator.Evaluate(evaluationParameters);

			InternalUpdateNumericAttribute(attribute, newValue, null, fromRecursiveCall);
		}

		public void UpdateAggregatorModMagnitudes(in HashSet<GameplayAttribute> attributesToUpdate, ActiveGameplayEffect activeEffect)
		{
			GameplayEffectSpec spec = activeEffect.Spec;
			foreach (GameplayAttribute attribute in attributesToUpdate)
			{
				if (!Owner || Owner.HasAttributeSetForAttribute(attribute) == false)
				{
					continue;
				}

				Aggregator aggregator = FindOrCreateAttributeAggregator(attribute);
				if (aggregator != null)
				{
					aggregator.UpdateAggregatorMod(activeEffect.Handle, attribute, spec, activeEffect.Handle);
				}
			}
		}

		public GameplayTagContainer GetGameplayEffectSourceTagsFromHandle(ActiveGameplayEffectHandle handle)
		{
			foreach (ActiveGameplayEffect effect in GameplayEffects_Internal)
			{
				if (effect.Handle == handle)
				{
					return effect.Spec.CapturedSourceTags.AggregatedTags;
				}
			}
			return null;
		}

		public GameplayTagContainer GetGameplayEffectTargetTagsFromHandle(ActiveGameplayEffectHandle handle)
		{
			foreach (ActiveGameplayEffect effect in GameplayEffects_Internal)
			{
				if (effect.Handle == handle)
				{
					return effect.Spec.CapturedTargetTags.AggregatedTags;
				}
			}
			return null;
		}

		public OnGameplayAttributeChange RegisterGameplayAttributeEvent(GameplayAttribute attribute)
		{
			if (!AttributeChangeDelegates.TryGetValue(attribute, out OnGameplayAttributeChange @delegate))
			{
				@delegate = new();
				AttributeChangeDelegates.Add(attribute, @delegate);
			}
			return @delegate;
		}

		public OnGameplayAttributeValueChange GetGameplayAttributeValueChangeDelegate(GameplayAttribute attribute)
		{
			if (!AttributeValueChangeDelegates.TryGetValue(attribute, out OnGameplayAttributeValueChange @delegate))
			{
				@delegate = new();
				AttributeValueChangeDelegates.Add(attribute, @delegate);
			}
			return @delegate;
		}
	}

	[CreateAssetMenu(fileName = "GameplayEffect", menuName = "GameplayAbilities/GameplayEffect", order = 0)]
	public class GameplayEffect : ScriptableObject
	{
#if ODIN_INSPECTOR
		[FoldoutGroup("Duration")]
#endif
		public GameplayEffectDurationType DurationPolicy;

#if ODIN_INSPECTOR
		[FoldoutGroup("Duration")]
		[ShowIf("DurationPolicy", GameplayEffectDurationType.HasDuration)]
#endif
		public GameplayEffectModifierMagnitude DurationMagnitude = new();

#if ODIN_INSPECTOR
		[FoldoutGroup("Duration")]
		[HideIf("DurationPolicy", GameplayEffectDurationType.Instant)]
#endif
		public ScalableFloat Period = new();

#if ODIN_INSPECTOR
		[FoldoutGroup("Duration")]
		[ShowIf("@Period.Value != 0")]
#endif
		public bool ExecutePeriodicEffectOnApplication = true;

#if ODIN_INSPECTOR
		[FoldoutGroup("Duration")]
		[ShowIf("@Period.Value != 0")]
#endif
		public GameplayEffectPeriodInhibitionRemovedPolicy PeriodInhibitionPolicy;

#if ODIN_INSPECTOR
		[FoldoutGroup("GameplayEffect")]
		[LabelText("Components")]
		[ListDrawerSettings
		(
			CustomAddFunction = "AddComponentMenu",
			CustomRemoveElementFunction = "RemoveComponent"
		)]
		[InlineEditor(InlineEditorObjectFieldModes.Foldout)]
#endif
		[SerializeReference]
		public List<GameplayEffectComponent> GEComponents = new();

#if ODIN_INSPECTOR
		[FoldoutGroup("GameplayEffect")]
#endif
		public List<GameplayModifierInfo> Modifiers = new();

#if ODIN_INSPECTOR
		[FoldoutGroup("GameplayEffect")]
#endif
		public List<GameplayEffectExecutionDefinition> Executions = new();

#if ODIN_INSPECTOR
		[FoldoutGroup("GameplayEffect")]
		[LabelText("Gameplay Effect Asset Tags")]
		[HideInInspector]
		[Obsolete]
#endif
		public InheritedTagContainer InheritableGameplayEffectTags = new();

#if ODIN_INSPECTOR
		[FoldoutGroup("GameplayEffect")]
		[LabelText("Granted Tags")]
		[HideInInspector]
		[Obsolete]
#endif
		public InheritedTagContainer InheritableOwnedTagsContainer = new();

#if ODIN_INSPECTOR
		[FoldoutGroup("GameplayEffect")]
		[LabelText("Granted Blocked Ability Tags")]
		[HideInInspector]
		[Obsolete]
#endif
		public InheritedTagContainer InheritableBlockedAbilityTagsContainer;

#if ODIN_INSPECTOR
		[FoldoutGroup("GameplayEffect")]
		[LabelText("Ongoing Tag Requirements")]
		[HideInInspector]
		[Obsolete]
#endif
		public GameplayTagRequirements OngoingTagRequirements;

#if ODIN_INSPECTOR
		[FoldoutGroup("GameplayEffect")]
		[LabelText("Application Tag Requirements")]
		[HideInInspector]
		[Obsolete]
#endif
		public GameplayTagRequirements ApplicationTagRequirements;

#if ODIN_INSPECTOR
		[FoldoutGroup("GameplayEffect")]
		[HideInInspector]
		[Obsolete]
#endif
		public GameplayTagRequirements RemovalTagRequirements;

#if ODIN_INSPECTOR
		[FoldoutGroup("GameplayEffect")]
		[HideInInspector]
		[Obsolete]
#endif
		public InheritedTagContainer RemoveGameplayEffectWithTags;

#if ODIN_INSPECTOR
		[FoldoutGroup("GameplayEffect")]
		[HideInInspector]
		[Obsolete]
#endif
		public GameplayTagRequirements GrantedApplicationImmunityTags;

#if ODIN_INSPECTOR
		[FoldoutGroup("GameplayEffect")]
		[HideInInspector]
		[Obsolete]
#endif
		public GameplayEffectQuery GrantedApplicationImmunityQuery;

#if ODIN_INSPECTOR
		[FoldoutGroup("GameplayEffect")]
		[HideInInspector]
		[Obsolete]
#endif
		public bool HasGrantedApplicationImmunityTags;

#if ODIN_INSPECTOR
		[FoldoutGroup("GameplayEffect")]
		[HideInInspector]
		[Obsolete]
#endif
		public GameplayEffectQuery RemoveGameplayEffectQuery;

		[HideInInspector]
		[Obsolete]
		public bool HasRemoveGameplayEffectQuery;

#if ODIN_INSPECTOR
		[FoldoutGroup("Stacking")]
#endif
		public GameplayEffectStackingType StackingType;

#if ODIN_INSPECTOR
		[FoldoutGroup("Stacking")]
		[HideIf("StackingType", GameplayEffectStackingType.None)]
#endif
		public int StackLimitCount;

#if ODIN_INSPECTOR
		[FoldoutGroup("Stacking")]
		[HideIf("StackingType", GameplayEffectStackingType.None)]
#endif
		public GameplayEffectStackingDurationPolicy StackDurationRefreshPolicy;

#if ODIN_INSPECTOR
		[FoldoutGroup("Stacking")]
		[HideIf("StackingType", GameplayEffectStackingType.None)]
#endif
		public GameplayEffectStackingPeriodPolicy StackPeriodResetPolicy;

#if ODIN_INSPECTOR
		[FoldoutGroup("Stacking")]
		[HideIf("StackingType", GameplayEffectStackingType.None)]
#endif
		public GameplayEffectStackingExpirationPolicy StackExpirationPolicy;

#if ODIN_INSPECTOR
		[FoldoutGroup("Stacking")]
		[HideIf("StackingType", GameplayEffectStackingType.None)]
#endif
		public List<GameplayEffect> OverflowEffects = new();

#if ODIN_INSPECTOR
		[FoldoutGroup("Stacking")]
#endif
		public bool DenyOverflowApplication;

#if ODIN_INSPECTOR
		[FoldoutGroup("Stacking")]
		[ShowIf("DenyOverflowApplication", true)]
#endif
		public bool ClearStackOnOverflow;

		[HideInInspector]
		public GameplayTagContainer CachedAssetTags = new();

		[HideInInspector]
		public GameplayTagContainer CachedGrantedTags = new();

		[HideInInspector]
		public GameplayTagContainer CachedBlockedAbilityTags = new();

		public GameplayTagContainer AssetTags => CachedAssetTags;

		public GameplayTagContainer GrantedTags => CachedGrantedTags;

		public GameplayTagContainer BlockedAbilityTags => CachedBlockedAbilityTags;

#if ODIN_INSPECTOR && UNITY_EDITOR
		protected virtual void OnValidate()
		{
			ConvertAssetTagsComponent();
			ConvertRemoveOtherComponent();
			ConvertTagRequirementsComponent();
			ConvertTargetTagsComponent();
		}

		private void AddComponentMenu()
		{
			var menu = new GenericMenu();

			// 获取所有GameplayEffectComponent的子类
			var componentTypes = TypeCache.GetTypesDerivedFrom<GameplayEffectComponent>();
			foreach (var type in componentTypes)
			{
				if (!type.IsAbstract)
				{
					menu.AddItem(new GUIContent(type.Name), false, () =>
					{
						var component = GameplayEffectComponent.CreateInstance(type, this);
						GEComponents.Add(component);

						// 如果这是一个Asset，确保将组件设置为子资源
						if (this != null)
						{
							AssetDatabase.AddObjectToAsset(component, this);
							EditorUtility.SetDirty(this);
							AssetDatabase.SaveAssets();
						}
					});
				}
			}

			menu.ShowAsContext();
		}

		private void RemoveComponent(GameplayEffectComponent component)
		{
			if (component != null)
			{
				// 从列表中移除
				GEComponents.Remove(component);

				// 如果这是一个Asset，删除子资源
				if (this != null)
				{
					// 确保在编辑器模式下
#if UNITY_EDITOR
					// 从资源文件中删除组件
					UnityEditor.AssetDatabase.RemoveObjectFromAsset(component);
					// 销毁组件对象
					UnityEditor.Undo.DestroyObjectImmediate(component);
					// 标记资源已修改
					UnityEditor.EditorUtility.SetDirty(this);
					// 保存更改
					UnityEditor.AssetDatabase.SaveAssets();
#endif
				}
			}
		}
#endif

		public void OnGameplayEffectChanged()
		{
			CachedAssetTags.Reset();
			CachedGrantedTags.Reset();
			CachedBlockedAbilityTags.Reset();

			foreach (GameplayEffectComponent GEComponent in GEComponents)
			{
				if (GEComponent != null)
				{
					GEComponent.OnGameplayEffectChanged();
				}
			}
		}

		public bool CanApply(in ActiveGameplayEffectsContainer active_ge_container, in GameplayEffectSpec ge_spec)
		{
			foreach (var ge_component in GEComponents)
			{
				if (!ge_component.CanGameplayEffectApply(active_ge_container, ge_spec))
				{
					return false;
				}
			}
			return true;
		}

		public bool OnAddedToActiveContainer(ActiveGameplayEffectsContainer active_ge_container, ActiveGameplayEffect active_ge)
		{
			var should_be_active = true;
			foreach (var ge_component in GEComponents)
			{
				should_be_active = ge_component.OnActiveGameplayEffectAdded(active_ge_container, active_ge);
			}
			return should_be_active;
		}

		public void OnExecuted(ActiveGameplayEffectsContainer active_ge_container, GameplayEffectSpec ge_spec)
		{
			foreach (var ge_component in GEComponents)
			{
				ge_component.OnGameplayEffectExecuted(active_ge_container, ge_spec);
			}
		}

		public void OnApplied(ActiveGameplayEffectsContainer active_ge_container, GameplayEffectSpec ge_spec)
		{
			foreach (var ge_component in GEComponents)
			{
				ge_component.OnGameplayEffectApplied(active_ge_container, ge_spec);
			}
		}

		public T GetComponent<T>() where T : GameplayEffectComponent
		{
			foreach (var GEComponent in GEComponents)
			{
				if (GEComponent is T component)
				{
					return component;
				}
			}
			return null;
		}

		public T AddComponent<T>() where T : GameplayEffectComponent, new()
		{
			var component = new T();
			GEComponents.Add(component);
			return component;
		}

		public T GetOrAddComponent<T>() where T : GameplayEffectComponent, new()
		{
			var component = GetComponent<T>();
			if (component == null)
			{
				component = new T();
				GEComponents.Add(component);
			}
			return component;
		}

		private void ConvertAssetTagsComponent()
		{
			GameplayEffect archetype = this;

			bool changed = InheritableGameplayEffectTags.CombinedTags != archetype.InheritableGameplayEffectTags.CombinedTags;
			if (changed)
			{
				AssetTagsGameplayEffectComponent assetTagsComponent = GetOrAddComponent<AssetTagsGameplayEffectComponent>();
				assetTagsComponent.SetAndApplyAssetTagChanges(InheritableGameplayEffectTags);
			}

			{
				AssetTagsGameplayEffectComponent assetTagsComponent = GetComponent<AssetTagsGameplayEffectComponent>();
				if (assetTagsComponent != null)
				{
					InheritableGameplayEffectTags = assetTagsComponent.ConfiguredAssetTags;
					InheritableGameplayEffectTags.UpdateInheritedTagProperties(archetype.InheritableGameplayEffectTags);
				}
			}
		}

		private void ConvertRemoveOtherComponent()
		{
			GameplayEffect archetype = this;

			bool tagChanged = RemoveGameplayEffectWithTags != archetype.RemoveGameplayEffectWithTags;
			bool queryChanged = RemoveGameplayEffectQuery != archetype.RemoveGameplayEffectQuery;

			bool changed = tagChanged || queryChanged;
			if (changed)
			{
				RemoveOtherGameplayEffectComponent removeOtherComponent = GetOrAddComponent<RemoveOtherGameplayEffectComponent>();

				while (removeOtherComponent.RemoveGameplayEffectsQueries.Count < 2)
				{
					removeOtherComponent.RemoveGameplayEffectsQueries.Add(new GameplayEffectQuery());
				}

				if (tagChanged)
				{
					removeOtherComponent.RemoveGameplayEffectsQueries[0] = GameplayEffectQuery.MakeQuery_MatchAllOwningTags(RemoveGameplayEffectWithTags.CombinedTags);
				}

				if (queryChanged)
				{
					removeOtherComponent.RemoveGameplayEffectsQueries[1] = RemoveGameplayEffectQuery;
				}
			}

			{
				RemoveOtherGameplayEffectComponent removeOtherComponent = GetComponent<RemoveOtherGameplayEffectComponent>();
				if (removeOtherComponent != null && !removeOtherComponent.RemoveGameplayEffectsQueries.IsEmpty())
				{
					RemoveGameplayEffectQuery = removeOtherComponent.RemoveGameplayEffectsQueries.Last();
				}
			}
		}

		private void ConvertTagRequirementsComponent()
		{
			GameplayEffect archetype = this;

			bool changed = ApplicationTagRequirements != archetype.ApplicationTagRequirements;
			if (changed)
			{
				// TagRequirementsGameplayEffectComponent tagRequirementsComponent = GetOrAddComponent<TagRequirementsGameplayEffectComponent>();
				// tagRequirementsComponent.SetAndApplyTagChanges(ApplicationTagRequirements);
			}
		}

		private void ConvertTargetTagsComponent()
		{
			GameplayEffect archetype = this;

			bool changed = InheritableOwnedTagsContainer.CombinedTags != archetype.InheritableOwnedTagsContainer.CombinedTags;
			if (changed)
			{
				TargetTagsGameplayEffectComponent targetTagsComponent = GetOrAddComponent<TargetTagsGameplayEffectComponent>();
				targetTagsComponent.SetAndApplyTargetTagChanges(InheritableOwnedTagsContainer);
			}

			{
				TargetTagsGameplayEffectComponent targetTagsComponent = GetComponent<TargetTagsGameplayEffectComponent>();
				if (targetTagsComponent != null)
				{
					InheritableOwnedTagsContainer = targetTagsComponent.ConfiguredTargetTagsChanges;
					InheritableOwnedTagsContainer.UpdateInheritedTagProperties(archetype.InheritableOwnedTagsContainer);
				}
			}
		}
	}
}