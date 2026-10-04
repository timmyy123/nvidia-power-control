using System;
using System.IO;

namespace NvpwrControlBlackwell
{
    class Program
    {
        static void Main()
        {
            string path = @"c:\Users\timmy\Downloads\nvidia-power-control\Scar16_4090_stock.rom";
            byte[] b = File.ReadAllBytes(path);
            Console.WriteLine("ROM size: " + b.Length);

            // Check VBIOS version string
            string ascii = System.Text.Encoding.ASCII.GetString(b);
            int idx = ascii.IndexOf("95.03.");
            if (idx >= 0)
            {
                Console.WriteLine("VBIOS version found at 0x" + idx.ToString("X") + ": " + ascii.Substring(idx, 14));
            }

            // Test VbiosResolver
            GpuProfile p = GpuProfiles.Detect("NVIDIA GeForce RTX 4090 Laptop GPU", @"PCI\VEN_10DE&DEV_2757&SUBSYS_213D1043&REV_A1");
            Console.WriteLine("Profile: " + (p != null ? p.Name + " stock=" + p.StockPowerW : "NULL"));

            VbiosResolutionResult res = VbiosResolver.Resolve(p, path, @"PCI\VEN_10DE&DEV_2757&SUBSYS_213D1043&REV_A1", "95.03.2b.00.31");
            Console.WriteLine("Resolved: " + res.Resolved);
            Console.WriteLine("Reason: " + res.Reason);
            Console.WriteLine("StockMaxW: " + res.StockMaxW);
            Console.WriteLine("ShadowOffset: 0x" + res.ShadowOffset.ToString("X"));
            Console.WriteLine("RomImageCount: " + res.RomImageCount);
            Console.WriteLine("MatchingRomImageCount: " + res.MatchingRomImageCount);
            Console.WriteLine("CandidateCount: " + res.CandidateCount);
        }
    }
}
