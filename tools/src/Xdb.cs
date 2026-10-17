using System; using System.IO; using System.Text; using System.Collections.Generic;
class Xdb {
  static List<string> Get(string p){
    byte[] b=File.ReadAllBytes(p); var list=new List<string>(); int s=-1;
    for(int i=0;i<b.Length;i++){ byte c=b[i];
      if(c>=32&&c<127){ if(s<0) s=i; }
      else { if(s>=0 && i-s>=4){ string t=Encoding.ASCII.GetString(b,s,i-s);
        if(t.EndsWith(".uxc")||t.EndsWith(".uxb")||t.EndsWith(".dat")||t.EndsWith(".ltt")||t.EndsWith(".ttf")) if(!list.Contains(t)) list.Add(t); } s=-1; } }
    return list;
  }
  static void Main(string[] a){
    var x=Get(a[0]); var y=Get(a[1]);
    Console.WriteLine("ZV-1F  global.xdb : "+x.Count+" 条");
    Console.WriteLine("ZV-E10 global.xdb : "+y.Count+" 条");
    var sx=new HashSet<string>(x); var sy=new HashSet<string>(y);
    Console.WriteLine("\n########## 只在 ZV-1F 注册 ("+x.FindAll(t=>!sy.Contains(t)).Count+") ##########");
    foreach(string t in x) if(!sy.Contains(t)) Console.WriteLine("  "+t);
    Console.WriteLine("\n########## 只在 ZV-E10 注册 ("+y.FindAll(t=>!sx.Contains(t)).Count+") ##########");
    foreach(string t in y) if(!sx.Contains(t)) Console.WriteLine("  "+t);
    Console.WriteLine("\n########## 两边都注册 ##########");
    foreach(string t in x) if(sy.Contains(t)) Console.WriteLine("  "+t);
  }
}
