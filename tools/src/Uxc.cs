using System; using System.IO; using System.Text; using System.Collections.Generic;
class Uxc {
  static uint U32(byte[] b,int o){ return BitConverter.ToUInt32(b,o); }
  static void Main(string[] a){
    foreach(string p in a){
      if(!File.Exists(p)){ Console.WriteLine("(无) "+p); continue; }
      byte[] b=File.ReadAllBytes(p);
      Console.WriteLine("############ "+p.Replace("E:\\Agent\\SOY-1F-64\\","")+"   "+b.Length+" B");
      Console.WriteLine("  首 64B: "+BitConverter.ToString(b,0,Math.Min(64,b.Length)).Replace("-"," "));
      Console.WriteLine("  头部 u32 (前 16 个): ");
      var sb=new StringBuilder();
      for(int i=0;i<16 && i*4+4<=b.Length;i++) sb.Append("["+i+"]=0x"+U32(b,i*4).ToString("X8")+" ");
      Console.WriteLine("    "+sb);
      // 字符串扫描
      var strs=new List<string>(); int s=-1;
      for(int i=0;i<b.Length;i++){
        byte c=b[i];
        if(c>=32&&c<127){ if(s<0) s=i; }
        else { if(s>=0 && i-s>=5){ strs.Add(s.ToString("X")+" \""+Encoding.ASCII.GetString(b,s,i-s)+"\""); } s=-1; }
      }
      Console.WriteLine("  可打印串(>=5) 共 "+strs.Count+"，前 40 条：");
      for(int i=0;i<Math.Min(40,strs.Count);i++) Console.WriteLine("    "+strs[i]);
      Console.WriteLine();
    }
  }
}
