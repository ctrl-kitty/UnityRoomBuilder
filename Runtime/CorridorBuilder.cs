using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RoomBuilderTool
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CorridorBuilder : MonoBehaviour
    {
        private const string PartPrefix = "CorridorPart_";

        [SerializeField] private CorridorDefinition definition = new CorridorDefinition();
        [SerializeField] private bool autoRebuild = true;

        public CorridorDefinition Definition => definition;

        public bool AutoRebuild
        {
            get => autoRebuild;
            set => autoRebuild = value;
        }

        public bool TryGetSolution(out CorridorSolution solution, out string error)
        {
            solution = default;
            error = null;
            EnsureDefinition();
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
                out solution,
                out error);
        }

        public void Rebuild()
        {
            EnsureDefinition();
            definition.ClampToMinimum();

            ClearGeneratedParts();

            if (!TryGetSolution(out CorridorSolution solution, out string _))
            {
                return;
            }

            Material[] materials = CaptureAnyRoomMaterials();

            if (solution.hasSecondRun)
            {
                BuildLShape(solution, definition.wallThickness, definition.floorThickness, definition.ceilingThickness, materials);
                BuildElbow(solution, definition.wallThickness, definition.floorThickness, definition.ceilingThickness, materials);
            }
            else
            {
                BuildRun(solution.firstRun, solution.clearWidth, solution.clearHeight, solution.up, definition.wallThickness, definition.floorThickness, definition.ceilingThickness, "A", materials, true, true, 0f, 0f, 0f, 0f);
            }
        }

        private void OnValidate()
        {
            EnsureDefinition();

            if (!autoRebuild)
            {
                return;
            }

#if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall -= DelayedRebuild;
                EditorApplication.delayCall += DelayedRebuild;
            }
            else
#endif
            {
                Rebuild();
            }
        }

        private void Reset()
        {
            Rebuild();
        }

        private void EnsureDefinition()
        {
            if (definition == null)
            {
                definition = new CorridorDefinition();
            }
        }

#if UNITY_EDITOR
        private void DelayedRebuild()
        {
            if (this == null || !autoRebuild)
            {
                return;
            }

            Rebuild();
        }
#endif

        private void BuildRun(
            CorridorRun run,
            float clearWidth,
            float clearHeight,
            Vector3 up,
            float wallThickness,
            float floorThickness,
            float ceilingThickness,
            string label,
            Material[] materials,
            bool buildLeftWall,
            bool buildRightWall,
            float leftWallTrimStart,
            float leftWallTrimEnd,
            float rightWallTrimStart,
            float rightWallTrimEnd)
        {
            if (run.length < RoomDefinition.MinimumValue)
            {
                return;
            }

            Vector3 forward = run.forward.normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 corridorCenter = (run.start + run.end) * 0.5f;
            Vector3 floorCenter = corridorCenter - up * (clearHeight * 0.5f + floorThickness * 0.5f);
            Vector3 ceilingCenter = corridorCenter + up * (clearHeight * 0.5f + ceilingThickness * 0.5f);
            Vector3 leftWallCenter = corridorCenter + right * (-clearWidth * 0.5f - wallThickness * 0.5f);
            Vector3 rightWallCenter = corridorCenter + right * (clearWidth * 0.5f + wallThickness * 0.5f);
            Quaternion rotation = Quaternion.LookRotation(forward, up);
            float leftTrimStart = Mathf.Max(0f, leftWallTrimStart);
            float leftTrimEnd = Mathf.Max(0f, leftWallTrimEnd);
            float rightTrimStart = Mathf.Max(0f, rightWallTrimStart);
            float rightTrimEnd = Mathf.Max(0f, rightWallTrimEnd);
            float leftWallLength = Mathf.Max(RoomDefinition.MinimumValue, run.length - leftTrimStart - leftTrimEnd);
            float rightWallLength = Mathf.Max(RoomDefinition.MinimumValue, run.length - rightTrimStart - rightTrimEnd);

            CreatePart(label + "_Floor", floorCenter, rotation, new Vector3(clearWidth + wallThickness * 2f, floorThickness, run.length), materials);
            CreatePart(label + "_Ceiling", ceilingCenter, rotation, new Vector3(clearWidth + wallThickness * 2f, ceilingThickness, run.length), materials);

            if (buildLeftWall)
            {
                Vector3 leftCenter = leftWallCenter + forward * ((leftTrimStart - leftTrimEnd) * 0.5f);
                CreatePart(label + "_LeftWall", leftCenter, rotation, new Vector3(wallThickness, clearHeight, leftWallLength), materials);
            }

            if (buildRightWall)
            {
                Vector3 rightCenterAdjusted = rightWallCenter + forward * ((rightTrimStart - rightTrimEnd) * 0.5f);
                CreatePart(label + "_RightWall", rightCenterAdjusted, rotation, new Vector3(wallThickness, clearHeight, rightWallLength), materials);
            }
        }

        private void BuildLShape(CorridorSolution solution, float wallThickness, float floorThickness, float ceilingThickness, Material[] materials)
        {
            Vector3 firstForward = solution.firstRun.forward.normalized;
            Vector3 secondForward = solution.secondRun.forward.normalized;
            Vector3 up = solution.up.normalized;
            Vector3 firstRight = Vector3.Cross(up, firstForward).normalized;
            Vector3 secondRight = Vector3.Cross(up, secondForward).normalized;
            float turnSign = Mathf.Sign(Vector3.Dot(Vector3.Cross(firstForward, secondForward), up));
            float outerWidth = solution.clearWidth + wallThickness * 2f;
            float trimAtElbow = outerWidth * 0.5f;

            bool firstOuterIsLeft = turnSign < 0f;
            bool secondOuterIsLeft = turnSign < 0f;

            BuildRun(
                solution.firstRun,
                solution.clearWidth,
                solution.clearHeight,
                solution.up,
                wallThickness,
                floorThickness,
                ceilingThickness,
                "A",
                materials,
                true,
                true,
                0f,
                firstOuterIsLeft ? trimAtElbow : 0f,
                0f,
                firstOuterIsLeft ? 0f : trimAtElbow);

            BuildRun(
                solution.secondRun,
                solution.clearWidth,
                solution.clearHeight,
                solution.up,
                wallThickness,
                floorThickness,
                ceilingThickness,
                "B",
                materials,
                true,
                true,
                secondOuterIsLeft ? trimAtElbow : 0f,
                0f,
                secondOuterIsLeft ? 0f : trimAtElbow,
                0f);
        }

        private void CreatePart(string nameSuffix, Vector3 worldCenter, Quaternion worldRotation, Vector3 scale, Material[] materials)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = PartPrefix + nameSuffix;
            part.transform.SetParent(transform, false);
            part.transform.position = worldCenter;
            part.transform.rotation = worldRotation;
            part.transform.localScale = scale;

            if (materials != null && materials.Length > 0)
            {
                MeshRenderer renderer = part.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private void BuildElbow(CorridorSolution solution, float wallThickness, float floorThickness, float ceilingThickness, Material[] materials)
        {
            Vector3 elbow = solution.firstRun.end;
            Vector3 firstForward = solution.firstRun.forward.normalized;
            Vector3 secondForward = solution.secondRun.forward.normalized;
            Vector3 up = solution.up.normalized;
            Vector3 firstRight = Vector3.Cross(up, firstForward).normalized;
            Vector3 secondRight = Vector3.Cross(up, secondForward).normalized;

            float turnSign = Mathf.Sign(Vector3.Dot(Vector3.Cross(firstForward, secondForward), up));
            if (Mathf.Abs(turnSign) < 0.5f)
            {
                return;
            }

            Vector3 outerSide1 = turnSign > 0f ? -firstRight : firstRight;
            Vector3 outerSide2 = turnSign > 0f ? -secondRight : secondRight;
            Vector3 innerSide1 = -outerSide1;
            Vector3 innerSide2 = -outerSide2;
            float outerWidth = solution.clearWidth + wallThickness * 2f;
            float halfOuterWidth = outerWidth * 0.5f;
            float quarterOuterWidth = outerWidth * 0.25f;
            Quaternion outerRotation = Quaternion.LookRotation(secondForward, up);
            Quaternion wallRotationA = Quaternion.LookRotation(firstForward, up);
            Quaternion wallRotationB = Quaternion.LookRotation(secondForward, up);

            Vector3 floorCenter = elbow + outerSide1 * quarterOuterWidth + outerSide2 * quarterOuterWidth
                - up * (solution.clearHeight * 0.5f + floorThickness * 0.5f);
            Vector3 ceilingCenter = elbow + outerSide1 * quarterOuterWidth + outerSide2 * quarterOuterWidth
                + up * (solution.clearHeight * 0.5f + ceilingThickness * 0.5f);
            Vector3 outerWallACenter = elbow
                + outerSide1 * (solution.clearWidth * 0.5f + wallThickness * 0.5f)
                + outerSide2 * quarterOuterWidth;
            Vector3 outerWallBCenter = elbow
                + outerSide2 * (solution.clearWidth * 0.5f + wallThickness * 0.5f)
                + outerSide1 * quarterOuterWidth;
            Vector3 cornerColumnCenter = elbow
                + outerSide1 * (solution.clearWidth * 0.5f + wallThickness * 0.5f)
                + outerSide2 * (solution.clearWidth * 0.5f + wallThickness * 0.5f);
            Vector3 innerCornerColumnCenter = elbow
                + innerSide1 * (solution.clearWidth * 0.5f + wallThickness * 0.5f)
                + innerSide2 * (solution.clearWidth * 0.5f + wallThickness * 0.5f);

            CreatePart("Elbow_Floor", floorCenter, outerRotation, new Vector3(halfOuterWidth, floorThickness, halfOuterWidth), materials);
            CreatePart("Elbow_Ceiling", ceilingCenter, outerRotation, new Vector3(halfOuterWidth, ceilingThickness, halfOuterWidth), materials);
            CreatePart("Elbow_OuterWallA", outerWallACenter, wallRotationA, new Vector3(wallThickness, solution.clearHeight, halfOuterWidth), materials);
            CreatePart("Elbow_OuterWallB", outerWallBCenter, wallRotationB, new Vector3(wallThickness, solution.clearHeight, halfOuterWidth), materials);
            CreatePart("Elbow_CornerColumn", cornerColumnCenter, outerRotation, new Vector3(wallThickness, solution.clearHeight, wallThickness), materials);
            CreatePart("Elbow_InnerCornerColumn", innerCornerColumnCenter, outerRotation, new Vector3(wallThickness, solution.clearHeight, wallThickness), materials);
        }

        private void ClearGeneratedParts()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith(PartPrefix))
                {
                    DestroyObjectImmediateSafe(child.gameObject);
                }
            }
        }

        private Material[] CaptureAnyRoomMaterials()
        {
            Material[] materials = CaptureRoomMaterials(definition.source != null ? definition.source.room : null);
            if (materials != null)
            {
                return materials;
            }

            return CaptureRoomMaterials(definition.target != null ? definition.target.room : null);
        }

        private static Material[] CaptureRoomMaterials(RoomBuilder room)
        {
            if (room == null)
            {
                return null;
            }

            for (int i = 0; i < room.transform.childCount; i++)
            {
                Transform partRoot = room.transform.GetChild(i);
                for (int childIndex = 0; childIndex < partRoot.childCount; childIndex++)
                {
                    MeshRenderer renderer = partRoot.GetChild(childIndex).GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0)
                    {
                        return renderer.sharedMaterials;
                    }
                }
            }

            return null;
        }

        private static void DestroyObjectImmediateSafe(GameObject target)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif
            Destroy(target);
        }
    }
}
