using System; using System.IO; using System.Text; using System.Collections.Generic;
class SettingCat {
  static uint U32(byte[] b,long o){ return BitConverter.ToUInt32(b,(int)o); }
  static ushort U16(byte[] b,long o){ return BitConverter.ToUInt16(b,(int)o); }
  static string S(byte[] b,long o){ var sb=new StringBuilder(); while(o<b.Length&&b[o]!=0){ sb.Append((char)b[o]); o++; } return sb.ToString(); }
  static List<string> Syms(string p){
    var res=new List<string>(); if(!File.Exists(p)) return res;
    byte[] b=File.ReadAllBytes(p);
    if(U32(b,0)!=0x464C457F) return res;
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
      var set=new List<string>();
      foreach(string nm in s) if(nm.StartsWith("cmnViewSetting")) set.Add(nm);
      Console.WriteLine("  cmnViewSetting* 共 "+set.Count);
      // 提取设置项名（去掉三个前缀）
      var items=new SortedDictionary<string,int>();
      foreach(string nm in set){
        string t=nm;
        foreach(string pre in new[]{"cmnViewSettingNode","cmnViewSettingPropertyList","cmnViewSettingProperties"})
          if(t.StartsWith(pre)){ t=t.Substring(pre.Length); break; }
        if(!items.ContainsKey(t)) items[t]=0; items[t]++;
      }
      Console.WriteLine("  去重后的菜单设置项 "+items.Count+" 个（只列含 Touch/TP/Panel 的）:");
      foreach(var kv in items){ string l=kv.Key.ToLowerInvariant();
        if(l.Contains("touch")||l.Contains("tp")&&l.Contains("panel")||l.Contains("panel"))
          Console.WriteLine("      "+kv.Key); }
    }
  }
}
