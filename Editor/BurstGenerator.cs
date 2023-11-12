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
        public static void GenerateDiscBuffer(in float3 xAxis, in float3 yAxis, in float3 zAxis, int sideVertCount,
            int sideTriCount, ref NativeArray<float3> vtx, ref NativeArray<int> idx,
            ref NativeArray<float2> zProjectedUv, ref NativeArray<float2> radialUv, ref NativeArray<Color32> vtxColor,
            ref NativeArray<float3> normals, ref NativeArray<Color32> gradTable, int segmentsU, int segmentsV,
            float angle, float innerRadius, float outerRadius, bool flipTriangles, bool doubleSided, float extrusion,
            UVType vertexColorUVType, UVAxis vertexColorMapType)

        {
            for (var i = 0; i < segmentsU + 1; i++)
            {
                var phi = 2 * math.PI * angle * ((float)i / segmentsU);
                var (x, y) = (math.cos(phi), math.sin(phi));
                // The direction from the center to outer circle
                var radialVDir = x * xAxis + y * yAxis;

                var vertOffset = i * (segmentsV + 1);

                for (var j = 0; j < segmentsV + 1; j++)
                {
                    var vertIdx = vertOffset + j;
                    var outerRate = OuterRate(j);
                    var extrusionRate = extrusion * (1.0f - j / (float)segmentsV);
                    var vert = radialVDir * outerRate + zAxis * extrusionRate;
                    vtx[vertIdx] = vert;

                    var topProjectedUvVal = math.float2(math.dot(vert, xAxis), math.dot(vert, yAxis));
                    topProjectedUvVal /= math.max(math.abs(innerRadius), math.abs(outerRadius));
                    topProjectedUvVal = topProjectedUvVal / 2 + 0.5f;
                    zProjectedUv[vertIdx] = topProjectedUvVal;

                    var radialUvVal = math.float2(i / (float)segmentsU, j / (float)segmentsV);
                    radialUv[vertIdx] = radialUvVal;

                    MapUvToColor(ref vtxColor, vertIdx, radialUvVal, topProjectedUvVal, vertexColorUVType,
                        vertexColorMapType, ref gradTable);
                }

                // Compute normal
                var binormal = math.normalizesafe(vtx[vertOffset + 1] - vtx[vertOffset]);

                for (var j = 0; j < segmentsV + 1; j++)
                {
                    var vertIdx = vertOffset + j;
                    var outerRate = OuterRate(j);
                    // Tangent is 90 degrees rotation of radialVDir
                    var tangent = y * xAxis + -x * yAxis;
                    var normal = math.normalize(math.cross(tangent, binormal));

                    if (flipTriangles)
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

                    normals[vertIdx] = normal;

                    if (doubleSided)
                    {
                        normals[vertIdx + sideVertCount] = -normal;
                    }
                }
            }

            if (doubleSided)
            {
                NativeArray<float3>.Copy(vtx, 0, vtx, sideVertCount, sideVertCount);
                NativeArray<float2>.Copy(zProjectedUv, 0, zProjectedUv, sideVertCount, sideVertCount);
                NativeArray<float2>.Copy(radialUv, 0, radialUv, sideVertCount, sideVertCount);
                NativeArray<Color32>.Copy(vtxColor, 0, vtxColor, sideVertCount, sideVertCount);
            }

            for (var i = 0; i < segmentsU; i++)
            {
                var sliceOriginIdx = i * (segmentsV + 1);
                for (var j = 0; j < segmentsV; j++)
                {
                    var triIdx = (segmentsV * i + j) * 6;
                    var a = sliceOriginIdx + j;
                    var b = a + 1;
                    var c = a + segmentsV + 1;
                    var d = c + 1;
                    PopulateQuad(ref idx, triIdx, a, b, c, d, !flipTriangles);

                    if (!doubleSided) continue;
                    triIdx += sideTriCount;
                    a += sideVertCount;
                    b += sideVertCount;
                    c += sideVertCount;
                    d += sideVertCount;
                    PopulateQuad(ref idx, triIdx, a, b, c, d, flipTriangles);
                }
            }

            return;

            float OuterRate(int segment)
            {
                return (outerRadius - innerRadius) * (segment / (float)segmentsV) + innerRadius;
            }
        }
    }
}