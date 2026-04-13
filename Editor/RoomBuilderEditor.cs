using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RoomBuilderTool.Editor
{
    [CustomEditor(typeof(RoomBuilderTool.RoomBuilder))]
    public class RoomBuilderEditor : UnityEditor.Editor
    {
        private SerializedProperty definitionProperty;
        private SerializedProperty autoRebuildProperty;
        private SerializedProperty openingsProperty;

        private void OnEnable()
        {
            definitionProperty = serializedObject.FindProperty("definition");
            autoRebuildProperty = serializedObject.FindProperty("autoRebuild");
            openingsProperty = definitionProperty.FindPropertyRelative("openings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var roomBuilder = (RoomBuilderTool.RoomBuilder)target;

            EditorGUILayout.PropertyField(autoRebuildProperty);
            EditorGUILayout.Space();

            DrawDefinitionSection("Dimensions", "width", "length", "height");
            DrawDefinitionSection("Wall Thickness", "eastWallThickness", "westWallThickness", "northWallThickness", "southWallThickness");
            DrawDefinitionSection("Surface Thickness", "floorThickness", "ceilingThickness");
            DrawOpeningsSection();

            if (roomBuilder.Definition != null && roomBuilder.Definition.HasInvalidValues())
            {
                EditorGUILayout.HelpBox("All dimensions and thickness values must be at least 0.01.", MessageType.Warning);
            }

            DrawOpeningWarnings(roomBuilder);

            EditorGUILayout.Space();
            DrawActions(roomBuilder);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDefinitionSection(string title, params string[] propertyNames)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            for (int i = 0; i < propertyNames.Length; i++)
            {
                SerializedProperty property = definitionProperty.FindPropertyRelative(propertyNames[i]);
                EditorGUILayout.PropertyField(property);
            }

            EditorGUILayout.Space();
        }

        private void DrawOpeningsSection()
        {
            EditorGUILayout.LabelField("Openings", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Hole"))
                {
                    AddOpening(RoomOpeningKind.Hole);
                }

                if (GUILayout.Button("Add Door"))
                {
                    AddOpening(RoomOpeningKind.Door);
                }

                if (GUILayout.Button("Add Window"))
                {
                    AddOpening(RoomOpeningKind.Window);
                }
            }

            EditorGUILayout.Space(4f);

            for (int i = 0; i < openingsProperty.arraySize; i++)
            {
                DrawOpeningElement(i);
            }

            if (openingsProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Add holes, doors, or windows to cut any surface. Cut depth follows the target wall, floor, or ceiling thickness automatically.", MessageType.Info);
            }

            EditorGUILayout.Space();
        }

        private void DrawOpeningElement(int index)
        {
            SerializedProperty openingProperty = openingsProperty.GetArrayElementAtIndex(index);
            SerializedProperty enabledProperty = openingProperty.FindPropertyRelative("enabled");
            SerializedProperty kindProperty = openingProperty.FindPropertyRelative("kind");
            SerializedProperty targetPartProperty = openingProperty.FindPropertyRelative("targetPart");
            SerializedProperty centerProperty = openingProperty.FindPropertyRelative("center");
            SerializedProperty sizeProperty = openingProperty.FindPropertyRelative("size");

            string label = "Opening " + (index + 1) + " - " + kindProperty.enumDisplayNames[kindProperty.enumValueIndex];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            using (new EditorGUILayout.HorizontalScope())
            {
                enabledProperty.boolValue = EditorGUILayout.Toggle(enabledProperty.boolValue, GUILayout.Width(18f));
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

                if (GUILayout.Button("Duplicate", GUILayout.Width(80f)))
                {
                    RoomOpeningKind kind = (RoomOpeningKind)kindProperty.enumValueIndex;
                    RoomPartType targetPart = (RoomPartType)targetPartProperty.enumValueIndex;
                    Vector2 center = centerProperty.vector2Value;
                    Vector2 size = sizeProperty.vector2Value;
                    bool isEnabled = enabledProperty.boolValue;

                    openingsProperty.InsertArrayElementAtIndex(index + 1);
                    SerializedProperty copyProperty = openingsProperty.GetArrayElementAtIndex(index + 1);
                    copyProperty.FindPropertyRelative("id").stringValue = string.Empty;
                    copyProperty.FindPropertyRelative("enabled").boolValue = isEnabled;
                    copyProperty.FindPropertyRelative("kind").enumValueIndex = (int)kind;
                    copyProperty.FindPropertyRelative("targetPart").enumValueIndex = (int)targetPart;
                    copyProperty.FindPropertyRelative("center").vector2Value = center;
                    copyProperty.FindPropertyRelative("size").vector2Value = size;
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                {
                    openingsProperty.DeleteArrayElementAtIndex(index);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.PropertyField(kindProperty);
            EditorGUILayout.PropertyField(targetPartProperty, new GUIContent("Target Surface"));
            EditorGUILayout.PropertyField(centerProperty, new GUIContent("Position"));
            EditorGUILayout.PropertyField(sizeProperty, new GUIContent("Size"));

            if ((RoomOpeningKind)kindProperty.enumValueIndex == RoomOpeningKind.Door)
            {
                EditorGUILayout.HelpBox("Doors snap to the bottom edge of the selected wall automatically. Position X controls the horizontal offset.", MessageType.None);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }

        private void DrawOpeningWarnings(RoomBuilderTool.RoomBuilder roomBuilder)
        {
            if (roomBuilder.Definition == null)
            {
                return;
            }

            List<string> messages = roomBuilder.Definition.GetOpeningValidationMessages();
            for (int i = 0; i < messages.Count; i++)
            {
                EditorGUILayout.HelpBox(messages[i], MessageType.Warning);
            }
        }

        private void DrawActions(RoomBuilderTool.RoomBuilder roomBuilder)
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild"))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(roomBuilder, "Rebuild Room");
                    roomBuilder.Rebuild();
                    EditorUtility.SetDirty(roomBuilder);
                }

                if (GUILayout.Button("Reset Defaults"))
                {
                    Undo.RecordObject(roomBuilder, "Reset Room Defaults");
                    roomBuilder.ResetDefinitionToDefaults();
                    serializedObject.Update();

                    if (roomBuilder.AutoRebuild)
                    {
                        roomBuilder.Rebuild();
                    }

                    EditorUtility.SetDirty(roomBuilder);
                }
            }
        }

        private void AddOpening(RoomOpeningKind kind)
        {
            int index = openingsProperty.arraySize;
            openingsProperty.InsertArrayElementAtIndex(index);
            SerializedProperty openingProperty = openingsProperty.GetArrayElementAtIndex(index);

            openingProperty.FindPropertyRelative("id").stringValue = string.Empty;
            openingProperty.FindPropertyRelative("enabled").boolValue = true;
            openingProperty.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            openingProperty.FindPropertyRelative("targetPart").enumValueIndex = (int)RoomPartType.NorthWall;

            Vector2 center = Vector2.zero;
            Vector2 size = new Vector2(1f, 1f);

            switch (kind)
            {
                case RoomOpeningKind.Door:
                    center = new Vector2(0f, 1f);
                    size = new Vector2(1.2f, 2.2f);
                    break;
                case RoomOpeningKind.Window:
                    center = new Vector2(0f, 1.5f);
                    size = new Vector2(1.5f, 1.2f);
                    break;
                case RoomOpeningKind.Hole:
                    center = Vector2.zero;
                    size = new Vector2(1f, 1f);
                    break;
            }

            openingProperty.FindPropertyRelative("center").vector2Value = center;
            openingProperty.FindPropertyRelative("size").vector2Value = size;
        }
    }
}
