using System;
using System.Diagnostics;

class Program {
    static void Main() {
        Process p = Process.GetProcessById(28076);
        foreach (ProcessModule m in p.Modules) {
            Console.WriteLine(m.ModuleName);
        }
    }
}
