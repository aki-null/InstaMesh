using System;
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

            var vertCount = (uSegments + 1) * (vSegments + 1);
            var triCount = uSegments * vSegments * 6;

            var genConfig = new BurstGenerator.GridGenerationConfig
            {
                XAxis = xAxis,
                YAxis = yAxis,
                ZAxis = zAxis,
                SideVertCount = vertCount,
                SideTriCount = triCount,
                SegmentsU = uSegments,
                SegmentsV = vSegments,
                Angle = angle,
                InnerRadius = innerRadius,
                OuterRadius = outerRadius,
                FlipTriangles = flipped,
                DoubleSided = doubleSided,
                Extrusion = extrusion,
                VertexColorUVType = vertexColorUVType,
                VertexColorMapType = vertexColorMapType
            };

            try
            {
                genConfig.PrepareBuffer();

                GradientNativeHelper.GenerateLut(vertexColor, colorSpace, out genConfig.GradTable);
                BurstGenerator.GenerateGrid(ref genConfig);

                mesh.Clear(false);
                mesh.indexFormat = genConfig.Vtx.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(genConfig.Vtx);
                mesh.SetNormals(genConfig.Normals);
                for (var i = 0; i < uvCount; ++i)
                {
                    mesh.SetUVs(i, genConfig.SelectUV(GetUVTypeAt(i)));
                }

                mesh.SetIndices(genConfig.Idx, MeshTopology.Triangles, 0);
                mesh.SetColors(genConfig.VtxColor);

                mesh.Optimize();
            }
            finally
            {
                genConfig.Dispose();
            }
        }
    }
}