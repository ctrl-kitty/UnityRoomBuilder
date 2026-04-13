using System;
using UnityEngine;

namespace RoomBuilderTool
{
    [Serializable]
    public class RoomOpeningDefinition
    {
        public string id = Guid.NewGuid().ToString("N");
        public bool enabled = true;
        public RoomOpeningKind kind = RoomOpeningKind.Hole;
        public RoomPartType targetPart = RoomPartType.NorthWall;
        public Vector2 center = new Vector2(0f, 1f);
        public Vector2 size = new Vector2(1f, 2f);

        public void EnsureId()
        {
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
            }
        }

        public void ClampSize(float minimumValue)
        {
            size.x = Mathf.Max(minimumValue, size.x);
            size.y = Mathf.Max(minimumValue, size.y);
        }
    }
}
