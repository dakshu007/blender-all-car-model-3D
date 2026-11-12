// WorldBuilder.Terrain.cs - the chunked world terrain with distance LODs.
//
//   * Heights on a 4 m grid (WorldField.HeightAt), normals and ground colours from the whole grid (seamless).
//   * 256 m chunks. Each has a LODGroup with 4 / 8 / 16 / 32 m meshes; the physics always uses the full 4 m mesh.
//   * Coarse LODs never cover a road: near roads a coarse vertex takes the lowest fine height around it.
//   * Every LOD mesh has a skirt (a strip hanging down along the chunk border) so neighbouring chunks at
//     different LODs never show a crack.
//   * TerrainLod follows GameSettings.Quality at run time (Low hands over to the 8 / 16 / 32 m grids soonest).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public partial class WorldBuilder
    {
        static readonly int[] LodSteps = { 1, 2, 4, 8 };
        byte[] terrEdge;           // distance to the nearest road edge per grid vertex (m, 255 = far)
        Color32[] terrCol;
        Vector3[] terrNrm;

        IEnumerator BuildTerrain()
        {
            terrX0 = WorldLayout.MinX - WorldLayout.TerrainPad;
            terrZ0 = WorldLayout.MinZ - WorldLayout.TerrainPad;
            int cx = Mathf.CeilToInt((WorldLayout.Size.x + 2f * WorldLayout.TerrainPad) / (TerrainCell * ChunkCells));
            int cz = Mathf.CeilToInt((WorldLayout.Size.y + 2f * WorldLayout.TerrainPad) / (TerrainCell * ChunkCells));
            int nx = cx * ChunkCells + 1, nz = cz * ChunkCells + 1;
            terrNx = nx; terrNz = nz;
            var h = new float[nx * nz];
            var edge = new byte[nx * nz];
            float tStart = Time.realtimeSinceStartup;
            for (int j = 0; j < nz; j++)
            {
                float z = terrZ0 + j * TerrainCell;
                for (int i = 0; i < nx; i++)
                {
                    float e;
                    h[j * nx + i] = field.HeightAt(terrX0 + i * TerrainCell, z, out e);
                    edge[j * nx + i] = (byte)Mathf.Clamp(Mathf.CeilToInt(e), 0, 255);
                }
                if (j % 12 == 0) { Progress = 0.09f + 0.12f * j / nz; yield return null; }
            }
            terrH = h;
            terrEdge = edge;
            // normals and ground colours from the whole grid, so chunk seams are invisible
            var nrm = new Vector3[nx * nz];
            var col = new Color32[nx * nz];
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int g = j * nx + i;
                    float hl = h[j * nx + Mathf.Max(0, i - 1)], hr = h[j * nx + Mathf.Min(nx - 1, i + 1)];
                    float hd = h[Mathf.Max(0, j - 1) * nx + i], hu = h[Mathf.Min(nz - 1, j + 1) * nx + i];
                    nrm[g] = new Vector3(hl - hr, 2f * TerrainCell, hd - hu).normalized;
                    col[g] = field.GroundColor(terrX0 + i * TerrainCell, terrZ0 + j * TerrainCell, h[g], nrm[g]);
                }
                if (j % 24 == 0) { Progress = 0.21f + 0.03f * j / nz; yield return null; }
            }
            terrNrm = nrm;
            terrCol = col;

            var root = new GameObject("World_Terrain").transform;
            var lods = new List<LODGroup>();
            var levels = new List<MeshRenderer[]>();
            int built = 0, tris = 0;
            for (int bz = 0; bz < cz; bz++)
                for (int bx = 0; bx < cx; bx++)
                {
                    var go = new GameObject("Terrain_" + bx + "_" + bz);
                    go.transform.SetParent(root, false);
                    var rs = new MeshRenderer[LodSteps.Length];
                    for (int l = 0; l < LodSteps.Length; l++)
                    {
                        var mesh = ChunkMesh(bx, bz, LodSteps[l]);
                        tris += l == 0 ? mesh.triangles.Length / 3 : 0;
                        var lg = l == 0 ? go : new GameObject("LOD" + l);
                        if (l > 0) lg.transform.SetParent(go.transform, false);
                        lg.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var mr = lg.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = MaterialLibrary.Terrain;
                        mr.shadowCastingMode = l <= 1 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        mr.lightProbeUsage = LightProbeUsage.Off;
                        mr.reflectionProbeUsage = l == 0 ? ReflectionProbeUsage.BlendProbes : ReflectionProbeUsage.Off;
                        rs[l] = mr;
                    }
                    go.AddComponent<MeshCollider>().sharedMesh = ColliderMesh(bx, bz);
                    go.AddComponent<TerrainSurface>();
                    var lod = go.AddComponent<LODGroup>();
                    lod.fadeMode = LODFadeMode.None;
                    lods.Add(lod);
                    levels.Add(rs);
                    if (++built % 4 == 0) { Progress = 0.24f + 0.06f * built / (cx * cz); yield return null; }
                }
            var tl = root.gameObject.AddComponent<TerrainLod>();
            tl.Init(lods, levels);
            Debug.Log("[BlossomRally] terrain " + nx + " x " + nz + " vertices in " + (cx * cz) + " chunks (" + tris + " full-detail tris, LODs 4/8/16/32 m), "
                      + ((Time.realtimeSinceStartup - tStart) * 1000f).ToString("0") + " ms");
            Progress = 0.30f;
        }

        /// <summary>Height of a vertex of a coarse LOD (step cells): near roads the lowest fine height within one coarse
        /// cell, so the coarse triangles can never rise through a road.</summary>
        float LodHeight(int gi, int gj, int step)
        {
            int g = gj * terrNx + gi;
            float hv = terrH[g];
            if (step == 1 || terrEdge[g] >= step * TerrainCell + 2f) return hv;
            int i0 = Mathf.Max(0, gi - step), i1 = Mathf.Min(terrNx - 1, gi + step);
            int j0 = Mathf.Max(0, gj - step), j1 = Mathf.Min(terrNz - 1, gj + step);
            for (int j = j0; j <= j1; j++)
                for (int i = i0, k = j * terrNx + i0; i <= i1; i++, k++)
                    if (terrH[k] < hv) hv = terrH[k];
            return hv;
        }

        /// <summary>Lowest point any LOD of the neighbouring chunks can have along the border near (gi, gj).</summary>
        float SkirtBottom(int gi, int gj)
        {
            int r = LodSteps[LodSteps.Length - 1];
            float lo = terrH[gj * terrNx + gi];
            int i0 = Mathf.Max(0, gi - r), i1 = Mathf.Min(terrNx - 1, gi + r);
            int j0 = Mathf.Max(0, gj - r), j1 = Mathf.Min(terrNz - 1, gj + r);
            if (terrEdge[gj * terrNx + gi] < 2f * r * TerrainCell + 4f)
            {
                // near roads a coarse neighbour may take any fine height of its block: cover the whole block
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++) lo = Mathf.Min(lo, terrH[j * terrNx + i]);
            }
            else
            {
                for (int i = i0; i <= i1; i++) lo = Mathf.Min(lo, terrH[gj * terrNx + i]);
                for (int j = j0; j <= j1; j++) lo = Mathf.Min(lo, terrH[j * terrNx + gi]);
            }
            return lo - 1.5f;
        }

        Mesh ChunkMesh(int bx, int bz, int step)
        {
            int cells = ChunkCells / step, m = cells + 1;
            int border = 4 * cells;
            var verts = new Vector3[m * m + border];
            var norms = new Vector3[verts.Length];
            var cols = new Color32[verts.Length];
            for (int j = 0; j < m; j++)
                for (int i = 0; i < m; i++)
                {
                    int gi = bx * ChunkCells + i * step, gj = bz * ChunkCells + j * step;
                    int g = gj * terrNx + gi;
                    verts[j * m + i] = new Vector3(terrX0 + gi * TerrainCell, LodHeight(gi, gj, step), terrZ0 + gj * TerrainCell);
                    norms[j * m + i] = terrNrm[g];
                    cols[j * m + i] = terrCol[g];
                }
            var tri = new List<int>(cells * cells * 6 + border * 6);
            for (int j = 0; j < cells; j++)
                for (int i = 0; i < cells; i++)
                {
                    int a = j * m + i, b = a + 1, c = a + m, d = c + 1;
                    tri.Add(a); tri.Add(c); tri.Add(b);
                    tri.Add(b); tri.Add(c); tri.Add(d);
                }
            // skirt: the border ring walked once round, each vertex copied down below anything a neighbour can show
            var ring = new int[border];
            int r = 0;
            for (int i = 0; i < cells; i++) ring[r++] = i;                               // south, west -> east
            for (int j = 0; j < cells; j++) ring[r++] = j * m + cells;                   // east, south -> north
            for (int i = cells; i > 0; i--) ring[r++] = cells * m + i;                   // north, east -> west
            for (int j = cells; j > 0; j--) ring[r++] = j * m;                           // west, north -> south
            int s0 = m * m;
            for (int k = 0; k < border; k++)
            {
                int top = ring[k];
                int ti = top % m, tj = top / m;
                int gi = bx * ChunkCells + ti * step, gj = bz * ChunkCells + tj * step;
                Vector3 v = verts[top];
                v.y = Mathf.Min(v.y - 0.5f, SkirtBottom(gi, gj));
                verts[s0 + k] = v;
                norms[s0 + k] = norms[top];
                cols[s0 + k] = cols[top];
            }
            for (int k = 0; k < border; k++)
            {
                int a = ring[k], b = ring[(k + 1) % border], c = s0 + k, d = s0 + (k + 1) % border;
                // the ring runs counter-clockwise seen from above: outward faces are (a, b, d) / (a, d, c)
                tri.Add(a); tri.Add(b); tri.Add(d);
                tri.Add(a); tri.Add(d); tri.Add(c);
            }
            var mesh = new Mesh { name = "Terrain_" + bx + "_" + bz + "_" + (step * (int)TerrainCell) + "m" };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.colors32 = cols;
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Full 4 m physics mesh of a chunk (no skirt, no colours).</summary>
        Mesh ColliderMesh(int bx, int bz)
        {
            int m = ChunkCells + 1;
            var verts = new Vector3[m * m];
            for (int j = 0; j < m; j++)
                for (int i = 0; i < m; i++)
                {
                    int gi = bx * ChunkCells + i, gj = bz * ChunkCells + j;
                    verts[j * m + i] = new Vector3(terrX0 + gi * TerrainCell, terrH[gj * terrNx + gi], terrZ0 + gj * TerrainCell);
                }
            var tri = new int[ChunkCells * ChunkCells * 6];
            int t = 0;
            for (int j = 0; j < ChunkCells; j++)
                for (int i = 0; i < ChunkCells; i++)
                {
                    int a = j * m + i, b = a + 1, c = a + m, d = c + 1;
                    tri[t++] = a; tri[t++] = c; tri[t++] = b;
                    tri[t++] = b; tri[t++] = c; tri[t++] = d;
                }
            var mesh = new Mesh { name = "TerrainCollider_" + bx + "_" + bz, vertices = verts, triangles = tri };
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Applies the terrain LOD distances of the current quality preset (and re-applies them when it changes).</summary>
    public class TerrainLod : MonoBehaviour
    {
        List<LODGroup> groups;
        List<MeshRenderer[]> levels;
        int applied = -1;
        float check;

        // screen-relative heights where each LOD (4 / 8 / 16 / 32 m) hands over. With lodBias 1.5 a 256 m chunk is at
        // ~0.95 of the screen height ~350 m away, so the chunks round the car always draw the 4 m mesh the physics
        // uses (no floating wheels off-road); Low drops to the coarse grids soonest.
        static readonly float[][] Heights =
        {
            new[] { 0.98f, 0.30f, 0.10f, 0.0005f },         // Low
            new[] { 0.90f, 0.38f, 0.15f, 0.0005f },         // Medium
            new[] { 0.70f, 0.30f, 0.12f, 0.0005f },         // High
            new[] { 0.52f, 0.24f, 0.09f, 0.0005f },         // Ultra
        };

        public void Init(List<LODGroup> g, List<MeshRenderer[]> l)
        {
            groups = g; levels = l;
            Apply();
        }

        void Update()
        {
            check -= Time.unscaledDeltaTime;
            if (check > 0f) return;
            check = 1f;
            if (GameSettings.Quality != applied) Apply();
        }

        void Apply()
        {
            if (groups == null) return;
            int q = Mathf.Clamp(GameSettings.Quality, 0, 3);
            applied = q;
            var hts = Heights[q];
            for (int k = 0; k < groups.Count; k++)
            {
                var g = groups[k];
                var rs = levels[k];
                if (g == null || rs == null) continue;
                var lods = new LOD[hts.Length];
                for (int l = 0; l < hts.Length; l++) lods[l] = new LOD(hts[l], new Renderer[] { rs[Mathf.Min(rs.Length - 1, l)] });
                g.SetLODs(lods);
                g.RecalculateBounds();
                // Low: only the nearest terrain casts shadows
                for (int l = 1; l < rs.Length; l++)
                    if (rs[l] != null) rs[l].shadowCastingMode = (l == 1 && q > 0) ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }
    }
}
