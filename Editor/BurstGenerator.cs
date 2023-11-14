using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace InstaMesh.Editor
{
    [BurstCompile]
    public static class BurstGenerator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PopulateQuad(ref NativeArray<int> buff, int offset, int a, int b, int c, int d, bool flip)
        {
            if (!flip)
            {
                buff[offset + 0] = a;
                buff[offset + 1] = c;
                buff[offset + 2] = b;
                buff[offset + 3] = c;
                buff[offset + 4] = d;
                buff[offset + 5] = b;
            }
            else
            {
                buff[offset + 0] = a;
                buff[offset + 1] = b;
                buff[offset + 2] = c;
                buff[offset + 3] = c;
                buff[offset + 4] = b;
                buff[offset + 5] = d;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MapUvToColor(ref NativeArray<Color32> buff, int buffIdx, in float2 radialUvVal,
            in float2 topProjectedUvVal, UVType colorUVType, UVAxis colorMapType,
            ref NativeArray<Color32> gradient)
        {
            var evalVal = colorMapType switch
            {
                UVAxis.U => colorUVType switch
                {
                    UVType.Radial => radialUvVal.x,
                    UVType.TopProjected => topProjectedUvVal.x,
                    _ => throw new ArgumentOutOfRangeException()
                },
                UVAxis.V => colorUVType switch
                {
                    UVType.Radial => radialUvVal.y,
                    UVType.TopProjected => topProjectedUvVal.y,
                    _ => throw new ArgumentOutOfRangeException()
                },
                _ => throw new ArgumentOutOfRangeException()
            };

            // Baked gradient LUT lookup
            evalVal = math.saturate(evalVal);
            var idxFlt = evalVal / GradientNativeHelper.StepSize;
            var idx = (int)math.floor(idxFlt);
            var leftColor = gradient[idx];
            if (idx + 1 < GradientNativeHelper.Size)
            {
                var rightColor = gradient[idx + 1];
                var rightWeight = math.frac(idxFlt);
                var leftWeight = 1 - rightWeight;
                var leftWeightColor = new Color(leftWeight, leftWeight, leftWeight);
                var rightWeightColor = new Color(rightWeight, rightWeight, rightWeight);
                leftColor = leftColor * leftWeightColor + rightColor * rightWeightColor;
            }

            buff[buffIdx] = leftColor;
        }

        [BurstCompile]
        public struct GridGenerationConfig : IDisposable
        {
            public float3 XAxis;
            public float3 YAxis;
            public float3 ZAxis;
            public int SideVertCount;
            public int SideTriCount;
            public NativeArray<float3> Vtx;
            public NativeArray<int> Idx;
            public NativeArray<float2> ZProjectedUv;
            public NativeArray<float2> RadialUv;
            public NativeArray<Color32> VtxColor;
            public NativeArray<float3> Normals;
            public NativeArray<Color32> GradTable;
            public int SegmentsU;
            public int SegmentsV;
            public float Angle;
            public float InnerRadius;
            public float OuterRadius;
            public BurstBool FlipTriangles;
            public BurstBool DoubleSided;
            public float Extrusion;
            public UVType VertexColorUVType;
            public UVAxis VertexColorMapType;

            public void PrepareBuffer()
            {
                var vertCount = DoubleSided ? SideVertCount * 2 : SideVertCount;
                Vtx = new NativeArray<float3>(vertCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                Normals = new NativeArray<float3>(vertCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                ZProjectedUv =
                    new NativeArray<float2>(vertCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                RadialUv = new NativeArray<float2>(vertCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                VtxColor = new NativeArray<Color32>(vertCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                var triCount = DoubleSided ? SideTriCount * 2 : SideTriCount;
                Idx = new NativeArray<int>(triCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            }

            public NativeArray<float2> SelectUV(UVType t)
            {
                return t switch
                {
                    UVType.TopProjected => ZProjectedUv,
                    UVType.Radial => RadialUv,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            public void Dispose()
            {
                Vtx.Dispose();
                Normals.Dispose();
                ZProjectedUv.Dispose();
                RadialUv.Dispose();
                Idx.Dispose();
                VtxColor.Dispose();
                GradTable.Dispose();
            }
        }

        [BurstCompile]
        public static void GenerateGrid(ref GridGenerationConfig config)
        {
            for (var i = 0; i < config.SegmentsU + 1; i++)
            {
                var phi = 2 * math.PI * config.Angle * ((float)i / config.SegmentsU);
                var (x, y) = (math.cos(phi), math.sin(phi));
                // The direction from the center to outer circle
                var radialVDir = x * config.XAxis + y * config.YAxis;

                var vertOffset = i * (config.SegmentsV + 1);

                for (var j = 0; j < config.SegmentsV + 1; j++)
                {
                    var vertIdx = vertOffset + j;
                    var outerRate = OuterRate(j, config.InnerRadius, config.OuterRadius, config.SegmentsV);
                    var extrusionRate = config.Extrusion * (1.0f - j / (float)config.SegmentsV);
                    var vert = radialVDir * outerRate + config.ZAxis * extrusionRate;
                    config.Vtx[vertIdx] = vert;

                    var topProjectedUvVal = math.float2(math.dot(vert, config.XAxis), math.dot(vert, config.YAxis));
                    topProjectedUvVal /= math.max(math.abs(config.InnerRadius), math.abs(config.OuterRadius));
                    topProjectedUvVal = topProjectedUvVal / 2 + 0.5f;
                    config.ZProjectedUv[vertIdx] = topProjectedUvVal;

                    var radialUvVal = math.float2(i / (float)config.SegmentsU, j / (float)config.SegmentsV);
                    config.RadialUv[vertIdx] = radialUvVal;

                    MapUvToColor(ref config.VtxColor, vertIdx, radialUvVal, topProjectedUvVal, config.VertexColorUVType,
                        config.VertexColorMapType, ref config.GradTable);
                }

                // Compute normal
                var binormal = math.normalizesafe(config.Vtx[vertOffset + 1] - config.Vtx[vertOffset]);

                for (var j = 0; j < config.SegmentsV + 1; j++)
                {
                    var vertIdx = vertOffset + j;
                    var outerRate = OuterRate(j, config.InnerRadius, config.OuterRadius, config.SegmentsV);
                    // Tangent is 90 degrees rotation of radialVDir
                    var tangent = y * config.XAxis + -x * config.YAxis;
                    var normal = math.normalize(math.cross(tangent, binormal));

                    if (config.FlipTriangles)
                    {
                        normal *= -1;
                    }

                    // The mesh flips when outer rate becomes negative, so flip the normal.
                    // The resulting mesh looks like a X shape in cross-section when this happens.
                    // Note that normal near the crossing point may be wrong depending on the config, but this can
                    // not be avoided due to how mesh and normal interpolation works.
                    if (outerRate < 0.0f)
                    {
                        normal *= -1;
                    }

                    config.Normals[vertIdx] = normal;

                    if (config.DoubleSided)
                    {
                        config.Normals[vertIdx + config.SideVertCount] = -normal;
                    }
                }
            }

            if (config.DoubleSided)
            {
                NativeArray<float3>.Copy(config.Vtx, 0, config.Vtx, config.SideVertCount, config.SideVertCount);
                NativeArray<float2>.Copy(config.ZProjectedUv, 0, config.ZProjectedUv, config.SideVertCount,
                    config.SideVertCount);
                NativeArray<float2>.Copy(config.RadialUv, 0, config.RadialUv, config.SideVertCount,
                    config.SideVertCount);
                NativeArray<Color32>.Copy(config.VtxColor, 0, config.VtxColor, config.SideVertCount,
                    config.SideVertCount);
            }

            for (var i = 0; i < config.SegmentsU; i++)
            {
                var sliceOriginIdx = i * (config.SegmentsV + 1);
                for (var j = 0; j < config.SegmentsV; j++)
                {
                    var triIdx = (config.SegmentsV * i + j) * 6;
                    var a = sliceOriginIdx + j;
                    var b = a + 1;
                    var c = a + config.SegmentsV + 1;
                    var d = c + 1;
                    PopulateQuad(ref config.Idx, triIdx, a, b, c, d, !config.FlipTriangles);

                    if (!config.DoubleSided) continue;
                    triIdx += config.SideTriCount;
                    a += config.SideVertCount;
                    b += config.SideVertCount;
                    c += config.SideVertCount;
                    d += config.SideVertCount;
                    PopulateQuad(ref config.Idx, triIdx, a, b, c, d, config.FlipTriangles);
                }
            }

            return;

            float OuterRate(int segment, float innerRadius, float outerRadius, int segmentsV)
            {
                return (outerRadius - innerRadius) * (segment / (float)segmentsV) + innerRadius;
            }
        }
    }
}