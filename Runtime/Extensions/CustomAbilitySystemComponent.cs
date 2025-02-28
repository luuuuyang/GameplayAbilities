using System.Collections.Generic;
using UnityEngine;
using System;

namespace GameplayAbilities
{
    [Serializable]
    public struct AbilityInputMapping
    {
        public GameplayAbility Ability;
        public int Level;
    }

    public struct MappedAbility
    {
        public GameplayAbilitySpecHandle Handle;
        public GameplayAbilitySpec Spec;

        public MappedAbility(GameplayAbilitySpecHandle handle, GameplayAbilitySpec spec)
        {
            Handle = handle;
            Spec = spec;
        }
    }

    public delegate void OnInitAbilityActorInfo();
    public delegate void OnGiveAbility(GameplayAbilitySpec abilitySpec);

    public class CustomAbilitySystemComponent : AbilitySystemComponent
    {
        public List<AbilityInputMapping> GrantedAbilities = new();
        public List<AttributeSet> GrantedAttributeSets = new();
        public List<GameplayEffect> GrantedEffects = new();
        public OnInitAbilityActorInfo OnInitAbilityActorInfo;
        public OnGiveAbility onGiveAbility;

        public bool ResetAbilitiesOnSpawn = true;
        public bool ResetAttributesOnSpawn = true;

        protected List<MappedAbility> AddedAbilityHandles = new();
        protected List<AttributeSet> AddedAttributeSets = new();
        protected List<ActiveGameplayEffectHandle> AddedEffects = new();

        protected override void Start()
        {
            base.Start();
            GrantStartupEffects();
        }

        void OnDestroy()
        {
            foreach (var addedEffect in AddedEffects)
            {
                RemoveActiveGameplayEffect(addedEffect);
            }

            AddedEffects.Clear();
            AddedAbilityHandles.Clear();
            AddedAttributeSets.Clear();
        }

        public override void InitAbilityActorInfo(GameObject ownerActor, GameObject avatarActor)
        {
            base.InitAbilityActorInfo(ownerActor, avatarActor);

            GrantDefaultAbilitiesAndAttributes(ownerActor, avatarActor);
            
            OnInitAbilityActorInfo?.Invoke();
        }

        public virtual void GrantDefaultAbilitiesAndAttributes(GameObject ownerActor, GameObject avatarActor)
        {
            if (ResetAttributesOnSpawn)
            {
                foreach (var attributeSet in AddedAttributeSets)
                {
                    RemoveSpawnedAttribute(attributeSet);
                }

                AddedAttributeSets.Clear();
            }

            if (ResetAbilitiesOnSpawn)
            {
                foreach (var defaultAbilityHandle in AddedAbilityHandles)
                {
                    SetRemoveAbilityOnEnd(defaultAbilityHandle.Handle);
                }

                AddedAbilityHandles.Clear();
            }

            foreach (var grantedAbility in GrantedAbilities)
            {
                GameplayAbility ability = grantedAbility.Ability;
                if (ability == null)
                {
                    continue;
                }

                GameplayAbilitySpec newAbilitySpec = BuildAbilitySpecFromClass(ability, grantedAbility.Level);

                if (ShouldGrantAbility(ability, grantedAbility.Level))
                {
                    GameplayAbilitySpecHandle abilityHandle = GiveAbility(ability);
                    AddedAbilityHandles.Add(new MappedAbility(abilityHandle, newAbilitySpec));
                }
            }

            foreach (var grantedAttributeSet in GrantedAttributeSets)
            {
                if (grantedAttributeSet != null)
                {
                    bool hasAttributeSet = GetAttributeSubobject(grantedAttributeSet.GetType()) != null;
                    if (!hasAttributeSet && ownerActor != null)
                    {
                        AttributeSet attributeSet = ScriptableObject.CreateInstance(grantedAttributeSet.GetType()) as AttributeSet;
                        if (attributeSet != null)
                        {
                            AddedAttributeSets.Add(attributeSet);
                            AddAttributeSetSubobject(attributeSet);
                        }
                    }
                }
            }
        }

        public void GrantStartupEffects()
        {
            foreach (ActiveGameplayEffectHandle addedEffect in AddedEffects)
            {
                RemoveActiveGameplayEffect(addedEffect);
            }

            GameplayEffectContextHandle effectContext = MakeEffectContext();
            effectContext.AddSourceObject(this);

            AddedEffects.Clear();

            foreach (var grantedEffect in GrantedEffects)
            {
                GameplayEffectSpecHandle newHandle = MakeOutgoingSpec(grantedEffect, 1, effectContext);
                if (newHandle.IsValid())
                {
                    ActiveGameplayEffectHandle effectHandle = ApplyGameplayEffectSpecToTarget(newHandle.Data, this);
                    AddedEffects.Add(effectHandle);
                }
            }
        }

        protected virtual bool ShouldGrantAbility(in GameplayAbility ability, in int level)
        {
            if (ResetAbilitiesOnSpawn)
            {
                return true;
            }

            List<GameplayAbilitySpec> abilitySpecs = GetActivatableAbilities();
            foreach (var activatableAbility in abilitySpecs)
            {
                if (activatableAbility.Ability == null)
                {
                    continue;
                }

                if (activatableAbility.Ability == ability && activatableAbility.Level == level)
                {
                    return false;
                }
            }

            return true;
        }

        protected override void OnGiveAbility(GameplayAbilitySpec abilitySpec)
        {
            base.OnGiveAbility(abilitySpec);

            onGiveAbility?.Invoke(abilitySpec);
        }   
    }
}
