using GameplayTags;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameplayAbilities
{
    [CreateAssetMenu(fileName = "GameplayAbilitiesDeveloperSettings", menuName = "GameplayAbilities/GameplayAbilitiesDeveloperSettings")]
    public class GameplayAbilitiesDeveloperSettings : ScriptableObject
    {
        public GameplayTag ActivateFailCanActivateAbilityTag;
        public GameplayTag ActivateFailCooldownTag;
        public GameplayTag ActivateFailCostTag;
        public GameplayTag ActivateFailNetworkingTag;
        public GameplayTag ActivateFailTagsBlockedTag;
        public GameplayTag ActivateFailTagsMissingTag;

        public bool AllowGameplayModEvaluationChannels;
        public GameplayModEvaluationChannel DefaultGameplayModEvaluationChannel = GameplayModEvaluationChannel.Channel0;
        public string[] GameplayModEvaluationChannelAliases = new string[10];

        public static GameplayAbilitiesDeveloperSettings GetOrCreateSettings()
        {
            // MonoScript script = MonoScript.FromScriptableObject(CreateInstance<GameplayAbilitiesDeveloperSettings>());
            // string scriptPath = AssetDatabase.GetAssetPath(script);
            // string settingsPath = scriptPath.Replace("Runtime/GameplayAbilitiesDeveloperSettings.cs", "Editor/Config/" + GameplayTagSource.DefaultName);

            // GameplayAbilitiesDeveloperSettings settings = AssetDatabase.LoadAssetAtPath<GameplayAbilitiesDeveloperSettings>(settingsPath);
            // if (settings == null)
            // {
            //     settings = CreateInstance<GameplayAbilitiesDeveloperSettings>();
            //     AssetDatabase.CreateAsset(settings, settingsPath);
            //     AssetDatabase.SaveAssets();
            // }
            GameplayAbilitiesDeveloperSettings settings = Resources.Load<GameplayAbilitiesDeveloperSettings>("GameplayAbilitiesDeveloperSettings");

            return settings;
        }

#if UNITY_EDITOR
        public static SerializedObject GetSerializedObject()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
#endif
    }
}
