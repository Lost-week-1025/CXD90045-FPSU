using System; using System.IO; using System.Text; using System.Collections.Generic;
class GenPtbl {
  static void Main(string[] a){
    string conf=a[0], outBin=a[1];
    var entries=new List<uint[]>();
    foreach(string raw in File.ReadAllLines(conf)){
      string t=raw.Trim();
      if(t.Length==0||t[0]!='0') continue;
      var f=t.Split(',');
      if(f.Length<4) continue;
      try{ entries.Add(new uint[]{ Convert.ToUInt32(f[0].Substring(2),16), Convert.ToUInt32(f[1].Substring(2),16),
                                  Convert.ToUInt32(f[2].Substring(2),16), Convert.ToUInt32(f[3].Substring(2),16) }); }
      catch{ }
    }
    Console.WriteLine("解析条目 = "+entries.Count);
    byte[] b=new byte[0x1000];
    uint magic=0x04100548, version=1;
    uint n=(uint)entries.Count, partMax=30, szBytes=0x1000, inSector=0, inPart=0;
    Action<int,uint> W=(o,v)=>{ b[o]=(byte)v; b[o+1]=(byte)(v>>8); b[o+2]=(byte)(v>>16); b[o+3]=(byte)(v>>24); };
    // 头部
    W(0x000,magic); W(0x004,version); W(0x008,n);
    // 条目
    for(int i=0;i<entries.Count;i++){
      int o=0x20+i*16; var e=entries[i];
      W(o+0,e[0]); W(o+4,e[1]); W(o+8,e[2]); W(o+12,e[3]);
    }
    // 副本头 (+0x200) —— 校验要求前 3 个 u32 与 +0 处“相等/小于等于”
    W(0x200,magic); W(0x204,version); W(0x208,partMax);
    W(0x20C,szBytes); W(0x210,inSector); W(0x214,inPart);
    File.WriteAllBytes(outBin,b);
    // 自检：复刻 tbl 校验规则
    Func<int,uint> R=o=>(uint)(b[o]|(b[o+1]<<8)|(b[o+2]<<16)|((uint)b[o+3]<<24));
    bool c1=R(0)==R(0x200), c2=R(4)==R(0x204), c3=R(8)<=R(0x208);
    Console.WriteLine("自检: [0]==[0x200] -> "+c1+"   [4]==[0x204] -> "+c2+"   [8]<=[0x208] -> "+c3+"   => "+(c1&&c2&&c3?"通过":"失败"));
    Console.WriteLine("输出 "+outBin+"  "+b.Length+" 字节");
    Console.WriteLine("");
    Console.WriteLine("--- 头部 0x00..0x3F ---");
    for(int o=0;o<0x40;o+=16) Console.WriteLine("  +"+o.ToString("X3")+": "+BitConverter.ToString(b,o,16).Replace("-"," "));
    Console.WriteLine("--- 尾部/副本头 0x1F0..0x21F ---");
    for(int o=0x1F0;o<0x220;o+=16) Console.WriteLine("  +"+o.ToString("X3")+": "+BitConverter.ToString(b,o,16).Replace("-"," "));
    Console.WriteLine("");
    Console.WriteLine("--- 条目（解析回来的前 6 条）---");
    for(int i=0;i<6;i++){ int o=0x20+i*16;
      Console.WriteLine("  ["+i+"] start=0x"+R(o).ToString("X8")+" size=0x"+R(o+4).ToString("X8")+" type="+R(o+8)+" flag=0x"+R(o+12).ToString("X8")); }
  }
}
