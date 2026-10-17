using System; using System.IO; using System.Text; using System.Collections.Generic;
class Where {
  static uint U32(byte[] b,long o){ return BitConverter.ToUInt32(b,(int)o); }
  static ushort U16(byte[] b,long o){ return BitConverter.ToUInt16(b,(int)o); }
  static string S(byte[] b,long o){ var sb=new StringBuilder(); while(o<b.Length&&b[o]!=0){ sb.Append((char)b[o]); o++; } return sb.ToString(); }
  static void Main(string[] a){
    string dir=a[0];
    string[] keys={"isTouchToggleSupported","isTouchPanelPadSupported","MenuSelectMode","setTouchable","BarCtrlDial",
                   "GestureAnalyzer","calcLocalFlickSpeed","onMouseEvent","acceptMultiTouch","getGestureAnalyzer",
                   "TouchPanelPad","cmnViewTPOperationUtil","CmnViewTPAreaEnableUtil","TouchPosition"};
    var found=new Dictionary<string,List<string>>();
    foreach(string f in Directory.GetFiles(dir,"*.so")){
      byte[] b; try{ b=File.ReadAllBytes(f); }catch{ continue; }
      if(b.Length<64||U32(b,0)!=0x464C457F) continue;
      long shoff=U32(b,32); int shent=U16(b,46),shnum=U16(b,48);
      long shstrOff=U32(b,shoff+(long)U16(b,50)*shent+16);
      var names=new List<string>();
      for(int i=0;i<shnum;i++){
        long o=shoff+(long)i*shent; uint type=U32(b,o+4);
        if(type!=11&&type!=2) continue;
        long symOff=U32(b,o+16); long symSize=U32(b,o+20); int ent=(int)U32(b,o+36);
        long lk=U32(b,o+24); long strOff=U32(b,shoff+lk*shent+16);
        int n=ent>0?(int)(symSize/ent):0;
        for(int k=0;k<n;k++){ long so=symOff+(long)k*ent; string nm=S(b,strOff+U32(b,so)); if(nm.Length>0) names.Add(nm); }
      }
      foreach(string nm in names) foreach(string k in keys)
        if(nm.IndexOf(k,StringComparison.Ordinal)>=0){
          if(!found.ContainsKey(k)) found[k]=new List<string>();
          string e=Path.GetFileName(f)+" :: "+nm;
          if(!found[k].Contains(e)) found[k].Add(e);
          break;
        }
    }
    foreach(string k in keys){
      Console.WriteLine("### "+k);
      if(!found.ContainsKey(k)){ Console.WriteLine("    (无)"); continue; }
      var byLib=new Dictionary<string,int>();
      foreach(string e in found[k]){ string lib=e.Split(new[]{' '},2)[0]; if(!byLib.ContainsKey(lib)) byLib[lib]=0; byLib[lib]++; }
      foreach(var kv in byLib) Console.WriteLine("    "+kv.Key.PadRight(24)+kv.Value+" 个符号");
      foreach(string e in found[k]) if(!e.StartsWith("viewUnified")&&!e.StartsWith("libObj")&&!e.StartsWith("appFw")) Console.WriteLine("      "+e);
    }
  }
}
