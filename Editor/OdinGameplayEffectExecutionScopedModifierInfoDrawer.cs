using GameplayTags;
using Sirenix.OdinInspector.Editor;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Sirenix.Utilities.Editor;

namespace GameplayAbilities.Editor
{
    public record AggregatorDetailsBackingData
    {
        public GameplayEffectScopedModifierAggregatorType AggregatorType;
        public GameplayEffectAttributeCaptureDefinition CaptureDefinition;
        public GameplayTag TransientAggregatorIdentifier;

        public AggregatorDetailsBackingData()
        {
            AggregatorType = GameplayEffectScopedModifierAggregatorType.CapturedAttributeBacked;
            CaptureDefinition = new GameplayEffectAttributeCaptureDefinition();
            TransientAggregatorIdentifier = new GameplayTag();
        }

        public AggregatorDetailsBackingData(in GameplayEffectAttributeCaptureDefinition captureDef)
        {
            AggregatorType = GameplayEffectScopedModifierAggregatorType.CapturedAttributeBacked;
            CaptureDefinition = captureDef;
            TransientAggregatorIdentifier = new GameplayTag();
        }

        public AggregatorDetailsBackingData(in GameplayTag identifier)
        {
            AggregatorType = GameplayEffectScopedModifierAggregatorType.Transient;
            CaptureDefinition = new GameplayEffectAttributeCaptureDefinition();
            TransientAggregatorIdentifier = identifier;
        }

        public override string ToString()
        {
            if (AggregatorType == GameplayEffectScopedModifierAggregatorType.CapturedAttributeBacked)
            {
                return $"Captured: {CaptureDefinition.AttributeToCapture.AttributeName} ({CaptureDefinition.AttributeSource})";
            }
            else
            {
                return $"Transient: {TransientAggregatorIdentifier}";
            }
        }
    }

    public class OdinGameplayEffectExecutionScopedModifierInfoDrawer : OdinValueDrawer<GameplayEffectExecutionScopedModifierInfo>
    {
        private List<AggregatorDetailsBackingData> availableBackingData = new List<AggregatorDetailsBackingData>();
        private bool isExecutionDefAttribute = false;

        protected override void Initialize()
        {
            base.Initialize();

            // Check if we're inside a GameplayEffectExecutionDefinition
            var parent = Property.FindParent(p => p.Info.TypeOfOwner == typeof(GameplayEffectExecutionDefinition), true);
            isExecutionDefAttribute = parent != null;

            if (isExecutionDefAttribute)
            {
                PopulateAvailableBackingData();
            }

            SetCurrentBackingData(GetCurrentBackingData());
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            // Draw the main label
            if (label != null)
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            }

            EditorGUI.indentLevel++;

            if (isExecutionDefAttribute && availableBackingData.Count > 0)
            {
                DrawBackingDataDropdown();
                // EditorGUILayout.Space(5);
            }

            // Draw other properties (excluding the hidden ones when in execution context)
            DrawOtherProperties();

            EditorGUI.indentLevel--;
        }

        private void PopulateAvailableBackingData()
        {
            availableBackingData.Clear();
            // Get the execution definition property
            var executionDefProperty = Property.FindParent(p => p.Info.TypeOfValue == typeof(GameplayEffectExecutionDefinition), true);
            if (executionDefProperty == null)
                return;

            // Get the CalculationClass property
            var calcClassProperty = executionDefProperty.Children.Get("CalculationClass");
            if (calcClassProperty?.ValueEntry?.WeakSmartValue is GameplayEffectExecutionCalculation execCalc && execCalc != null)
            {
                // Get valid scoped modifier attributes
                var captureDefs = new List<GameplayEffectAttributeCaptureDefinition>();
                execCalc.GetValidScopedModifierAttributeCaptureDefinitions(captureDefs);

                foreach (var captureDef in captureDefs)
                {
                    availableBackingData.Add(new AggregatorDetailsBackingData(captureDef));
                }

                // Get valid transient aggregator identifiers
                var validTransientIds = execCalc.GetValidTransientAggregatorIdentifiers();
                if (validTransientIds != null)
                {
                    foreach (var tag in validTransientIds.GameplayTags)
                    {
                        availableBackingData.Add(new AggregatorDetailsBackingData(tag));
                    }
                }
            }
        }

        private void DrawBackingDataDropdown()
        {
            var currentBackingData = GetCurrentBackingData();
            var displayNames = availableBackingData.Select(data => data.ToString()).ToArray();
            var currentIndex = availableBackingData.IndexOf(currentBackingData);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Backing Data", GUILayout.Width(EditorGUIUtility.labelWidth));
            var newIndex = EditorGUILayout.Popup(currentIndex, displayNames);
            if (newIndex != currentIndex && newIndex >= 0 && newIndex < availableBackingData.Count)
            {
                SetCurrentBackingData(availableBackingData[newIndex]);
            }

            EditorGUILayout.EndHorizontal();

            // Draw detailed information about the current backing data
            if (currentBackingData != null)
            {
                DrawBackingDataDetails(currentBackingData);
            }
        }

        private void DrawBackingDataDetails(AggregatorDetailsBackingData backingData)
        {
            EditorGUI.indentLevel++;

            if (backingData.AggregatorType == GameplayEffectScopedModifierAggregatorType.CapturedAttributeBacked)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField("Attribute:", backingData.CaptureDefinition.AttributeToCapture.AttributeName);
                EditorGUILayout.LabelField("Source:", backingData.CaptureDefinition.AttributeSource.ToString());
                EditorGUILayout.LabelField("Snapshot:", backingData.CaptureDefinition.Snapshot ? "Yes" : "No");

                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField("Identifier:", backingData.TransientAggregatorIdentifier.ToString());
                EditorGUILayout.LabelField("Description:", "Temporary value exposed by calculation");

                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        private void DrawOtherProperties()
        {
            foreach (var child in Property.Children)
            {
                var propertyName = child.Name;

                // Skip the properties that are handled by the dropdown when in execution context
                if (isExecutionDefAttribute && (
                    propertyName == "CaptureAttribute" ||
                    propertyName == "TransientAggregatorIdentifier" ||
                    propertyName == "AggregatorType"))
                {
                    continue;
                }

                child.Draw();
            }
        }

        private AggregatorDetailsBackingData GetCurrentBackingData()
        {
            if (Property.ValueEntry?.WeakSmartValue is GameplayEffectExecutionScopedModifierInfo info)
            {
                foreach (var backingData in availableBackingData)
                {
                    if (backingData.AggregatorType == info.AggregatorType)
                    {
                        if (info.AggregatorType == GameplayEffectScopedModifierAggregatorType.CapturedAttributeBacked)
                        {
                            if (AreAttributeCaptureDefinitionsEqual(info.CaptureAttribute, backingData.CaptureDefinition))
                            {
                                return backingData;
                            }
                        }
                        else
                        {
                            if (info.TransientAggregatorIdentifier.Equals(backingData.TransientAggregatorIdentifier))
                            {
                                return backingData;
                            }
                        }
                    }
                }
            }

            return availableBackingData.FirstOrDefault();
        }

        private void SetCurrentBackingData(AggregatorDetailsBackingData backingData)
        {
            if (Property.ValueEntry?.WeakSmartValue is GameplayEffectExecutionScopedModifierInfo info)
            {
                // Check if the data is already the same to avoid unnecessary changes
                bool isSameData = (info.AggregatorType == backingData.AggregatorType) &&
                    (AreAttributeCaptureDefinitionsEqual(info.CaptureAttribute, backingData.CaptureDefinition)) &&
                    (info.TransientAggregatorIdentifier.Equals(backingData.TransientAggregatorIdentifier));

                if (!isSameData)
                {
                    Property.ValueEntry.WeakSmartValue = new GameplayEffectExecutionScopedModifierInfo
                    {
                        AggregatorType = backingData.AggregatorType,
                        CaptureAttribute = backingData.AggregatorType == GameplayEffectScopedModifierAggregatorType.CapturedAttributeBacked
                            ? backingData.CaptureDefinition
                            : new GameplayEffectAttributeCaptureDefinition(),
                        TransientAggregatorIdentifier = backingData.AggregatorType == GameplayEffectScopedModifierAggregatorType.Transient
                            ? backingData.TransientAggregatorIdentifier
                            : new GameplayTag(),
                        ModifierOp = info.ModifierOp,
                        ModifierMagnitude = info.ModifierMagnitude,
                        EvaluationChannelSettings = info.EvaluationChannelSettings,
                        SourceTags = info.SourceTags,
                        TargetTags = info.TargetTags
                    };
                }
            }
        }

        private bool AreAttributeCaptureDefinitionsEqual(GameplayEffectAttributeCaptureDefinition a, GameplayEffectAttributeCaptureDefinition b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            return a.AttributeToCapture.Equals(b.AttributeToCapture) &&
                   a.AttributeSource == b.AttributeSource &&
                   a.Snapshot == b.Snapshot;
        }
    }
}
