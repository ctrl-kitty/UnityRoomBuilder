using UnityEngine;

namespace RoomBuilderTool
{
    [DisallowMultipleComponent]
    public class RoomPart : MonoBehaviour
    {
        [SerializeField] private RoomPartType partType;
        [SerializeField] private RoomBuilder owner;

        public RoomPartType PartType => partType;
        public RoomBuilder Owner => owner;

        public void Initialize(RoomBuilder roomBuilder, RoomPartType type)
        {
            owner = roomBuilder;
            partType = type;
        }
    }
}
