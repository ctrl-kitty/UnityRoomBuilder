using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RoomBuilderTool
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class RoomBuilder : MonoBehaviour
    {
        private const string GeneratedPartPrefix = "RoomPart_";
        private const string SegmentPrefix = "Segment_";
        private const float SegmentEpsilon = 0.0001f;

        [SerializeField] private RoomDefinition definition = new RoomDefinition();
        [SerializeField] private bool autoRebuild = true;

        private struct SurfaceLayout
        {
            public Vector3 rootPosition;
            public Vector2 geometryCenter;
            public Vector2 geometrySize;
            public Vector2 usableSize;
            public float thickness;
        }

        public RoomDefinition Definition => definition;

        public bool AutoRebuild
        {
            get => autoRebuild;
            set => autoRebuild = value;
        }

        public void Rebuild()
        {
            EnsureDefinition();
            definition.ClampToMinimum();

            float outerWidth = definition.width + definition.eastWallThickness + definition.westWallThickness;
            float outerLength = definition.length + definition.northWallThickness + definition.southWallThickness;
            float slabOffsetX = (definition.eastWallThickness - definition.westWallThickness) * 0.5f;
            float slabOffsetZ = (definition.northWallThickness - definition.southWallThickness) * 0.5f;

            BuildSurface(
                RoomPartType.Floor,
                new SurfaceLayout
                {
                    rootPosition = new Vector3(0f, -definition.floorThickness * 0.5f, 0f),
                    geometryCenter = new Vector2(slabOffsetX, slabOffsetZ),
                    geometrySize = new Vector2(outerWidth, outerLength),
                    usableSize = new Vector2(definition.width, definition.length),
                    thickness = definition.floorThickness
                });

            BuildSurface(
                RoomPartType.Ceiling,
                new SurfaceLayout
                {
                    rootPosition = new Vector3(0f, definition.height + definition.ceilingThickness * 0.5f, 0f),
                    geometryCenter = new Vector2(slabOffsetX, slabOffsetZ),
                    geometrySize = new Vector2(outerWidth, outerLength),
                    usableSize = new Vector2(definition.width, definition.length),
                    thickness = definition.ceilingThickness
                });

            BuildSurface(
                RoomPartType.EastWall,
                new SurfaceLayout
                {
                    rootPosition = new Vector3(definition.width * 0.5f + definition.eastWallThickness * 0.5f, definition.height * 0.5f, 0f),
                    geometryCenter = Vector2.zero,
                    geometrySize = new Vector2(definition.length, definition.height),
                    usableSize = new Vector2(definition.length, definition.height),
                    thickness = definition.eastWallThickness
                });

            BuildSurface(
                RoomPartType.WestWall,
                new SurfaceLayout
                {
                    rootPosition = new Vector3(-definition.width * 0.5f - definition.westWallThickness * 0.5f, definition.height * 0.5f, 0f),
                    geometryCenter = Vector2.zero,
                    geometrySize = new Vector2(definition.length, definition.height),
                    usableSize = new Vector2(definition.length, definition.height),
                    thickness = definition.westWallThickness
                });

            BuildSurface(
                RoomPartType.NorthWall,
                new SurfaceLayout
                {
                    rootPosition = new Vector3(0f, definition.height * 0.5f, definition.length * 0.5f + definition.northWallThickness * 0.5f),
                    geometryCenter = new Vector2(slabOffsetX, 0f),
                    geometrySize = new Vector2(outerWidth, definition.height),
                    usableSize = new Vector2(definition.width, definition.height),
                    thickness = definition.northWallThickness
                });

            BuildSurface(
                RoomPartType.SouthWall,
                new SurfaceLayout
                {
                    rootPosition = new Vector3(0f, definition.height * 0.5f, -definition.length * 0.5f - definition.southWallThickness * 0.5f),
                    geometryCenter = new Vector2(slabOffsetX, 0f),
                    geometrySize = new Vector2(outerWidth, definition.height),
                    usableSize = new Vector2(definition.width, definition.height),
                    thickness = definition.southWallThickness
                });
        }

        public void ResetDefinitionToDefaults()
        {
            EnsureDefinition();
            definition.ResetToDefaults();
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
            ResetDefinitionToDefaults();
            Rebuild();
        }

        private void BuildSurface(RoomPartType partType, SurfaceLayout layout)
        {
            Transform root = GetOrCreatePartRoot(partType);
            root.localPosition = layout.rootPosition;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            Material[] preservedMaterials = CaptureSurfaceMaterials(root);
            ClearGeneratedSegments(root);

            Rect geometryRect = Rect.MinMaxRect(
                layout.geometryCenter.x - layout.geometrySize.x * 0.5f,
                layout.geometryCenter.y - layout.geometrySize.y * 0.5f,
                layout.geometryCenter.x + layout.geometrySize.x * 0.5f,
                layout.geometryCenter.y + layout.geometrySize.y * 0.5f);

            List<Rect> openings = GetValidOpeningRects(partType, layout.usableSize);
            List<float> cutsA = new List<float> { geometryRect.xMin, geometryRect.xMax };
            List<float> cutsB = new List<float> { geometryRect.yMin, geometryRect.yMax };

            for (int i = 0; i < openings.Count; i++)
            {
                cutsA.Add(Mathf.Clamp(openings[i].xMin, geometryRect.xMin, geometryRect.xMax));
                cutsA.Add(Mathf.Clamp(openings[i].xMax, geometryRect.xMin, geometryRect.xMax));
                cutsB.Add(Mathf.Clamp(openings[i].yMin, geometryRect.yMin, geometryRect.yMax));
                cutsB.Add(Mathf.Clamp(openings[i].yMax, geometryRect.yMin, geometryRect.yMax));
            }

            SortAndDeduplicateCuts(cutsA);
            SortAndDeduplicateCuts(cutsB);

            int segmentIndex = 0;
            for (int aIndex = 0; aIndex < cutsA.Count - 1; aIndex++)
            {
                float minA = cutsA[aIndex];
                float maxA = cutsA[aIndex + 1];
                if (maxA - minA <= SegmentEpsilon)
                {
                    continue;
                }

                for (int bIndex = 0; bIndex < cutsB.Count - 1; bIndex++)
                {
                    float minB = cutsB[bIndex];
                    float maxB = cutsB[bIndex + 1];
                    if (maxB - minB <= SegmentEpsilon)
                    {
                        continue;
                    }

                    Vector2 cellCenter = new Vector2((minA + maxA) * 0.5f, (minB + maxB) * 0.5f);
                    if (!geometryRect.Contains(cellCenter) || IsPointInsideAnyOpening(cellCenter, openings))
                    {
                        continue;
                    }

                    Vector2 planeCenter = cellCenter;
                    Vector2 planeSize = new Vector2(maxA - minA, maxB - minB);
                    CreateSegment(root, partType, segmentIndex, planeCenter, planeSize, layout.thickness, preservedMaterials);
                    segmentIndex++;
                }
            }
        }

        private void EnsureDefinition()
        {
            if (definition == null)
            {
                definition = new RoomDefinition();
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

        private Transform GetOrCreatePartRoot(RoomPartType partType)
        {
            string childName = GetPartObjectName(partType);
            Transform child = transform.Find(childName);
            if (child == null)
            {
                child = FindPartByComponent(partType);
            }

            if (child == null)
            {
                GameObject partObject = new GameObject(childName);
                child = partObject.transform;
                child.SetParent(transform, false);
            }

            child.name = childName;
            child.SetParent(transform, false);
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;

            StripLegacyPrimitiveComponents(child.gameObject);

            RoomPart roomPart = child.GetComponent<RoomPart>();
            if (roomPart == null)
            {
                roomPart = child.gameObject.AddComponent<RoomPart>();
            }

            roomPart.Initialize(this, partType);
            return child;
        }

        private Transform FindPartByComponent(RoomPartType partType)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                RoomPart roomPart = child.GetComponent<RoomPart>();
                if (roomPart != null && roomPart.PartType == partType)
                {
                    return child;
                }
            }

            return null;
        }

        private static string GetPartObjectName(RoomPartType partType)
        {
            return GeneratedPartPrefix + partType;
        }

        private static void SortAndDeduplicateCuts(List<float> cuts)
        {
            cuts.Sort();

            for (int i = cuts.Count - 2; i >= 0; i--)
            {
                if (Mathf.Abs(cuts[i + 1] - cuts[i]) <= SegmentEpsilon)
                {
                    cuts.RemoveAt(i + 1);
                }
            }
        }

        private static bool IsPointInsideAnyOpening(Vector2 point, List<Rect> openings)
        {
            for (int i = 0; i < openings.Count; i++)
            {
                if (openings[i].Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        private List<Rect> GetValidOpeningRects(RoomPartType partType, Vector2 usableSize)
        {
            var validRects = new List<Rect>();
            Rect usableBounds = Rect.MinMaxRect(-usableSize.x * 0.5f, -usableSize.y * 0.5f, usableSize.x * 0.5f, usableSize.y * 0.5f);

            if (definition.openings == null)
            {
                return validRects;
            }

            for (int i = 0; i < definition.openings.Count; i++)
            {
                RoomOpeningDefinition opening = definition.openings[i];
                if (opening == null || !opening.enabled || opening.targetPart != partType)
                {
                    continue;
                }

                if (!definition.IsOpeningKindSupported(opening))
                {
                    continue;
                }

                Rect rect = definition.GetOpeningRect(opening);
                if (!RectFitsInside(usableBounds, rect))
                {
                    continue;
                }

                bool overlaps = false;
                for (int rectIndex = 0; rectIndex < validRects.Count; rectIndex++)
                {
                    if (validRects[rectIndex].Overlaps(rect))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    validRects.Add(rect);
                }
            }

            return validRects;
        }

        private static bool RectFitsInside(Rect bounds, Rect rect)
        {
            return rect.xMin >= bounds.xMin - SegmentEpsilon
                && rect.xMax <= bounds.xMax + SegmentEpsilon
                && rect.yMin >= bounds.yMin - SegmentEpsilon
                && rect.yMax <= bounds.yMax + SegmentEpsilon;
        }

        private void CreateSegment(
            Transform root,
            RoomPartType partType,
            int segmentIndex,
            Vector2 planeCenter,
            Vector2 planeSize,
            float thickness,
            Material[] preservedMaterials)
        {
            GameObject segmentObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segmentObject.name = SegmentPrefix + segmentIndex;
            segmentObject.transform.SetParent(root, false);

            Transform segment = segmentObject.transform;

            switch (partType)
            {
                case RoomPartType.Floor:
                case RoomPartType.Ceiling:
                    segment.localPosition = new Vector3(planeCenter.x, 0f, planeCenter.y);
                    segment.localScale = new Vector3(planeSize.x, thickness, planeSize.y);
                    break;
                case RoomPartType.EastWall:
                case RoomPartType.WestWall:
                    segment.localPosition = new Vector3(0f, planeCenter.y, planeCenter.x);
                    segment.localScale = new Vector3(thickness, planeSize.y, planeSize.x);
                    break;
                case RoomPartType.NorthWall:
                case RoomPartType.SouthWall:
                    segment.localPosition = new Vector3(planeCenter.x, planeCenter.y, 0f);
                    segment.localScale = new Vector3(planeSize.x, planeSize.y, thickness);
                    break;
            }

            segment.localRotation = Quaternion.identity;

            if (preservedMaterials != null && preservedMaterials.Length > 0)
            {
                MeshRenderer renderer = segment.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterials = preservedMaterials;
                }
            }
        }

        private static Material[] CaptureSurfaceMaterials(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                MeshRenderer renderer = root.GetChild(i).GetComponent<MeshRenderer>();
                if (renderer != null && renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0)
                {
                    return renderer.sharedMaterials;
                }
            }

            MeshRenderer rootRenderer = root.GetComponent<MeshRenderer>();
            if (rootRenderer != null && rootRenderer.sharedMaterials != null && rootRenderer.sharedMaterials.Length > 0)
            {
                return rootRenderer.sharedMaterials;
            }

            return null;
        }

        private static void ClearGeneratedSegments(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroyObjectImmediateSafe(root.GetChild(i).gameObject);
            }
        }

        private static void StripLegacyPrimitiveComponents(GameObject target)
        {
            DestroyComponentImmediateSafe(target.GetComponent<MeshFilter>());
            DestroyComponentImmediateSafe(target.GetComponent<MeshRenderer>());
            DestroyComponentImmediateSafe(target.GetComponent<BoxCollider>());
        }

        private static void DestroyComponentImmediateSafe(Object component)
        {
            if (component == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(component);
                return;
            }
#endif
            Destroy(component);
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
