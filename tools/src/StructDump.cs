using System; using System.IO; using System.Text; using System.Collections.Generic;
class StructDump {
  static uint U32(byte[] b,long o){ return BitConverter.ToUInt32(b,(int)o); }
  static ushort U16(byte[] b,long o){ return BitConverter.ToUInt16(b,(int)o); }
  static string S(byte[] b,long o){ var sb=new StringBuilder(); while(o<b.Length&&b[o]!=0){ sb.Append((char)b[o]); o++; } return sb.ToString(); }
  static List<long> L0=new List<long>();
  static long V2O(long va){ for(int i=0;i<L0.Count;i+=3){ if(va>=L0[i]&&va<L0[i]+L0[i+2]) return L0[i+1]+(va-L0[i]); } return -1; }
  static void Main(string[] a){
    byte[] b=File.ReadAllBytes(a[0]);
    long phoff=U32(b,28); int phent=U16(b,42),phnum=U16(b,44);
    for(int i=0;i<phnum;i++){ long o=phoff+(long)i*phent; if(U32(b,o)==1){ L0.Add(U32(b,o+8)); L0.Add(U32(b,o+4)); L0.Add(U32(b,o+16)); } }
    long shoff=U32(b,32); int shent=U16(b,46),shnum=U16(b,48);
    // 收集所有重定位 (offset -> 符号名)
    var reloc=new Dictionary<long,string>();
    for(int i=0;i<shnum;i++){ long o=shoff+(long)i*shent; uint type=U32(b,o+4);
      if(type!=9&&type!=4) continue;
      long lk=U32(b,o+24); long sh2=shoff+lk*shent;
      long symOff=U32(b,sh2+16); long symSize=U32(b,sh2+20); int ent=(int)U32(b,sh2+36);
      long sstr=U32(b,shoff+U32(b,sh2+24)*shent+16);
      int nn=ent>0?(int)(symSize/ent):0; var nm=new Dictionary<long,string>();
      for(int k=0;k<nn;k++){ long so=symOff+(long)k*ent; nm[(long)k]=S(b,sstr+U32(b,so)); }
      long off=U32(b,o+16); long size=U32(b,o+20); int m=(ent>0)?(int)(size/ent):0;
      for(int k=0;k<m;k++){ long ro=off+(long)k*ent; long r_off=U32(b,ro); long si=(long)(U32(b,ro+4)>>8);
        string n=(nm.ContainsKey(si)?nm[si]:"?sym"+si); reloc[r_off]=(reloc.ContainsKey(r_off)?reloc[r_off]+" | ":"")+n; } }
    long[] slots={0xBE2E04,0xBE319C,0xBE2E04-0x380};
    foreach(long s0 in slots){
      long lo=s0-0x30, hi=s0+0x60;
      Console.WriteLine("\n########## 结构体区域 0x"+lo.ToString("X")+" .. 0x"+hi.ToString("X")+" (锚点 0x"+s0.ToString("X")+") ##########");
      for(long va=lo; va<hi; va+=4){
        long fo=V2O(va); if(fo<0||fo+4>b.Length){ Console.WriteLine("  0x"+va.ToString("X")+"  (未映射)"); continue; }
        uint d=U32(b,fo);
        string tag=reloc.ContainsKey(va)?("  <- RELOC: "+reloc[va]):"";
        Console.WriteLine("  0x"+va.ToString("X")+"  u32=0x"+d.ToString("X8").PadRight(9)+" ("+d.ToString().PadLeft(10)+")"+tag);
      }
    }
    // 统计：间距直方图
    Console.WriteLine("\n########## 239 个槽位的间距分析 ##########");
    var list=new List<long>(); foreach(var kv in reloc) if(kv.Value.Contains("setDefWidgetMode")) list.Add(kv.Key);
    list.Sort();
    Console.WriteLine("  共 "+list.Count+" 个槽位");
    var gap=new SortedDictionary<long,int>();
    for(int i=1;i<list.Count;i++){ long d=list[i]-list[i-1]; if(!gap.ContainsKey(d)) gap[d]=0; gap[d]++; }
    foreach(var kv in gap) Console.WriteLine("    间距 0x"+kv.Key.ToString("X")+" ("+kv.Key+") 出现 "+kv.Value+" 次");
    Console.WriteLine("  首尾: 0x"+list[0].ToString("X")+" .. 0x"+list[list.Count-1].ToString("X"));
  }
}
