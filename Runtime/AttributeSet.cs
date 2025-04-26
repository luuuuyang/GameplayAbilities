using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using BeardPhantom.RuntimeTypeCache;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameplayAbilities
{
	[Serializable]
	public class GameplayAttributeData
	{
		[ReadOnly]
		public float BaseValue;
		[ReadOnly]
		public float CurrentValue;

		public GameplayAttributeData(float defaultValue = 0)
		{
			BaseValue = defaultValue;
			CurrentValue = defaultValue;
		}
	}

	[Serializable]
	public record GameplayAttribute/* : ISerializationCallbackReceiver*/
	{
		[LabelText("Attribute")]
		[ValueDropdown("CollectAttributeNamesFromAssembly")]
		public string AttributeName;

		public FieldInfo Property
		{
			get
			{
				if (string.IsNullOrEmpty(AttributeName))
				{
					return null;
				}

				string[] attributeNameParts = AttributeName.Split('.');
				if (attributeNameParts.Length != 2)
				{
					return null;
				}

				string attributeSetName = attributeNameParts[0];
				string attributeName = attributeNameParts[1];
				Type attributeSetType = GlobalTypeCache.GetTypesDerivedFrom<AttributeSet>().FirstOrDefault(t => t.Name == attributeSetName);

				if (attributeSetType == null)
				{
					return null;
				}

				return attributeSetType.GetField(attributeName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			}
			set
			{
				if (value != null)
				{
					AttributeName = $"{value.DeclaringType.Name}.{value.Name}";
				}
				else
				{
					AttributeName = null;
				}
			}
		}

		public GameplayAttribute()
		{
			Property = null;
		}

		public GameplayAttribute(string attributeName)
		{
			AttributeName = attributeName;
		}

		public GameplayAttribute(FieldInfo fieldInfo)
		{
			Property = fieldInfo;
		}

		public static implicit operator GameplayAttribute(FieldInfo fieldInfo)
		{
			return new GameplayAttribute(fieldInfo);
		}

		public bool IsValid()
		{
			return Property != null;
		}

		public Type GetAttributeSetClass()
		{
			return GlobalTypeCache.GetTypesDerivedFrom<AttributeSet>().First(t => t.Name == Property.DeclaringType.Name);
		}

		public bool IsSystemAttribute()
		{
			return GetAttributeSetClass().IsSubclassOf(typeof(AbilitySystemComponent));
		}

		public static bool IsGameplayAttributeDataField(in FieldInfo fieldInfo)
		{
			return fieldInfo.FieldType == typeof(GameplayAttributeData);
		}

#if UNITY_EDITOR
		public static List<string> CollectAttributeNamesFromAssembly()
		{
			List<string> attributeNames = new List<string>();

			IEnumerable<Type> attributeSetTypes = GlobalTypeCache.GetTypesDerivedFrom<AttributeSet>().Where(t => !t.IsAbstract);

			foreach (Type type in attributeSetTypes)
			{
				IEnumerable<FieldInfo> fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(f => IsGameplayAttributeDataField(f));
				foreach (FieldInfo field in fields)
				{
					attributeNames.Add($"{type.Name}.{field.Name}");
				}
			}

			return attributeNames;
		}
#endif

		public float GetNumericValue(AttributeSet attributeSet)
		{
			if (attributeSet == null)
			{
				return 0f;
			}

			if (Property == null)
			{
				return 0f;
			}

			if (Property.FieldType == typeof(float))
			{
				return (float)Property.GetValue(attributeSet);
			}
			else if (Property.FieldType == typeof(GameplayAttributeData))
			{
				if (Property.GetValue(attributeSet) is GameplayAttributeData attributeData)
				{
					return attributeData.CurrentValue;
				}
			}

			return 0f;
		}

		public float GetNumericValueChecked(AttributeSet dest)
		{
			return GetNumericValue(dest);
		}

		public void SetNumericValueChecked(float newValue, AttributeSet dest)
		{
			Debug.Assert(dest != null);

			if (Property == null)
			{
				return;
			}

			float oldValue;
			if (Property.FieldType == typeof(float))
			{
				oldValue = (float)Property.GetValue(dest);
				dest.PreAttributeChange(this, ref newValue);
				Property.SetValue(dest, newValue);
				dest.PostAttributeChange(this, oldValue, newValue);
			}
			else if (Property.FieldType == typeof(GameplayAttributeData))
			{
				GameplayAttributeData attributeData = Property.GetValue(dest) as GameplayAttributeData;
				Debug.Assert(attributeData != null);

				oldValue = attributeData.CurrentValue;
				dest.PreAttributeChange(this, ref newValue);
				attributeData.CurrentValue = newValue;
				dest.PostAttributeChange(this, oldValue, newValue);
			}
			else
			{
				Debug.Assert(false);
			}
		}

		public override string ToString()
		{
			return string.IsNullOrEmpty(AttributeName) ? Property.Name : AttributeName;
		}

		// public void OnBeforeSerialize()
		// {
		// 	// Debug.Log($"OnBeforeSerialize: {AttributeName}");
		// }

		// public void OnAfterDeserialize()
		// {
		// 	if (string.IsNullOrEmpty(AttributeName))
		// 	{
		// 		return;
		// 	}


		// 	Type attributeSetType = GlobalTypeCache.GetTypesDerivedFrom<AttributeSet>()
		// 		.First(t => t.Name == AttributeName.Split('.').First());
		// 	Attribute = attributeSetType.GetField(AttributeName.Split('.').Last(),
		// 		BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);


		// 	Debug.Log("OnAfterDeserialize: " + attributeSetType + " " + Attribute);
		// }
	}

	[TypeCacheTarget]
	public partial class AttributeSet : ScriptableObject
	{
		public GameObject OwningActor { get; set; }

		public AbilitySystemComponent OwningAbilitySystemComponent
		{
			get
			{
				return AbilitySystemGlobals.GetAbilitySystemComponentFromActor(OwningActor);
			}
		}

		public AbilitySystemComponent OwningAbilitySystemComponentChecked
		{
			get
			{
				AbilitySystemComponent result = OwningAbilitySystemComponent;
				Debug.Assert(result != null);
				return result;
			}
		}

		public static void GetAttributesFromSetClass(Type attributeSetClass, List<GameplayAttribute> attributes)
		{
			IEnumerable<FieldInfo> fields = attributeSetClass
								 .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
								 .Where(f => f.FieldType == typeof(float) || GameplayAttribute.IsGameplayAttributeDataField(f));

			foreach (FieldInfo field in fields)
			{
				attributes.Add(new GameplayAttribute(field));
			}
		}

		public virtual bool PreGameplayEffectExecute(GameplayEffectModCallbackData data)
		{
			return true;
		}

		public virtual void PostGameplayEffectExecute(in GameplayEffectModCallbackData data)
		{

		}

		public virtual void PreAttributeChange(in GameplayAttribute attribute, ref float newValue)
		{

		}

		public virtual void PostAttributeChange(in GameplayAttribute attribute, float oldValue, float newValue)
		{

		}

		public virtual void PreAttributeBaseChange(in GameplayAttribute attribute, ref float newValue)
		{

		}

		public virtual void PostAttributeBaseChange(in GameplayAttribute attribute, float oldValue, float newValue)
		{

		}

		public virtual void OnAttributeAggregatorCreated(in GameplayAttribute attribute, Aggregator aggregator)
		{

		}
	}
}