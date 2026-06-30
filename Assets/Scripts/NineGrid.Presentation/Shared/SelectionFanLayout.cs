using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Shared
{
    /// <summary>
    /// 扇形选项布局：复刻 BounceCards 默认间距与倾角，供入场/悬停/退场共用。
    /// </summary>
    [Serializable]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "SelectionFanLayout")]
    public sealed class SelectionFanLayout
    {
        [SerializeField] private float[] baseRotations = { 10f, 5f, -3f, -10f, 2f };
        [SerializeField] private float[] baseOffsetsX = { -2.2f, -1.1f, 0f, 1.1f, 2.2f };
        [SerializeField] private float fallbackSpacing = 1.1f;

        public float GetOffsetX(int index, int count)
        {
            if (index >= 0 && index < baseOffsetsX.Length)
            {
                return baseOffsetsX[index];
            }

            if (count <= 1)
            {
                return 0f;
            }

            float center = (count - 1) * 0.5f;
            return (index - center) * fallbackSpacing;
        }

        public float GetRotationZ(int index)
        {
            return index >= 0 && index < baseRotations.Length
                ? baseRotations[index]
                : 0f;
        }

        public Vector3 GetLocalPosition(int index, int count)
        {
            return new Vector3(GetOffsetX(index, count), 0f, 0f);
        }
    }
}
