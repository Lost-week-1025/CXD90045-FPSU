using System; using System.IO; using System.Text; using System.Collections.Generic;
class Uxc2 {
  static int U16(byte[] b,int o){ return (int)BitConverter.ToUInt16(b,o); }
  static void Main(string[] a){
    var files=new List<string>();
    foreach(string p in a){ if(p.EndsWith(".xdb")) continue; files.Add(p); }
    foreach(string p in files){
      if(!File.Exists(p)){ Console.WriteLine("(无) "+p); continue; }
      byte[] b=File.ReadAllBytes(p);
      Console.WriteLine("=== "+Path.GetFileName(p).PadRight(28)+" "+b.Length+" B");
      var vals=new List<int>();
      for(int o=8;o+2<=b.Length && vals.Count<90;o+=2){ int v=U16(b,o); vals.Add(v); if(v==0xFFFF) break; }
      Console.WriteLine("  u16表(0x08起): "+string.Join(",",vals.ConvertAll(x=>x==0xFFFF?"FFFF":x.ToString("X"))));
      int mx=0; foreach(int v in vals) if(v!=0xFFFF && v>mx) mx=v;
      Console.WriteLine("  最大偏移 0x"+mx.ToString("X")+" / 文件 0x"+b.Length.ToString("X")+"  "+(mx<b.Length?"在文件内":"越界"));
    }
    foreach(string p in a){
      if(!p.EndsWith(".xdb")) continue;
      if(!File.Exists(p)){ Console.WriteLine("(无) "+p); continue; }
      byte[] b=File.ReadAllBytes(p);
      Console.WriteLine("\n=== "+Path.GetFileName(p)+"  "+b.Length+" B");
      Console.WriteLine("  首 64B: "+BitConverter.ToString(b,0,Math.Min(64,b.Length)).Replace("-"," "));
      int cnt=0; var sb=new StringBuilder(); int s=-1;
      for(int i=0;i<b.Length;i++){ byte c=b[i];
        if(c>=32&&c<127){ if(s<0) s=i; }
        else { if(s>=0 && i-s>=3){ if(cnt<25) sb.Append("["+s.ToString("X")+"]"+Encoding.ASCII.GetString(b,s,i-s)+"  "); cnt++; } s=-1; } }
      Console.WriteLine("  可打印串(>=3) 共 "+cnt+":");
      Console.WriteLine("  "+sb);
    }
  }
}
