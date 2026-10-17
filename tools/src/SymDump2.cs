using System; using System.IO; using System.Text; using System.Collections.Generic;
class SymDump2 {
  static uint U32(byte[] b,long o){ return BitConverter.ToUInt32(b,(int)o); }
  static ushort U16(byte[] b,long o){ return BitConverter.ToUInt16(b,(int)o); }
  static string S(byte[] b,long o){ var sb=new StringBuilder(); while(o<b.Length&&b[o]!=0){ sb.Append((char)b[o]); o++; } return sb.ToString(); }
  static void Main(string[] a){
    string p=a[0]; byte[] b=File.ReadAllBytes(p);
    long shoff=U32(b,32); int shent=U16(b,46),shnum=U16(b,48);
    long shstr=U32(b,shoff+(long)U16(b,50)*shent+16);
    long tv=0,to=0,ts=0;
    for(int i=0;i<shnum;i++){ long o=shoff+(long)i*shent; string nm=S(b,shstr+U32(b,o));
      if(nm==".text"){ tv=U32(b,o+12); to=U32(b,o+16); ts=U32(b,o+20); } }
    Console.WriteLine("## "+Path.GetFileName(p)+"  .text va=0x"+tv.ToString("X")+" off=0x"+to.ToString("X"));
    var want=new List<string>(); for(int i=1;i<a.Length;i++) want.Add(a[i]);
    var got=new Dictionary<string,List<long[]>>(); foreach(string w in want) got[w]=new List<long[]>();
    for(int i=0;i<shnum;i++){ long o=shoff+(long)i*shent; uint type=U32(b,o+4);
      if(type!=11&&type!=2) continue;
      long symOff=U32(b,o+16); long symSize=U32(b,o+20); int ent=(int)U32(b,o+36);
      long lk=U32(b,o+24); long strOff=U32(b,shoff+lk*shent+16);
      int n=ent>0?(int)(symSize/ent):0;
      for(int k=0;k<n;k++){ long so=symOff+(long)k*ent; string nm=S(b,strOff+U32(b,so));
        byte info=b[so+12]; if((info&0xF)!=2) continue;
        foreach(string w in want) if(nm==w) got[w].Add(new long[]{U32(b,so+4),U32(b,so+8)});
      } }
    foreach(string w in want){
      Console.WriteLine("\n#### "+w);
      if(got[w].Count==0){ Console.WriteLine("   (无)"); continue; }
      foreach(long[] g in got[w]){
        long thumb=g[0]&1; long va=g[0]& ~1L; long sz=g[1];
        long fo=to+(va-tv);
        Console.WriteLine("   va=0x"+va.ToString("X")+"  Thumb="+(thumb==1)+"  size="+sz);
        long lim=Math.Min(sz+8, 72);
        for(long i=0;i<lim;i+=2){ if(fo+i+2>b.Length) break;
          ushort h=U16(b,fo+i);
          Console.WriteLine("      +"+i.ToString("X2").PadLeft(2)+"  0x"+h.ToString("X4")+"   "+Convert.ToString(h,2).PadLeft(16,'0'));
        }
      }
    }
  }
}
