using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using BeardPhantom.RuntimeTypeCache;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
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
	public class GameplayAttribute/* : ISerializationCallbackReceiver*/
	{
#if ODIN_INSPECTOR
		[LabelText("Attribute")]
		[ValueDropdown("CollectAttributeNamesFromAssembly")]
#endif
		public string AttributeName;

		private FieldInfo Attribute;

		public GameplayAttribute()
		{
			Attribute = null;
		}

		public GameplayAttribute(FieldInfo fieldInfo)
		{
			Attribute = fieldInfo;

			if (fieldInfo != null)
			{
				AttributeName = $"{fieldInfo.DeclaringType.Name}.{fieldInfo.Name}";
			}
		}

		public static implicit operator GameplayAttribute(FieldInfo fieldInfo)
		{
			return new GameplayAttribute(fieldInfo);
		}

		public bool IsValid()
		{
			return Attribute != null;
		}

		public void SetField(FieldInfo fieldInfo)
		{
			Attribute = fieldInfo;
			if (fieldInfo != null)
			{
				AttributeName = $"{fieldInfo.DeclaringType.Name}.{fieldInfo.Name}";
			}
			else
			{
				AttributeName = null;
			}
		}

		public FieldInfo GetField()
		{
			if (string.IsNullOrEmpty(AttributeName))
			{
				return null;
			}

			foreach (var type in GlobalTypeCache.GetTypesDerivedFrom<AttributeSet>())
			{
				Debug.Log("type: " + type.Name);
			}

			Type attributeSetType = GlobalTypeCache.GetTypesDerivedFrom<AttributeSet>()
				.First(t => t.Name == AttributeName.Split('.').First());
			Attribute = attributeSetType.GetField(AttributeName.Split('.').Last(),
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			return Attribute;
		}

		public Type GetAttributeSetClass()
		{
			return GlobalTypeCache.GetTypesDerivedFrom<AttributeSet>()
				.First(t => t.Name == Attribute.DeclaringType.Name);
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
		public static List<string> CollectAttributeNames()
		{
			var attributeNames = new List<string>();

			// 遍历项目中的所有资源
			string fullname = typeof(AttributeSet).FullName;
			string[] guids = AssetDatabase.FindAssets($"t:{fullname}");
			foreach (string guid in guids)
			{
				string assetPath = AssetDatabase.GUIDToAssetPath(guid);
				UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

				foreach (UnityEngine.Object asset in assets)
				{
					// 检查是否是AttributeSet的子类
					if (asset != null)
					{
						var type = asset.GetType();
						var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
						var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
						foreach (var field in fields)
						{
							if (field.FieldType == typeof(GameplayAttributeData))
							{
								attributeNames.Add(asset.name + "." + field.Name);
							}
						}
					}
				}
			}

			return attributeNames;
		}

		public static List<string> CollectAttributeNamesFromAssembly()
		{
			var attributeNames = new List<string>();

			// 获取当前程序集中的所有类型
			var attributeSetTypes = TypeCache.GetTypesDerivedFrom<AttributeSet>()
				.Where(t => !t.IsAbstract);

			foreach (var type in attributeSetTypes)
			{
				// 获取所有字段
				var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
								 .Where(f => f.FieldType == typeof(GameplayAttributeData));

				foreach (var field in fields)
				{
					attributeNames.Add($"{type.Name}.{field.Name}");
				}
			}

			return attributeNames;
		}
#endif

		public float GetNumericValue(AttributeSet attributeSet)
		{
			if (string.IsNullOrEmpty(AttributeName) || attributeSet == null)
				return 0f;

			// 使用反射获取属性值
			var type = attributeSet.GetType();
			var field = type.GetField(AttributeName.Split('.').Last(),
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			if (field == null)
				return 0f;

			// 检查字段类型
			if (field.FieldType == typeof(float))
			{
				// 直接获取 float 值
				return (float)field.GetValue(attributeSet);
			}
			else if (field.FieldType == typeof(GameplayAttributeData))
			{
				// 获取 GameplayAttributeData 的当前值
				var attributeData = field.GetValue(attributeSet) as GameplayAttributeData;
				if (attributeData != null)
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

			float oldValue = 0f;

			// 使用反射获取属性值
			var type = dest.GetType();
			var field = type.GetField(AttributeName.Split('.').Last(),
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			if (field == null)
				return;

			// 检查字段类型
			if (field.FieldType == typeof(float))
			{
				// 直接获取 float 值
				oldValue = (float)field.GetValue(dest);
				dest.PreAttributeChange(this, newValue);
				field.SetValue(dest, newValue);
				dest.PostAttributeChange(this, oldValue, newValue);
			}
			else if (field.FieldType == typeof(GameplayAttributeData))
			{
				// 获取 GameplayAttributeData 的当前值
				GameplayAttributeData attributeData = field.GetValue(dest) as GameplayAttributeData;
				Debug.Assert(attributeData != null);

				oldValue = attributeData.CurrentValue;
				dest.PreAttributeChange(this, newValue);
				attributeData.CurrentValue = newValue;
				dest.PostAttributeChange(this, oldValue, newValue);
			}
			else
			{
				Debug.Assert(false);
			}
		}

		public static bool operator ==(GameplayAttribute a, GameplayAttribute b)
		{
			return a.Attribute == b.Attribute;
		}

		public static bool operator !=(GameplayAttribute a, GameplayAttribute b)
		{
			return !(a == b);
		}

		public override bool Equals(object obj)
		{
			if (obj == null || !(obj is GameplayAttribute))
				return false;
			return Attribute == ((GameplayAttribute)obj).Attribute;
		}

		public override int GetHashCode()
		{
			return Attribute.GetHashCode();
		}

		public string GetName()
		{
			return string.IsNullOrEmpty(AttributeName) ? Attribute.Name : AttributeName;
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
	public class AttributeSet : ScriptableObject
	{
		public virtual bool PreGameplayEffectExecute(GameplayEffectModCallbackData data)
		{
			return true;
		}

		public virtual void PostGameplayEffectExecute(GameplayEffectModCallbackData data)
		{

		}

		public virtual void PreAttributeChange(GameplayAttribute attribute, float newValue)
		{
		}

		public virtual void PostAttributeChange(GameplayAttribute attribute, float oldValue, float newValue)
		{

		}

		public virtual void PreAttributeBaseChange(GameplayAttribute attribute, float newValue)
		{

		}

		public virtual void PostAttributeBaseChange(GameplayAttribute attribute, float oldValue, float newValue)
		{

		}

		public virtual void OnAttributeAggregatorCreated(GameplayAttribute attribute, Aggregator aggregator)
		{

		}

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
	}
}