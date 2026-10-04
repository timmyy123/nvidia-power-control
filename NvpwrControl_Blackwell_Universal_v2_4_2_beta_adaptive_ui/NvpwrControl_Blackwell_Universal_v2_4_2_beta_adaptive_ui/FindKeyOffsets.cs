using System;
using System.IO;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        string path = @"C:\Windows\System32\DriverStore\FileRepository\nvmii.inf_amd64_185e17ffef00b7bf\nvlddmkm.sys";
        byte[] bytes = File.ReadAllBytes(path);

        string[] targets = new string[] {
            "RMForceLockedClocksMode",
            "RmPerfLimitsOverride",
            "RMClkVfOverride",
            "RMEnableOverclockingAllPstates",
            "RmVoltThresholdCtrlCtrl",
            "romOverride"
        };

        foreach (var t in targets)
        {
            byte[] pat = Encoding.ASCII.GetBytes(t);
            int idx = FindPattern(bytes, pat);
            Console.WriteLine(string.Format("{0,-30} at 0x{1:X8}", t, idx));
        }
    }

    static int FindPattern(byte[] src, byte[] pat)
    {
        int max = src.Length - pat.Length;
        for (int i = 0; i <= max; i++)
        {
            bool match = true;
            for (int j = 0; j < pat.Length; j++)
            {
                if (src[i + j] != pat[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
