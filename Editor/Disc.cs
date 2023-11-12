using System;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine;
using Unity.Mathematics;

namespace InstaMesh.Editor
{
    [Serializable]
    public sealed class Disc
    {
        public float innerRadius;
        public float outerRadius = 1;
        public float extrusion;
        [Range(0, 1)] public float angle = 1;
        public int uSegments = 32;
        public int vSegments = 32;
        public Axis axis = Axis.Z;
        public bool flipped;
        public bool doubleSided;

        public UVType vertexColorUVType;
        public UVAxis vertexColorMapType;
        public Gradient vertexColor;

        [Range(1, 8)] public int uvCount = 1;
        public UVType uv0;
        public UVType uv1;
        public UVType uv2;
        public UVType uv3;
        public UVType uv4;
        public UVType uv5;
        public UVType uv6;
        public UVType uv7;

        private UVType GetUVTypeAt(int i)
        {
            return i switch
            {
                0 => uv0,
                1 => uv1,
                2 => uv2,
                3 => uv3,
                4 => uv4,
                5 => uv5,
                6 => uv6,
                7 => uv7,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private Axis GetRotatedAxis(Axis a)
        {
            return axis switch
            {
                Axis.X => a switch
                {
                    Axis.X => Axis.Y,
                    Axis.Y => Axis.Z,
                    Axis.Z => Axis.X,
                    _ => throw new ArgumentOutOfRangeException(nameof(a), a, null)
                },
                Axis.Y => a switch
                {
                    Axis.X => Axis.Z,
                    Axis.Y => Axis.X,
                    Axis.Z => Axis.Y,
                    _ => throw new ArgumentOutOfRangeException(nameof(a), a, null)
                },
                Axis.Z => a switch
                {
                    Axis.X => Axis.X,
                    Axis.Y => Axis.Y,
                    Axis.Z => Axis.Z,
                    _ => throw new ArgumentOutOfRangeException(nameof(a), a, null)
                },
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private const int GradTableSize = 256;
        private const float GradTableStepSize = 1.0f / (GradTableSize - 1);

        public void Generate(Mesh mesh, ColorSpace colorSpace)
        {
            if (uSegments < 3) return;
            if (vSegments < 1) return;

            // Axis selection
            var xAxis = float3.zero;
            var yAxis = float3.zero;
            var zAxis = float3.zero;
            xAxis[(int)GetRotatedAxis(Axis.X)] = 1;
            yAxis[(int)GetRotatedAxis(Axis.Y)] = 1;
            zAxis[(int)GetRotatedAxis(Axis.Z)] = 1;

            var vtx = new NativeArray<float3>();
            var normals = new NativeArray<float3>();
            var zProjectedUv = new NativeArray<float2>();
            var radialUv = new NativeArray<float2>();
            var idx = new NativeArray<int>();
            var vtxColor = new NativeArray<Color32>();
            var gradTable = new NativeArray<Color32>();

            var flipTriangles = flipped;

            try
            {
                var vertCount = (uSegments + 1) * (vSegments + 1);
                var sideVertCount = vertCount;
                if (doubleSided) vertCount *= 2;
                
                vtx = new NativeArray<float3>(vertCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                normals = new NativeArray<float3>(vertCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                zProjectedUv = new NativeArray<float2>(vertCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                radialUv = new NativeArray<float2>(vertCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                vtxColor = new NativeArray<Color32>(vertCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                
                GradientNativeHelper.GenerateLut(vertexColor, colorSpace, out gradTable);

                var triCount = uSegments * vSegments * 6;
                var sideTriCount = triCount;
                if (doubleSided) triCount *= 2;
                idx = new NativeArray<int>(triCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                BurstGenerator.GenerateDiscBuffer(xAxis, yAxis, zAxis, sideVertCount, sideTriCount, ref vtx, ref idx,
                    ref zProjectedUv, ref radialUv, ref vtxColor, ref normals, ref gradTable, uSegments, vSegments,
                    angle, innerRadius, outerRadius, flipTriangles, doubleSided, extrusion, vertexColorUVType,
                    vertexColorMapType);

                var selectUv = new Func<UVType, NativeArray<float2>>(t =>
                {
                    return t switch
                    {
                        UVType.TopProjected => zProjectedUv,
                        UVType.Radial => radialUv,
                        _ => new NativeArray<float2>()
                    };
                });

                mesh.Clear(false);
                mesh.indexFormat = vtx.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(vtx);
                mesh.SetNormals(normals);
                for (var i = 0; i < uvCount; ++i)
                {
                    mesh.SetUVs(i, selectUv(GetUVTypeAt(i)));
                }

                mesh.SetIndices(idx, MeshTopology.Triangles, 0);
                mesh.SetColors(vtxColor);

                mesh.Optimize();
            }
            finally
            {
                vtx.Dispose();
                normals.Dispose();
                zProjectedUv.Dispose();
                radialUv.Dispose();
                idx.Dispose();
                vtxColor.Dispose();
                gradTable.Dispose();
            }
        }
    }
}