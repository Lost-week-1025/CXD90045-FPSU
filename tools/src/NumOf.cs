using System; using System.IO; using System.Text; using System.Collections.Generic;
class NumOf {
  static uint U32(byte[] b,long o){ return BitConverter.ToUInt32(b,(int)o); }
  static ushort U16(byte[] b,long o){ return BitConverter.ToUInt16(b,(int)o); }
  static string S(byte[] b,long o){ var sb=new StringBuilder(); while(o<b.Length&&b[o]!=0){ sb.Append((char)b[o]); o++; } return sb.ToString(); }
  static List<long> L=new List<long>();
  static long V2O(long va){ for(int i=0;i<L.Count;i+=3){ if(va>=L[i]&&va<L[i]+L[i+2]) return L[i+1]+(va-L[i]); } return -1; }
  static void Main(string[] a){
    string p=a[0]; byte[] b=File.ReadAllBytes(p);
    long phoff=U32(b,28); int phent=U16(b,42),phnum=U16(b,44);
    for(int i=0;i<phnum;i++){ long o=phoff+(long)i*phent; if(U32(b,o)==1){ L.Add(U32(b,o+8)); L.Add(U32(b,o+4)); L.Add(U32(b,o+16)); } }
    long shoff=U32(b,32); int shent=U16(b,46),shnum=U16(b,48);
    var rows=new List<string>();
    for(int i=0;i<shnum;i++){
      long o=shoff+(long)i*shent; uint type=U32(b,o+4);
      if(type!=11&&type!=2) continue;
      long symOff=U32(b,o+16); long symSize=U32(b,o+20); int ent=(int)U32(b,o+36);
      long lk=U32(b,o+24); long strOff=U32(b,shoff+lk*shent+16);
      int n=ent>0?(int)(symSize/ent):0;
      for(int k=0;k<n;k++){ long so=symOff+(long)k*ent; string nm=S(b,strOff+U32(b,so));
        if(nm.Length==0||nm.IndexOf("NumOfTouch",StringComparison.Ordinal)<0) continue;
        long va=U32(b,so+4); uint sz=U32(b,so+8); long fo=V2O(va);
        long val=-1;
        if(fo>=0 && fo+4<=b.Length) val=U32(b,fo);
        rows.Add("  "+nm.PadRight(58)+" = "+val.ToString().PadLeft(4)+"   (va=0x"+va.ToString("X6")+", sz="+sz+")");
      }
    }
    rows.Sort();
    Console.WriteLine("NumOfTouch* 常量共 "+rows.Count+" 个：");
    foreach(string r in rows) Console.WriteLine(r);
  }
}
