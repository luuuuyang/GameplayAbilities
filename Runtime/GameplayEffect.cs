using GameplayTags;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;

namespace GameplayAbilities
{
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
		[ShowIf("@AbilitySystemGlobals.Instance.ShouldAllowGameplayModEvaluationChannels()")]
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
	public class AttributeBasedFloat : IEquatable<AttributeBasedFloat>
	{
		public ScalableFloat Coefficient;
		public ScalableFloat PreMultiplyAdditiveValue;
		public ScalableFloat PostMultiplyAdditiveValue;
		public GameplayEffectAttributeCaptureDefinition BackingAttribute;
		public AnimationCurve AttributeCurve;
		public AttributeBasedFloatCalculationType AttributeCalculationType;
		[ShowIf("@AbilitySystemGlobals.Instance.ShouldAllowGameplayModEvaluationChannels()")]
		public GameplayModEvaluationChannel FinalChannel;
		public GameplayTagContainer SourceTagFilter;
		public GameplayTagContainer TargetTagFilter;

		public AttributeBasedFloat()
		{
			Coefficient = new ScalableFloat(1);
			PreMultiplyAdditiveValue = new ScalableFloat(0);
			PostMultiplyAdditiveValue = new ScalableFloat(0);
			BackingAttribute = new GameplayEffectAttributeCaptureDefinition();
			AttributeCurve = null;
			AttributeCalculationType = AttributeBasedFloatCalculationType.AttributeMagnitude;
			FinalChannel = GameplayModEvaluationChannel.Channel0;
			SourceTagFilter = new GameplayTagContainer();
			TargetTagFilter = new GameplayTagContainer();
		}

		public float CalculateMagnitude(in GameplayEffectSpec relevantSpec)
		{
			GameplayEffectAttributeCaptureSpec captureSpec = relevantSpec.CapturedRelevantAttributes.FindCaptureSpecByDefinition(BackingAttribute, true);
			Debug.Assert(captureSpec != null, $"Attempted to calculate an attribute-based float from spec: {relevantSpec} that did not have the required captured attribute: {BackingAttribute}");

			float attributeValue = 0;

			if (AttributeCalculationType == AttributeBasedFloatCalculationType.AttributeBaseValue)
			{
				captureSpec.AttemptCalculateAttributeBaseValue(ref attributeValue);
			}
			else
			{
				AggregatorEvaluateParameters evaluationParameters = new()
				{
					SourceTags = relevantSpec.CapturedSourceTags.AggregatedTags,
					TargetTags = relevantSpec.CapturedTargetTags.AggregatedTags,
					AppliedSourceTagFilter = SourceTagFilter,
					AppliedTargetTagFilter = TargetTagFilter
				};

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

			if (AttributeCurve != null && AttributeCurve.length > 0)
			{
				attributeValue = AttributeCurve.Evaluate(attributeValue);
			}

			float specLevel = relevantSpec.Level;
			return Coefficient.GetValueAtLevel(specLevel) * (attributeValue + PreMultiplyAdditiveValue.GetValueAtLevel(specLevel)) + PostMultiplyAdditiveValue.GetValueAtLevel(specLevel);
		}

		public bool Equals(AttributeBasedFloat other)
		{
			if (other is null)
			{
				return false;
			}

			if (Coefficient != other.Coefficient ||
				PreMultiplyAdditiveValue != other.PreMultiplyAdditiveValue ||
				PostMultiplyAdditiveValue != other.PostMultiplyAdditiveValue ||
				BackingAttribute != other.BackingAttribute ||
				AttributeCurve != other.AttributeCurve ||
				AttributeCalculationType != other.AttributeCalculationType ||
				FinalChannel != other.FinalChannel)
			{
				return false;
			}

			if (SourceTagFilter.Count != other.SourceTagFilter.Count ||
				!SourceTagFilter.HasAllExact(other.SourceTagFilter))
			{
				return false;
			}

			if (TargetTagFilter.Count != other.TargetTagFilter.Count ||
				!TargetTagFilter.HasAllExact(other.TargetTagFilter))
			{
				return false;
			}

			return true;
		}

		public static bool operator ==(AttributeBasedFloat lhs, AttributeBasedFloat rhs)
		{
			if (lhs is null)
			{
				return rhs is null;
			}

			return lhs.Equals(rhs);
		}

		public static bool operator !=(AttributeBasedFloat lhs, AttributeBasedFloat rhs)
		{
			return !(lhs == rhs);
		}

		public override bool Equals(object obj)
		{
			return Equals(obj);
		}

		public override int GetHashCode()
		{
			HashCode hashCode = new();
			hashCode.Add(Coefficient);
			hashCode.Add(PreMultiplyAdditiveValue);
			hashCode.Add(PostMultiplyAdditiveValue);
			hashCode.Add(BackingAttribute);
			hashCode.Add(AttributeCurve);
			hashCode.Add(AttributeCalculationType);
			hashCode.Add(FinalChannel);
			hashCode.Add(SourceTagFilter);
			hashCode.Add(TargetTagFilter);
			return hashCode.ToHashCode();
		}
	}

	[Serializable]
	public record CustomCalculationBasedFloat
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
			Debug.Assert(calcCDO != null);

			float customBaseValue = calcCDO.CalculateBaseMagnitude_Implementation(relevantSpec);

			float specLvl = relevantSpec.Level;

			float finalValue = Coefficient.GetValueAtLevel(specLvl) * (customBaseValue + PreMultiplyAdditiveValue.GetValueAtLevel(specLvl)) + PostMultiplyAdditiveValue.GetValueAtLevel(specLvl);
			if (FinalLookupCurve != null && FinalLookupCurve.length > 0)
			{
				finalValue = FinalLookupCurve.Evaluate(finalValue);
			}

			return finalValue;
		}
	}

	[Serializable]
	public struct SetByCallerFloat
	{
		public string DataName;
		public GameplayTag DataTag;
	}

	[Serializable]
	public class GameplayEffectModifierMagnitude : IEquatable<GameplayEffectModifierMagnitude>
	{
		public GameplayEffectMagnitudeCalculation MagnitudeCalculationType;

		[ShowIf("MagnitudeCalculationType", GameplayEffectMagnitudeCalculation.ScalableFloat)]
		public ScalableFloat ScalableFloatMagnitude;

		[ShowIf("MagnitudeCalculationType", GameplayEffectMagnitudeCalculation.AttributeBased)]
		public AttributeBasedFloat AttributeBasedMagnitude;

		[ShowIf("MagnitudeCalculationType", GameplayEffectMagnitudeCalculation.CustomCalculationClass)]
		public CustomCalculationBasedFloat CustomMagnitude;

		[ShowIf("MagnitudeCalculationType", GameplayEffectMagnitudeCalculation.SetByCaller)]
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

		public bool Equals(GameplayEffectModifierMagnitude other)
		{
			if (other is null)
			{
				return false;
			}

			if (MagnitudeCalculationType != other.MagnitudeCalculationType)
			{
				return false;
			}

			switch (MagnitudeCalculationType)
			{
				case GameplayEffectMagnitudeCalculation.ScalableFloat:
					if (ScalableFloatMagnitude != other.ScalableFloatMagnitude)
					{
						return false;
					}
					break;
				case GameplayEffectMagnitudeCalculation.AttributeBased:
					if (AttributeBasedMagnitude != other.AttributeBasedMagnitude)
					{
						return false;
					}
					break;
				case GameplayEffectMagnitudeCalculation.CustomCalculationClass:
					if (CustomMagnitude != other.CustomMagnitude)
					{
						return false;
					}
					break;
				case GameplayEffectMagnitudeCalculation.SetByCaller:
					if (SetByCallerMagnitude.DataName != other.SetByCallerMagnitude.DataName)
					{
						return false;
					}
					break;
			}

			return true;
		}

		public static bool operator ==(GameplayEffectModifierMagnitude lhs, GameplayEffectModifierMagnitude rhs)
		{
			if (lhs is null)
			{
				return rhs is null;
			}

			return lhs.Equals(rhs);
		}

		public static bool operator !=(GameplayEffectModifierMagnitude lhs, GameplayEffectModifierMagnitude rhs)
		{
			return !(lhs == rhs);
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as GameplayEffectModifierMagnitude);
		}

		public override int GetHashCode()
		{
			switch (MagnitudeCalculationType)
			{
				case GameplayEffectMagnitudeCalculation.ScalableFloat:
					return HashCode.Combine(MagnitudeCalculationType, ScalableFloatMagnitude);
				case GameplayEffectMagnitudeCalculation.AttributeBased:
					return HashCode.Combine(MagnitudeCalculationType, AttributeBasedMagnitude);
				case GameplayEffectMagnitudeCalculation.CustomCalculationClass:
					return HashCode.Combine(MagnitudeCalculationType, CustomMagnitude);
				case GameplayEffectMagnitudeCalculation.SetByCaller:
					return HashCode.Combine(MagnitudeCalculationType, SetByCallerMagnitude);
				default:
					return HashCode.Combine(MagnitudeCalculationType);
			}
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
					GameplayModMagnitudeCalculation calcCDO = CustomMagnitude.CalculationClassMagnitude;
					Debug.Assert(calcCDO != null);
					captureDefs.AddRange(calcCDO.AttributeCaptureDefinitions);
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
	public record ConditionalGameplayEffect
	{
		public GameplayEffect EffectClass;
		public GameplayTagContainer RequiredSourceTags;

		public bool CanApply(in GameplayTagContainer sourceTags, float sourceLevel)
		{
			return sourceTags.HasAll(RequiredSourceTags);
		}

		public GameplayEffectSpecHandle CreateSpec(GameplayEffectContextHandle effectContext, float sourceLevel)
		{
			return EffectClass != null ? new GameplayEffectSpecHandle(new GameplayEffectSpec(EffectClass, effectContext, sourceLevel)) : new GameplayEffectSpecHandle();
		}
	}

	[Serializable]
	public struct GameplayEffectExecutionDefinition
	{
		public GameplayEffectExecutionCalculation CalculationClass;

		[ShowIf("@CalculationClass != null && CalculationClass.RequiresPassedInTags")]
		public GameplayTagContainer PassedInTags;

		[ShowIf("@CalculationClass != null && (CalculationClass.RelevantAttributesToCapture.Count > 0 || CalculationClass.InvalidScopedModifierAttributes.Count > 0 || CalculationClass.ValidTransientAggregatorIdentifiers.Count > 0)")]
		public List<GameplayEffectExecutionScopedModifierInfo> CalculationModifiers;
		
		[Tooltip("Other Gameplay Effects that will be applied to the target of this execution if the execution is successful. Note if no execution class is selected, these will always apply.")]
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
	public record GameplayModifierInfo
	{
		[HideLabel]
		public GameplayAttribute Attribute = new();
		public GameplayModOp ModifierOp = GameplayModOp.Additive;
		public GameplayEffectModifierMagnitude ModifierMagnitude = new();
		[ShowIf("@AbilitySystemGlobals.Instance.ShouldAllowGameplayModEvaluationChannels()")]
		public GameplayModEvaluationChannelSettings EvaluationChannelSettings;
		// used for Execution or MMC
		public GameplayTagRequirements SourceTags;
		public GameplayTagRequirements TargetTags;
	}

	[Serializable]
	public record InheritedTagContainer
	{
		[ReadOnly]
		public GameplayTagContainer CombinedTags = new();

		[LabelText("Add to Inherited")]
		public GameplayTagContainer Added = new();

		[LabelText("Remove from Inherited")]
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
				GameplayTagContainer removesThatApply = new(Removed.Filter(applyToContainer));
				GameplayTagContainer removeOverridesAdd = new(Added.FilterExact(Removed));
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

	public record ModifierSpec
	{
		public float EvaluatedMagnitude;
	}

	public class GameplayEffectModifiedAttribute
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
	}

	public class GameplayEffectAttributeCaptureSpec
	{
		public GameplayEffectAttributeCaptureDefinition BackingDefinition;
		public Aggregator AttributeAggregator;

		public GameplayEffectAttributeCaptureSpec(GameplayEffectAttributeCaptureDefinition definition)
		{
			BackingDefinition = definition;
			AttributeAggregator = new Aggregator();
		}

		public bool HasValidCapture()
		{
			return AttributeAggregator is not null;
		}

		public bool AttemptCalculateAttributeBaseValue(ref float basevalue)
		{
			if (AttributeAggregator is not null)
			{
				basevalue = AttributeAggregator.BaseValue;
				return true;
			}
			return false;
		}

		public bool AttemptCalculateAttributeMagnitude(AggregatorEvaluateParameters evalParams, ref float magnitude)
		{
			if (AttributeAggregator is not null)
			{
				magnitude = AttributeAggregator.Evaluate(evalParams);
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

		public bool ShouldRefreshLinkedAggregator(in Aggregator changedAggregator)
		{
			return BackingDefinition.Snapshot == false && (changedAggregator is null || changedAggregator == AttributeAggregator);
		}

		public void RegisterLinkedAggregatorCallbacks(ActiveGameplayEffectHandle handle)
		{
			if (BackingDefinition.Snapshot == false)
			{
				AttributeAggregator?.AddDependent(handle);
			}
		}

		public void UnregisterLinkedAggregatorCallbacks(ActiveGameplayEffectHandle handle)
		{
			AttributeAggregator?.RemoveDependent(handle);
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
			SourceAttributes = other.SourceAttributes;
			TargetAttributes = other.TargetAttributes;
			HasNonSnapshottedAttributes = other.HasNonSnapshottedAttributes;
		}

		public GameplayEffectAttributeCaptureSpec FindCaptureSpecByDefinition(GameplayEffectAttributeCaptureDefinition definition, bool onlyIncludeValidCapture)
		{
			bool sourceAttribute = definition.AttributeSource == GameplayEffectAttributeCaptureSource.Source;
			List<GameplayEffectAttributeCaptureSpec> attributeArray = sourceAttribute ? SourceAttributes : TargetAttributes;

			GameplayEffectAttributeCaptureSpec matchingSpec = attributeArray.Find((element) => element.BackingDefinition == definition);

			if (matchingSpec is not null && onlyIncludeValidCapture && !matchingSpec.HasValidCapture())
			{
				matchingSpec = null;
			}

			return matchingSpec;
		}

		public bool HasValidCapturedAttributes(in List<GameplayEffectAttributeCaptureDefinition> captureDefsToCheck)
		{
			bool hasValid = true;

			foreach (GameplayEffectAttributeCaptureDefinition curDef in captureDefsToCheck)
			{
				GameplayEffectAttributeCaptureSpec captureSpec = FindCaptureSpecByDefinition(curDef, true);
				if (captureSpec is null)
				{
					hasValid = false;
					break;
				}
			}

			return hasValid;
		}

		public void CaptureAttributes(AbilitySystemComponent abilitySystemComponent, GameplayEffectAttributeCaptureSource captureSource)
		{
			if (abilitySystemComponent != null)
			{
				bool sourceComponent = captureSource == GameplayEffectAttributeCaptureSource.Source;
				List<GameplayEffectAttributeCaptureSpec> attributeArray = sourceComponent ? SourceAttributes : TargetAttributes;
				foreach (GameplayEffectAttributeCaptureSpec curCaptureSpec in attributeArray)
				{
					abilitySystemComponent.CaptureAttributeForGameplayEffect(curCaptureSpec);
				}
			}
		}

		public void AddCaptureDefinition(GameplayEffectAttributeCaptureDefinition captureDefinition)
		{
			bool sourceAttribute = captureDefinition.AttributeSource == GameplayEffectAttributeCaptureSource.Source;
			List<GameplayEffectAttributeCaptureSpec> attributeArray = sourceAttribute ? SourceAttributes : TargetAttributes;

			if (!attributeArray.Exists((element) => element.BackingDefinition == captureDefinition))
			{
				attributeArray.Add(new GameplayEffectAttributeCaptureSpec(captureDefinition));

				if (!captureDefinition.Snapshot)
				{
					HasNonSnapshottedAttributes = true;
				}
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

		public float Level { get; private set; }
		public GameplayEffectContextHandle EffectContext { get; private set; } = new();

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
			ModifiedAttributes = other.ModifiedAttributes;
			CapturedRelevantAttributes = other.CapturedRelevantAttributes;
			TargetEffectSpecs = other.TargetEffectSpecs;
			Duration = other.Duration;
			Period = other.Period;
			CapturedSourceTags = other.CapturedSourceTags;
			CapturedTargetTags = other.CapturedTargetTags;
			DynamicGrantedTags = other.DynamicGrantedTags;
			DynamicAssetTags = other.DynamicAssetTags;
			Modifiers = other.Modifiers;
			StackCount = other.StackCount;
			CompletedSourceAttributeCapture = other.CompletedSourceAttributeCapture;
			CompletedTargetAttributeCapture = other.CompletedTargetAttributeCapture;
			DurationLocked = other.DurationLocked;
			GrantedAbilitySpecs = other.GrantedAbilitySpecs;
			SetByCallerNameMagnitudes = other.SetByCallerNameMagnitudes;
			SetByCallerTagMagnitudes = other.SetByCallerTagMagnitudes;
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

		public void SetDuration(float newDuration, bool lockDuration)
		{
			if (!DurationLocked)
			{
				Duration = newDuration;
				DurationLocked = lockDuration;
			}
		}

		public void CaptureDataFromSource(bool skipRecaptureSourceActorTags = false)
		{
			if (!skipRecaptureSourceActorTags)
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
			GameplayEffectModifiedAttribute modifiedAttribute = new()
			{
				Attribute = attribute
			};
			ModifiedAttributes.Add(modifiedAttribute);
			return modifiedAttribute;
		}

		public GameplayEffectModifiedAttribute GetModifiedAttribute(in GameplayAttribute attribute)
		{
			foreach (GameplayEffectModifiedAttribute modifiedAttribute in ModifiedAttributes)
			{
				if (modifiedAttribute.Attribute == attribute)
				{
					return modifiedAttribute;
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

		public void CaptureAttributeDataFromTarget(AbilitySystemComponent targetAbilitySystemComponent)
		{
			CapturedRelevantAttributes.CaptureAttributes(targetAbilitySystemComponent, GameplayEffectAttributeCaptureSource.Target);
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

		public override string ToString()
		{
			return Def.name;
		}
	}

	public class ActiveGameplayEffect : IEquatable<ActiveGameplayEffect>
	{
		public ActiveGameplayEffectHandle Handle;
		public GameplayEffectSpec Spec;
		public List<GameplayAbilitySpecHandle> GrantedAbilityHandles = new();
		public bool IsInhibited;
		public float StartWorldTime;
		public bool IsPendingRemove;
		public TimerHandle PeriodHandle = new();
		public TimerHandle DurationHandle = new();
		public ActiveGameplayEffectEvents EventSet = new();

		public float Duration => Spec.Duration;
		public float Period => Spec.Period;
		public float EndTime => Duration == GameplayEffectConstants.InfiniteDuration ? -1 : Duration + StartWorldTime;

		public ActiveGameplayEffect()
		{

		}

		public ActiveGameplayEffect(ActiveGameplayEffectHandle handle, in GameplayEffectSpec spec)
		{
			Handle = handle;
			Spec = spec;
		}

		public ActiveGameplayEffect(in ActiveGameplayEffect other)
		{
			Handle = other.Handle;
			Spec = other.Spec;
			GrantedAbilityHandles = other.GrantedAbilityHandles;
			IsInhibited = other.IsInhibited;
			StartWorldTime = other.StartWorldTime;
			IsPendingRemove = other.IsPendingRemove;
			PeriodHandle = other.PeriodHandle;
			DurationHandle = other.DurationHandle;
			EventSet = other.EventSet;
		}

		public float GetTimeRemaining(float worldTime)
		{
			return Duration == GameplayEffectConstants.InfiniteDuration ? -1 : Duration - (worldTime - StartWorldTime);
		}

		public bool Equals(ActiveGameplayEffect other)
		{
			if (other is null)
			{
				return false;
			}

			return Handle == other.Handle;
		}

		public static bool operator ==(ActiveGameplayEffect lhs, ActiveGameplayEffect rhs)
		{
			if (lhs is null)
			{
				return rhs is null;
			}

			return lhs.Equals(rhs);
		}

		public static bool operator !=(ActiveGameplayEffect lhs, ActiveGameplayEffect rhs)
		{
			return !(lhs == rhs);
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as ActiveGameplayEffect);
		}

		public override int GetHashCode()
		{
			return Handle.GetHashCode();
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
		public OnGivenActiveGameplayEffectRemoved OnActiveGameplayEffectRemovedDelegate = new();
		public Dictionary<GameplayAttribute, Aggregator> AttributeAggregatorMap = new();
		private List<ActiveGameplayEffect> GameplayEffects_Internal = new();
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
						Debug.LogWarning($"Application of {spec} denied (StackLimit)");
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

				GameplayEffect GEDef = existingSpec.Def;

				if (GEDef.StackDurationRefreshPolicy == GameplayEffectStackingDurationPolicy.NeverRefresh)
				{
					setDuration = false;
				}
				else
				{
					RestartActiveGameplayEffectDuration(existingStackableGE);
				}

				if (GEDef.StackPeriodResetPolicy == GameplayEffectStackingPeriodPolicy.NeverReset)
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

			appliedEffectSpec.CapturedTargetTags.ActorTags.Reset();
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

			AddCustomMagnitudeExternalDependencies(effect);

			bool active = effectDef.OnAddedToActiveContainer(this, effect);
			effect.IsInhibited = true;

			ActiveGameplayEffectHandle effectHandle = effect.Handle;
			Owner.SetActiveGameplayEffectInhibit(effectHandle, !active);
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

		public void GetAllActiveGameplayEffectSpecs(List<GameplayEffectSpec> outSpecCopies)
		{
			foreach (ActiveGameplayEffect activeEffect in GameplayEffects_Internal)
			{
				outSpecCopies.Add(activeEffect.Spec);
			}
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

				RemoveCustomMagnitudeExternalDependencies(effect);
			}
			else
			{
				Debug.LogWarning($"InternalOnActiveGameplayEffectRemoved called with no GameplayEffect: {effect.Handle}");
			}

			effect.EventSet.OnEffectRemoved?.Invoke(gameplayEffectRemovalInfo);

			OnActiveGameplayEffectRemovedDelegate?.Invoke(effect);
		}

		private void AddCustomMagnitudeExternalDependencies(ActiveGameplayEffect effect)
		{
			// GameplayEffect GEDef = effect.Spec.Def;
			// if (GEDef != null)
			// {
			// 	foreach(GameplayModifierInfo curMod in GEDef.Modifiers
			// 	{
			// 		GameplayModMagnitudeCalculation modCalcClass= curMod.ModifierMagnitude.GetCustomMagnitudeCalculationClass();
			// 		if (modCalcClass != null)
			// 		{
			// 			curMod.ModifierMagnitude.CalculationClassMagnitude = modCalcClass;
			// 		}
			// 	}
			// }
		}

		private void RemoveCustomMagnitudeExternalDependencies(ActiveGameplayEffect effect)
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

			specToUse.CapturedTargetTags.ActorTags.Reset();
			Owner.GetOwnedGameplayTags(specToUse.CapturedTargetTags.ActorTags);

			specToUse.CalculateModifierMagnitudes();

			bool modifierSuccessfullyExecuted = false;

			for (int i = 0; i < specToUse.Modifiers.Count; i++)
			{
				GameplayModifierInfo modDef = specToUse.Def.Modifiers[i];

				bool useModifierTagRequirementsOnAllGameplayEffects = true;
				if (useModifierTagRequirementsOnAllGameplayEffects)
				{
					if (!modDef.SourceTags.IsEmpty() && !modDef.SourceTags.RequirementsMet(spec.CapturedSourceTags.ActorTags))
					{
						continue;
					}

					if (!modDef.TargetTags.IsEmpty() && !modDef.TargetTags.RequirementsMet(spec.CapturedTargetTags.ActorTags))
					{
						continue;
					}
				}

				GameplayModifierEvaluatedData evalData = new(modDef.Attribute, modDef.ModifierOp, specToUse.GetModifierMagnitude(i, true));
				modifierSuccessfullyExecuted |= InternalExecuteMod(specToUse, evalData);
			}

			List<GameplayEffectSpecHandle> conditionalEffectSpecs = new();

			foreach (GameplayEffectExecutionDefinition curExecDef in specToUse.Def.Executions)
			{
				bool runConditionalEffects = true;

				if (curExecDef.CalculationClass != null)
				{
					GameplayEffectCustomExecutionParams executionParams = new GameplayEffectCustomExecutionParams();
					GameplayEffectCustomExecutionOutput executionOutput = new GameplayEffectCustomExecutionOutput();
					curExecDef.CalculationClass.Execute(executionParams, out executionOutput);

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
				Debug.LogWarning($"{Owner.name} does not have attribute {modEvalData.Attribute}. Skipping modifer");
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
			if (Owner == null)
			{
				Debug.LogWarning($"ActiveGameplayEffectsContainer.SetAttributeBaseValue: This ActiveGameplayEffectsContainer has an invalid owner. Unable to set attribute {attribute.AttributeName} to {newBaseValue}");
				return;
			}

			AttributeSet set = Owner.GetAttributeSubobject(attribute.GetAttributeSetClass());
			if (!set)
			{
				Debug.LogWarning($"ActiveGameplayEffectsContainer.SetAttributeBaseValue: Unable to get attribute set for attribute {attribute.AttributeName}");
				return;
			}

			float oldBaseValue = 0;

			set.PreAttributeBaseChange(attribute, ref newBaseValue);

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
				aggregator.SetBaseValue(newBaseValue);
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

			Owner.UpdateTagMap(effect.Spec.Def.GrantedTags, -1);
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
			return TimerManager.Instance.GetTimeSeconds();
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

		public List<float> GetActiveEffectsTimeRemaining(in GameplayEffectQuery query)
		{
			float currentTime = GetWorldTime();

			List<float> returnList = new();

			foreach (ActiveGameplayEffect effect in GameplayEffects_Internal)
			{
				if (!query.Matches(effect))
				{
					continue;
				}

				float elapsed = currentTime - effect.StartWorldTime;
				float duration = effect.Duration;

				returnList.Add(duration - elapsed);
			}

			return returnList;
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

		public int GetActiveEffectCount(in GameplayEffectQuery query, bool enforceOnGoingCheck = true)
		{
			int count = 0;

			foreach (ActiveGameplayEffect effect in GameplayEffects_Internal)
			{
				if (!effect.IsInhibited || !enforceOnGoingCheck)
				{
					if (query.Matches(effect))
					{
						count += effect.Spec.StackCount;
					}
				}
			}

			return count;
		}

		public void OnStackCountChange(ActiveGameplayEffect activeEffect, int oldStackCount, int newStackCount)
		{
			Debug.Log($"OnStackCountChange: {activeEffect}. OldStackCount: {oldStackCount}. NewStackCount: {newStackCount}");

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

		public void CleanupAttributeAggregator(in GameplayAttribute attribute)
		{
			if (AttributeAggregatorMap.TryGetValue(attribute, out Aggregator aggregator))
			{
				aggregator.OnDirty = null;
				aggregator.OnDirtyRecursive = null;

				AttributeAggregatorMap.Remove(attribute);
			}
		}

		public void OnAttributeAggregatorDirty(Aggregator aggregator, GameplayAttribute attribute, bool fromRecursiveCall = false)
		{
			Debug.Assert(AttributeAggregatorMap.ContainsKey(attribute));

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

		public void SetActiveGameplayEffectLevel(ActiveGameplayEffectHandle handle, int newLevel)
		{
			foreach (ActiveGameplayEffect effect in GameplayEffects_Internal)
			{
				if (effect.Handle == handle)
				{
					if (effect.Spec.Level != newLevel)
					{
						effect.Spec.SetLevel(newLevel);

						effect.Spec.CalculateModifierMagnitudes();
						UpdateAllAggregatorModMagnitudes(effect);
					}
					break;
				}
			}
		}

		public void DebugCyclicAggregatorBroadcasts(Aggregator triggeredAggregator)
		{
			foreach (KeyValuePair<GameplayAttribute, Aggregator> it in AttributeAggregatorMap)
			{
				Aggregator aggregator = it.Value;
				GameplayAttribute attribute = it.Key;

				if (aggregator != null)
				{
					if (aggregator == triggeredAggregator)
					{
						Debug.LogWarning($" Attribute {attribute} was the triggered aggregator ({Owner.name})");
					}
					else if (aggregator.BroadcastingDirtyCount > 0)
					{
						Debug.LogWarning($" Attribute {attribute} is broadcasting dirty ({Owner.name})");
					}
					else
					{
						continue;
					}

					foreach (ActiveGameplayEffectHandle handle in aggregator.Dependents)
					{
						AbilitySystemComponent asc = handle.OwningAbilitySystemComponent;
						if (asc != null)
						{
							Debug.LogWarning($"  Dependant ({asc.name}) GE: {asc.GetGameplayEffectDefForHandle(handle).name}");
						}
					}
				}
			}
		}
	}

	[CreateAssetMenu(fileName = "GameplayEffect", menuName = "GameplayAbilities/GameplayEffect", order = 0)]
	public class GameplayEffect : ScriptableObject
	{
		[FoldoutGroup("Duration")]
		public GameplayEffectDurationType DurationPolicy;

		[FoldoutGroup("Duration")]
		[ShowIf("DurationPolicy", GameplayEffectDurationType.HasDuration)]
		public GameplayEffectModifierMagnitude DurationMagnitude = new();

		[FoldoutGroup("Duration")]
		[HideIf("DurationPolicy", GameplayEffectDurationType.Instant)]
		public ScalableFloat Period = new();

		[FoldoutGroup("Duration")]
		[ShowIf("@Period.Value != 0")]
		public bool ExecutePeriodicEffectOnApplication = true;

		[FoldoutGroup("Duration")]
		[ShowIf("@Period.Value != 0")]
		public GameplayEffectPeriodInhibitionRemovedPolicy PeriodInhibitionPolicy;

		[FoldoutGroup("GameplayEffect")]
		[LabelText("Components")]
		[ListDrawerSettings
		(
			CustomAddFunction = "AddComponentMenu",
			CustomRemoveElementFunction = "RemoveComponent"
		)]
		[InlineEditor(InlineEditorObjectFieldModes.Foldout)]
		[SerializeReference]
		public List<GameplayEffectComponent> GEComponents = new();

		[FoldoutGroup("GameplayEffect")]
		[ListDrawerSettings(ShowIndexLabels = true)]
		public List<GameplayModifierInfo> Modifiers = new();

		[FoldoutGroup("GameplayEffect")]
		public List<GameplayEffectExecutionDefinition> Executions = new();

		[FoldoutGroup("GameplayEffect")]
		[HideInInspector]
		[Obsolete("Conditional Gameplay Effects is deprecated. Use AdditionalEffectsGameplayEffectComponent instead")]
		public List<ConditionalGameplayEffect> ConditionalGameplayEffects = new();

		[FoldoutGroup("Expiration")]
		[HideInInspector]
		[Obsolete("Premature Expiration Effect Classes is deprecated. Use AdditionalEffectsGameplayEffectComponent instead")]
		public List<GameplayEffect> PrematureExpirationEffectClasses = new();

		[FoldoutGroup("Expiration")]
		[HideInInspector]
		[Obsolete("Routine Expiration Effect Classes is deprecated. Use AdditionalEffectsGameplayEffectComponent instead")]
		public List<GameplayEffect> RoutineExpirationEffectClasses = new();

		[FoldoutGroup("GameplayEffect")]
		[LabelText("Gameplay Effect Asset Tags")]
		[HideInInspector]
		[Obsolete]
		public InheritedTagContainer InheritableGameplayEffectTags = new();

		[FoldoutGroup("GameplayEffect")]
		[LabelText("Granted Tags")]
		[HideInInspector]
		[Obsolete]
		public InheritedTagContainer InheritableOwnedTagsContainer = new();

		[FoldoutGroup("GameplayEffect")]
		[LabelText("Granted Blocked Ability Tags")]
		[HideInInspector]
		[Obsolete]
		public InheritedTagContainer InheritableBlockedAbilityTagsContainer;

		[FoldoutGroup("GameplayEffect")]
		[LabelText("Ongoing Tag Requirements")]
		[HideInInspector]
		[Obsolete]
		public GameplayTagRequirements OngoingTagRequirements;

		[FoldoutGroup("GameplayEffect")]
		[LabelText("Application Tag Requirements")]
		[HideInInspector]
		[Obsolete]
		public GameplayTagRequirements ApplicationTagRequirements;

		[FoldoutGroup("GameplayEffect")]
		[HideInInspector]
		[Obsolete]
		public GameplayTagRequirements RemovalTagRequirements;

		[FoldoutGroup("GameplayEffect")]
		[HideInInspector]
		[Obsolete]
		public InheritedTagContainer RemoveGameplayEffectWithTags;

		[FoldoutGroup("GameplayEffect")]
		[HideInInspector]
		[Obsolete]
		public GameplayTagRequirements GrantedApplicationImmunityTags;

		[FoldoutGroup("GameplayEffect")]
		[HideInInspector]
		[Obsolete]
		public GameplayEffectQuery GrantedApplicationImmunityQuery;

		[FoldoutGroup("GameplayEffect")]
		[HideInInspector]
		[Obsolete]
		public bool HasGrantedApplicationImmunityTags;

		[FoldoutGroup("GameplayEffect")]
		[HideInInspector]
		[Obsolete]
		public GameplayEffectQuery RemoveGameplayEffectQuery;

		[HideInInspector]
		[Obsolete]
		public bool HasRemoveGameplayEffectQuery;

		[FoldoutGroup("Stacking")]
		public GameplayEffectStackingType StackingType;

		[FoldoutGroup("Stacking")]
		[HideIf("StackingType", GameplayEffectStackingType.None)]
		public int StackLimitCount;

		[FoldoutGroup("Stacking")]
		[HideIf("StackingType", GameplayEffectStackingType.None)]
		public GameplayEffectStackingDurationPolicy StackDurationRefreshPolicy;

		[FoldoutGroup("Stacking")]
		[HideIf("StackingType", GameplayEffectStackingType.None)]
		public GameplayEffectStackingPeriodPolicy StackPeriodResetPolicy;

		[FoldoutGroup("Stacking")]
		[HideIf("StackingType", GameplayEffectStackingType.None)]
		public GameplayEffectStackingExpirationPolicy StackExpirationPolicy;

		[FoldoutGroup("Stacking")]
		[HideIf("StackingType", GameplayEffectStackingType.None)]
		public List<GameplayEffect> OverflowEffects = new();

		[FoldoutGroup("Stacking")]
		public bool DenyOverflowApplication;

		[FoldoutGroup("Stacking")]
		[ShowIf("DenyOverflowApplication", true)]
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

#if UNITY_EDITOR
		protected virtual void OnValidate()
		{
			ConvertAssetTagsComponent();
			ConvertRemoveOtherComponent();
			ConvertAdditionalEffectsComponent();
			ConvertTagRequirementsComponent();
			ConvertTargetTagsComponent();

			OnGameplayEffectChanged();
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
							AssetDatabase.Refresh();
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
					AssetDatabase.RemoveObjectFromAsset(component);
					// 销毁组件对象
					Undo.DestroyObjectImmediate(component);
					// 标记资源已修改
					EditorUtility.SetDirty(this);
					// 保存更改
					AssetDatabase.SaveAssets();
					AssetDatabase.Refresh();

					OnValidate();
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

		public bool CanApply(in ActiveGameplayEffectsContainer activeGEContainer, in GameplayEffectSpec GESpec)
		{
			foreach (GameplayEffectComponent GEComponent in GEComponents)
			{
				if (!GEComponent.CanGameplayEffectApply(activeGEContainer, GESpec))
				{
					Debug.LogWarning($"{GESpec.Def.name} could not apply. Blocked by {GEComponent.name}");
					return false;
				}
			}
			return true;
		}

		public bool OnAddedToActiveContainer(ActiveGameplayEffectsContainer activeGEContainer, ActiveGameplayEffect activeGE)
		{
			bool shouldBeActive = true;
			foreach (GameplayEffectComponent GEComponent in GEComponents)
			{
				if (GEComponent != null)
				{
					shouldBeActive = GEComponent.OnActiveGameplayEffectAdded(activeGEContainer, activeGE);
				}
			}

			return shouldBeActive;
		}

		public void OnExecuted(ActiveGameplayEffectsContainer activeGEContainer, GameplayEffectSpec GESpec)
		{
			foreach (GameplayEffectComponent GEComponent in GEComponents)
			{
				if (GEComponent != null)
				{
					GEComponent.OnGameplayEffectExecuted(activeGEContainer, GESpec);
				}
			}
		}

		public void OnApplied(ActiveGameplayEffectsContainer activeGEContainer, GameplayEffectSpec GESpec)
		{
			foreach (GameplayEffectComponent GEComponent in GEComponents)
			{
				if (GEComponent != null)
				{
					GEComponent.OnGameplayEffectApplied(activeGEContainer, GESpec);
				}
			}

			Debug.Log($"Applied: {GESpec.Def.name}");
		}

		public T GetComponent<T>() where T : GameplayEffectComponent
		{
			foreach (GameplayEffectComponent GEComponent in GEComponents)
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
			T component = new();
			GEComponents.Add(component);
			return component;
		}

		public T GetOrAddComponent<T>() where T : GameplayEffectComponent, new()
		{
			T component = GetComponent<T>();
			if (component == null)
			{
				component = new T();
				GEComponents.Add(component);
			}
			return component;
		}

		private void ConvertAssetTagsComponent()
		{
			AssetTagsGameplayEffectComponent assetTagsComponent = GetComponent<AssetTagsGameplayEffectComponent>();
			if (assetTagsComponent != null)
			{
				InheritableGameplayEffectTags = assetTagsComponent.ConfiguredAssetTags;
				// InheritableGameplayEffectTags.UpdateInheritedTagProperties(archetype.InheritableGameplayEffectTags);
			}
		}

		private void ConvertRemoveOtherComponent()
		{
			RemoveOtherGameplayEffectComponent removeOtherComponent = GetComponent<RemoveOtherGameplayEffectComponent>();
			if (removeOtherComponent != null && !removeOtherComponent.RemoveGameplayEffectsQueries.IsEmpty())
			{
				RemoveGameplayEffectQuery = removeOtherComponent.RemoveGameplayEffectsQueries.Last();
			}
		}

		private void ConvertAdditionalEffectsComponent()
		{
			AdditionalEffectsGameplayEffectComponent additionalEffectsComponent = GetComponent<AdditionalEffectsGameplayEffectComponent>();
			if (additionalEffectsComponent != null)
			{
				ConditionalGameplayEffects = additionalEffectsComponent.OnApplicationGameplayEffects;
				PrematureExpirationEffectClasses = additionalEffectsComponent.OnCompletePrematurely;
				RoutineExpirationEffectClasses = additionalEffectsComponent.OnCompleteNormal;
			}
		}
		private void ConvertTagRequirementsComponent()
		{
			TargetTagRequirementsGameplayEffectComponent tagRequirementsComponent = GetComponent<TargetTagRequirementsGameplayEffectComponent>();
			if (tagRequirementsComponent != null)
			{
				ApplicationTagRequirements = tagRequirementsComponent.ApplicationTagRequirements;
				OngoingTagRequirements = tagRequirementsComponent.OngoingTagRequirements;
				RemovalTagRequirements = tagRequirementsComponent.RemovalTagRequirements;
			}
		}

		private void ConvertTargetTagsComponent()
		{
			TargetTagsGameplayEffectComponent targetTagsComponent = GetComponent<TargetTagsGameplayEffectComponent>();
			if (targetTagsComponent != null)
			{
				InheritableOwnedTagsContainer = targetTagsComponent.ConfiguredTargetTagsChanges;
				// InheritableOwnedTagsContainer.UpdateInheritedTagProperties(archetype.InheritableOwnedTagsContainer);
			}
		}
	}
}