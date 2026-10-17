# 工具说明

全部为**自写工具**，源码在 `src/`。工具只读取文件、做纯离线解析，不联网、不连设备。

## 依赖

| 工具类型 | 依赖 |
|---|---|
| C# 工具 | .NET Framework 4.x 或 .NET 6+（`csc.exe` 亦可直接编译单文件） |
| Ghidra 脚本 | Ghidra 12.x（`analyzeHeadless`） |

## C# 工具（`src/*.cs`）

### 容器与包结构

| 工具 | 用途 |
|---|---|
| `SonyTool.cs` | DAT 容器解析：记录流枚举、终止符定位、CRC-32 重算 |
| `ByteFind.cs` | 在二进制中按模式定位结构（带上下文） |
| `BkChk.cs` | 备份区载荷结构检查 |

### 完整性校验

| 工具 | 用途 |
|---|---|
| `VerifySums.cs` | 逐项核对分区 `.sum` 清单（CRC-32 / 大小 / 路径） |
| `TreeDiff.cs` | 两棵文件树的全量差异（路径 + SHA-256 + 大小） |

### 分区表

| 工具 | 用途 |
|---|---|
| `GenPtbl.cs` | 合成 4 KB 私有分区表，并自校验（双头一致性 + 分区数范围） |

### ELF / ABI 分析

| 工具 | 用途 |
|---|---|
| `Syms2.cs` | 两个 `.so` 的导出符号对比（重合度） |
| `RealDef.cs` | **按 `size > 12` 找函数真实现**（排除 PLT 桩）—— 见下方"教训" |
| `SymDump2.cs` | 按符号名精确 dump 反汇编 |
| `SymVal2.cs` / `NumOf.cs` | 读取符号地址处的数据与常量值 |
| `RelSym.cs` | 解析重定位 → 变体数组 → 实际选项名 |
| `Reloc.cs` / `VtBase.cs` / `VtBase2.cs` | 重定位解析与 vtable 基址回溯 |
| `VtSearch.cs` | 按 vtable 槽偏移搜虚调用点（含 imm12 直方图自校验） |
| `Histo2.cs` | Thumb BL/BLX 调用直方图 |
| `StructDump.cs` | 重定位邻域 dump（判定 vtable vs 数据表） |

### 配置与资源

| 工具 | 用途 |
|---|---|
| `Where.cs` | 定位符号在哪个库里 |
| `Variants.cs` | 提取机型变体代号分布（`TypeRK` / `TypeYF` …） |
| `SettingCat.cs` | 提取菜单设置项目录（`cmnViewSetting*`） |
| `DefWhere.cs` | 区分"定义"与"导入"符号 |
| `CallFind.cs` / `Callers.cs` | 调用点扫描与所在函数定位 |
| `Xdb.cs` | 两份资源注册表（`.xdb`）对比 |
| `Uxc.cs` / `Uxc2.cs` / `UxcParse.cs` | `.uxc` 头部与结构解析 |

## Ghidra 脚本（`src/ghidra/*.java`）

放在 `$GHIDRA_INSTALL_DIR/ghidra_scripts/` 下，用
`analyzeHeadless <proj> <name> -import <file> -postScript <Script>.java` 调用。

| 脚本 | 用途 |
|---|---|
| `TouchXref.java` | 按符号名匹配 + 交叉引用 + 反编译调用者 |
| `Callers2.java` | 指定地址的调用者及其反编译 |
| `ArgScan.java` | 调用点 **实参 + 前序指令** 扫描（用于反推虚调用/布尔实参） |
| `LMode2.java` | 批量反编译名字含关键字的函数 |
| `Deep4.java` | 定点反编译一组函数并读取数据常量 |

---

## 教训（写进工具设计的几条）

### 1. `size == 12` 的符号都是 PLT 桩

在 `viewUnified*.so` 里，凡同名符号大小为 12 字节的，都是跳板（`adr/add/ldr pc,[r12,#imm]!`）。
真实现在**别的库**。找真实现要么按 `size > 12` 过滤（`RealDef.cs`），要么用 `DefWhere.cs`
区分定义/导入。

### 2. Ghidra 地址 = 文件虚拟地址 + imageBase

本机 imageBase 为 `0x10000`。直接用 Ghidra 地址去算文件偏移会**偏移 64 KB**，
读到的是完全无关的字节。本项目因此踩过两次。

### 3. Ghidra 反编译不可尽信

曾把 `movs r1, #0` 渲染成 `SUB41(ptr, 0)`，**语义完全相反**
（前者是常量 0，后者是取指针低字节）。
**关键结论必须回到原始反汇编复核。**

### 4. 重定位计数不可跨节求和

不同重定位节共用同一张符号表，跨节求和会重复计数。
本项目曾把 239 个引用数成 480 个。**必须用各节自己的 `sh_link` 符号表独立解析。**

### 5. 数据段里的"函数指针 + 小整数"，先怀疑 vtable

判定依据：**同一函数序列在不同锚点重复出现** ⇒ 是 vtable 阵列，不是带参数的配置表。

### 6. 虚调用无法用操作码模式匹配捕获

`ldr rX,[rY,#imm]` + `blx rX` 的目标地址不在指令里。
想找虚调用点，只能**按槽偏移反查**（`VtSearch.cs` 的思路），而且**必须先确定正确的槽索引**。

### 7. vtable 边界不能靠"空槽"找

vtables 在 `.data.rel.ro` 里**首尾相连、没有空隙**，
用"连续 N 个空槽"当边界会落到错误位置（本项目因此先算出 0、再算出 511，都是错的）。
应当用 Itanium ABI 的 **RTTI 指针**（指向 `_ZTI*` 的重定位）作为 vtable 头标记。

### 8. 一定要做自校验

`VtSearch.cs` 不直接查目标偏移，而是把一段 imm12 全部统计一遍，
看哪个偏移的命中数与预期的引用数同量级 —— **这样错了会立刻暴露**。
本项目就是靠这个发现索引 177 是错的。
---

## 密钥配置（开源版必读）

**本仓库不发布密钥。** 源码中原本硬编码的密钥已在开源版中移除，
改为运行时从以下两处读取（按顺序）：

1. 环境变量
   ```
   set SOY_KEY_AES=<32 位 HEX>
   set SOY_KEY_MODEL=<64 位 HEX>        # Windows
   export SOY_KEY_AES=...               # Linux / WSL
   ```
2. 程序目录下的 `keys.txt`（格式见 `src/keys.txt.example`）

未提供时工具会**明确报错退出**，不会静默产出错误结果。

> `keys.txt` 已在 `.gitignore` 中。**切勿提交。**

### 为什么这样处理

密钥不是固件包结构的一部分 —— 它来自设备侧，不与包一起分发。
本仓库发布的是格式与算法；要实际解密，需要你自己合法取得密钥。
详见根目录 `DISCLAIMER.txt` 第 4 节。
---

## 本仓库不包含的工具

| 工具 | 为什么不含 |
|---|---|
| 完整重打包 / 固件生成 | 会显著降低"产出可刷写修改固件"的门槛；失败不可逆。见 `docs/07-repack-design-analysis.md` 第七节 |
| 密钥派生 / 密钥恢复 | 本仓库不发布密钥，也不发布密钥获取工具 |
| 任何写入设备 / 写入镜像的路径 | 本仓库只做离线、只读分析 |

**原则：只发布"读"的能力，不发布"写"的端到端能力。**
校验类工具（如 `VerifySums.cs`）保留，因为它只读不写。

---

## 仓库安全检查工具

| 脚本 | 用途 |
|---|---|
| `check-repo.ps1` | 全树或 staged 文件的安全扫描。**不需要 git 也能全树扫** |
| `setup-hooks.ps1` | 启用 `.githooks/pre-commit`（执行 `git config core.hooksPath .githooks`） |

```powershell
# 全树扫描（默认扫本脚本所在仓库根）
.\check-repo.ps1

# 指定目录
.\check-repo.ps1 -Root D:\some\repo

# 只扫已 staged 的文件（需要 git）
.\check-repo.ps1 -Staged
```

退出码：`0` 通过，`1` 发现硬失败项。

**规则与 `.githooks/pre-commit` 完全一致** —— 改一处必须同步改另一处。

> `.ps1` 文件保存为 **UTF-8 with BOM**。
> Windows PowerShell 5.1 对无 BOM 的 UTF-8 会按 ANSI 解码，中文会导致语法错误。