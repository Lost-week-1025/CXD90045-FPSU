using System; using System.IO; using System.Text; using System.Collections.Generic;
class VtBase2 {
  static uint U32(byte[] b,long o){ return BitConverter.ToUInt32(b,(int)o); }
  static ushort U16(byte[] b,long o){ return BitConverter.ToUInt16(b,(int)o); }
  static string S(byte[] b,long o){ var sb=new StringBuilder(); while(o<b.Length&&b[o]!=0){ sb.Append((char)b[o]); o++; } return sb.ToString(); }
  static List<long> L0=new List<long>();
  static long V2O(long va){ for(int i=0;i<L0.Count;i+=3){ if(va>=L0[i]&&va<L0[i]+L0[i+2]) return L0[i+1]+(va-L0[i]); } return -1; }
  static long TV=0, TS=0;
  static Dictionary<long,long> REL=new Dictionary<long,long>();
  static bool IsSlot(byte[] b,long va){
    if(REL.ContainsKey(va)) return true;
    long fo=V2O(va); if(fo<0||fo+4>b.Length) return false;
    uint d=U32(b,fo); return d>=TV && d<TV+TS;
  }
  static void Main(string[] a){
    byte[] b=File.ReadAllBytes(a[0]);
    long phoff=U32(b,28); int phent=U16(b,42),phnum=U16(b,44);
    for(int i=0;i<phnum;i++){ long o=phoff+(long)i*phent; if(U32(b,o)==1){ L0.Add(U32(b,o+8)); L0.Add(U32(b,o+4)); L0.Add(U32(b,o+16)); } }
    long shoff=U32(b,32); int shent=U16(b,46),shnum=U16(b,48);
    long shstr=U32(b,shoff+(long)U16(b,50)*shent+16);
    for(int i=0;i<shnum;i++){ long o=shoff+(long)i*shent; string nm=S(b,shstr+U32(b,o));
      if(nm==".text"){ TV=U32(b,o+12); TS=U32(b,o+20); } }
    for(int i=0;i<shnum;i++){ long o=shoff+(long)i*shent; uint type=U32(b,o+4);
      if(type!=9&&type!=4) continue;
      long ro0=U32(b,o+16); long sz=U32(b,o+20); int ent=(int)U32(b,o+36);
      int m=(ent>0)?(int)(sz/ent):0;
      for(int k=0;k<m;k++){ long ro=ro0+(long)k*ent; REL[U32(b,ro)]=U32(b,ro+4)&0xFF; } }
    Console.WriteLine("## "+Path.GetFileName(a[0])+"  .text 0x"+TV.ToString("X")+" size 0x"+TS.ToString("X")+"   reloc槽 "+REL.Count);
    long anchor=0xBE2E04;
    long end=anchor; int zc=0;
    for(long va=anchor; va<anchor+0x400; va+=4){
      if(IsSlot(b,va)){ zc=0; end=va; } else { zc++; if(zc>=3){ break; } }
    }
    long start=anchor; zc=0;
    for(long va=anchor; va>anchor-0x800; va-=4){
      if(IsSlot(b,va)){ zc=0; start=va; } else { zc++; if(zc>=3){ break; } }
    }
    long span=end-start+4;
    Console.WriteLine("\n===== 结论 =====");
    Console.WriteLine("  表区间 = 0x"+start.ToString("X")+" .. 0x"+end.ToString("X")+"   "+span+" B = "+(span/4)+" 个 4 字节位");
    Console.WriteLine("  setDefWidgetMode @ 0x"+anchor.ToString("X")+"  相对表头槽索引 = "+((anchor-start)/4));
    Console.WriteLine("  若按 Itanium ABI（[-2]=offset-to-top, [-1]=RTTI），虚函数索引 = "+((anchor-start)/4-2));
    Console.WriteLine("\n===== 表头前 0x14 B 与表头后 0x30 B =====");
    for(long va=start-0x14; va<start+0x30; va+=4){
      long fo=V2O(va); if(fo<0||fo+4>b.Length){ Console.WriteLine("  0x"+va.ToString("X")+"  (未映射)"); continue; }
      uint d=U32(b,fo);
      string s=REL.ContainsKey(va)?("RELOC type="+REL[va]):("raw 0x"+d.ToString("X8"));
      Console.WriteLine("  0x"+va.ToString("X")+(va==start?" <<< 表头":(va==anchor?" <<< setDefWidgetMode":""))+"   "+s+(IsSlot(b,va)?"  [slot]":"  [gap]"));
    }
    Console.WriteLine("\n===== 前后各 6 个空槽位置（用于判断表边界是否可靠）=====");
    for(long va=start-0x30; va<start; va+=4) Console.WriteLine("   表头前 0x"+va.ToString("X")+"  "+(IsSlot(b,va)?"slot":"gap"));
    for(long va=end+4; va<=end+0x18; va+=4) Console.WriteLine("   表尾后 0x"+va.ToString("X")+"  "+(IsSlot(b,va)?"slot":"gap"));
  }
}
