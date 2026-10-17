# 04 · ZV-E10 与 ZV-1F 的 UI 架构差异

> 示意图：`diagrams/05-ui-architecture.svg`
> 机型代号：ZV-E10 = `RK`（平台 `SYSASTRA-DSLR`），ZV-1F = `YT`（平台 `SYSASTRA-DSC`）

## 树规模

| | ZV-E10 | ZV-1F |
|---|---|---|
| `/usr` 文件数 | 973 | 4,011 |
| `/usr` 体积 | 215.6 MB | 309.8 MB |

ZV-1F 多出的部分里，**3,220 个是 `share/screenreader/`（语音朗读数据）**，与 UI 无关。
真正的 UI 差异规模比文件数差小得多。

## UI 架构

| | ZV-E10 | ZV-1F |
|---|---|---|
| 主程序 | `bin/im.elf`（23,240 B） | `bin/im.elf`（同尺寸，内容不同） |
| 框架库 | `libObj.so` 20.8 MB（3,386 导出） | `libObj.so` 3.8 MB（954 导出） |
| 渲染库 | `viewUnified2..8.so` 8 个，共 19.6 MB | **无** |
| 业务框架 | `libBizFw.so` + `libBizFw2.so` | 无（合进单体） |
| 视图定义 | **214 个 `view*.uxc`** | **0 个** |
| 单体 UI | 无 | **`appFw.so` 51.9 MB** |
| 加载方式 | 数据驱动（`.xdb` 注册 230+ 视图） | 代码内嵌（`.xdb` 注册 0 视图） |

## 资源层差异

### 位图命名

| | ZV-E10 | ZV-1F |
|---|---|---|
| 命名方式 | 语义式 `image_<用途>_<画幅>` | 数字式 `image_NN_<画幅>` |
| 画幅 | `_43`（磁盘上只有这个） | `_43` 与 `_34`（都在） |
| 最大单包 | `image_cmn_43` 26.7 MB | `image_00_43` 9.2 MB |

ZV-E10 的用途分类：`cmn` / `DSC_Only` / `DSC_Entry_Only` / `DSLR_Only` /
`EVF` / `Fn_Menu` / `Rich_Help_Guide` / `cmn_o_direct`。

### 竖屏支持

ZV-1F 独有：

- `string_<lang>_vertical.uxc` × 44
- `string_<lang>_horizontal.uxc` × 44
- `image_NN_34.uxc`（3:4 竖版）
- `area_check_data_vertical.dat`

ZV-E10 完全没有这一套。**这是移植时必须处理的第一个现实问题。**

### 语言串

两边都有 `string_<lang>.uxc` 与 `string_<lang>_f.uxc`，但内容不同。
规律是：ZV-E10 的非 `_f` 版更大、`_f` 版更小 —— 说明两者对"完整/精简"两套语言数据的分割方式不同。

### 样式与颜色

`color_cmn.uxc` / `style_cmn.uxc` 同名但内容不同，格式一致
（`color_cmn.uxc` 为 8 字节步长的记录表）。

## 容器格式：没有变

`.uxc` 魔数 `uxc\x07`。`master_camera.uxc` 的头部 u16 索引表在两机上**逐字节相同**：

```
8, 4E, 14F, 15, 0, 86, 2FF, 464, 537, 55D, 790, 7E6, 888, 8E6,
9A7, A22, A80, B41, BBC, C5C, C7C, CAB, CD6, D01, D2C, D57, D82,
DAD, DD8, E03, E2E, E59, E84, EAF, FFFF
```

**这是本项目最有用的一条结论**：为一方写的 `.uxc` 解析工具，可以直接用于另一方。

`.xdb` 魔数 `uxa\x07`，是资源注册表（文件名清单）。

## ABI 对比

见 README 第 6 节的表。要点：

- **底层 8 个库导出符号 100% 相同** ⇒ 底层一行不用动
- `libObj.so` 只有 24.1% 重合 ⇒ UI 层就是那个要换的边界

## 由此得到的移植判断

| 改动目标 | 性质 |
|---|---|
| 菜单**项**的存废与取值 | 数据（`CautionConfig.so` 的按机型变体数组） |
| **触摸是否消费** | 代码（控件创建时传入的模式实参） |
| 布局**组成与位置** | 代码（`LG_*::Layoutlayout_XXX` 构造函数） |
| 参数控制面板行为 | **已是现成能力**（`CmnViewIroiroConDataMgr`，两机都有） |

**"布局是代码生成的"这一点出乎最初预期** —— 一开始以为 `.uxc` 里存的是完整布局数据，
实际是 `LG_*` 族 C++ 构造函数写死了控件组成，`.uxc` 提供的是参数与几何。