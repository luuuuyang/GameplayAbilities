using System;
using System.Collections.Generic;
using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    public record GameplayAbilityTargetData
    {
        public virtual List<WeakReference<GameObject>> Actors { get; set; } = new();

        public List<ActiveGameplayEffectHandle> ApplyGameplayEffect(in GameplayEffect gameplayEffect, in GameplayEffectContextHandle effectContext, float level)
        {
            GameplayEffectSpec tempSpecToApply = new(gameplayEffect, effectContext, level);
            return ApplyGameplayEffectSpec(tempSpecToApply);
        }

        public virtual List<ActiveGameplayEffectHandle> ApplyGameplayEffectSpec(GameplayEffectSpec spec)
        {
            List<ActiveGameplayEffectHandle> appliedHandles = new();

            if (spec.EffectContext == null || spec.EffectContext.InstigatorAbilitySystemComponent == null)
            {
                return appliedHandles;
            }

            List<WeakReference<GameObject>> actors = Actors;

            appliedHandles.Capacity = actors.Count;

            foreach (WeakReference<GameObject> targetActor in actors)
            {
                if (targetActor.TryGetTarget(out GameObject gameObject))
                {
                    AbilitySystemComponent targetComponent = AbilitySystemBlueprintLibrary.GetAbilitySystemComponent(gameObject);
                    if (targetComponent != null)
                    {
                        GameplayEffectSpec specToApply = new(spec);
                        GameplayEffectContextHandle effectContext = specToApply.EffectContext.Duplicate();
                        specToApply.SetContext(effectContext);

                        AddTargetDataToContext(effectContext, false);

                        appliedHandles.Add(effectContext.InstigatorAbilitySystemComponent.ApplyGameplayEffectSpecToTarget(specToApply, targetComponent));
                    }
                }
            }

            return appliedHandles;
        }

        public virtual void AddTargetDataToContext(GameplayEffectContextHandle context, bool includeActorArray)
        {
            if (includeActorArray)
            {
                List<WeakReference<GameObject>> weakArray = Actors;
                if (weakArray.Count > 0)
                {
                    context.AddActors(weakArray);
                }
            }
        }
    }

    public record GameplayAbilityTargetDataHandle
    {
        public List<GameplayAbilityTargetData> Data = new();

        public GameplayAbilityTargetDataHandle()
        {
        }

        public GameplayAbilityTargetDataHandle(GameplayAbilityTargetData data)
        {
            Data.Add(data);
        }

        public void Clear()
        {
            Data.Clear();
        }

        public int Count => Data.Count;

        public bool IsValid(int index)
        {
            return index < Data.Count && Data[index] != null;
        }

        public GameplayAbilityTargetData this[int index]
        {
            get
            {
                if (!IsValid(index))
                {
                    throw new IndexOutOfRangeException("Index is out of range");
                }
                return Data[index];
            }
        }

        public void Add(GameplayAbilityTargetData data)
        {
            Data.Add(data);
        }

        public void AddRange(in GameplayAbilityTargetDataHandle data)
        {
            Data.AddRange(data.Data);
        }
    }

    public record GameplayAbilityTargetData_ActorArray : GameplayAbilityTargetData
    {
        public List<WeakReference<GameObject>> TargetActorArray = new();
        public override List<WeakReference<GameObject>> Actors => TargetActorArray;
    }

    public delegate void AbilityTargetDataSetDelegate(in GameplayAbilityTargetDataHandle targetData, GameplayTag gameplayTag);
}
