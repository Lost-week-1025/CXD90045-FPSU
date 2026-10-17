using System; using System.IO; using System.Text; using System.Collections.Generic;
class Histo2 {
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
    // 1) 统计各符号表里 setDefWidgetMode 出现次数 + 各重定位节引用次数
    Console.WriteLine("===== 符号/重定位计数核对 =====");
    for(int i=0;i<shnum;i++){ long o=shoff+(long)i*shent; uint type=U32(b,o+4); string sn=S(b,shstr+U32(b,o));
      if(type!=11&&type!=2&&type!=9&&type!=4) continue;
      if(type==11||type==2){ int n=0; long symOff=U32(b,o+16); long symSize=U32(b,o+20); int ent=(int)U32(b,o+36);
        long lk=U32(b,o+24); long strOff=U32(b,shoff+lk*shent+16);
        int nn=ent>0?(int)(symSize/ent):0;
        for(int k=0;k<nn;k++){ long so=symOff+(long)k*ent; string nm=S(b,strOff+U32(b,so));
          if(nm.Contains("setDefWidgetMode")) n++; }
        if(n>0) Console.WriteLine("   符号表 "+sn+"("+type+")  含 setDefWidgetMode: "+n);
      } else {
        // 重定位节：用自己 link 的符号表
        long lk=U32(b,o+24); long sh2=shoff+lk*shent; long symOff=U32(b,sh2+16); long symSize=U32(b,sh2+20);
        int ent=(int)U32(b,sh2+36); long sstr=U32(b,shoff+U32(b,sh2+24)*shent+16);
        int nn=ent>0?(int)(symSize/ent):0;
        var nm2=new Dictionary<long,string>();
        for(int k=0;k<nn;k++){ long so=symOff+(long)k*ent; nm2[(long)k]=S(b,sstr+U32(b,so)); }
        long off=U32(b,o+16); long size=U32(b,o+20);
        int cnt=0; var slots=new List<long>();
        int m=(ent>0)?(int)(size/ent):0;
        for(int k=0;k<m;k++){ long ro=off+(long)k*ent; long si=(long)(U32(b,ro+4)>>8);
          if(nm2.ContainsKey(si)&&nm2[si].Contains("setDefWidgetMode")){ cnt++; slots.Add(U32(b,ro)); } }
        if(cnt>0) Console.WriteLine("   重定位节 "+sn+"(type "+type+")  引用 setDefWidgetMode: "+cnt+"  (GOT槽: "+string.Join(",",slots.ConvertAll(x=>"0x"+x.ToString("X")))+")");
      }
    }
    // 2) 直方图里 200..900 的目标
    var hist=new Dictionary<long,List<long>>();
    for(long i=0;i+4<=ts;i+=2){
      long fo=to+i, va=tv+i;
      ushort h1=U16(b,fo), h2=U16(b,fo+2);
      if((h1&0xF800)!=0xF000) continue;
      bool isBLX=(h2&0xD000)==0xC000, isBL=(h2&0xD000)==0xD000;
      if(!isBLX&&!isBL) continue;
      int S1=(h1>>10)&1, im10=h1&0x3FF, J1=(h2>>13)&1, J2=(h2>>11)&1, im11=h2&0x7FF;
      int I1=(~(J1^S1))&1, I2=(~(J2^S1))&1;
      long imm=(long)((S1<<24)|(I1<<23)|(I2<<22)|(im10<<12)|(im11<<1));
      if((imm&0x1000000)!=0) imm-=0x2000000;
      long cand; if(isBL) cand=(va+4+imm)& ~1L; else cand=((va+4)& ~3L)+imm & ~1L;
      if(cand<tv||cand>=tv+ts) continue;
      if(!hist.ContainsKey(cand)) hist[cand]=new List<long>();
      hist[cand].Add(va);
    }
    var list=new List<KeyValuePair<long,List<long>>>(hist);
    list.Sort((x,y)=>y.Value.Count.CompareTo(x.Value.Count));
    Console.WriteLine("\n===== 调用次数 200~900 的目标 =====");
    foreach(var kv in list){ int c=kv.Value.Count; if(c<200||c>900) continue;
      Console.WriteLine("   目标 0x"+kv.Key.ToString("X")+"  "+c+" 次");
    }
  }
}
