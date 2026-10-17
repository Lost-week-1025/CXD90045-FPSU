using System; using System.IO; using System.Text; using System.Collections.Generic;
class SymVal2 {
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
      for(int k=0;k<n;k++){
        long so=symOff+(long)k*ent;
        string nm=S(b,strOff+U32(b,so));
        if(nm.Length==0) continue;
        if(nm.IndexOf("Touch",StringComparison.Ordinal)<0) continue;
        long va=U32(b,so+4); uint sz=U32(b,so+8); byte info=b[so+12];
        long fo=V2O(va);
        var sb=new StringBuilder();
        if(fo>=0){ int rd=(int)Math.Min(32,sz>0?sz:32); if(fo+rd<=b.Length) for(int j=0;j<rd;j++) sb.Append(b[fo+j].ToString("X2")+(j%4==3?" ":" ")); }
        rows.Add("  "+(info==2?"FUNC":"obj ")+" "+nm.PadRight(60)+" va=0x"+va.ToString("X6")+" sz="+sz.ToString().PadLeft(5)+"  "+sb);
      }
    }
    Console.WriteLine("含 Touch 的符号共 "+rows.Count+" 个，列出前 60：");
    for(int i=0;i<Math.Min(60,rows.Count);i++) Console.WriteLine(rows[i]);
  }
}
