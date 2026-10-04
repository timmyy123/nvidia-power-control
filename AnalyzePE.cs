using System;
using System.IO;
using System.Text;

class Program {
    static void Main() {
        byte[] data = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        Console.WriteLine("File size: " + data.Length);

        // Check PE
        int peOffset = BitConverter.ToInt32(data, 0x3C);
        Console.WriteLine("PE offset: 0x" + peOffset.ToString("X"));

        // Let's search for imports
        string text = Encoding.ASCII.GetString(data);
        string[] knownDlls = { "d3d", "d2d", "dxgi", "user32", "gdi32", "nvapi" };
        foreach (var dll in knownDlls) {
            int pos = 0;
            while ((pos = text.IndexOf(dll, pos, StringComparison.OrdinalIgnoreCase)) >= 0) {
                int start = Math.Max(0, pos - 10);
                int len = Math.Min(40, text.Length - start);
                Console.WriteLine("Found " + dll + " at 0x" + pos.ToString("X") + ": " + text.Substring(start, len).Replace("\0", " "));
                pos += dll.Length;
                if (pos > 0x100000) break; // only first few
            }
        }
    }
}
