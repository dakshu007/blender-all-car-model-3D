// WorldBuilder.Roads.cs - turns the road geometry (RoadMesh.cs: ribbon + skirts, junction clipping, caps, lane
// lines) into static meshes with a top-surface collider. Pieces live under World_Static, so they static-batch.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public partial class WorldBuilder
    {
        Transform roadsRoot;
        RoadMeshBuilder roadMesh;

        void BuildRoadMeshes()
        {
            roadsRoot = new GameObject("World_Roads").transform;
            roadsRoot.SetParent(staticRoot, false);
            roadMesh = new RoadMeshBuilder(field, TerrainHeight);
            var lineMat = MaterialLibrary.Standard("RoadLine", new Color(0.95f, 0.94f, 0.9f), 0.35f, 0f);
            int pieces = 0, clipped = 0, caps = 0;
            for (int r = 0; r < roads.Count; r++)
            {
                var def = roads[r];
                var mat = RoadMaterial(def);
                float tile = def.surface == Surface.Paving ? 4f : 7f;
                bool lines = def.surface == Surface.Asphalt && def.lines;
                var list = roadMesh.WithExtensions(r);
                int count = list.Count + (def.closed ? 1 : 0);
                // split into runs of <= 240 samples (bridges are part of the road surface)
                var run = new List<RoadSample>();
                int part = 0;
                for (int i = 0; i < count; i++)
                {
                    var s = list[i % list.Count];
                    if (run.Count >= 240)
                    {
                        clipped += MakeRoadPiece(def, roadMesh.Piece(r, run, tile, lines), def.name + "_" + part++, mat, lineMat);
                        var last = run[run.Count - 1];
                        run.Clear();
                        run.Add(last);
                    }
                    run.Add(s);
                }
                if (run.Count > 1) clipped += MakeRoadPiece(def, roadMesh.Piece(r, run, tile, lines), def.name + "_" + part++, mat, lineMat);
                pieces += part;
            }
            foreach (var j in field.junctions)
            {
                if (!j.endToEnd) continue;
                var cap = roadMesh.Cap(j);
                if (cap == null) continue;
                MakeRoadPiece(roads[j.master], cap, roads[j.master].name + "_Cap" + caps++, RoadMaterial(roads[j.master]), lineMat);
            }
            Debug.Log("[BlossomRally] roads: " + pieces + " pieces, " + field.junctions.Count + " junctions (" + clipped + " clipped triangles, " + caps + " caps)");
        }

        Material RoadMaterial(RoadDef def)
        {
            string tex = def.texture;
            Color tint = def.tint;
            if (string.IsNullOrEmpty(tex))
                switch (def.surface)
                {
                    case Surface.Asphalt: tex = "ENV_Road_Asphalt"; break;
                    case Surface.Gravel: tex = "ENV_Road_Gravel"; break;
                    case Surface.Dirt: tex = "ENV_Road_Dirt"; break;
                    case Surface.Snow: tex = "ENV_Road_Gravel"; if (tint == Color.white) tint = new Color(1.35f, 1.4f, 1.5f); break;
                    case Surface.Ice: tex = "ENV_Road_Asphalt"; if (tint == Color.white) tint = new Color(1.25f, 1.38f, 1.55f); break;
                    case Surface.Sand: tex = "ENV_Road_Dirt"; if (tint == Color.white) tint = new Color(1.3f, 1.12f, 0.86f); break;
                    default: tex = "ENV_Road_Paving"; break;
                }
            bool smoothTop = def.surface == Surface.Asphalt || def.surface == Surface.Ice;
            string key = "Road_" + def.surface + "_" + tex + (tint == Color.white ? "" : "_" + ColorUtility.ToHtmlStringRGB(tint * 0.5f));
            var mat = MaterialLibrary.Road(key, Resources.Load<Texture2D>("Env/" + tex), def.surface == Surface.Ice ? 0.55f : smoothTop ? 0.22f : 0.08f);
            if (tint != Color.white) mat.SetColor("_Color", tint);
            if (def.surface == Surface.Gravel || def.surface == Surface.Dirt || def.surface == Surface.Sand) mat.SetFloat("_PuddleAmount", 1f);
            else if (def.surface == Surface.Paving) mat.SetFloat("_PuddleAmount", 0.6f);
            else if (def.surface == Surface.Snow || def.surface == Surface.Ice) mat.SetFloat("_PuddleAmount", 0.2f);
            else mat.SetFloat("_PuddleAmount", 0.8f);
            return mat;
        }

        int MakeRoadPiece(RoadDef def, RoadMeshData d, string name, Material mat, Material lineMat)
        {
            if (d.t.Count == 0) return d.clipped;
            var mesh = new Mesh { name = name };
            if (d.v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(d.v);
            mesh.SetUVs(0, d.uv);
            mesh.SetTriangles(d.t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(roadsRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.lightProbeUsage = LightProbeUsage.Off;
            if (d.ct.Count > 0)
            {
                var col = new Mesh { name = name + "_Col" };
                if (d.cv.Count > 65000) col.indexFormat = IndexFormat.UInt32;
                col.SetVertices(d.cv);
                col.SetTriangles(d.ct, 0);
                col.RecalculateBounds();
                go.AddComponent<MeshCollider>().sharedMesh = col;
            }
            go.AddComponent<SurfaceTag>().surface = def.surface;
            if (d.lt.Count == 0) return d.clipped;
            var lm = new Mesh { name = name + "_Lines" };
            if (d.lv.Count > 65000) lm.indexFormat = IndexFormat.UInt32;
            lm.SetVertices(d.lv);
            lm.SetTriangles(d.lt, 0);
            lm.RecalculateNormals();
            var lgo = new GameObject(name + "_Lines");
            lgo.transform.SetParent(go.transform, false);
            lgo.AddComponent<MeshFilter>().sharedMesh = lm;
            var lr = lgo.AddComponent<MeshRenderer>();
            lr.sharedMaterial = lineMat;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.lightProbeUsage = LightProbeUsage.Off;
            return d.clipped;
        }
    }
}
