using System; using System.IO; using System.Text;
class ByteFind {
  static void Main(string[] a){
    byte[] b=File.ReadAllBytes(a[0]);
    // 搜 0x04100548 小端与大端
    byte[][] pats={ new byte[]{0x48,0x05,0x10,0x04}, new byte[]{0x04,0x10,0x05,0x48} };
    string[] names={"LE 0x04100548","BE 0x04100548"};
    for(int k=0;k<pats.Length;k++){
      int n=0;
      for(long i=0;i<=b.Length-4;i++){
        bool ok=true; for(int j=0;j<4;j++) if(b[i+j]!=pats[k][j]){ok=false;break;}
        if(ok){ Console.WriteLine("  ["+names[k]+"] @0x"+i.ToString("X")); if(++n>=8){Console.WriteLine("   ...");break;} }
      }
      if(n==0) Console.WriteLine("  ["+names[k]+"] 未找到");
    }
    // 搜 ASCII 串
    foreach(string s in new[]{"82461.00","nflasha","dnflasha","ptbl"}){
      byte[] p=Encoding.ASCII.GetBytes(s); int n=0;
      for(long i=0;i<=b.Length-p.Length;i++){
        bool ok=true; for(int j=0;j<p.Length;j++) if(b[i+j]!=p[j]){ok=false;break;}
        if(ok){ if(n<6) Console.WriteLine("  [\"" + s + "\"] @0x"+i.ToString("X")); n++; }
      }
      Console.WriteLine("  [\"" + s + "\"] 命中 " + n + " 处");
    }
  }
}
