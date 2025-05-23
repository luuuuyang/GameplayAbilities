using GameplayTags;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameplayAbilities
{
	public static class AbilitySystemPrivate
	{
		public static float MultiplyMods(in List<AggregatorMod> mods)
		{
			float result = 1;

			foreach (AggregatorMod mod in mods)
			{
				if (mod.Qualifies)
				{
					result *= mod.EvaluatedMagnitude;
				}
			}

			return result;
		}
	}

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
		private bool IsQualified;

		public void UpdateQualifies(in AggregatorEvaluateParameters parameters)
		{
			GameplayTagContainer emptyTagContainer = new();
			GameplayTagContainer srcTags = parameters.SourceTags is not null ? parameters.SourceTags : emptyTagContainer;
			GameplayTagContainer tgtTags = parameters.TargetTags is not null ? parameters.TargetTags : emptyTagContainer;
			bool sourceMet = SourceTagReqs is null || SourceTagReqs.IsEmpty() || SourceTagReqs.RequirementsMet(srcTags);
			bool targetMet = TargetTagReqs is null || TargetTagReqs.IsEmpty() || TargetTagReqs.RequirementsMet(tgtTags);

			bool sourceFilterMet = parameters.AppliedSourceTagFilter.Count == 0;
			bool targetFilterMet = parameters.AppliedTargetTagFilter.Count == 0;

			if (ActiveHandle.IsValid())
			{
				foreach (ActiveGameplayEffectHandle handleToIgnore in parameters.IgnoreHandles)
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
	}

	public class AggregatorModChannel
	{
		public List<AggregatorMod>[] Mods = new List<AggregatorMod>[(int)GameplayModOp.Max];

		public AggregatorModChannel()
		{
			for (int modOpIdx = 0; modOpIdx < Mods.Length; modOpIdx++)
			{
				Mods[modOpIdx] = new List<AggregatorMod>();
			}
		}

		public float EvaluateWithBase(float inlineBaseValue, in AggregatorEvaluateParameters parameters)
		{
			foreach (AggregatorMod mod in Mods[(int)GameplayModOp.Override])
			{
				if (mod.Qualifies)
				{
					return mod.EvaluatedMagnitude;
				}
			}

			float additive = SumMods(Mods[(int)GameplayModOp.Additive], GameplayEffectUtilities.GetModifierBiasByModifierOp(GameplayModOp.Additive), parameters);
			float multiplicitive = SumMods(Mods[(int)GameplayModOp.Multiplicitive], GameplayEffectUtilities.GetModifierBiasByModifierOp(GameplayModOp.Multiplicitive), parameters);
			float division = SumMods(Mods[(int)GameplayModOp.Division], GameplayEffectUtilities.GetModifierBiasByModifierOp(GameplayModOp.Division), parameters);
			float finalAdd = SumMods(Mods[(int)GameplayModOp.AddFinal], GameplayEffectUtilities.GetModifierBiasByModifierOp(GameplayModOp.AddFinal), parameters);
			float compoundMultiply = AbilitySystemPrivate.MultiplyMods(Mods[(int)GameplayModOp.MultiplyCompound]);

			if (Mathf.Approximately(division, 0))
			{
				Debug.LogWarning("Division summation was 0.0f in AggregatorModChannel.");
				division = 1;
			}

			return (inlineBaseValue + additive) * multiplicitive / division * compoundMultiply + finalAdd;
		}

		public bool ReverseEvaluate(float finalValue, in AggregatorEvaluateParameters parameters, out float computedValue)
		{
			foreach (AggregatorMod mod in Mods[(int)GameplayModOp.Override])
			{
				if (mod.Qualifies)
				{
					computedValue = finalValue;
					return false;
				}
			}

			float additive = SumMods(Mods[(int)GameplayModOp.Additive], GameplayEffectUtilities.GetModifierBiasByModifierOp(GameplayModOp.Additive), parameters);
			float multiplicitive = SumMods(Mods[(int)GameplayModOp.Multiplicitive], GameplayEffectUtilities.GetModifierBiasByModifierOp(GameplayModOp.Multiplicitive), parameters);
			float division = SumMods(Mods[(int)GameplayModOp.Division], GameplayEffectUtilities.GetModifierBiasByModifierOp(GameplayModOp.Division), parameters);
			float finalAdd = SumMods(Mods[(int)GameplayModOp.AddFinal], GameplayEffectUtilities.GetModifierBiasByModifierOp(GameplayModOp.AddFinal), parameters);
			float compoundMultiply = AbilitySystemPrivate.MultiplyMods(Mods[(int)GameplayModOp.MultiplyCompound]);

			if (Mathf.Approximately(division, 0))
			{
				Debug.LogWarning("Division summation was 0.0f in AggregatorModChannel.");
				division = 1;
			}

			if (multiplicitive <= float.Epsilon)
			{
				computedValue = finalValue;
				return false;
			}

			computedValue = (finalValue - finalAdd) / (compoundMultiply * division / multiplicitive) - additive;
			return true;
		}

		public void AddMod(float evaluatedMagnitude, GameplayModOp modOp, in GameplayTagRequirements sourceTagReqs, in GameplayTagRequirements targetTagReqs, in ActiveGameplayEffectHandle activeHandle)
		{
			List<AggregatorMod> modList = Mods[(int)modOp];

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
			Debug.Assert(handle.IsValid());

			for (int modOpIdx = 0; modOpIdx < Mods.Length; modOpIdx++)
			{
				Mods[modOpIdx].RemoveAll(mod => mod.ActiveHandle == handle);
			}
		}

		public void AddModsFrom(in AggregatorModChannel other)
		{
			for (int modOpIdx = 0; modOpIdx < Mods.Length; modOpIdx++)
			{
				Mods[modOpIdx].AddRange(other.Mods[modOpIdx]);
			}
		}

		public void GetAllAggregatorMods(GameplayModEvaluationChannel channel, Dictionary<GameplayModEvaluationChannel, List<AggregatorMod>[]> mods)
		{
			mods.Add(channel, Mods);
		}

		public void UpdateQualifiesOnAllMods(in AggregatorEvaluateParameters parameters)
		{
			for (int modOpIdx = 0; modOpIdx < Mods.Length; modOpIdx++)
			{
				foreach (AggregatorMod mod in Mods[modOpIdx])
				{
					mod.UpdateQualifies(parameters);
				}
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

		public float EvaluateWithBaseToChannel(float inlineBaseValue, in AggregatorEvaluateParameters parameters, GameplayModEvaluationChannel finalChannel)
		{
			float computedValue = inlineBaseValue;

			foreach (var channelEntry in ModChannelsMap)
			{
				if (channelEntry.Key <= finalChannel)
				{
					var curChannel = channelEntry.Value;
					computedValue = curChannel.EvaluateWithBase(computedValue, parameters);
				}
				else
				{
					break;
				}
			}

			return computedValue;
		}

		public void EvaluateQualificationForAllMods(in AggregatorEvaluateParameters parameters)
		{
			foreach (var mapIt in ModChannelsMap)
			{
				AggregatorModChannel channel = mapIt.Value;
				channel.UpdateQualifiesOnAllMods(parameters);
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

		public void GetAllAggregatorMods(Dictionary<GameplayModEvaluationChannel, List<AggregatorMod>[]> mods)
		{
			foreach (var channelEntry in ModChannelsMap)
			{
				GameplayModEvaluationChannel curChannelEnum = channelEntry.Key;
				AggregatorModChannel curChannel = channelEntry.Value;

				curChannel.GetAllAggregatorMods(curChannelEnum, mods);
			}
		}
	}

	public class Aggregator
	{
		public delegate void OnAggregatorDirty(Aggregator aggregator);

		public OnAggregatorDirty OnDirty;
		public OnAggregatorDirty OnDirtyRecursive;

		public float BaseValue { get; private set; }
		public AggregatorModChannelContainer ModChannels = new();
		public List<ActiveGameplayEffectHandle> Dependents = new();
		public int BroadcastingDirtyCount;

		public Aggregator(float baseValue = 0)
		{
			BaseValue = baseValue;
			BroadcastingDirtyCount = 0;
		}

		public void SetBaseValue(float newBaseValue, bool broadcastDirtyEvent = true)
		{
			BaseValue = newBaseValue;
			if (broadcastDirtyEvent)
			{
				BroadcastOnDirty();
			}
		}

		public float Evaluate(in AggregatorEvaluateParameters parameters)
		{
			EvaluateQualificationForAllMods(parameters);
			return ModChannels.EvaluateWithBase(BaseValue, parameters);
		}

		public float EvaluateToChannel(in AggregatorEvaluateParameters parameters, GameplayModEvaluationChannel finalChannel)
		{
			EvaluateQualificationForAllMods(parameters);
			return ModChannels.EvaluateWithBaseToChannel(BaseValue, parameters, finalChannel);
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

		public float EvaluateContribution(in AggregatorEvaluateParameters parameters, ActiveGameplayEffectHandle activeHandle)
		{
			if (activeHandle.IsValid())
			{
				AggregatorEvaluateParameters paramsExcludingHandle = new();
				paramsExcludingHandle.IgnoreHandles.Add(activeHandle);

				return Evaluate(parameters) - Evaluate(paramsExcludingHandle);
			}

			return 0f;
		}

		public void EvaluateQualificationForAllMods(in AggregatorEvaluateParameters parameters)
		{
			ModChannels.EvaluateQualificationForAllMods(parameters);
		}

		public static float StaticExecModOnBaseValue(float baseValue, GameplayModOp modifierOp, float evaluatedMagnitude)
		{
			switch (modifierOp)
			{
				case GameplayModOp.Override:
					baseValue = evaluatedMagnitude;
					break;
				case GameplayModOp.AddBase:
				case GameplayModOp.AddFinal:
					baseValue += evaluatedMagnitude;
					break;
				case GameplayModOp.MultiplyAdditive:
				case GameplayModOp.MultiplyCompound:
					baseValue *= evaluatedMagnitude;
					break;
				case GameplayModOp.DivideAdditive:
					if (Mathf.Approximately(evaluatedMagnitude, 0) == false)
					{
						baseValue /= evaluatedMagnitude;
					}
					break;
			}
			return baseValue;
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

		public void UpdateAggregatorMod(ActiveGameplayEffectHandle activeHandle, in GameplayAttribute attribute, in GameplayEffectSpec spec, ActiveGameplayEffectHandle handle)
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

		public void GetAllAggregatorMods(Dictionary<GameplayModEvaluationChannel, List<AggregatorMod>[]> mods)
		{
			ModChannels.GetAllAggregatorMods(mods);
		}

		public void TakeSnapshotOf(in Aggregator aggregatorToSnapshot)
		{
			BaseValue = aggregatorToSnapshot.BaseValue;
			ModChannels = aggregatorToSnapshot.ModChannels;
		}

		private void BroadcastOnDirty()
		{
			const int MAX_BROADCAST_DIRTY = 10;

			if (BroadcastingDirtyCount > MAX_BROADCAST_DIRTY)
			{
				OnDirtyRecursive?.Invoke(this);

				Debug.LogWarning("Aggregator detected cyclic attribute dependencies. We are skipping a recursive dirty call. Its possible the resulting attribute values are not what you expect!");

#if UNITY_EDITOR
				UnityEngine.Object.FindObjectsByType<AbilitySystemComponent>(FindObjectsSortMode.None).ToList().ForEach(asc => asc.DebugCyclicAggregatorBroadcasts(this));
#endif
				return;
			}

			BroadcastingDirtyCount++;
			OnDirty?.Invoke(this);

			List<ActiveGameplayEffectHandle> dependantsLocalCopy = new(Dependents);
			Dependents.Clear();

			foreach (ActiveGameplayEffectHandle handle in dependantsLocalCopy)
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