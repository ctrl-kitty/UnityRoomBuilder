using UnityEditor;
using UnityEngine;

namespace RoomBuilderTool.Editor
{
    public class RoomBuilderWindow : EditorWindow
    {
        private string roomName = "Room";
        private readonly RoomDefinition definition = new RoomDefinition();
        private bool autoRebuild = true;

        [MenuItem("Tools/Room Builder/Create Room")]
        public static void OpenWindow()
        {
            GetWindow<RoomBuilderWindow>("Create Room");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Create Rectangular Room", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            roomName = EditorGUILayout.TextField("Room Name", roomName);

            DrawFloatField("Width", ref definition.width);
            DrawFloatField("Length", ref definition.length);
            DrawFloatField("Height", ref definition.height);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Wall Thickness", EditorStyles.boldLabel);
            DrawFloatField("East", ref definition.eastWallThickness);
            DrawFloatField("West", ref definition.westWallThickness);
            DrawFloatField("North", ref definition.northWallThickness);
            DrawFloatField("South", ref definition.southWallThickness);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Surface Thickness", EditorStyles.boldLabel);
            DrawFloatField("Floor", ref definition.floorThickness);
            DrawFloatField("Ceiling", ref definition.ceilingThickness);

            EditorGUILayout.Space();
            autoRebuild = EditorGUILayout.Toggle("Auto Rebuild", autoRebuild);

            definition.ClampToMinimum();

            if (GUILayout.Button("Create Room", GUILayout.Height(32f)))
            {
                CreateRoom();
            }
        }

        private static void DrawFloatField(string label, ref float value)
        {
            value = EditorGUILayout.FloatField(label, value);
        }

        private void CreateRoom()
        {
            definition.ClampToMinimum();

            string finalName = string.IsNullOrWhiteSpace(roomName) ? "Room" : roomName.Trim();
            GameObject roomObject = new GameObject(finalName);
            Undo.RegisterCreatedObjectUndo(roomObject, "Create Room");

            if (SceneView.lastActiveSceneView != null)
            {
                roomObject.transform.position = SceneView.lastActiveSceneView.pivot;
            }

            var builder = roomObject.AddComponent<RoomBuilderTool.RoomBuilder>();
            builder.AutoRebuild = autoRebuild;

            builder.Definition.width = definition.width;
            builder.Definition.length = definition.length;
            builder.Definition.height = definition.height;
            builder.Definition.eastWallThickness = definition.eastWallThickness;
            builder.Definition.westWallThickness = definition.westWallThickness;
            builder.Definition.northWallThickness = definition.northWallThickness;
            builder.Definition.southWallThickness = definition.southWallThickness;
            builder.Definition.floorThickness = definition.floorThickness;
            builder.Definition.ceilingThickness = definition.ceilingThickness;

            builder.Rebuild();

            Selection.activeGameObject = roomObject;
            EditorGUIUtility.PingObject(roomObject);
        }
    }
}
