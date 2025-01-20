using GameplayTags;
using System.Collections.Generic;
using UnityEngine;

namespace GameplayAbilities
{
	public class AggregatorEvaluateParameters
	{
		public GameplayTagContainer SourceTags;
		public GameplayTagContainer TargetTags;
		public List<ActiveGameplayEffectHandle> IgnoreHandles = new();
		public GameplayTagContainer AppliedSourceTagFilter = new();
		public GameplayTagContainer AppliedTargetTagFilter = new();
	}

	public class AggregatorMod
	{
		public GameplayTagRequirements SourceTagReqs;
		public GameplayTagRequirements TargetTagReqs;
		public float EvaluatedMagnitude;
		public float StackCount;
		public ActiveGameplayEffectHandle ActiveHandle;

		public bool Qualifies => IsQualified;

		public void UpdateQualifies(in AggregatorEvaluateParameters parameters)
		{
			GameplayTagContainer emptyTagContainer = new();
			GameplayTagContainer srcTags = parameters.SourceTags is not null ? parameters.SourceTags : emptyTagContainer;
			GameplayTagContainer tgtTags = parameters.TargetTags is not null ? parameters.TargetTags : emptyTagContainer;
			bool sourceMet = SourceTagReqs is null || SourceTagReqs.RequirementsMet(srcTags);
			bool targetMet = TargetTagReqs is null || TargetTagReqs.RequirementsMet(tgtTags);

			bool sourceFilterMet = parameters.AppliedSourceTagFilter.Num == 0;
			bool targetFilterMet = parameters.AppliedTargetTagFilter.Num == 0;

			if (ActiveHandle.IsValid())
			{
				foreach (var handleToIgnore in parameters.IgnoreHandles)
				{
					if (handleToIgnore == ActiveHandle)
					{
						IsQualified = false;
						return;
					}
				}
			}

			AbilitySystemComponent handleComponent = ActiveHandle.OwningAbilitySystemComponent;
			if (handleComponent != null)
			{
				if (!sourceFilterMet)
				{
					GameplayTagContainer sourceTags = handleComponent.GetGameplayEffectSourceTagsFromHandle(ActiveHandle);
					sourceFilterMet = sourceTags is not null && sourceTags.HasAll(parameters.AppliedSourceTagFilter);
				}

				if (!targetFilterMet)
				{
					GameplayTagContainer targetTags = handleComponent.GetGameplayEffectTargetTagsFromHandle(ActiveHandle);
					targetFilterMet = targetTags is not null && targetTags.HasAll(parameters.AppliedTargetTagFilter);
				}
			}

			IsQualified = sourceMet && targetMet && sourceFilterMet && targetFilterMet;
		}

		private bool IsQualified;
	}

	public class AggregatorModChannel
	{
		public Dictionary<GameplayModOp, List<AggregatorMod>> Mods = new()
		{
			{ GameplayModOp.Override, new() },
			{ GameplayModOp.Additive, new() },
			{ GameplayModOp.Multiply, new() },
			{ GameplayModOp.Divide, new() }
		};

		public float EvaluateWithBase(float inlineBaseValue, in AggregatorEvaluateParameters parameters)
		{
			foreach (AggregatorMod mod in Mods[GameplayModOp.Override])
			{
				if (mod.Qualifies)
				{
					return mod.EvaluatedMagnitude;
				}
			}

			float additive = SumMods(Mods[GameplayModOp.Additive], GameplayEffectUtilities.GetModifierBiasByModifierOp(GameplayModOp.Additive), parameters);
			float multiplicitive = SumMods(Mods[GameplayModOp.Multiply], GameplayEffectUtilities.GetModifierBiasByModifierOp(GameplayModOp.Multiply), parameters);
			float division = SumMods(Mods[GameplayModOp.Divide], GameplayEffectUtilities.GetModifierBiasByModifierOp(GameplayModOp.Divide), parameters);

			if (Mathf.Approximately(division, 0))
			{
				Debug.LogWarning("Division summation was 0.0f in AggregatorModChannel.");
				division = 1;
			}

			return (inlineBaseValue + additive) * multiplicitive / division;
		}

		public void AddMod(float evaluatedMagnitude, GameplayModOp modOp, in GameplayTagRequirements sourceTagReqs, in GameplayTagRequirements targetTagReqs, in ActiveGameplayEffectHandle activeHandle)
		{
			List<AggregatorMod> modList = Mods[modOp];

			AggregatorMod newMod = new()
			{
				SourceTagReqs = sourceTagReqs,
				TargetTagReqs = targetTagReqs,
				EvaluatedMagnitude = evaluatedMagnitude,
				StackCount = 0,
				ActiveHandle = activeHandle
			};

			modList.Add(newMod);
		}

		public void RemoveModsWithActiveHandle(ActiveGameplayEffectHandle handle)
		{
			foreach (GameplayModOp modOp in Mods.Keys)
			{
				Mods[modOp].RemoveAll(mod => mod.ActiveHandle == handle);
			}
		}

		public static float SumMods(in List<AggregatorMod> mods, float bias, in AggregatorEvaluateParameters parameters)
		{
			float sum = bias;
			foreach (AggregatorMod mod in mods)
			{
				if (mod.Qualifies)
				{
					sum += mod.EvaluatedMagnitude - bias;
				}
			}
			return sum;
		}

		public void AddModsFrom(in AggregatorModChannel other)
		{
			foreach (GameplayModOp modOp in other.Mods.Keys)
			{
				Mods[modOp].AddRange(other.Mods[modOp]);
			}
		}

		public void UpdateQualifiesOnAllMods(in AggregatorEvaluateParameters parameters)
		{
			foreach (GameplayModOp modOp in Mods.Keys)
			{
				foreach (AggregatorMod mod in Mods[modOp])
				{
					mod.UpdateQualifies(parameters);
				}
			}
		}
	}

	public class AggregatorModChannelContainer
	{
		public Dictionary<GameplayModEvaluationChannel, AggregatorModChannel> ModChannelsMap = new();

		public AggregatorModChannel FindOrAddModChannel(GameplayModEvaluationChannel channel)
		{
			if (!ModChannelsMap.TryGetValue(channel, out AggregatorModChannel foundChannel))
			{
				foundChannel = new AggregatorModChannel();
				ModChannelsMap[channel] = foundChannel;
			}
			return foundChannel;
		}

		public float EvaluateWithBase(float inlineBaseValue, in AggregatorEvaluateParameters parameters)
		{
			float computedValue = inlineBaseValue;

			foreach (var channelEntry in ModChannelsMap)
			{
				AggregatorModChannel curChannel = channelEntry.Value;
				computedValue = curChannel.EvaluateWithBase(computedValue, parameters);
			}

			return computedValue;
		}

		public void EvaluateQualificationForAllMods(in AggregatorEvaluateParameters parameters)
		{
			foreach (var channelEntry in ModChannelsMap)
			{
				AggregatorModChannel curChannel = channelEntry.Value;
				curChannel.UpdateQualifiesOnAllMods(parameters);
			}
		}

		public void AddModsFrom(in AggregatorModChannelContainer other)
		{
			foreach (var sourceChannelEntry in other.ModChannelsMap)
			{
				GameplayModEvaluationChannel sourceChannelEnum = sourceChannelEntry.Key;
				AggregatorModChannel sourceChannel = sourceChannelEntry.Value;

				AggregatorModChannel targetChannel = FindOrAddModChannel(sourceChannelEnum);
				targetChannel.AddModsFrom(sourceChannel);
			}
		}

		public void RemoveAggregatorMod(in ActiveGameplayEffectHandle activeHandle)
		{
			if (activeHandle.IsValid())
			{
				foreach (var channelEntry in ModChannelsMap)
				{
					AggregatorModChannel curChannel = channelEntry.Value;
					curChannel.RemoveModsWithActiveHandle(activeHandle);
				}
			}
		}
	}

	public class Aggregator
	{
		public delegate void OnAggregatorDirty(Aggregator aggregator);

		public OnAggregatorDirty OnDirty;
		public OnAggregatorDirty OnDirtyRecursive;

		public float BaseValue;
		public AggregatorModChannelContainer ModChannels = new();
		public List<ActiveGameplayEffectHandle> Dependents = new();
		public int BroadcastingDirtyCount;

		public Aggregator(float baseValue = 0)
		{
			BaseValue = baseValue;
			BroadcastingDirtyCount = 0;
		}

		public static float StaticExecModOnBaseValue(float baseValue, GameplayModOp modifierOp, float evaluatedMagnitude)
		{
			switch (modifierOp)
			{
				case GameplayModOp.Override:
					baseValue = evaluatedMagnitude;
					break;
				case GameplayModOp.Additive:
					baseValue += evaluatedMagnitude;
					break;
				case GameplayModOp.Multiply:
					baseValue *= evaluatedMagnitude;
					break;
				case GameplayModOp.Divide:
					baseValue /= evaluatedMagnitude;
					break;
			}
			return baseValue;
		}

		public float Evaluate(in AggregatorEvaluateParameters parameters)
		{
			EvaluateQualificationForAllMods(parameters);
			return ModChannels.EvaluateWithBase(BaseValue, parameters);
		}

		public float EvaluateWithBase(float inlineBaseValue, in AggregatorEvaluateParameters parameters)
		{
			EvaluateQualificationForAllMods(parameters);
			return ModChannels.EvaluateWithBase(inlineBaseValue, parameters);
		}

		public float EvaluateBonus(in AggregatorEvaluateParameters parameters)
		{
			return Evaluate(parameters) - BaseValue;
		}

		public void EvaluateQualificationForAllMods(in AggregatorEvaluateParameters parameters)
		{
			ModChannels.EvaluateQualificationForAllMods(parameters);
		}

		public void AddAggregatorMod(float evaluatedMagnitude, GameplayModOp modifierOp, GameplayModEvaluationChannel modifierChannel, in GameplayTagRequirements sourceTagReqs, in GameplayTagRequirements targetTagReqs, ActiveGameplayEffectHandle activeHandle)
		{
			AggregatorModChannel modChannelToAddTo = ModChannels.FindOrAddModChannel(modifierChannel);
			modChannelToAddTo.AddMod(evaluatedMagnitude, modifierOp, sourceTagReqs, targetTagReqs, activeHandle);
			
			BroadcastOnDirty();
		}

		public void RemoveAggregatorMod(ActiveGameplayEffectHandle activeHandle)
		{
			ModChannels.RemoveAggregatorMod(activeHandle);
			BroadcastOnDirty();
		}

		public void UpdateAggregatorMod(ActiveGameplayEffectHandle activeHandle, GameplayAttribute attribute, GameplayEffectSpec spec, ActiveGameplayEffectHandle handle)
		{
			ModChannels.RemoveAggregatorMod(activeHandle);

			for (int modIdx = 0; modIdx < spec.Modifiers.Count; modIdx++)
			{
				GameplayModifierInfo modDef = spec.Def.Modifiers[modIdx];
				if (modDef.Attribute == attribute)
				{
					AggregatorModChannel modChannel = ModChannels.FindOrAddModChannel(modDef.EvaluationChannelSettings.EvaluationChannel);
					modChannel.AddMod(spec.GetModifierMagnitude(modIdx, true), modDef.ModifierOp, modDef.SourceTags, modDef.TargetTags, handle);
				}
			}

			BroadcastOnDirty();
		}

		public void AddModsFrom(in Aggregator sourceAggregator)
		{
			ModChannels.AddModsFrom(sourceAggregator.ModChannels);
		}

		public void AddDependent(ActiveGameplayEffectHandle handle)
		{
			Dependents.Add(handle);
		}

		public void RemoveDependent(ActiveGameplayEffectHandle handle)
		{
			Dependents.Remove(handle);
		}

		public void TakeSnapshotOf(in Aggregator aggregatorToSnapshot)
		{
			BaseValue = aggregatorToSnapshot.BaseValue;
			ModChannels = aggregatorToSnapshot.ModChannels;
		}

		public void BroadcastOnDirty()
		{
			const int MAX_BROADCAST_DIRTY = 10;

			if (BroadcastingDirtyCount > MAX_BROADCAST_DIRTY)
			{
				OnDirtyRecursive?.Invoke(this);
				return;
			}

			BroadcastingDirtyCount++;
			OnDirty?.Invoke(this);

			List<ActiveGameplayEffectHandle> dependantsLocalCopy = Dependents;
			Dependents.Clear();

			foreach (var handle in dependantsLocalCopy)
			{
				AbilitySystemComponent asc = handle.OwningAbilitySystemComponent;
				if (asc != null)
				{
					asc.OnMagnitudeDependencyChange(handle, this);
					Dependents.Add(handle);
				}
			}

			BroadcastingDirtyCount--;
		}
	}
}