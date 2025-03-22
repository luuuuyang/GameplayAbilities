using GameplayTags;
using UnityEditor;
using UnityEngine;

namespace GameplayAbilities
{
    public class GameplayAbilitiesDeveloperSettings : ScriptableObject
    {
        public GameplayTag ActivateFailCanActivateAbilityTag;
        public GameplayTag ActivateFailCooldownTag;
        public GameplayTag ActivateFailCostTag;
        public GameplayTag ActivateFailNetworkingTag;
        public GameplayTag ActivateFailTagsBlockedTag;
        public GameplayTag ActivateFailTagsMissingTag;

        public static GameplayAbilitiesDeveloperSettings GetOrCreateSettings()
        {
            MonoScript script = MonoScript.FromScriptableObject(CreateInstance<GameplayAbilitiesDeveloperSettings>());
            string scriptPath = AssetDatabase.GetAssetPath(script);
            string settingsPath = scriptPath.Replace("Runtime/GameplayAbilitiesDeveloperSettings.cs", "Editor/Config/" + GameplayTagSource.DefaultName);

            GameplayAbilitiesDeveloperSettings settings = AssetDatabase.LoadAssetAtPath<GameplayAbilitiesDeveloperSettings>(settingsPath);
            if (settings == null)
            {
                settings = CreateInstance<GameplayAbilitiesDeveloperSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        public static SerializedObject GetSerializedObject()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
    }
}
