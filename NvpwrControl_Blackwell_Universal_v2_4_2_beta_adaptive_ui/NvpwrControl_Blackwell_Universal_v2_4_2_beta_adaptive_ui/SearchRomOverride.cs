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
        Regex r = new Regex(@"romOverride[A-Za-z0-9_]*");
        var matches = r.Matches(ascii);

        Console.WriteLine("--- romOverride occurrences ---");
        var set = new System.Collections.Generic.HashSet<string>();
        foreach (Match m in matches)
        {
            if (set.Add(m.Value)) Console.WriteLine(m.Value);
        }
    }
}
