using System.Collections.Generic;
using UnityEngine;

namespace RoomBuilderTool
{
    [System.Serializable]
    public class RoomDefinition
    {
        public const float MinimumValue = 0.01f;

        public float width = 4f;
        public float length = 4f;
        public float height = 3f;

        public float eastWallThickness = 0.2f;
        public float westWallThickness = 0.2f;
        public float northWallThickness = 0.2f;
        public float southWallThickness = 0.2f;

        public float floorThickness = 0.2f;
        public float ceilingThickness = 0.2f;

        public List<RoomOpeningDefinition> openings = new List<RoomOpeningDefinition>();

        public void ClampToMinimum()
        {
            if (openings == null)
            {
                openings = new List<RoomOpeningDefinition>();
            }

            width = Mathf.Max(MinimumValue, width);
            length = Mathf.Max(MinimumValue, length);
            height = Mathf.Max(MinimumValue, height);
            eastWallThickness = Mathf.Max(MinimumValue, eastWallThickness);
            westWallThickness = Mathf.Max(MinimumValue, westWallThickness);
            northWallThickness = Mathf.Max(MinimumValue, northWallThickness);
            southWallThickness = Mathf.Max(MinimumValue, southWallThickness);
            floorThickness = Mathf.Max(MinimumValue, floorThickness);
            ceilingThickness = Mathf.Max(MinimumValue, ceilingThickness);

            for (int i = 0; i < openings.Count; i++)
            {
                if (openings[i] == null)
                {
                    openings[i] = new RoomOpeningDefinition();
                }

                openings[i].EnsureId();
                openings[i].ClampSize(MinimumValue);
            }
        }

        public bool HasInvalidValues()
        {
            return width < MinimumValue
                || length < MinimumValue
                || height < MinimumValue
                || eastWallThickness < MinimumValue
                || westWallThickness < MinimumValue
                || northWallThickness < MinimumValue
                || southWallThickness < MinimumValue
                || floorThickness < MinimumValue
                || ceilingThickness < MinimumValue;
        }

        public void ResetToDefaults()
        {
            width = 4f;
            length = 4f;
            height = 3f;
            eastWallThickness = 0.2f;
            westWallThickness = 0.2f;
            northWallThickness = 0.2f;
            southWallThickness = 0.2f;
            floorThickness = 0.2f;
            ceilingThickness = 0.2f;
            openings.Clear();
        }

        public Vector2 GetOpeningSurfaceSize(RoomPartType partType)
        {
            switch (partType)
            {
                case RoomPartType.EastWall:
                case RoomPartType.WestWall:
                    return new Vector2(length, height);
                case RoomPartType.NorthWall:
                case RoomPartType.SouthWall:
                    return new Vector2(width, height);
                case RoomPartType.Floor:
                case RoomPartType.Ceiling:
                    return new Vector2(width, length);
                default:
                    return Vector2.zero;
            }
        }

        public float GetSurfaceThickness(RoomPartType partType)
        {
            switch (partType)
            {
                case RoomPartType.EastWall:
                    return eastWallThickness;
                case RoomPartType.WestWall:
                    return westWallThickness;
                case RoomPartType.NorthWall:
                    return northWallThickness;
                case RoomPartType.SouthWall:
                    return southWallThickness;
                case RoomPartType.Floor:
                    return floorThickness;
                case RoomPartType.Ceiling:
                    return ceilingThickness;
                default:
                    return MinimumValue;
            }
        }

        public bool IsOpeningKindSupported(RoomOpeningDefinition opening)
        {
            if (opening == null)
            {
                return false;
            }

            if (opening.kind == RoomOpeningKind.Hole)
            {
                return true;
            }

            return opening.targetPart != RoomPartType.Floor && opening.targetPart != RoomPartType.Ceiling;
        }

        public RoomOpeningDefinition GetOpeningById(string openingId)
        {
            if (string.IsNullOrEmpty(openingId) || openings == null)
            {
                return null;
            }

            for (int i = 0; i < openings.Count; i++)
            {
                if (openings[i] != null && openings[i].id == openingId)
                {
                    return openings[i];
                }
            }

            return null;
        }

        public List<string> GetOpeningValidationMessages()
        {
            var messages = new List<string>();
            var validatedRects = new Dictionary<RoomPartType, List<Rect>>();

            for (int i = 0; i < openings.Count; i++)
            {
                RoomOpeningDefinition opening = openings[i];
                if (opening == null || !opening.enabled)
                {
                    continue;
                }

                opening.EnsureId();

                if (!IsOpeningKindSupported(opening))
                {
                    messages.Add(GetOpeningLabel(i, opening) + " only supports wall surfaces.");
                    continue;
                }

                if (opening.size.x < MinimumValue || opening.size.y < MinimumValue)
                {
                    messages.Add(GetOpeningLabel(i, opening) + " size must be at least 0.01.");
                    continue;
                }

                Rect rect = GetOpeningRect(opening);
                Vector2 surfaceSize = GetOpeningSurfaceSize(opening.targetPart);
                Rect bounds = new Rect(-surfaceSize.x * 0.5f, -surfaceSize.y * 0.5f, surfaceSize.x, surfaceSize.y);

                if (!RectFitsInside(bounds, rect))
                {
                    messages.Add(GetOpeningLabel(i, opening) + " is outside the valid area for " + opening.targetPart + ".");
                    continue;
                }

                if (opening.kind == RoomOpeningKind.Door)
                {
                    float expectedBottom = -surfaceSize.y * 0.5f;
                    if (Mathf.Abs(rect.yMin - expectedBottom) > MinimumValue)
                    {
                        messages.Add(GetOpeningLabel(i, opening) + " door must sit on the bottom edge of the wall.");
                    }
                }

                if (!validatedRects.TryGetValue(opening.targetPart, out List<Rect> existingRects))
                {
                    existingRects = new List<Rect>();
                    validatedRects.Add(opening.targetPart, existingRects);
                }

                for (int rectIndex = 0; rectIndex < existingRects.Count; rectIndex++)
                {
                    if (existingRects[rectIndex].Overlaps(rect))
                    {
                        messages.Add(GetOpeningLabel(i, opening) + " overlaps another opening on " + opening.targetPart + ".");
                        break;
                    }
                }

                existingRects.Add(rect);
            }

            return messages;
        }

        public Rect GetOpeningRect(RoomOpeningDefinition opening)
        {
            Vector2 surfaceSize = GetOpeningSurfaceSize(opening.targetPart);
            Vector2 center = opening.center;

            if (opening.kind == RoomOpeningKind.Door)
            {
                center.y = -surfaceSize.y * 0.5f + opening.size.y * 0.5f;
            }

            Vector2 halfSize = opening.size * 0.5f;
            return Rect.MinMaxRect(
                center.x - halfSize.x,
                center.y - halfSize.y,
                center.x + halfSize.x,
                center.y + halfSize.y);
        }

        private static string GetOpeningLabel(int index, RoomOpeningDefinition opening)
        {
            return "Opening " + (index + 1) + " (" + opening.kind + ")";
        }

        private static bool RectFitsInside(Rect bounds, Rect rect)
        {
            return rect.xMin >= bounds.xMin - MinimumValue
                && rect.xMax <= bounds.xMax + MinimumValue
                && rect.yMin >= bounds.yMin - MinimumValue
                && rect.yMax <= bounds.yMax + MinimumValue;
        }
    }
}
