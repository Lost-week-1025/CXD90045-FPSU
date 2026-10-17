using System; using System.IO; using System.Text; using System.Collections.Generic; using System.Security.Cryptography;
class VerifySums {
  // ★ 开源版：密钥不随仓库发布，由 Key() 从环境变量/keys.txt 读取
  static byte[] KEY_AES = Key("SOY_KEY_AES");
  static byte[] K0 = Key("SOY_KEY_MODEL");
  // ─────────────────────────────────────────────────────────────────────
  // ★ 密钥已从开源版本中移除。本仓库不发布任何可直接解密的密钥。
  //   运行前必须自行提供（按顺序尝试）：
  //     1) 环境变量  SOY_KEY_AES / SOY_KEY_MODEL
  //     2) 同目录 keys.txt，每行形如  NAME=HEX
  //   未提供时明确报错退出，不静默产出错误结果。
  //   ★ keys.txt 已在 .gitignore 中，切勿提交。
  // ─────────────────────────────────────────────────────────────────────
  static byte[] Key(string name){
    var v = Environment.GetEnvironmentVariable(name);
    if(string.IsNullOrEmpty(v)){
      try{
        var f = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keys.txt");
        if(File.Exists(f))
          foreach(var ln in File.ReadAllLines(f)){
            int i = ln.IndexOf('=');
            if(i>0 && ln.Substring(0,i).Trim().Equals(name, StringComparison.OrdinalIgnoreCase)){
              v = ln.Substring(i+1).Trim(); break;
            }
          }
      }catch{}
    }
    if(string.IsNullOrEmpty(v)) throw new Exception(
      "未提供密钥: " + name + "\n本仓库不发布密钥。请自行从你合法持有的设备/固件中取得后，" +
      "通过环境变量 " + name + " 或程序目录下的 keys.txt 提供。");
    v = v.Replace(" ","").Replace("-","").Replace("\t","");
    if(v.Length % 2 != 0) throw new Exception("密钥 HEX 长度不是偶数字节: " + name);
    var b = new byte[v.Length/2];
    for(int i=0;i<b.Length;i++) b[i] = Convert.ToByte(v.Substring(i*2,2),16);
    return b;
  }
  static StringBuilder L=new StringBuilder(); static void W(string s){ L.AppendLine(s); }
  static byte[] Hex(string s){ var b=new byte[s.Length/2]; for(int i=0;i<b.Length;i++) b[i]=Convert.ToByte(s.Substring(i*2,2),16); return b; }
  static uint[] T=new uint[256];
  static void InitT(){ for(uint i=0;i<256;i++){ uint c=i; for(int k=0;k<8;k++) c=((c&1)!=0)?(c>>1)^0xEDB88320u:c>>1; T[i]=c; } }
  static uint Crc(byte[] d,long o,long len){ uint c=0xFFFFFFFFu; long e=o+len; for(long i=o;i<e;i++) c=(c>>8)^T[(c^d[i])&0xFF]; return ~c; }
  static uint U32(byte[] b,long o){ return (uint)(b[o]|(b[o+1]<<8)|(b[o+2]<<16)|((uint)b[o+3]<<24)); }
  static ulong Oct(byte[] b,long o,int n){ ulong v=0; for(int i=0;i<n;i++){ byte c=b[o+i]; if(c<'0'||c>'7') continue; v=v*8+(ulong)(c-'0'); } return v; }
  static string TStr(byte[] b,long o,int n){ int e=0; while(e<n&&b[o+e]!=0) e++; return Encoding.UTF8.GetString(b,(int)o,e); }
  static string Asc(byte[] d,long o,int n){ var sb=new StringBuilder(); for(int i=0;i<n&&o+i<d.Length;i++){ byte c=d[o+i]; sb.Append(c>=32&&c<127?(char)c:'.'); } return sb.ToString(); }

  static byte[] Deframe(byte[] dat,out long po,out byte[] iv){
    long N=dat.Length; long fo=-1; for(long i=0;i<0x1000&&i+4<N;i++) if(dat[i]=='F'&&dat[i+1]=='D'&&dat[i+2]=='A'&&dat[i+3]=='T'){fo=i;break;}
    po=fo+4; long ct=N-po; int us=(int)(ct-(ct%16));
    iv=new byte[16]; Array.Copy(dat,N-0x110,iv,0,16);
    byte[] p=new byte[us];
    using(var a=Aes.Create()){ a.Mode=CipherMode.ECB; a.Padding=PaddingMode.None; a.Key=KEY_AES;
      byte[] h=a.CreateDecryptor().TransformFinalBlock(dat,(int)po,512); Array.Copy(h,0,p,0,512); }
    using(var a=Aes.Create()){ a.Mode=CipherMode.CBC; a.Padding=PaddingMode.None; a.Key=K0; a.IV=iv;
      var d=a.CreateDecryptor(); const int CH=1<<20; byte[] ti=new byte[CH],to=new byte[CH];
      long pos=512; while(pos<us){ int w=(int)Math.Min((long)CH,(long)us-pos); Array.Copy(dat,po+pos,ti,0,w);
        int n=d.TransformBlock(ti,0,w,to,0); Array.Copy(to,0,p,pos,n); pos+=n; } d.TransformFinalBlock(new byte[0],0,0); }
    // 解帧
    var outb=new MemoryStream();
    long f=0;
    while(f*1024+4<=us){
      long fs=f*1024; int se=p[fs+2]|(p[fs+3]<<8); int sz=se&0x7FFF;
      if(sz<=0||sz>1020) break;
      outb.Write(p,(int)fs+4,sz); f++;
    }
    return outb.ToArray();
  }
  static void Main(string[] a){
    InitT();
    string dat=a[0], log=a[1];
    byte[] D=File.ReadAllBytes(dat);
    long po; byte[] iv;
    byte[] img=Deframe(D,out po,out iv);
    W("DAT: "+dat); W("解帧后镜像: "+img.Length+" 字节");
    long t0=U32(img,0x30); long tsz=U32(img,0x34);
    W("tar @0x"+t0.ToString("X")+"  大小="+tsz);
    // 遍历 tar 建立 name -> (dataOffset,size)
    var map=new Dictionary<string,long[]>();
    var sumFiles=new List<string>();
    long p=t0;
    while(p+512<=img.Length){
      bool zero=true; for(int i=0;i<512;i++) if(img[p+i]!=0){zero=false;break;}
      if(zero) break;
      string name=TStr(img,p,100); if(name.Length==0) break;
      char type=(char)img[p+156]; ulong sz=Oct(img,p+124,12);
      if(name.EndsWith(".sum")) sumFiles.Add(name);
      map[name]=new long[]{p+512,(long)sz};
      p = p + 512 + (long)((sz+511)/512*512);
    }
    W("tar 条目数: "+map.Count+"   其中 .sum 文件: "+sumFiles.Count);
    W("");
    // 先打印 part_image.sum 全文
    if(map.ContainsKey("0701_part_image_sum/part_image.sum")){
      long[] e=map["0701_part_image_sum/part_image.sum"];
      W("===== part_image.sum 全文 ("+e[1]+" 字节) =====");
      W(Asc(img,e[0],(int)e[1]));
      W("");
    }
    W("===== 逐段校验 =====");
    long totalEnt=0,totalOk=0,totalBad=0,totalMissing=0;
    foreach(string sf in sumFiles){
      // NN01_X_sum -> NN00_X
      string[] parts=sf.Split('/');
      if(parts.Length<2) continue;
      string d1=parts[0];
      if(d1.Length<5||!d1.EndsWith("_sum")) continue;
      int lastDigit=d1[3]-'0';
      if(lastDigit<1) continue;
      string root=d1.Substring(0,3)+(char)('0'+lastDigit-1)+d1.Substring(4);
      if(root.EndsWith("_sum")) root=root.Substring(0,root.Length-4);
      long[] se=map[sf];
      byte[] txt=new byte[se[1]]; Array.Copy(img,se[0],txt,0,(int)se[1]);
      string body=Encoding.UTF8.GetString(txt);
      string[] lines=body.Split('\n');
      int ok=0,bad=0,miss=0;
      var badNames=new List<string>();
      foreach(string ln in lines){
        string line=ln.Trim(); if(line.Length==0) continue;
        string[] f=line.Split(',');
        if(f.Length<11) continue;
        string type=f[0].Trim(), crcS=f[1].Trim(), sizeS=f[2].Trim(), name=f[10].Trim();
        if(name.Length==0) continue;
        totalEnt++;
        if(type!="0"){ ok++; continue; }
        string full=root+"/"+name;
        if(!map.ContainsKey(full)){ miss++; totalMissing++; W("  [缺] "+sf+" -> "+full); continue; }
        long[] fe=map[full];
        uint want=Convert.ToUInt32(crcS,16);
        uint got=Crc(img,fe[0],fe[1]);
        long wantSz=Convert.ToInt64(sizeS,16);
        bool szOk=(wantSz==fe[1]);
        if(want==got&&szOk){ ok++; totalOk++; }
        else { bad++; totalBad++; if(badNames.Count<12) badNames.Add(name+" 期望"+crcS+" 实得"+got.ToString("x8")+(szOk?"":" 大小不符")); }
      }
      W("  "+sf.PadRight(48)+" 条目="+(ok+bad+miss)+"  通过="+ok+"  不符="+bad+"  缺="+miss);
      foreach(string s in badNames) W("      [!] "+s);
    }
    W("");
    W("总计: 条目="+totalEnt+"  通过="+totalOk+"  不符="+totalBad+"  缺="+totalMissing);
    File.WriteAllText(log,L.ToString(),new UTF8Encoding(false)); Console.WriteLine("ok");
  }
}
