using System;

namespace RoomBuilderTool
{
    [Serializable]
    public class CorridorDefinition
    {
        public RoomOpeningReference source = new RoomOpeningReference();
        public RoomOpeningReference target = new RoomOpeningReference();
        public CorridorPathMode pathMode = CorridorPathMode.Straight;
        public float wallThickness = 0.2f;
        public float floorThickness = 0.2f;
        public float ceilingThickness = 0.2f;

        public void ClampToMinimum()
        {
            wallThickness = UnityEngine.Mathf.Max(RoomDefinition.MinimumValue, wallThickness);
            floorThickness = UnityEngine.Mathf.Max(RoomDefinition.MinimumValue, floorThickness);
            ceilingThickness = UnityEngine.Mathf.Max(RoomDefinition.MinimumValue, ceilingThickness);
        }
    }
}
