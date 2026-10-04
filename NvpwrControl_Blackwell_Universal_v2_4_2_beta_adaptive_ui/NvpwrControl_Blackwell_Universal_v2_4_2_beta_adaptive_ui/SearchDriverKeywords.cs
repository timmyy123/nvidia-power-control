using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
    static void Main(string[] args)
    {
        string path = @"C:\Windows\System32\DriverStore\FileRepository\nvmii.inf_amd64_185e17ffef00b7bf\nvlddmkm.sys";
        byte[] bytes = File.ReadAllBytes(path);
        Console.WriteLine("nvlddmkm.sys size: " + bytes.Length);

        string ascii = Encoding.ASCII.GetString(bytes);
        Regex r = new Regex(@"[A-Za-z0-9_]{4,40}");
        var matches = r.Matches(ascii);

        Console.WriteLine("--- Matching keywords ---");
        foreach (Match m in matches)
        {
            string s = m.Value;
            if (s.IndexOf("romOverride", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("VfPoints", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("PerfLimit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("VoltLimit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("VoltageLimit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Vrel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Vop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Overclocking", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine(s);
            }
        }
    }
}
