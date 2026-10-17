using System; using System.IO; using System.Text; using System.Collections.Generic;
class RealDef {
  static uint U32(byte[] b,long o){ return BitConverter.ToUInt32(b,(int)o); }
  static ushort U16(byte[] b,long o){ return BitConverter.ToUInt16(b,(int)o); }
  static string S(byte[] b,long o){ var sb=new StringBuilder(); while(o<b.Length&&b[o]!=0){ sb.Append((char)b[o]); o++; } return sb.ToString(); }
  static void Main(string[] a){
    string dir=a[0];
    string[] keys={"LayoutMode","LayoutToggle","LayoutableWidgetBase","setMode","AbstractLayouterWrapper","LayouterWrapper"};
    foreach(string f in Directory.GetFiles(dir,"*.so")){
      byte[] b; try{ b=File.ReadAllBytes(f); }catch{ continue; }
      if(b.Length<64||U32(b,0)!=0x464C457F) continue;
      long shoff=U32(b,32); int shent=U16(b,46),shnum=U16(b,48);
      var rows=new List<string>();
      for(int i=0;i<shnum;i++){ long o=shoff+(long)i*shent; uint type=U32(b,o+4);
        if(type!=11&&type!=2) continue;
        long symOff=U32(b,o+16); long symSize=U32(b,o+20); int ent=(int)U32(b,o+36);
        long lk=U32(b,o+24); long strOff=U32(b,shoff+lk*shent+16);
        int n=ent>0?(int)(symSize/ent):0;
        for(int k=0;k<n;k++){ long so=symOff+(long)k*ent;
          string nm=S(b,strOff+U32(b,so)); if(nm.Length==0) continue;
          if(nm.Contains("_ZTI")||nm.Contains("_ZTV")||nm.Contains("_ZTS")) continue;
          bool hit=false; foreach(string key in keys) if(nm.Contains(key)){ hit=true; break; }
          if(!hit) continue;
          byte info=b[so+12]; if((info&0xF)==0) continue;      // 跳过未定义
          if((info&0xF)!=2) continue;                           // 只要 FUNC
          uint sz=U32(b,so+8); if(sz<=12) continue;             // 跳过 12 字节的 PLT 桩
          rows.Add("    "+nm+"\n        va=0x"+U32(b,so+4).ToString("X")+" size="+sz);
        } }
      if(rows.Count>0){ Console.WriteLine("#### "+Path.GetFileName(f)+"  ("+rows.Count+" 个真实现)"); foreach(string r in rows) Console.WriteLine(r); }
    }
  }
}
