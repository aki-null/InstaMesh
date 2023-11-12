using Unity.Collections;
using UnityEngine;

namespace InstaMesh.Editor
{
    public static class GradientNativeHelper
    {
        public const int Size = 256;
        public const float StepSize = 1.0f / (Size - 1);

        public static void GenerateLut(Gradient grad, ColorSpace colorSpace, out NativeArray<Color32> table)
        {
            table = new NativeArray<Color32>(Size, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            for (var i = 0; i < Size; ++i)
            {
                var t = i / (float)(Size - 1);
                var color = grad.Evaluate(t);
                if (colorSpace == ColorSpace.Linear)
                {
                    color = color.linear;
                }

                table[i] = color;
            }
        }
    }
}