using System;
using System.IO;

namespace NvpwrControlBlackwell
{
    class Program
    {
        static void Main()
        {
            using (NvApiSession s = new NvApiSession())
            {
                string err;
                if (!s.Open(out err)) { Console.WriteLine("NvApiSession open failed: " + err); return; }

                NvApiSession.GpuBufferDelegate infoFn = s.GetGpuBuffer(0x507B4B59);
                NvApiSession.GpuBufferDelegate stFn = s.GetGpuBuffer(0x21537AD4);
                NvApiSession.GpuBufferDelegate getFn = s.GetGpuBuffer(0x23F1B133);

                byte[] infoBuf = new byte[6188];
                BitConverter.GetBytes(0x0001182C).CopyTo(infoBuf, 0);
                infoFn(s.Gpu, NvApiSession.CallBuffer(infoFn, s.Gpu, infoBuf) == 0 ? IntPtr.Zero : IntPtr.Zero); // wait, let's use CallBuffer properly
            }
        }
    }
}
