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

        string ascii = Encoding.ASCII.GetString(bytes);
        Regex r = new Regex(@"\b(RM[A-Za-z0-9_]{3,35}|Rm[A-Za-z0-9_]{3,35})\b");
        var matches = r.Matches(ascii);

        Console.WriteLine("--- Matching RM keys ---");
        var set = new System.Collections.Generic.HashSet<string>();
        foreach (Match m in matches)
        {
            string s = m.Value;
            if (s.IndexOf("Override", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Voltage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Volt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Limit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Clock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Boost", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (set.Add(s)) Console.WriteLine(s);
            }
        }
    }
}
