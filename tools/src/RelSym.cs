using System; using System.IO; using System.Text; using System.Collections.Generic;
using System.Text.RegularExpressions;
class RelSym {
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
    var byVa=new Dictionary<long,string>(); var symVal=new List<long>();
    var hdrs=new List<long[]>();
    for(int i=0;i<shnum;i++){ long o=shoff+(long)i*shent; hdrs.Add(new long[]{o,U32(b,o+4),U32(b,o+16),U32(b,o+20),U32(b,o+24),U32(b,o+36)}); }
    long shstr=U32(b,shoff+(long)U16(b,50)*shent+16);
    foreach(long[] h in hdrs){
      if(h[1]!=11&&h[1]!=2) continue;
      long symOff=h[2]; long symSize=h[3]; int ent=(int)h[5];
      if(ent<=0) continue;
      long strOff=U32(b,shoff+h[4]*shent+16);
      int n=(int)(symSize/ent);
      for(int k=0;k<n;k++){ long so=symOff+(long)k*ent; long va=U32(b,so); string nm=S(b,strOff+U32(b,so));
        symVal.Add(va); if(nm.Length>0&&!byVa.ContainsKey(va)) byVa[va]=nm; }
    }
    var want=new[]{"NodesTouchOperationTypeRK","NodesTouchOperationTypeYF","NodesTouchOperationType01","NodesTouchOperationType02",
                   "NodesTouchTrackingTypeRK","NodesTouchOperationRangeDefault","NodesTouchOperationDefault","NodesTouchPadSettingsRoot"};
    var wantVa=new List<long[]>();
    foreach(long[] h in hdrs){
      if(h[1]!=11&&h[1]!=2) continue;
      long symOff=h[2]; long symSize=h[3]; int ent=(int)h[5]; if(ent<=0) continue;
      long strOff=U32(b,shoff+h[4]*shent+16);
      int n=(int)(symSize/ent);
      for(int k=0;k<n;k++){ long so=symOff+(long)k*ent; string nm=S(b,strOff+U32(b,so));
        foreach(string w in want) if(nm.EndsWith(w,StringComparison.Ordinal)) wantVa.Add(new long[]{U32(b,so+4),U32(b,so+8),w.Length}); 
      }
    }
    // 去重 + 带名字
    var wv=new Dictionary<long,string>();
    foreach(long[] h in hdrs){
      if(h[1]!=11&&h[1]!=2) continue;
      long symOff=h[2]; long symSize=h[3]; int ent=(int)h[5]; if(ent<=0) continue;
      long strOff=U32(b,shoff+h[4]*shent+16);
      int n=(int)(symSize/ent);
      for(int k=0;k<n;k++){ long so=symOff+(long)k*ent; string nm=S(b,strOff+U32(b,so));
        foreach(string w in want) if(nm.EndsWith(w,StringComparison.Ordinal)){ long va=U32(b,so+4); wv[va]="sz="+U32(b,so+8)+"  "+nm; } }
    }
    var rels=new Dictionary<long,long>();
    foreach(long[] h in hdrs){
      if(h[1]!=9 && h[1]!=4) continue;
      long off=h[2]; long size=h[3]; int ent=(int)h[5]; if(ent<=0) continue;
      int n=(int)(size/ent);
      for(int k=0;k<n;k++){ long ro=off+(long)k*ent; rels[U32(b,ro)]=(long)(U32(b,ro+4)>>8); }
    }
    Console.WriteLine("解析 "+wv.Count+" 个变体数组（重定位总数 "+rels.Count+"）：");
    foreach(var kv in wv){
      long va=kv.Key; Console.WriteLine("\n### "+kv.Value+"   va=0x"+va.ToString("X6"));
      var m=Regex.Match(kv.Value,"sz=(\\d+)"); long sz=m.Success?long.Parse(m.Groups[1].Value):0;
      int cnt=(int)(sz/4);
      for(int j=0;j<cnt;j++){
        long slot=va+j*4;
        if(rels.ContainsKey(slot)){ long si=rels[slot]; long tgt=(si<symVal.Count)?symVal[(int)si]:-1;
          Console.WriteLine("    ["+j+"] -> "+(byVa.ContainsKey(tgt)?byVa[tgt]:("?sym"+si+" 0x"+tgt.ToString("X6")))); }
        else { long fo=V2O(slot); long v=(fo>=0&&fo+4<=b.Length)?(long)U32(b,fo):-1;
          Console.WriteLine("    ["+j+"] (无重定位) 值=0x"+v.ToString("X")); }
      }
    }
  }
}
