using System; using System.IO; using System.Text; using System.Collections.Generic; using System.Text.RegularExpressions;
class Variants {
  static uint U32(byte[] b,long o){ return BitConverter.ToUInt32(b,(int)o); }
  static ushort U16(byte[] b,long o){ return BitConverter.ToUInt16(b,(int)o); }
  static string S(byte[] b,long o){ var sb=new StringBuilder(); while(o<b.Length&&b[o]!=0){ sb.Append((char)b[o]); o++; } return sb.ToString(); }
  static List<string> Syms(string p){
    var res=new List<string>(); if(!File.Exists(p)) return res;
    byte[] b=File.ReadAllBytes(p);
    if(b.Length<64||U32(b,0)!=0x464C457F) return res;
    long shoff=U32(b,32); int shent=U16(b,46),shnum=U16(b,48);
    for(int i=0;i<shnum;i++){
      long o=shoff+(long)i*shent; uint type=U32(b,o+4);
      if(type!=11&&type!=2) continue;
      long symOff=U32(b,o+16); long symSize=U32(b,o+20); int ent=(int)U32(b,o+36);
      long lk=U32(b,o+24); long strOff=U32(b,shoff+lk*shent+16);
      int n=ent>0?(int)(symSize/ent):0;
      for(int k=0;k<n;k++){ long so=symOff+(long)k*ent; string nm=S(b,strOff+U32(b,so)); if(nm.Length>0) res.Add(nm); }
    }
    return res;
  }
  static void Main(string[] a){
    foreach(string p in a){
      var s=Syms(p);
      Console.WriteLine("########## "+Path.GetFileName(p)+"  符号 "+s.Count);
      int cnv=0; foreach(string nm in s) if(nm.StartsWith("cmnViewSetting")) cnv++;
      Console.WriteLine("  cmnViewSetting* = "+cnv);
      var v=new SortedDictionary<string,int>();
      foreach(string nm in s){
        foreach(Match m in Regex.Matches(nm,"Type([A-Za-z0-9]{2})$"))
          { string k=m.Groups[1].Value; if(!v.ContainsKey(k)) v[k]=0; v[k]++; }
      }
      Console.WriteLine("  结尾 Type<XX> 的变体代号（按出现次数）:");
      foreach(var kv in v) Console.WriteLine("      Type"+kv.Key.PadRight(6)+kv.Value);
      // 看几个具体的 Touch 常量
      Console.WriteLine("  s*Touch* 常量样本:");
      int c=0;
      foreach(string nm in s) if(nm.StartsWith("s") && nm.IndexOf("Touch",StringComparison.Ordinal)>=0 && c<25){ Console.WriteLine("      "+nm); c++; }
    }
  }
}
