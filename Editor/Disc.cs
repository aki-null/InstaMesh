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
        public enum UVType
        {
            ZProjected,
            Radial
        }

        public float innerRadius;
        public float outerRadius = 1;
        public float extrusion;
        [Range(0, 1)] public float angle = 1;
        public int segments = 32;
        public int vSegments = 32;
        public Axis axis = Axis.Z;
        public bool flipped;
        public bool doubleSided;

        public UVType uv0;
        public UVType uv1;
        public UVType uv2;

        private void PopulateQuad(NativeArray<int> buff, int offset, int a, int b, int c, int d, bool flip)
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

        public void Generate(Mesh mesh)
        {
            if (segments < 3) return;
            if (vSegments < 1) return;

            // Axis selection
            var xAxis = float3.zero;
            var yAxis = float3.zero;
            var zAxis = float3.zero;
            var axisIndex = (int)axis;
            xAxis[(axisIndex + 1) % 3] = 1;
            yAxis[(axisIndex + 2) % 3] = 1;
            zAxis[(axisIndex + 0) % 3] = 1;

            var vtx = new NativeArray<float3>();
            var normals = new NativeArray<float3>();
            var zProjectedUv = new NativeArray<float2>();
            var radialUv = new NativeArray<float2>();
            var idx = new NativeArray<int>();

            var flipTriangles = flipped;

            try
            {
                float OuterRate(int segment)
                {
                    return (outerRadius - innerRadius) * (segment / (float)vSegments) + innerRadius;
                }

                var len = (segments + 1) * (vSegments + 1);
                var n = len;
                if (doubleSided) len *= 2;
                vtx = new NativeArray<float3>(len, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                normals = new NativeArray<float3>(len, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                zProjectedUv = new NativeArray<float2>(len, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                radialUv = new NativeArray<float2>(len, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                var triLen = segments * vSegments * 6;
                var triN = triLen;
                if (doubleSided) triLen *= 2;
                idx = new NativeArray<int>(triLen, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                for (var i = 0; i < segments + 1; i++)
                {
                    var phi = 2 * math.PI * angle * ((float)i / segments);
                    var (x, y) = (math.cos(phi), math.sin(phi));
                    // The direction from the center to outer circle
                    var radialVDir = x * xAxis + y * yAxis;

                    var vertOffset = i * (vSegments + 1);

                    for (var j = 0; j < vSegments + 1; j++)
                    {
                        var vertIdx = vertOffset + j;
                        var outerRate = OuterRate(j);
                        var extrusionRate = extrusion * (1.0f - j / (float)vSegments);
                        vtx[vertIdx] = radialVDir * outerRate + zAxis * extrusionRate;
                        zProjectedUv[vertIdx] = math.float2(y, x) * outerRate / 2 + 0.5f;
                        radialUv[vertIdx] = math.float2((float)i / segments, j / (float)vSegments);
                    }

                    // Compute normal
                    var binormal = math.normalizesafe(vtx[vertOffset + 1] - vtx[vertOffset]);

                    for (var j = 0; j < vSegments + 1; j++)
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
                            normals[vertIdx + n] = -normal;
                        }
                    }
                }

                if (doubleSided)
                {
                    NativeArray<float3>.Copy(vtx, 0, vtx, n, n);
                    NativeArray<float2>.Copy(zProjectedUv, 0, zProjectedUv, n, n);
                    NativeArray<float2>.Copy(radialUv, 0, radialUv, n, n);
                }

                for (var i = 0; i < segments; i++)
                {
                    var sliceOriginIdx = i * (vSegments + 1);
                    for (var j = 0; j < vSegments; j++)
                    {
                        var triIdx = (vSegments * i + j) * 6;
                        var a = sliceOriginIdx + j;
                        var b = a + 1;
                        var c = a + vSegments + 1;
                        var d = c + 1;
                        PopulateQuad(idx, triIdx, a, b, c, d, !flipTriangles);

                        if (!doubleSided) continue;
                        triIdx += triN;
                        a += n;
                        b += n;
                        c += n;
                        d += n;
                        PopulateQuad(idx, triIdx, a, b, c, d, flipTriangles);
                    }
                }

                var selectUv = new Func<UVType, NativeArray<float2>>(t =>
                {
                    return t switch
                    {
                        UVType.ZProjected => zProjectedUv,
                        UVType.Radial => radialUv,
                        _ => new NativeArray<float2>()
                    };
                });


                mesh.Clear(false);
                mesh.indexFormat = vtx.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(vtx);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, selectUv(uv0));
                mesh.SetUVs(1, selectUv(uv1));
                mesh.SetUVs(2, selectUv(uv2));
                mesh.SetIndices(idx, MeshTopology.Triangles, 0);

                mesh.Optimize();
            }
            finally
            {
                vtx.Dispose();
                normals.Dispose();
                zProjectedUv.Dispose();
                radialUv.Dispose();
                idx.Dispose();
            }
        }
    }
}