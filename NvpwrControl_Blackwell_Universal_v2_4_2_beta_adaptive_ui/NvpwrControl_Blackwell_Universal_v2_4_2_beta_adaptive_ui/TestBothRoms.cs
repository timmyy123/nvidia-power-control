using System;
using System.Collections.Generic;
using System.IO;

class Program
{
    private sealed class RomImage
    {
        public int Start;
        public int End;
        public int DeviceId;
        public int CodeType;
    }

    private sealed class PowerCandidate
    {
        public RomImage Image;
        public int TableOffset;
        public int EntryOffset;
        public int EntryIndex;
        public int MaxFieldOffset;
        public uint Min;
        public uint Default;
        public uint Max;
        public uint BasePower;
        public int ShadowOffset { get { return MaxFieldOffset - Image.Start; } }
    }

    static void Main()
    {
        string[] roms = new string[] {
            @"c:\Users\timmy\Downloads\nvidia-power-control\Scar16_4090_stock.rom",
            @"c:\Users\timmy\Downloads\nvidia-power-control\NvpwrControl_Blackwell_Universal_v2_4_2_beta_adaptive_ui\NvpwrControl_Blackwell_Universal_v2_4_2_beta_adaptive_ui\dist\state\rom-cache\auto-20260929-060251.rom"
        };

        foreach (var path in roms)
        {
            Console.WriteLine("\n========================================================");
            Console.WriteLine("Testing ROM: " + Path.GetFileName(path));
            Console.WriteLine("========================================================");
            byte[] b = File.ReadAllBytes(path);

            List<RomImage> allImages = FindNvidiaImages(b);
            List<RomImage> legacyImages = new List<RomImage>();
            foreach (var img in allImages)
                if (img.CodeType == 0) legacyImages.Add(img);

            for (int i = 0; i < legacyImages.Count; i++)
            {
                int nextStart = i + 1 < legacyImages.Count ? legacyImages[i + 1].Start : b.Length;
                legacyImages[i].End = nextStart;
            }

            List<PowerCandidate> candidates = new List<PowerCandidate>();
            int stockW = 175;

            foreach (var img in legacyImages)
            {
                FindPowerCandidates(b, img, stockW, candidates);
            }

            var uniqueShadows = new HashSet<int>();
            var deduped = new List<PowerCandidate>();
            foreach (var c in candidates)
            {
                if (uniqueShadows.Add(c.ShadowOffset))
                {
                    deduped.Add(c);
                }
            }

            Console.WriteLine("Raw candidates: " + candidates.Count + ", Unique: " + deduped.Count);
            if (deduped.Count == 1)
            {
                var win = deduped[0];
                Console.WriteLine(string.Format("SUCCESS! Resolved ShadowOffset = 0x{0:X} ({1} W) Image.Start=0x{2:X6} RawMax=0x{3:X6}",
                    win.ShadowOffset, win.Max / 1000, win.Image.Start, win.MaxFieldOffset));
            }
            else
            {
                Console.WriteLine("FAILED! Count = " + deduped.Count);
            }
        }
    }

    private static List<RomImage> FindNvidiaImages(byte[] b)
    {
        List<RomImage> hits = new List<RomImage>();
        for (int s = 0; s + 0x1A < b.Length; s++)
        {
            if (b[s] != 0x55 || b[s + 1] != 0xAA) continue;
            int pcirRel = BitConverter.ToUInt16(b, s + 0x18);
            if (pcirRel <= 0) continue;
            int p = s + pcirRel;
            if (p < 0 || p + 0x18 > b.Length) continue;
            if (b[p] != 'P' || b[p + 1] != 'C' || b[p + 2] != 'I' || b[p + 3] != 'R') continue;
            if (BitConverter.ToUInt16(b, p + 4) != 0x10DE) continue;

            RomImage img = new RomImage();
            img.Start = s;
            img.DeviceId = BitConverter.ToUInt16(b, p + 6);
            img.CodeType = b[p + 0x14];
            hits.Add(img);
        }
        hits.Sort((x, y) => x.Start.CompareTo(y.Start));
        return hits;
    }

    private static void FindPowerCandidates(byte[] b, RomImage image, int stockPowerW, List<PowerCandidate> output)
    {
        int start = Math.Max(0, image.Start);
        int end = Math.Min(b.Length, image.End > image.Start ? image.End : b.Length);
        uint expectedMax = (uint)(stockPowerW * 1000);

        for (int t = start; t + 0x30 <= end; t++)
        {
            byte ver = b[t];
            if (ver != 0x40 && ver != 0x30) continue;
            int hdr = b[t + 1];
            int esize = b[t + 2];
            int count = b[t + 3];

            if (hdr < 0x20 || hdr > 0x60 || esize < 0x40 || esize > 0x80) continue;
            if (count < 1 || count > 64) continue;
            long tableEnd = (long)t + (long)hdr + (long)count * (long)esize;
            if (tableEnd > end) continue;

            for (int i = 0; i < count; i++)
            {
                int e = t + hdr + i * esize;
                uint min, def, max, basePower;
                int maxFieldOffset;

                if (ver == 0x40) // Blackwell
                {
                    if (e + 0x19 > end) break;
                    min = BitConverter.ToUInt32(b, e + 0x0D);
                    def = BitConverter.ToUInt32(b, e + 0x11);
                    max = BitConverter.ToUInt32(b, e + 0x15);
                    basePower = (esize >= 0x4D && e + 0x4D <= end) ? BitConverter.ToUInt32(b, e + 0x49) : def;
                    maxFieldOffset = e + 0x15;
                }
                else // 0x30 Ada Lovelace
                {
                    if (e + 0x22 > end) break;
                    min = BitConverter.ToUInt32(b, e + 0x06);
                    max = BitConverter.ToUInt32(b, e + 0x0A);
                    def = BitConverter.ToUInt32(b, e + 0x1A);
                    basePower = BitConverter.ToUInt32(b, e + 0x1E);
                    maxFieldOffset = e + 0x0A;
                }

                if (max != expectedMax) continue;
                if (!Plausible(min, def, max, basePower)) continue;

                PowerCandidate c = new PowerCandidate();
                c.Image = image;
                c.TableOffset = t;
                c.EntryOffset = e;
                c.EntryIndex = i;
                c.MaxFieldOffset = maxFieldOffset;
                c.Min = min;
                c.Default = def;
                c.Max = max;
                c.BasePower = basePower;
                output.Add(c);
            }
        }
    }

    private static bool Plausible(uint min, uint def, uint max, uint basePower)
    {
        if (min < 1000 || min > 200000) return false;
        if (max < 10000 || max > 500000) return false;
        if (min > max) return false;
        return true;
    }
}
