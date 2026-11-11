// StageDef.cs - rally event definitions (split from StageCourse.cs so the world data can reference it
// without engine objects). The valley's five events come first; region events are appended at load.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public enum StageKind { TimeAttack, Drift }

    public class StageDef
    {
        public string key, name, blurb;
        public int road;                 // index into WorldBuilder.roads / roadSamples
        public string roadName;          // region stages: resolved to road at load (WorldBuilder)
        public RegionId region = RegionId.Valley;
        public StageKind kind;
        public float startOffset;        // start line, metres along the road from its first sample
        public float endOffset;          // open roads: finish line this far before the last sample (closed: one full lap)
        public float goldSpeed = 20f;    // time attack: average speed (m/s) needed for gold
        public float limitSpeed = 12.5f; // drift: time limit = length / limitSpeed
        public int bronzePts, silverPts, goldPts;

        public const float SilverFactor = 1.15f;   // silver time = gold time x 1.15
        public const float BronzeFactor = 1.35f;   // bronze time = gold time x 1.35

        /// <summary>Every event: the valley's five, then the regions' (registered once the world's roads exist).</summary>
        public static StageDef[] All { get { return all ?? Valley; } }
        static StageDef[] all;

        static readonly StageDef[] Valley =
        {
            new StageDef { key = "ValleyLoop", name = "Valley Loop", blurb = "Time attack - one lap of the asphalt valley loop",
                           road = 0, kind = StageKind.TimeAttack, startOffset = 45f, goldSpeed = 30f },
            new StageDef { key = "PagodaClimb", name = "Pagoda Climb", blurb = "Time attack - asphalt hairpins up to the pagoda",
                           road = 3, kind = StageKind.TimeAttack, startOffset = 44f, endOffset = 14f, goldSpeed = 13.5f },
            new StageDef { key = "TeaTerrace", name = "Tea Terrace Sprint", blurb = "Time attack - gravel switchbacks through the tea terraces",
                           road = 1, kind = StageKind.TimeAttack, startOffset = 20f, endOffset = 45f, goldSpeed = 14f },
            new StageDef { key = "BambooRun", name = "Bamboo Run", blurb = "Time attack - fast dirt road through the bamboo forest",
                           road = 2, kind = StageKind.TimeAttack, startOffset = 20f, endOffset = 20f, goldSpeed = 18f },
            new StageDef { key = "PagodaDrift", name = "Pagoda Drift Zone", blurb = "Drift challenge - score drift points up the hairpins before time runs out",
                           road = 3, kind = StageKind.Drift, startOffset = 44f, endOffset = 14f, limitSpeed = 12.5f,
                           bronzePts = 2500, silverPts = 6000, goldPts = 10000 },
        };

        /// <summary>Adds region stages, resolving roadName to a road index (stages whose road is missing are skipped).</summary>
        public static void Register(List<StageDef> extra, System.Func<string, int> roadIndex)
        {
            var list = new List<StageDef>(Valley);
            foreach (var d in extra)
            {
                if (d == null) continue;
                if (!string.IsNullOrEmpty(d.roadName))
                {
                    int r = roadIndex(d.roadName);
                    if (r < 0) continue;
                    d.road = r;
                }
                list.Add(d);
            }
            all = list.ToArray();
        }

        public float GoldTime(float length) { return length / goldSpeed; }
        public float SilverTime(float length) { return GoldTime(length) * SilverFactor; }
        public float BronzeTime(float length) { return GoldTime(length) * BronzeFactor; }
        public float TimeLimit(float length) { return length / limitSpeed; }

        /// <summary>0 none, 1 bronze, 2 silver, 3 gold.</summary>
        public int MedalForTime(float t, float length)
        {
            if (t <= GoldTime(length)) return 3;
            if (t <= SilverTime(length)) return 2;
            if (t <= BronzeTime(length)) return 1;
            return 0;
        }

        public int MedalForPoints(float pts)
        {
            if (pts >= goldPts) return 3;
            if (pts >= silverPts) return 2;
            if (pts >= bronzePts) return 1;
            return 0;
        }
    }

}
