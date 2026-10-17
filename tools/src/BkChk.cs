using System; using System.IO; using System.Text;
class BkChk {
  static void Main(string[] a){
    foreach(string p in a){
      if(!File.Exists(p)){ Console.WriteLine("(无) "+p); continue; }
      byte[] b=File.ReadAllBytes(p);
      Console.WriteLine("=== "+Path.GetFileName(p)+"   "+b.Length+" B");
      Console.WriteLine("  +0x00 u32 = 0x"+BitConverter.ToUInt32(b,0).ToString("X8"));
      Console.WriteLine("  +0x04 u32 = 0x"+BitConverter.ToUInt32(b,4).ToString("X8")+"   (真机特征 0xAAAAAAAA)");
      Console.WriteLine("  +0x0C asc = \""+Encoding.ASCII.GetString(b,0x0C,4).TrimEnd('\0')+"\"");
      Console.WriteLine("  +0x0BD A40 u32 = 0x"+BitConverter.ToUInt32(b,0x0bda40).ToString("X8")+"   (文档: 真机 BIN=0, 修好=0x20098000)");
      Console.WriteLine("  +0x0BD B14 u32 = 0x"+BitConverter.ToUInt32(b,0x0bdb14).ToString("X8")+"   (文档: 真机 BIN=0, 修好=0x0007C000)");
      Console.WriteLine("  该处 16B: "+(BitConverter.ToString(b,0x0bda40,16)));
      Console.WriteLine("  该处 16B: "+(BitConverter.ToString(b,0x0bdb14,16)));
    }
  }
}
