using System; using System.IO; using System.Text; using System.Collections.Generic;
class CallFind {
  static uint U32(byte[] b,long o){ return BitConverter.ToUInt32(b,(int)o); }
  static ushort U16(byte[] b,long o){ return BitConverter.ToUInt16(b,(int)o); }
  static string S(byte[] b,long o){ var sb=new StringBuilder(); while(o<b.Length&&b[o]!=0){ sb.Append((char)b[o]); o++; } return sb.ToString(); }
  static void Main(string[] a){
    string dir=a[0];
    string target="_ZN20LayoutableWidgetBase16setDefWidgetModeEN2ux6wgtsys6Widget10WidgetModeE";
    string getter="_ZN20LayoutableWidgetBase16getDefWidgetModeEv";
    Console.WriteLine("===== A) 各库重定位表：谁引用 setDefWidgetMode / getDefWidgetMode =====");
    foreach(string f in Directory.GetFiles(dir,"*.so")){
      byte[] b; try{ b=File.ReadAllBytes(f); }catch{ continue; }
      if(b.Length<64||U32(b,0)!=0x464C457F) continue;
      long shoff=U32(b,32); int shent=U16(b,46),shnum=U16(b,48);
      var names=new Dictionary<long,string>(); var relSecs=new List<long[]>();
      for(int i=0;i<shnum;i++){ long o=shoff+(long)i*shent; uint type=U32(b,o+4);
        if(type==9||type==4) relSecs.Add(new long[]{U32(b,o+16),U32(b,o+20),U32(b,o+36)});
        if(type==11||type==2){ long symOff=U32(b,o+16); long symSize=U32(b,o+20); int ent=(int)U32(b,o+36);
          long lk=U32(b,o+24); long strOff=U32(b,shoff+lk*shent+16);
          int n=ent>0?(int)(symSize/ent):0;
          for(int k=0;k<n;k++){ long so=symOff+(long)k*ent; string nm=S(b,strOff+U32(b,so)); if(nm.Length>0) names[(long)k]=nm; } } }
      int c1=0,c2=0;
      foreach(long[] rs in relSecs){ long off=rs[0],size=rs[1]; int ent=(int)rs[2]; if(ent<=0) continue;
        int n=(int)(size/ent);
        for(int k=0;k<n;k++){ long ro=off+(long)k*ent; long si=(long)(U32(b,ro+4)>>8);
          if(names.ContainsKey(si)){ string nm=names[si]; if(nm==target) c1++; else if(nm==getter) c2++; } } }
      if(c1>0||c2>0) Console.WriteLine("   "+Path.GetFileName(f).PadRight(24)+" setDefWidgetMode×"+c1+"  getDefWidgetMode×"+c2);
    }
    Console.WriteLine("\n===== B) libObj.so 内部直接 bl 到 setDefWidgetMode =====");
    string lp=Path.Combine(dir,"libObj.so"); byte[] lb=File.ReadAllBytes(lp);
    long lshoff=U32(lb,32); int lshent=U16(lb,46),lshnum=U16(lb,48);
    long lshstr=U32(lb,lshoff+(long)U16(lb,50)*lshent+16);
    long tv=0,to=0,ts=0;
    for(int i=0;i<lshnum;i++){ long o=lshoff+(long)i*lshent; string nm=S(lb,lshstr+U32(lb,o));
      if(nm==".text"){ tv=U32(lb,o+12); to=U32(lb,o+16); ts=U32(lb,o+20); } }
    long tgt=0x161CC8;
    var funcs=new SortedDictionary<long,string>();
    for(int i=0;i<lshnum;i++){ long o=lshoff+(long)i*lshent; uint type=U32(lb,o+4);
      if(type!=11&&type!=2) continue;
      long symOff=U32(lb,o+16); long symSize=U32(lb,o+20); int ent=(int)U32(lb,o+36);
      long lk=U32(lb,o+24); long strOff=U32(lb,lshoff+lk*lshent+16);
      int n=ent>0?(int)(symSize/ent):0;
      for(int k=0;k<n;k++){ long so=symOff+(long)k*ent; string nm=S(lb,strOff+U32(lb,so));
        byte info=lb[so+12]; if((info&0xF)!=2) continue; funcs[U32(lb,so+4)& ~1L]=nm; } }
    Console.WriteLine("   libObj 函数符号 "+funcs.Count+" 个，.text "+ts+" B");
    int hits=0;
    for(long i=0;i+4<=ts;i+=2){
      long fo=to+i, va=tv+i;
      ushort h1=U16(lb,fo), h2=U16(lb,fo+2);
      long cand=-1;
      if((h1&0xF800)==0xF000 && (h2&0x8000)!=0){
        int S1=(h1>>10)&1, im10=h1&0x3FF, J1=(h2>>13)&1, J2=(h2>>11)&1, im11=h2&0x7FF;
        int I1=(~(J1^S1))&1, I2=(~(J2^S1))&1;
        long imm=(long)((S1<<24)|(I1<<23)|(I2<<22)|(im10<<12)|(im11<<1));
        if((imm & 0x1000000)!=0) imm -= 0x2000000;
        cand=(va+4+imm) & ~1L;
      }
      if(cand==tgt){ hits++;
        long best=-1; string bn="?";
        foreach(var kv in funcs){ if(kv.Key<=va){ best=kv.Key; bn=kv.Value; } else break; }
        Console.WriteLine("   调用点 0x"+va.ToString("X")+"   所在函数 "+bn+" (0x"+best.ToString("X")+")");
      }
    }
    Console.WriteLine("   >>> 内部直接调用 "+hits+" 处");
  }
}
