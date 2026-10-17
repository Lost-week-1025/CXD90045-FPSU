using System; using System.IO; using System.Text; using System.Collections.Generic;
class Reloc {
  static uint U32(byte[] b,long o){ return BitConverter.ToUInt32(b,(int)o); }
  static ushort U16(byte[] b,long o){ return BitConverter.ToUInt16(b,(int)o); }
  static string S(byte[] b,long o){ var sb=new StringBuilder(); while(o<b.Length&&b[o]!=0){ sb.Append((char)b[o]); o++; } return sb.ToString(); }
  static List<long> L=new List<long>();
  static long V2O(long va){ for(int i=0;i<L.Count;i+=3){ if(va>=L[i]&&va<L[i]+L[i+2]) return L[i+1]+(va-L[i]); } return -1; }
  static void Main(string[] a){
    string p=a[0]; byte[] b=File.ReadAllBytes(p);
    long phoff=U32(b,28); int phent=U16(b,42),phnum=U16(b,44);
    for(int i=0;i<phnum;i++){ long o=phoff+(long)i*phent; if(U32(b,o)==1){ L.Add(U32(b,o+8)); L.Add(U32(b,o+4)); L.Add(U32(b,o+16)); } }
    long shoff=U32(b,32); int shent=U16(b,46),shnum=U16(b,48);
    long shstr=U32(b,shoff+(long)U16(b,50)*shent+16);
    // 1) 收集目标符号地址
    var targets=new Dictionary<long,string>();
    for(int i=0;i<shnum;i++){
      long o=shoff+(long)i*shent; uint type=U32(b,o+4);
      if(type!=11&&type!=2) continue;
      long symOff=U32(b,o+16); long symSize=U32(b,o+20); int ent=(int)U32(b,o+36);
      long lk=U32(b,o+24); long strOff=U32(b,shoff+lk*shent+16);
      int n=ent>0?(int)(symSize/ent):0;
      for(int k=0;k<n;k++){ long so=symOff+(long)k*ent; string nm=S(b,strOff+U32(b,so));
        if(nm.IndexOf("TouchOperationType",StringComparison.Ordinal)>=0 || nm.IndexOf("TouchTrackingType",StringComparison.Ordinal)>=0
           || nm.IndexOf("NumOfTouch",StringComparison.Ordinal)>=0 || nm.IndexOf("TouchpadEnabledAreaType",StringComparison.Ordinal)>=0
           || nm.IndexOf("TouchPadSettingsType",StringComparison.Ordinal)>=0)
          targets[U32(b,so+4)]=nm+"  (sz="+U32(b,so+8)+")";
      }
    }
    Console.WriteLine("目标符号 "+targets.Count+" 个");
    // 2) 遍历重定位节
    var rels=new Dictionary<long,long>();   // offset -> addend/target
    for(int i=0;i<shnum;i++){
      long o=shoff+(long)i*shent; uint type=U32(b,o+4);
      if(type!=9 && type!=4) continue;      // SHT_REL=9, SHT_RELA=4
      string sname=S(b,shstr+U32(b,o)); long off=U32(b,o+16); long size=U32(b,o+20); int ent=(int)U32(b,o+36);
      if(ent<=0) continue;
      int n=(int)(size/ent); bool rela=(type==4);
      for(int k=0;k<n;k++){ long ro=off+(long)k*ent;
        long r_offset=U32(b,ro);
        long add=rela?U32(b,ro+8):0;
        uint r_info=U32(b,ro+4); uint rtype=r_info&0xFF;
        if(targets.ContainsKey(r_offset)) rels[r_offset]=add;
      }
    }
    // 3) 输出：符号 -> 重定位指向的地址 -> 该地址的内容
    foreach(var kv in targets){
      long va=kv.Key; string nm=kv.Value;
      if(!rels.ContainsKey(va)){ Console.WriteLine("  "+nm.PadRight(58)+" va=0x"+va.ToString("X6")+"  (无重定位)"); continue; }
      long tgt=rels[va]; long fo=V2O(tgt);
      var sb=new StringBuilder();
      if(fo>=0){ for(int j=0;j<32 && fo+j<b.Length;j++) sb.Append(b[fo+j].ToString("X2")+" "); }
      Console.WriteLine("  "+nm.PadRight(58)+" va=0x"+va.ToString("X6")+" -> 0x"+tgt.ToString("X6")+"  "+(fo<0?"(未映射)":sb.ToString()));
    }
  }
}
