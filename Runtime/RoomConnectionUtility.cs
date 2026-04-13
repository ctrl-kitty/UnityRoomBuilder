using System.Collections.Generic;
using UnityEngine;

namespace RoomBuilderTool
{
    public struct OpeningWorldData
    {
        public RoomBuilder room;
        public RoomOpeningDefinition opening;
        public Vector3 center;
        public Vector3 outerFaceCenter;
        public Vector3 outwardNormal;
        public Vector3 right;
        public Vector3 up;
        public float width;
        public float height;
        public float thickness;
    }

    public struct CorridorRun
    {
        public Vector3 start;
        public Vector3 end;
        public Vector3 forward;
        public float length;
    }

    public struct CorridorSolution
    {
        public CorridorPathMode pathMode;
        public Vector3 up;
        public Vector3 right;
        public float clearWidth;
        public float clearHeight;
        public float floorLevel;
        public CorridorRun firstRun;
        public CorridorRun secondRun;
        public bool hasSecondRun;
    }

    public static class RoomConnectionUtility
    {
        private const float DirectionTolerance = 0.98f;
        private const float MinimumLength = 0.01f;

        public static bool TryResolveOpening(RoomOpeningReference openingReference, out OpeningWorldData data, out string error)
        {
            data = default;
            error = null;

            if (openingReference == null || openingReference.room == null)
            {
                error = "Opening reference is missing a room.";
                return false;
            }

            RoomBuilder room = openingReference.room;
            RoomDefinition definition = room.Definition;
            if (definition == null)
            {
                error = "Room definition is missing.";
                return false;
            }

            if (!HasUniformScale(room.transform.lossyScale))
            {
                error = "Room must use uniform scale for corridor connections.";
                return false;
            }

            RoomOpeningDefinition opening = definition.GetOpeningById(openingReference.openingId);
            if (opening == null)
            {
                error = "Opening could not be found on the selected room.";
                return false;
            }

            if (!opening.enabled)
            {
                error = "Opening is disabled.";
                return false;
            }

            if (opening.targetPart == RoomPartType.Floor || opening.targetPart == RoomPartType.Ceiling)
            {
                error = "Corridors only support wall openings in this version.";
                return false;
            }

            if (opening.kind == RoomOpeningKind.Window)
            {
                error = "Windows cannot be used as corridor connections in this version.";
                return false;
            }

            Rect rect = definition.GetOpeningRect(opening);
            Vector2 center2D = rect.center;
            float thickness = definition.GetSurfaceThickness(opening.targetPart);
            Transform transform = room.transform;

            Vector3 rootLocalPosition = GetSurfaceRootLocalPosition(definition, opening.targetPart);
            Vector3 localOpeningCenter;
            Vector3 outwardNormal;
            Vector3 right;

            switch (opening.targetPart)
            {
                case RoomPartType.NorthWall:
                    localOpeningCenter = rootLocalPosition + new Vector3(center2D.x, center2D.y, 0f);
                    outwardNormal = transform.forward;
                    right = transform.right;
                    break;
                case RoomPartType.SouthWall:
                    localOpeningCenter = rootLocalPosition + new Vector3(center2D.x, center2D.y, 0f);
                    outwardNormal = -transform.forward;
                    right = transform.right;
                    break;
                case RoomPartType.EastWall:
                    localOpeningCenter = rootLocalPosition + new Vector3(0f, center2D.y, center2D.x);
                    outwardNormal = transform.right;
                    right = transform.forward;
                    break;
                case RoomPartType.WestWall:
                    localOpeningCenter = rootLocalPosition + new Vector3(0f, center2D.y, center2D.x);
                    outwardNormal = -transform.right;
                    right = transform.forward;
                    break;
                default:
                    error = "Unsupported opening target.";
                    return false;
            }

            Vector3 up = transform.up;
            Vector3 center = transform.TransformPoint(localOpeningCenter);

            data = new OpeningWorldData
            {
                room = room,
                opening = opening,
                center = center,
                outerFaceCenter = center + outwardNormal * (thickness * 0.5f),
                outwardNormal = outwardNormal.normalized,
                right = right.normalized,
                up = up.normalized,
                width = rect.width,
                height = rect.height,
                thickness = thickness
            };

            return true;
        }

        public static bool TrySolve(
            OpeningWorldData source,
            OpeningWorldData target,
            CorridorPathMode mode,
            float wallThickness,
            float floorThickness,
            float ceilingThickness,
            out CorridorSolution solution,
            out string error)
        {
            solution = default;
            error = null;

            if (source.room == null || target.room == null)
            {
                error = "Both corridor endpoints need valid rooms.";
                return false;
            }

            if (source.room == target.room)
            {
                error = "Choose openings from two different rooms.";
                return false;
            }

            if (Vector3.Dot(source.up, target.up) < DirectionTolerance)
            {
                error = "Openings must share the same up direction.";
                return false;
            }

            switch (mode)
            {
                case CorridorPathMode.Straight:
                    return TrySolveStraight(source, target, wallThickness, floorThickness, ceilingThickness, out solution, out error);
                case CorridorPathMode.LShape:
                    return TrySolveLShape(source, target, wallThickness, floorThickness, ceilingThickness, out solution, out error);
                default:
                    error = "Unsupported corridor mode.";
                    return false;
            }
        }

        public static List<RoomOpeningDefinition> GetAvailableCorridorOpenings(RoomBuilder room)
        {
            var openings = new List<RoomOpeningDefinition>();
            if (room == null || room.Definition == null || room.Definition.openings == null)
            {
                return openings;
            }

            for (int i = 0; i < room.Definition.openings.Count; i++)
            {
                RoomOpeningDefinition opening = room.Definition.openings[i];
                if (opening != null)
                {
                    openings.Add(opening);
                }
            }

            return openings;
        }

        public static List<string> GetOpeningLabels(RoomBuilder room)
        {
            var labels = new List<string>();
            List<RoomOpeningDefinition> openings = GetAvailableCorridorOpenings(room);
            for (int i = 0; i < openings.Count; i++)
            {
                RoomOpeningDefinition opening = openings[i];

                string idSuffix = string.IsNullOrEmpty(opening.id) || opening.id.Length < 6
                    ? opening.id
                    : opening.id.Substring(0, 6);
                labels.Add((i + 1) + ". " + opening.kind + " / " + opening.targetPart + " / " + opening.size.x.ToString("0.##") + "x" + opening.size.y.ToString("0.##") + " / " + idSuffix);
            }

            return labels;
        }

        private static bool TrySolveStraight(
            OpeningWorldData source,
            OpeningWorldData target,
            float wallThickness,
            float floorThickness,
            float ceilingThickness,
            out CorridorSolution solution,
            out string error)
        {
            solution = default;
            error = null;

            if (Vector3.Dot(source.outwardNormal, target.outwardNormal) > -DirectionTolerance)
            {
                error = "Straight corridors require opposite-facing openings.";
                return false;
            }

            Vector3 axis = source.outwardNormal;
            if (Mathf.Abs(Vector3.Dot(axis, target.outwardNormal) + 1f) > 0.05f)
            {
                error = "Openings are not aligned for a straight corridor.";
                return false;
            }

            float widthMin;
            float widthMax;
            if (!TryGetIntervalOverlap(source.center, source.right, source.width, target.center, source.right, target.width, out widthMin, out widthMax))
            {
                error = "Openings do not overlap horizontally for a straight corridor.";
                return false;
            }

            float heightMin = Mathf.Max(Vector3.Dot(source.center, source.up) - source.height * 0.5f, Vector3.Dot(target.center, source.up) - target.height * 0.5f);
            float heightMax = Mathf.Min(Vector3.Dot(source.center, source.up) + source.height * 0.5f, Vector3.Dot(target.center, source.up) + target.height * 0.5f);
            if (heightMax - heightMin < RoomDefinition.MinimumValue)
            {
                error = "Openings do not overlap vertically for a straight corridor.";
                return false;
            }

            float clearWidth = widthMax - widthMin;
            float clearHeight = heightMax - heightMin;

            float rightCenter = (widthMin + widthMax) * 0.5f;
            float upCenter = (heightMin + heightMax) * 0.5f;
            float sourceRightCenter = Vector3.Dot(source.center, source.right);
            float sourceUpCenter = Vector3.Dot(source.center, source.up);
            float targetRightCenter = Vector3.Dot(target.center, source.right);
            float targetUpCenter = Vector3.Dot(target.center, source.up);

            Vector3 start = source.outerFaceCenter
                + source.right * (rightCenter - sourceRightCenter)
                + source.up * (upCenter - sourceUpCenter);
            Vector3 end = target.outerFaceCenter
                + source.right * (rightCenter - targetRightCenter)
                + source.up * (upCenter - targetUpCenter);

            float length = Vector3.Dot(end - start, axis);
            if (length < MinimumLength)
            {
                error = "Rooms are too close or not facing each other for a straight corridor.";
                return false;
            }

            solution = new CorridorSolution
            {
                pathMode = CorridorPathMode.Straight,
                up = source.up,
                right = source.right,
                clearWidth = clearWidth,
                clearHeight = clearHeight,
                floorLevel = heightMin,
                firstRun = new CorridorRun
                {
                    start = start,
                    end = start + axis * length,
                    forward = axis,
                    length = length
                },
                hasSecondRun = false
            };

            return true;
        }

        private static bool TrySolveLShape(
            OpeningWorldData source,
            OpeningWorldData target,
            float wallThickness,
            float floorThickness,
            float ceilingThickness,
            out CorridorSolution solution,
            out string error)
        {
            solution = default;
            error = null;

            if (Mathf.Abs(Vector3.Dot(source.outwardNormal, target.outwardNormal)) > 0.2f)
            {
                error = "L-shaped corridors require perpendicular wall openings.";
                return false;
            }

            float heightMin = Mathf.Max(Vector3.Dot(source.center, source.up) - source.height * 0.5f, Vector3.Dot(target.center, source.up) - target.height * 0.5f);
            float heightMax = Mathf.Min(Vector3.Dot(source.center, source.up) + source.height * 0.5f, Vector3.Dot(target.center, source.up) + target.height * 0.5f);
            if (heightMax - heightMin < RoomDefinition.MinimumValue)
            {
                error = "Openings do not overlap vertically for an L-shaped corridor.";
                return false;
            }

            float clearWidth = Mathf.Min(source.width, target.width);
            float clearHeight = heightMax - heightMin;

            float upCenter = (heightMin + heightMax) * 0.5f;
            Vector3 start = source.outerFaceCenter + source.up * (upCenter - Vector3.Dot(source.center, source.up));
            Vector3 end = target.outerFaceCenter + target.up * (upCenter - Vector3.Dot(target.center, target.up));

            Vector2 start2 = new Vector2(start.x, start.z);
            Vector2 end2 = new Vector2(end.x, end.z);
            Vector2 sourceDir = new Vector2(source.outwardNormal.x, source.outwardNormal.z).normalized;
            Vector2 targetApproachDir = new Vector2(-target.outwardNormal.x, -target.outwardNormal.z).normalized;

            if (sourceDir.sqrMagnitude < 0.99f || targetApproachDir.sqrMagnitude < 0.99f)
            {
                error = "L-shaped corridors only support horizontal wall openings.";
                return false;
            }

            if (!TryGetBestElbow(start2, end2, sourceDir, targetApproachDir, out Vector2 elbow2, out float sourceDistance, out float targetDistance))
            {
                error = "Could not find a valid elbow point for the L-shaped corridor.";
                return false;
            }

            if (sourceDistance < MinimumLength || targetDistance < MinimumLength)
            {
                error = "L-shaped corridor legs are too short.";
                return false;
            }

            Vector3 elbow = new Vector3(elbow2.x, start.y, elbow2.y);
            CorridorRun firstRun = new CorridorRun
            {
                start = start,
                end = elbow,
                forward = (elbow - start).normalized,
                length = Vector3.Distance(start, elbow)
            };
            CorridorRun secondRun = new CorridorRun
            {
                start = elbow,
                end = end,
                forward = (end - elbow).normalized,
                length = Vector3.Distance(elbow, end)
            };

            solution = new CorridorSolution
            {
                pathMode = CorridorPathMode.LShape,
                up = source.up,
                right = Vector3.Cross(source.up, firstRun.forward).normalized,
                clearWidth = clearWidth,
                clearHeight = clearHeight,
                floorLevel = heightMin,
                firstRun = firstRun,
                secondRun = secondRun,
                hasSecondRun = true
            };

            return true;
        }

        private static bool TryGetBestElbow(Vector2 start, Vector2 end, Vector2 sourceDir, Vector2 targetApproachDir, out Vector2 elbow, out float sourceDistance, out float targetDistance)
        {
            elbow = Vector2.zero;
            sourceDistance = 0f;
            targetDistance = 0f;

            Vector2[] candidates =
            {
                new Vector2(start.x, end.y),
                new Vector2(end.x, start.y)
            };

            bool found = false;
            float bestScore = float.MaxValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                Vector2 candidate = candidates[i];
                Vector2 sourceVector = candidate - start;
                Vector2 targetVector = end - candidate;

                float candidateSourceDistance = sourceVector.magnitude;
                float candidateTargetDistance = targetVector.magnitude;
                if (candidateSourceDistance < MinimumLength || candidateTargetDistance < MinimumLength)
                {
                    continue;
                }

                Vector2 sourceDirection = sourceVector / candidateSourceDistance;
                Vector2 targetDirection = targetVector / candidateTargetDistance;
                if (Vector2.Dot(sourceDirection, sourceDir) < DirectionTolerance)
                {
                    continue;
                }

                if (Vector2.Dot(targetDirection, targetApproachDir) < DirectionTolerance)
                {
                    continue;
                }

                float score = candidateSourceDistance + candidateTargetDistance;
                if (!found || score < bestScore)
                {
                    found = true;
                    bestScore = score;
                    elbow = candidate;
                    sourceDistance = candidateSourceDistance;
                    targetDistance = candidateTargetDistance;
                }
            }

            return found;
        }

        private static bool TryGetIntervalOverlap(Vector3 centerA, Vector3 axisA, float sizeA, Vector3 centerB, Vector3 projectionAxis, float sizeB, out float min, out float max)
        {
            float centerValueA = Vector3.Dot(centerA, projectionAxis);
            float centerValueB = Vector3.Dot(centerB, projectionAxis);
            float halfA = sizeA * 0.5f;
            float halfB = sizeB * 0.5f;

            min = Mathf.Max(centerValueA - halfA, centerValueB - halfB);
            max = Mathf.Min(centerValueA + halfA, centerValueB + halfB);
            return max - min >= RoomDefinition.MinimumValue;
        }

        private static Vector3 GetSurfaceRootLocalPosition(RoomDefinition definition, RoomPartType partType)
        {
            float slabOffsetX = (definition.eastWallThickness - definition.westWallThickness) * 0.5f;

            switch (partType)
            {
                case RoomPartType.EastWall:
                    return new Vector3(definition.width * 0.5f + definition.eastWallThickness * 0.5f, definition.height * 0.5f, 0f);
                case RoomPartType.WestWall:
                    return new Vector3(-definition.width * 0.5f - definition.westWallThickness * 0.5f, definition.height * 0.5f, 0f);
                case RoomPartType.NorthWall:
                    return new Vector3(slabOffsetX, definition.height * 0.5f, definition.length * 0.5f + definition.northWallThickness * 0.5f);
                case RoomPartType.SouthWall:
                    return new Vector3(slabOffsetX, definition.height * 0.5f, -definition.length * 0.5f - definition.southWallThickness * 0.5f);
                default:
                    return Vector3.zero;
            }
        }

        private static bool HasUniformScale(Vector3 scale)
        {
            return Mathf.Abs(scale.x - scale.y) < 0.0001f && Mathf.Abs(scale.x - scale.z) < 0.0001f;
        }
    }
}
