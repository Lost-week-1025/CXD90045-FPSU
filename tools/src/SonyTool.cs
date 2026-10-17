using System; using System.IO; using System.Text; using System.Collections.Generic; using System.Security.Cryptography;
class SonyTool {
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
  static StringBuilder L=new StringBuilder();
  static void W(string s){ L.AppendLine(s); }
  static byte[] Hex(string s){ var b=new byte[s.Length/2]; for(int i=0;i<b.Length;i++) b[i]=Convert.ToByte(s.Substring(i*2,2),16); return b; }
  static string Hx(byte[] d,long o,int n){ var sb=new StringBuilder(); for(int i=0;i<n && o+i<d.Length;i++) sb.Append(d[o+i].ToString("X2")); return sb.ToString(); }

  static uint[] T=new uint[256];
  static void InitT(){ for(uint i=0;i<256;i++){ uint c=i; for(int k=0;k<8;k++) c=((c&1)!=0)?(c>>1)^0xEDB88320u:c>>1; T[i]=c; } }
  static uint Step(uint c,byte b){ return (c>>8)^T[(c^b)&0xFF]; }
  static uint BufCrc(byte[] d,long off,long len){ uint c=0xFFFFFFFFu; long e=off+len; for(long i=off;i<e;i++) c=Step(c,d[i]); return ~c; }
  static uint FileCrc(string p,long len){ uint c=0xFFFFFFFFu; byte[] b=new byte[1<<20];
    using(FileStream fs=File.OpenRead(p)){ long left=len; while(left>0){ int w=(int)Math.Min((long)b.Length,left); int g=fs.Read(b,0,w); if(g<=0)break; for(int i=0;i<g;i++) c=Step(c,b[i]); left-=g; } } return ~c; }
  static uint BEu(byte[] d,long o){ return ((uint)d[o]<<24)|((uint)d[o+1]<<16)|((uint)d[o+2]<<8)|d[o+3]; }
  static void PutBE(byte[] d,long o,uint v){ d[o]=(byte)(v>>24); d[o+1]=(byte)(v>>16); d[o+2]=(byte)(v>>8); d[o+3]=(byte)v; }
  static int FrameSum(byte[] p,long fs){ int s=p[fs+2]|(p[fs+3]<<8); for(int i=0;i<1020;i+=2) s=(s+(p[fs+4+i]|(p[fs+4+i+1]<<8)))&0xFFFF; return s&0xFFFF; }

  static long FindFdat(byte[] all){ for(long i=0;i<0x1000 && i+4<all.Length;i++) if(all[i]=='F'&&all[i+1]=='D'&&all[i+2]=='A'&&all[i+3]=='T') return i; return -1; }

  // 分块解密 FDAT -> plain (len == usable)
  static byte[] DecryptFdat(byte[] all,out long payloadOff,out byte[] iv){
    long N=all.Length; long fdatOff=FindFdat(all); payloadOff=fdatOff+4;
    long ctLen=N-payloadOff; int usable=(int)(ctLen-(ctLen%16));
    iv=new byte[16]; Array.Copy(all,N-0x110,iv,0,16);
    byte[] plain=new byte[usable];
    using(var a=Aes.Create()){ a.Mode=CipherMode.ECB; a.Padding=PaddingMode.None; a.Key=KEY_AES;
      byte[] h=a.CreateDecryptor().TransformFinalBlock(all,(int)payloadOff,512); Array.Copy(h,0,plain,0,512); }
    using(var a=Aes.Create()){ a.Mode=CipherMode.CBC; a.Padding=PaddingMode.None; a.Key=K0; a.IV=iv;
      var dec=a.CreateDecryptor();
      const int CH=1<<20; byte[] tin=new byte[CH]; byte[] tout=new byte[CH];
      long pos=512, end=usable;
      while(pos<end){ int want=(int)Math.Min((long)CH,end-pos); Array.Copy(all,payloadOff+pos,tin,0,want);
        int n=dec.TransformBlock(tin,0,want,tout,0); Array.Copy(tout,0,plain,pos,n); pos+=n; }
      dec.TransformFinalBlock(new byte[0],0,0);
    }
    return plain;
  }

  static void Main(string[] args){
    InitT();
    string cmd=args[0], log=args[args.Length-1];
    try{
      if(cmd=="dump"){
        byte[] all=File.ReadAllBytes(args[1]);
        long po; byte[] iv; byte[] plain=DecryptFdat(all,out po,out iv);
        long off=Convert.ToInt64(args[2],16); int len=Convert.ToInt32(args[3]);
        W("payloadOff=0x"+po.ToString("X")+" plainLen="+plain.Length+" iv="+Hx(iv,0,16));
        W("dump @0x"+off.ToString("X")+" len="+len);
        for(int i=0;i<len;i+=16){ int n=Math.Min(16,len-i); W("  0x"+(off+i).ToString("X8")+"  "+Hx(plain,off+i,n)); }
        if(args.Length>6 && args[4]!="-"){
          long b=Convert.ToInt64(args[4],16); int hdr=Convert.ToInt32(args[5],16);
          W(""); W("记录解析 base=0x"+b.ToString("X")+" hdr=0x"+hdr.ToString("X")+" 8B/rec");
          for(int r=0;r<64;r++){
            long o=b+hdr+r*8; if(o+8>plain.Length||o+8>off+len) break;
            uint le=(uint)(plain[o]|(plain[o+1]<<8)|(plain[o+2]<<16)|((uint)plain[o+3]<<24));
            int id=(plain[o+4]|(plain[o+5]<<8)); int cs=(plain[o+6]|(plain[o+7]<<8));
            W("   #"+r+" @0x"+o.ToString("X")+" bytes="+Hx(plain,o,8)+"  LE=0x"+le.ToString("X8")+"  ID=0x"+id.ToString("X4")+"  c=0x"+cs.ToString("X4"));
          }
        }
      }
      else if(cmd=="find"){
        byte[] all=File.ReadAllBytes(args[1]);
        long po; byte[] iv; byte[] plain=DecryptFdat(all,out po,out iv);
        byte[] pat=Hex(args[2]); W("寻找 "+args[2]+"  payloadOff=0x"+po.ToString("X"));
        int cnt=0;
        for(long i=0;i+pat.Length<=plain.Length;i++){ bool ok=true; for(int k=0;k<pat.Length;k++) if(plain[i+k]!=pat[k]){ok=false;break;}
          if(ok){ W("  @"+i.ToString("X")+" frame="+(i/1024)+" inFrame="+(i%1024)+" ctx="+Hx(plain,i,16)); if(++cnt>80){W("  ...");break;} } }
        W("命中 "+cnt);
      }
      else if(cmd=="verify"){
        byte[] all=File.ReadAllBytes(args[1]); long N=all.Length;
        W("== verify "+args[1]); W("  size="+N+" magic="+Hx(all,0,8));
        W("  marker=["+Encoding.ASCII.GetString(all,(int)(N-8),4)+"]  stored=0x"+BEu(all,N-4).ToString("X8")+"  calc=0x"+FileCrc(args[1],N-12).ToString("X8")+"  CRC_OK="+(BEu(all,N-4)==FileCrc(args[1],N-12)));
        long po; byte[] iv; byte[] plain=DecryptFdat(all,out po,out iv);
        long nf=plain.Length/1024; long bad=0; var badList=new List<long>();
        for(long f=0;f<nf;f++){ long fs=f*1024; if((plain[fs]|(plain[fs+1]<<8))!=FrameSum(plain,fs)){ bad++; if(badList.Count<10) badList.Add(f); } }
        W("  帧数="+nf+" 校验失败="+bad+(bad>0?" 例: "+string.Join(",",badList.ConvertAll(x=>x.ToString()).ToArray()):""));
        W("  FDAT payload=0x"+po.ToString("X")+"  usable="+plain.Length+" 尾部残余="+(N-(po+plain.Length)));
      }
      else if(cmd=="patch"){
        byte[] all=File.ReadAllBytes(args[1]); string dst=args[2];
        byte[] oldP=Hex(args[3]); byte[] newP=Hex(args[4]);
        long po; byte[] iv; byte[] plain=DecryptFdat(all,out po,out iv);
        W("patch: payloadOff=0x"+po.ToString("X")+" plainLen="+plain.Length);
        var frames=new SortedSet<long>(); int n=0;
        for(long i=0;i+oldP.Length<=plain.Length;i++){ bool ok=true; for(int k=0;k<oldP.Length;k++) if(plain[i+k]!=oldP[k]){ok=false;break;}
          if(ok){ for(int k=0;k<newP.Length;k++){ plain[i+k]=newP[k]; frames.Add((i+k)/1024); }
            W("  补丁 @"+i.ToString("X")+" frame="+(i/1024)+" inFrame="+(i%1024)); n++; } }
        W("共 "+n+" 处, 涉及帧 "+frames.Count);
        foreach(long fr in frames){ long fs=fr*1024; int oc=plain[fs]|(plain[fs+1]<<8); int ns=FrameSum(plain,fs);
          plain[fs]=(byte)(ns&0xFF); plain[fs+1]=(byte)((ns>>8)&0xFF);
          W("   帧 "+fr+" 校验 0x"+oc.ToString("X4")+" -> 0x"+ns.ToString("X4")); }
        // 输出: 头 + 重加密(分块) + 尾
        using(FileStream fo=File.Create(dst)){
          fo.Write(all,0,(int)po);
          using(var a=Aes.Create()){ a.Mode=CipherMode.ECB; a.Padding=PaddingMode.None; a.Key=KEY_AES;
            byte[] h=a.CreateEncryptor().TransformFinalBlock(plain,0,512); fo.Write(h,0,512); }
          using(var a=Aes.Create()){ a.Mode=CipherMode.CBC; a.Padding=PaddingMode.None; a.Key=K0; a.IV=iv;
            var enc=a.CreateEncryptor();
            const int CH=1<<20; byte[] tin=new byte[CH]; byte[] tout=new byte[CH+32];
            long pos=512; long end=plain.Length;
            while(pos<end){ int want=(int)Math.Min((long)CH,end-pos); Array.Copy(plain,pos,tin,0,want);
              int m=enc.TransformBlock(tin,0,want,tout,0); fo.Write(tout,0,m); pos+=want; }
            byte[] fin=enc.TransformFinalBlock(new byte[0],0,0); fo.Write(fin,0,fin.Length); }
          long tstart=po+plain.Length;
          if(tstart<all.Length) fo.Write(all,(int)tstart,(int)(all.Length-tstart));
        }
        // 重算 DEND CRC
        byte[] fin2=File.ReadAllBytes(dst); long N=fin2.Length;
        uint ocrc=BEu(fin2,N-4);
        uint ncrc=BufCrc(fin2,0,N-12);
        PutBE(fin2,N-4,ncrc);
        File.WriteAllBytes(dst,fin2);
        W("DEND CRC: 0x"+ocrc.ToString("X8")+" -> 0x"+ncrc.ToString("X8"));
        W("输出 "+dst+" ("+N+" 字节)");
      }
    } catch(Exception ex){ W("EXCEPTION "+ex.GetType().FullName+" : "+ex.Message); W(ex.StackTrace); }
    File.WriteAllText(log,L.ToString(),new UTF8Encoding(false));
    Console.WriteLine("log -> "+log);
  }
}
