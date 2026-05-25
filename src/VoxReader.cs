using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

public class VoxModel
{
    public int SizeX, SizeY, SizeZ;                          // MagicaVoxel axes: X=right, Y=depth, Z=up
    public (byte X, byte Y, byte Z, byte ColorIndex)[] Voxels;
    public string Name = "";
    public Vector3I SceneTranslation;                        // model center in MV world space (Z=up)
}

public class VoxFile
{
    public VoxModel[] Models;
    public Color[] Palette;                                  // 256 entries; index 0 = empty/transparent
}

public static class VoxReader
{
    public static VoxFile Load(string resPath)
    {
        var abs = ProjectSettings.GlobalizePath(resPath);
        return Parse(File.ReadAllBytes(abs));
    }

    // ── Chunk types ──────────────────────────────────────────────────────────

    private record NTrn(int ChildId, string Name, Vector3I Translation);
    private record NGrp(int[] ChildIds);
    private record NShp(int ModelId);

    // ── Parser ───────────────────────────────────────────────────────────────

    private static VoxFile Parse(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var r  = new BinaryReader(ms);

        if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "VOX ")
            throw new InvalidDataException("Not a MagicaVoxel .vox file");
        r.ReadInt32(); // version

        // MAIN chunk
        ReadChunkId(r);            // "MAIN"
        r.ReadInt32();             // content size (always 0 for MAIN)
        long end = ms.Position + r.ReadInt32();

        var rawModels = new List<(int sx, int sy, int sz, List<(byte, byte, byte, byte)> voxels)>();
        var palette   = DefaultPalette();

        var ntrns = new Dictionary<int, NTrn>();
        var ngrps = new Dictionary<int, NGrp>();
        var nshps = new Dictionary<int, NShp>();

        while (ms.Position < end)
        {
            var id          = ReadChunkId(r);
            var contentSize = r.ReadInt32();
            var childSize   = r.ReadInt32();
            var contentEnd  = ms.Position + contentSize;

            switch (id)
            {
                case "PACK":
                    r.ReadInt32();
                    break;

                case "SIZE":
                    rawModels.Add((r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), null));
                    break;

                case "XYZI":
                    int n    = r.ReadInt32();
                    var voxs = new List<(byte, byte, byte, byte)>(n);
                    for (int i = 0; i < n; i++)
                        voxs.Add((r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte()));
                    var last = rawModels[^1];
                    rawModels[^1] = (last.sx, last.sy, last.sz, voxs);
                    break;

                case "RGBA":
                    // .vox spec: chunk entry i is the color for palette index i+1.
                    for (int i = 0; i < 256; i++)
                        palette[(i + 1) % 256] = new Color(
                            r.ReadByte() / 255f, r.ReadByte() / 255f,
                            r.ReadByte() / 255f, r.ReadByte() / 255f);
                    palette[0] = new Color(0, 0, 0, 0);
                    break;

                case "nTRN": ntrns[ReadNTrn(r, out var ntrn)] = ntrn; break;
                case "nGRP": ngrps[ReadNGrp(r, out var ngrp)] = ngrp; break;
                case "nSHP": nshps[ReadNShp(r, out var nshp)] = nshp; break;
            }

            // Skip any remaining content + child chunks we didn't consume
            ms.Position = contentEnd + childSize;
        }

        // Build VoxModel array
        var models = new VoxModel[rawModels.Count];
        for (int i = 0; i < rawModels.Count; i++)
        {
            var (sx, sy, sz, voxList) = rawModels[i];
            var arr = new (byte X, byte Y, byte Z, byte ColorIndex)[voxList?.Count ?? 0];
            for (int j = 0; j < arr.Length; j++)
            {
                var v = voxList[j];
                arr[j] = (v.Item1, v.Item2, v.Item3, v.Item4);
            }
            models[i] = new VoxModel { SizeX = sx, SizeY = sy, SizeZ = sz, Voxels = arr };
        }

        ApplySceneGraph(models, ntrns, nshps);
        return new VoxFile { Models = models, Palette = palette };
    }

    // ── Scene graph readers ──────────────────────────────────────────────────

    private static int ReadNTrn(BinaryReader r, out NTrn node)
    {
        int id      = r.ReadInt32();
        var attrs   = ReadDict(r);
        int childId = r.ReadInt32();
        r.ReadInt32(); // reserved
        r.ReadInt32(); // layer id
        int numFrames = r.ReadInt32();

        var translation = Vector3I.Zero;
        for (int f = 0; f < numFrames; f++)
        {
            var frame = ReadDict(r);
            if (f == 0 && frame.TryGetValue("_t", out string t))
                translation = ParseVec3I(t);
        }

        attrs.TryGetValue("_name", out string name);
        node = new NTrn(childId, name ?? "", translation);
        return id;
    }

    private static int ReadNGrp(BinaryReader r, out NGrp node)
    {
        int id = r.ReadInt32();
        ReadDict(r);
        int n       = r.ReadInt32();
        var children = new int[n];
        for (int i = 0; i < n; i++) children[i] = r.ReadInt32();
        node = new NGrp(children);
        return id;
    }

    private static int ReadNShp(BinaryReader r, out NShp node)
    {
        int id = r.ReadInt32();
        ReadDict(r);
        int numModels = r.ReadInt32();
        int modelId   = r.ReadInt32();
        ReadDict(r); // model attributes
        for (int i = 1; i < numModels; i++) { r.ReadInt32(); ReadDict(r); }
        node = new NShp(modelId);
        return id;
    }

    // ── Scene graph application ──────────────────────────────────────────────

    private static void ApplySceneGraph(
        VoxModel[] models,
        Dictionary<int, NTrn> ntrns,
        Dictionary<int, NShp> nshps)
    {
        foreach (var (_, ntrn) in ntrns)
        {
            // Each named object is a transform node whose child is a shape node
            if (!nshps.TryGetValue(ntrn.ChildId, out var nshp)) continue;
            int mid = nshp.ModelId;
            if (mid < 0 || mid >= models.Length) continue;
            models[mid].Name             = ntrn.Name;
            models[mid].SceneTranslation = ntrn.Translation;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string ReadChunkId(BinaryReader r)
        => Encoding.ASCII.GetString(r.ReadBytes(4));

    private static Dictionary<string, string> ReadDict(BinaryReader r)
    {
        int n    = r.ReadInt32();
        var dict = new Dictionary<string, string>(n);
        for (int i = 0; i < n; i++)
        {
            string k = Encoding.ASCII.GetString(r.ReadBytes(r.ReadInt32()));
            string v = Encoding.ASCII.GetString(r.ReadBytes(r.ReadInt32()));
            dict[k] = v;
        }
        return dict;
    }

    private static Vector3I ParseVec3I(string s)
    {
        var p = s.Split(' ');
        return p.Length >= 3
            ? new Vector3I(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]))
            : Vector3I.Zero;
    }

    private static Color[] DefaultPalette()
    {
        var p = new Color[256];
        p[0] = new Color(0, 0, 0, 0);
        for (int i = 1; i < 256; i++) p[i] = Colors.White;
        return p;
    }
}
