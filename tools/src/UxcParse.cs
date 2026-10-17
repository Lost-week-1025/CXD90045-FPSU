using System; using System.IO; using System.Text; using System.Collections.Generic;
class UxcParse {
  static uint U32(byte[] b,long o){ return BitConverter.ToUInt32(b,(int)o); }
  static ushort U16(byte[] b,long o){ return BitConverter.ToUInt16(b,(int)o); }
  static void Main(string[] a){
    foreach(string p in a){
      if(!File.Exists(p)){ Console.WriteLine("(无) "+p); continue; }
      byte[] b=File.ReadAllBytes(p);
      Console.WriteLine("################ "+Path.GetFileName(p)+"   "+b.Length+" B");
      Console.WriteLine("  头部:");
      Console.WriteLine("    +00 magic  = "+BitConverter.ToString(b,0,4).Replace("-"," ")+"  (\""+Encoding.ASCII.GetString(b,0,3)+"\"+"+b[3]+")");
      Console.WriteLine("    +04 u32    = 0x"+U32(b,4).ToString("X8"));
      Console.WriteLine("    +08 u16    = "+U16(b,8)+"   (0x"+U16(b,8).ToString("X")+")");
      Console.WriteLine("    +0A u16    = "+U16(b,10)+"   (0x"+U16(b,10).ToString("X")+")");
      Console.WriteLine("    +0C u16    = "+U16(b,12)+"   (0x"+U16(b,12).ToString("X")+")");
      Console.WriteLine("    +0E u16    = "+U16(b,14)+"   (0x"+U16(b,14).ToString("X")+")");
      // 递增 u16 偏移表
      var tab=new List<int>(); long o=0x10;
      while(o+2<=b.Length){ int v=U16(b,o);
        if(tab.Count>0 && v<=tab[tab.Count-1] && v!=0) break;
        if(v>0 && v>=b.Length) break;
        tab.Add(v); o+=2;
        if(tab.Count>4000) break;
      }
      Console.WriteLine("  递增偏移表: 起点 0x10, 共 "+tab.Count+" 项");
      var sb=new StringBuilder();
      for(int i=0;i<tab.Count;i++){ sb.Append(tab[i].ToString("X")); if(i<tab.Count-1) sb.Append(","); if((i+1)%16==0){ Console.WriteLine("      "+sb); sb.Clear(); } }
      if(sb.Length>0) Console.WriteLine("      "+sb);
      Console.WriteLine("  表结束于 0x"+(0x10+tab.Count*2).ToString("X"));
      // 每段 dump
      Console.WriteLine("  各段内容 (offset[i] .. offset[i+1])：");
      for(int i=0;i<tab.Count;i++){
        int s=tab[i], e=(i+1<tab.Count)?tab[i+1]:-1;
        if(e<0||e>s+64) e=Math.Min(b.Length, s+64);
        if(s>=b.Length) continue;
        var h=new StringBuilder(); var u=new StringBuilder();
        for(int j=s;j<e && j<b.Length;j++){ h.Append(b[j].ToString("X2")); if(j<e-1) h.Append(" "); }
        for(int j=s;j+1<e && j+1<b.Length;j+=2) { u.Append(U16(b,j)); u.Append(" "); }
        Console.WriteLine("    ["+i.ToString().PadLeft(2)+"] 0x"+s.ToString("X5")+"-0x"+e.ToString("X5")+" ("+(e-s)+"B)");
        Console.WriteLine("         hex: "+h);
        Console.WriteLine("         u16: "+u);
      }
    }
  }
}
