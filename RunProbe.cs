using System;
using NvpwrControlBlackwell;

class Program {
    static void Main() {
        var state = NvApiTuner.Probe(true);
        Console.WriteLine("Core: " + state.CoreMHz.Supported + " Min=" + state.CoreMHz.Min + " Max=" + state.CoreMHz.Max + " Cur=" + state.CoreMHz.Current);
        Console.WriteLine("Mem: " + state.MemoryMHz.Supported + " Min=" + state.MemoryMHz.Min + " Max=" + state.MemoryMHz.Max + " Cur=" + state.MemoryMHz.Current);
        Console.WriteLine("NVVDD: " + state.NvvddMv.Supported + " Min=" + state.NvvddMv.Min + " Max=" + state.NvvddMv.Max + " Cur=" + state.NvvddMv.Current);
        Console.WriteLine("XBAR: " + state.XbarMHz.Supported);
        Console.WriteLine("MSVDD: " + state.MsvddMv.Supported);
        Console.WriteLine("Error: " + state.Error);
    }
}
