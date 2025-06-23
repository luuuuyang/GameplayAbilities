using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayAbilities.Editor
{
    public class GameplayAbilitiesDeveloperSettingsProvider : SettingsProvider
    {
        private SerializedObject SerializedObject;
        
        public GameplayAbilitiesDeveloperSettingsProvider(string path, SettingsScope scope = SettingsScope.User) : base(path, scope)
        {
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            base.OnActivate(searchContext, rootElement);
            SerializedObject = GameplayAbilitiesDeveloperSettings.GetSerializedObject();
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.PropertyField(SerializedObject.FindProperty("AllowGameplayModEvaluationChannels"));
            EditorGUILayout.PropertyField(SerializedObject.FindProperty("DefaultGameplayModEvaluationChannel"));
            EditorGUILayout.PropertyField(SerializedObject.FindProperty("GameplayModEvaluationChannelAliases"));
            EditorGUILayout.PropertyField(SerializedObject.FindProperty("UseTurnBasedTimerManager"));
            SerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        [SettingsProvider]
        public static SettingsProvider CreateGameplayAbilitiesDeveloperSettingsProvider()
        {
            return new GameplayAbilitiesDeveloperSettingsProvider("Project/GameplayAbilities", SettingsScope.Project);
        }
    }
}