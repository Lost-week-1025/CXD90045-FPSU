using System; using System.IO; using System.Text; using System.Collections.Generic;
class VtSearch {
  static uint U32(byte[] b,long o){ return BitConverter.ToUInt32(b,(int)o); }
  static ushort U16(byte[] b,long o){ return BitConverter.ToUInt16(b,(int)o); }
  static string S(byte[] b,long o){ var sb=new StringBuilder(); while(o<b.Length&&b[o]!=0){ sb.Append((char)b[o]); o++; } return sb.ToString(); }
  static void Main(string[] a){
    foreach(string p in a){
      byte[] b=File.ReadAllBytes(p);
      long shoff=U32(b,32); int shent=U16(b,46),shnum=U16(b,48);
      long shstr=U32(b,shoff+(long)U16(b,50)*shent+16);
      long tv=0,to=0,ts=0;
      for(int i=0;i<shnum;i++){ long o=shoff+(long)i*shent; string nm=S(b,shstr+U32(b,o));
        if(nm==".text"){ tv=U32(b,o+12); to=U32(b,o+16); ts=U32(b,o+20); } }
      Console.WriteLine("################ "+Path.GetFileName(p)+"   .text 0x"+tv.ToString("X")+" size 0x"+ts.ToString("X"));
      // 统计 ldr.w rX,[rY,#imm12] 后 1..4 条内出现 blx rX 的模式，按 imm12 直方图
      var hist=new Dictionary<int,Dictionary<int,List<long>>>();
      for(long i=0;i+8<=ts;i+=2){
        long fo=to+i, va=tv+i;
        ushort h1=U16(b,fo);
        if((h1 & 0xFFF0)!=0xF8D0) continue;      // LDR.W (imm12)
        ushort h2=U16(b,fo+2);
        int rt=(h2>>12)&0xF; int imm=h2 & 0xFFF;
        if(imm<0x40 || imm>0x600) continue;
        // 往后找 blx rt
        bool found=false;
        for(long k=2;k<=8;k+=2){
          ushort hn=U16(b,fo+k);
          if(hn==(ushort)(0x4780|(rt<<3))){ found=true; break; }   // blx rt
          if((hn & 0xF800)==0xF000) { if(k+2<10) k+=2; continue; } // 跳过 32 位指令
        }
        if(!found) continue;
        if(!hist.ContainsKey(imm)) hist[imm]=new Dictionary<int,List<long>>();
        if(!hist[imm].ContainsKey(rt)) hist[imm][rt]=new List<long>();
        hist[imm][rt].Add(va);
      }
      var rows=new List<KeyValuePair<int,int>>();
      foreach(var kv in hist){ int tot=0; foreach(var r in kv.Value) tot+=r.Value.Count; rows.Add(new KeyValuePair<int,int>(kv.Key,tot)); }
      rows.Sort((x,y)=>y.Value.CompareTo(x.Value));
      Console.WriteLine("  imm12 直方图（前 20 名，含 0x2C4 定位）：");
      foreach(var kv in rows.GetRange(0,Math.Min(20,rows.Count))){
        Console.WriteLine("    imm12=0x"+kv.Key.ToString("X4")+"  (槽索引 "+(kv.Key/4)+")   命中 "+kv.Value+" 处"+(kv.Key==0x2C4?"   ★★★ 目标":"")+(kv.Value>=200&&kv.Value<=300?"   <<< 接近 239":""));
      }
      Console.WriteLine("\n  === 0x2C4 的全部命中详情 ===");
      if(hist.ContainsKey(0x2C4)){
        foreach(var r in hist[0x2C4]){
          Console.WriteLine("  Rt=r"+r.Key+"   "+r.Value.Count+" 处");
          foreach(long va in r.Value) Console.WriteLine("      0x"+va.ToString("X"));
        }
      } else Console.WriteLine("  (0x2C4 无命中)");
    }
  }
}
