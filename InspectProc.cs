using System;
using System.Diagnostics;

class Program {
    static void Main() {
        try {
            Process p = Process.GetProcessById(28076);
            Console.WriteLine("Process: " + p.ProcessName);
            Console.WriteLine("Responding: " + p.Responding);
            Console.WriteLine("Start time: " + p.StartTime);
            Console.WriteLine("Threads: " + p.Threads.Count);
            foreach (ProcessThread t in p.Threads) {
                Console.WriteLine(" Thread: ID=" + t.Id + " State=" + t.ThreadState);
            }
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
