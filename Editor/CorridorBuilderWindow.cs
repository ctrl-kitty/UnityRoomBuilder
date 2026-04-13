using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RoomBuilderTool.Editor
{
    public class CorridorBuilderWindow : EditorWindow
    {
        private RoomBuilder sourceRoom;
        private RoomBuilder targetRoom;
        private int sourceOpeningIndex;
        private int targetOpeningIndex;
        private CorridorPathMode pathMode = CorridorPathMode.Straight;
        private float wallThickness = 0.2f;
        private float floorThickness = 0.2f;
        private float ceilingThickness = 0.2f;

        [MenuItem("Tools/Room Builder/Connect Openings")]
        public static void OpenWindow()
        {
            GetWindow<CorridorBuilderWindow>("Connect Openings");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Connect Room Openings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            RoomBuilder newSourceRoom = (RoomBuilder)EditorGUILayout.ObjectField("Source Room", sourceRoom, typeof(RoomBuilder), true);
            if (newSourceRoom != sourceRoom)
            {
                sourceRoom = newSourceRoom;
                sourceOpeningIndex = 0;
            }
            sourceOpeningIndex = DrawOpeningPopup("Source Opening", sourceRoom, sourceOpeningIndex);

            EditorGUILayout.Space(4f);

            RoomBuilder newTargetRoom = (RoomBuilder)EditorGUILayout.ObjectField("Target Room", targetRoom, typeof(RoomBuilder), true);
            if (newTargetRoom != targetRoom)
            {
                targetRoom = newTargetRoom;
                targetOpeningIndex = 0;
            }
            targetOpeningIndex = DrawOpeningPopup("Target Opening", targetRoom, targetOpeningIndex);

            EditorGUILayout.Space();

            pathMode = (CorridorPathMode)EditorGUILayout.EnumPopup("Path Mode", pathMode);
            wallThickness = Mathf.Max(RoomDefinition.MinimumValue, EditorGUILayout.FloatField("Wall Thickness", wallThickness));
            floorThickness = Mathf.Max(RoomDefinition.MinimumValue, EditorGUILayout.FloatField("Floor Thickness", floorThickness));
            ceilingThickness = Mathf.Max(RoomDefinition.MinimumValue, EditorGUILayout.FloatField("Ceiling Thickness", ceilingThickness));

            EditorGUILayout.Space();
            DrawPreview();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!CanCreateConnection(out _)))
            {
                if (GUILayout.Button("Create Corridor", GUILayout.Height(32f)))
                {
                    CreateCorridor();
                }
            }
        }

        private int DrawOpeningPopup(string label, RoomBuilder room, int currentIndex)
        {
            List<string> labels = RoomConnectionUtility.GetOpeningLabels(room);
            if (labels.Count == 0)
            {
                EditorGUILayout.Popup(label, -1, new[] { "No openings available" });
                return 0;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, labels.Count - 1);
            return EditorGUILayout.Popup(label, currentIndex, labels.ToArray());
        }

        private void DrawPreview()
        {
            if (!TryBuildTemporaryDefinition(out CorridorDefinition definition))
            {
                EditorGUILayout.HelpBox("Select two rooms and one opening on each room.", MessageType.Info);
                return;
            }

            definition.ClampToMinimum();

            if (!RoomConnectionUtility.TryResolveOpening(definition.source, out OpeningWorldData source, out string error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
                return;
            }

            if (!RoomConnectionUtility.TryResolveOpening(definition.target, out OpeningWorldData target, out error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
                return;
            }

            if (!RoomConnectionUtility.TrySolve(
                source,
                target,
                definition.pathMode,
                definition.wallThickness,
                definition.floorThickness,
                definition.ceilingThickness,
                out CorridorSolution solution,
                out error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                "Valid " + solution.pathMode + " corridor\n"
                + "Width: " + solution.clearWidth.ToString("0.##") + "\n"
                + "Height: " + solution.clearHeight.ToString("0.##") + "\n"
                + "Length: " + GetTotalLength(solution).ToString("0.##"),
                MessageType.Info);
        }

        private bool CanCreateConnection(out string error)
        {
            error = null;

            if (!TryBuildTemporaryDefinition(out CorridorDefinition definition))
            {
                return false;
            }

            definition.ClampToMinimum();

            if (!RoomConnectionUtility.TryResolveOpening(definition.source, out OpeningWorldData source, out error))
            {
                return false;
            }

            if (!RoomConnectionUtility.TryResolveOpening(definition.target, out OpeningWorldData target, out error))
            {
                return false;
            }

            return RoomConnectionUtility.TrySolve(
                source,
                target,
                definition.pathMode,
                definition.wallThickness,
                definition.floorThickness,
                definition.ceilingThickness,
                out CorridorSolution _,
                out error);
        }

        private bool TryBuildTemporaryDefinition(out CorridorDefinition definition)
        {
            definition = null;

            if (sourceRoom == null || targetRoom == null)
            {
                return false;
            }

            string sourceOpeningId = GetOpeningId(sourceRoom, sourceOpeningIndex);
            string targetOpeningId = GetOpeningId(targetRoom, targetOpeningIndex);
            if (string.IsNullOrEmpty(sourceOpeningId) || string.IsNullOrEmpty(targetOpeningId))
            {
                return false;
            }

            definition = new CorridorDefinition
            {
                source = new RoomOpeningReference { room = sourceRoom, openingId = sourceOpeningId },
                target = new RoomOpeningReference { room = targetRoom, openingId = targetOpeningId },
                pathMode = pathMode,
                wallThickness = wallThickness,
                floorThickness = floorThickness,
                ceilingThickness = ceilingThickness
            };
            return true;
        }

        private void CreateCorridor()
        {
            if (!CanCreateConnection(out string error) || !TryBuildTemporaryDefinition(out CorridorDefinition definition))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(error);
                }
                return;
            }

            var corridorObject = new GameObject("Corridor_Connection");
            Undo.RegisterCreatedObjectUndo(corridorObject, "Create Corridor");
            corridorObject.transform.position = Vector3.zero;
            corridorObject.transform.rotation = Quaternion.identity;

            CorridorBuilder builder = corridorObject.AddComponent<CorridorBuilder>();
            builder.Definition.source.room = definition.source.room;
            builder.Definition.source.openingId = definition.source.openingId;
            builder.Definition.target.room = definition.target.room;
            builder.Definition.target.openingId = definition.target.openingId;
            builder.Definition.pathMode = definition.pathMode;
            builder.Definition.wallThickness = definition.wallThickness;
            builder.Definition.floorThickness = definition.floorThickness;
            builder.Definition.ceilingThickness = definition.ceilingThickness;
            builder.Rebuild();

            Selection.activeGameObject = corridorObject;
            EditorGUIUtility.PingObject(corridorObject);
        }

        private static string GetOpeningId(RoomBuilder room, int index)
        {
            List<RoomOpeningDefinition> openings = RoomConnectionUtility.GetAvailableCorridorOpenings(room);
            if (openings.Count == 0)
            {
                return null;
            }

            if (index < 0 || index >= openings.Count)
            {
                return null;
            }

            RoomOpeningDefinition opening = openings[index];
            return opening != null ? opening.id : null;
        }

        private static float GetTotalLength(CorridorSolution solution)
        {
            float length = solution.firstRun.length;
            if (solution.hasSecondRun)
            {
                length += solution.secondRun.length;
            }

            return length;
        }
    }
}
