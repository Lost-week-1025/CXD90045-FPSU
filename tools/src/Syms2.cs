using System; using System.IO; using System.Text; using System.Collections.Generic;
class Syms2 {
  static uint U32(byte[] b,long o){ return BitConverter.ToUInt32(b,(int)o); }
  static ushort U16(byte[] b,long o){ return BitConverter.ToUInt16(b,(int)o); }
  static string S(byte[] b,long o){ var sb=new StringBuilder(); while(o<b.Length&&b[o]!=0){ sb.Append((char)b[o]); o++; } return sb.ToString(); }
  static List<string> Get(string p){
    var res=new List<string>(); if(!File.Exists(p)) return res;
    byte[] b=File.ReadAllBytes(p);
    if(b.Length<52 || U32(b,0)!=0x464C457F) return res;
    long shoff=U32(b,32); int shent=U16(b,46), shnum=U16(b,48);
    long shstrOff=shoff+(long)U16(b,50)*shent; shstrOff=U32(b,shstrOff+16);
    long symOff=-1,strOff=-1; long symSize=0; int symEnt=0;
    for(int i=0;i<shnum;i++){
      long o=shoff+(long)i*shent;
      uint type=U32(b,o+4);
      if(type!=11 && type!=2) continue;               // 11=SHT_DYNSYM, 2=SHT_SYMTAB
      string nm=S(b, shstrOff+U32(b,o));
      if(nm==".dynsym" || (nm==".symtab" && symOff<0)){
        symOff=U32(b,o+16); symSize=U32(b,o+20); symEnt=(int)U32(b,o+36);
        long lk=U32(b,o+24); long lo=shoff+lk*shent; strOff=U32(b,lo+16);
      }
    }
    if(symOff<0||symEnt==0) return res;
    int n=(int)(symSize/symEnt);
    for(int i=0;i<n;i++){
      long o=symOff+(long)i*symEnt;
      byte info=b[o+12]; if((info&0xF)==0) continue;
      string nm=S(b,strOff+U32(b,o));
      int st=(info>>4)&0xF; if(st==0) continue;
      if(nm.Length>0 && !res.Contains(nm)) res.Add(nm);
    }
    return res;
  }
  static void Main(string[] a){
    var A=Get(a[0]); var B=Get(a[1]);
    var sa=new HashSet<string>(A); var sb=new HashSet<string>(B);
    int both=0,oa=0,ob=0;
    foreach(string t in A){ if(sb.Contains(t)) both++; else oa++; }
    foreach(string t in B){ if(!sa.Contains(t)) ob++; }
    Console.WriteLine(Path.GetFileName(a[0]).PadRight(30)+" 导出"+A.Count.ToString().PadLeft(6)+"  |  "+Path.GetFileName(a[1]).PadRight(12)+" 导出"+B.Count.ToString().PadLeft(6)+
      "  |  共有"+both.ToString().PadLeft(6)+"  仅1F"+oa.ToString().PadLeft(5)+"  仅E10"+ob.ToString().PadLeft(5)+
      "  |  重合 "+(Math.Max(A.Count,B.Count)==0?0:Math.Round(100.0*both/Math.Max(A.Count,B.Count),1))+"%");
  }
}
