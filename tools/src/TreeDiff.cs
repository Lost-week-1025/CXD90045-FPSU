using System; using System.IO; using System.Text; using System.Collections.Generic; using System.Security.Cryptography;
class TreeDiff {
  static string Hash(string p){
    using(var sha=SHA256.Create()) using(var fs=File.OpenRead(p)){
      var h=sha.ComputeHash(fs); var sb=new StringBuilder(); foreach(byte b in h) sb.Append(b.ToString("x2")); return sb.ToString();
    }
  }
  static Dictionary<string,KeyValuePair<string,long>> Scan(string root){
    var d=new Dictionary<string,KeyValuePair<string,long>>(StringComparer.OrdinalIgnoreCase);
    foreach(string f in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)){
      string rel=f.Substring(root.Length).TrimStart('\\','/').Replace('/','\\');
      var fi=new FileInfo(f);
      if(!d.ContainsKey(rel)) d[rel]=new KeyValuePair<string,long>(f,fi.Length);
    }
    return d;
  }
  static void Main(string[] a){
    string ra=a[0], rb=a[1], outF=a[2];
    var A=Scan(ra); var B=Scan(rb);
    var sb=new StringBuilder();
    sb.AppendLine("ZV-1F 树: "+ra+"   文件 "+A.Count);
    sb.AppendLine("ZV-E10树: "+rb+"   文件 "+B.Count);
    sb.AppendLine();
    var onlyA=new List<string>(); var onlyB=new List<string>();
    var same=new List<string>(); var diff=new List<string>();
    foreach(var kv in A){ if(!B.ContainsKey(kv.Key)) onlyA.Add(kv.Key); }
    foreach(var kv in B){ if(!A.ContainsKey(kv.Key)) onlyB.Add(kv.Key); }
    onlyA.Sort(); onlyB.Sort();
    // 同名文件：先比大小，相同再比哈希
    foreach(var kv in A){
      if(!B.ContainsKey(kv.Key)) continue;
      long sa=kv.Value.Value, sbz=B[kv.Key].Value;
      if(sa!=sbz){ diff.Add(kv.Key); continue; }
      string ha=Hash(kv.Value.Key), hb=Hash(B[kv.Key].Key);
      if(ha==hb) same.Add(kv.Key); else diff.Add(kv.Key);
    }
    same.Sort(); diff.Sort();
    sb.AppendLine("===== 汇总 =====");
    sb.AppendLine("  仅 ZV-1F 有      : "+onlyA.Count);
    sb.AppendLine("  仅 ZV-E10 有     : "+onlyB.Count);
    sb.AppendLine("  同名且内容相同   : "+same.Count);
    sb.AppendLine("  同名但内容不同   : "+diff.Count);
    sb.AppendLine();
    Action<string,List<string>,string> dump=(title,list,root)=>{
      sb.AppendLine("===== "+title+" ("+list.Count+") =====");
      foreach(string r in list){
        string full=Path.Combine(root,r);
        long sz=0; try{ sz=new FileInfo(full).Length; }catch{}
        sb.AppendLine("  "+sz.ToString().PadLeft(10)+"  "+r);
      }
      sb.AppendLine();
    };
    dump("仅 ZV-1F 有的文件", onlyA, ra);
    dump("仅 ZV-E10 有的文件", onlyB, rb);
    dump("同名但内容不同", diff, ra);
    // 相同文件只统计目录分布
    sb.AppendLine("===== 同名且内容相同 —— 按目录统计 ("+same.Count+") =====");
    var byd=new SortedDictionary<string,int>();
    foreach(string r in same){ string d=Path.GetDirectoryName(r); if(d==null||d.Length==0) d="<根>"; if(!byd.ContainsKey(d)) byd[d]=0; byd[d]++; }
    foreach(var kv in byd) sb.AppendLine("  "+kv.Value.ToString().PadLeft(6)+"  "+kv.Key);
    File.WriteAllText(outF, sb.ToString(), new UTF8Encoding(false));
    Console.WriteLine("写出 "+outF+"  "+sb.Length+" 字符");
    Console.WriteLine("仅ZV-1F="+onlyA.Count+"  仅ZV-E10="+onlyB.Count+"  相同="+same.Count+"  不同="+diff.Count);
  }
}
