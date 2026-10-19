# CXD90045-FPSU

**Sony CXD90045 平台固件包结构研究，以及 ZV-1F → ZV-E10 的 UI 交互移植可行性分析。**

> 两个机型：ZV-1F（`SYSASTRA-DSC` / `YT`）与 ZV-E10（`SYSASTRA-DSLR` / `RK`）。
> 两者同为 **CXD90045**（Astra，4× Cortex-A5，即 BIONZ X）。
>
> 本仓库发布的是**格式与机制的研究结论、自写工具源码、自制示意图**。
> **不含任何索尼固件、二进制、界面资源、设备转储或密钥。**
> 使用前请先读 DISCLAIMER.txt。

---

## 先读这一节

| 事项 | 说明 |
|---|---|
| **本仓库不含固件** | 分析所需的 `BODYDATA.DAT` 必须由你自行从合法来源取得 |
| **本仓库不含密钥** | 文档只描述密钥的用途、长度、在结构中的位置与解密流程，**不含密钥字节** |
| **本仓库不含反编译导出** | 只发布结论与机制描述；必要的少量反汇编引用仅用于说明机制 |
| **没有人在真机上验证过移植** | 本项目**没有实施** UI 移植。所有可行性判断都标注了依据与置信度 |
| **刷机有真实风险** | 相机可能变砖。若你打算动手，请先做静态校验，再考虑上机 |
| **不提供完整重打包流程** | 只描述结构与失效模式，不提供端到端产出可刷写固件的能力 |

法律与责任边界见 `DISCLAIMER.txt`。

---

## 这个仓库想回答的问题

Sony 的摄像机固件是一个**五层嵌套包**。要研究 UI，得先能完整拆开它。

拆开之后发现一个有意思的事实：

> **ZV-E10 与 ZV-1F 的 UI 架构看起来完全不同，
> 但底层用的是同一套 `ux` 框架、同一批抽象库、同一种资源容器格式。**

于是问题变成：**能不能把 ZV-1F 那套更新的交互体验，搬到 ZV-E10 上？**

这个仓库就是围绕这个问题的工作记录 —— 包括走过的弯路。

**而最终的分析结论改变了问题的形式**（详见第 8、9 节）：

> **不需要搬 ZV-1F 的 UI。**
> ZV-E10 自己的菜单机制是**完整的** —— 传送带滚动、高亮、动画、重绘全在。
> 它缺的只是**一层把触摸手势翻译成「方向」的输入消费者**。
>
> 于是问题从「移植 UI」变成「补一个缺失的输入层」。
> 这件事的工程量与影响面都小一个量级，而且**不需要改动任何原厂既有逻辑**。

---

## 核心发现

### 1. DAT 容器：一个极简却完整的递归结构

```
89 55 46 55 0D 0A 1A 0A            <- 8 B 魔数
[u32be size][4B ASCII type][data]   <- 记录，可重复 N 次
[u32be 4]["DEND"][u32be CRC32]      <- 终止符
```

记录类型是 4 字节 ASCII，前 4 位编号决定顺序，后缀是语义名：
`0000_*` 元信息、`0100_*` 主数据、`0110_backup` 备份载荷、
`0300_partconf` 分区配置、`0700_part_image` 分区镜像、`08xx_*` 机型设置。

**为什么值得研究**：记录长度自描述、互相独立 —— 可以**只取需要的记录**而不解密整包。
对"只关心 UI 资源"的研究，这一点极大降低了成本。

![DAT 容器结构](diagrams/png/01-dat-container.png)

### 2. 加密与容器解耦，按位置切分两种算法

| 区间 | 算法 | 说明 |
|---|---|---|
| 前 512 B | AES-128-ECB | 全平台一把 `key_aes`，位置与长度固定，与机型无关 |
| 其余部分 | AES-256-CBC | 机型密钥 + IV；IV 取自 DAT 偏移 `N-0x110` |

**容器本身不含任何密钥字段** —— 密钥来自设备侧，不与包一起分发。
这正是本项目不发布密钥的原因：它们不属于包结构的一部分。

### 3. 分帧层：为传输可靠性设计，不是为防篡改

```
[u16 checksum LE][u16 sizeAndEnd LE][payload <=1020 B][0xFF 填充]   每帧固定 1024 B
```

- `checksum` = 求和（从帧内 `+2` 起的 511 个小端 u16）& `0xFFFF`
- `sizeAndEnd` 的 bit15 = 末帧标志
- ZV-E10：363,725 帧 × 1024 B + **272 B 尾部**

> **尾部 272 B 必须原样复制，绝不可重新加密。**
> 这是回打包时最容易踩的坑 —— 它不在帧流里，不参与帧校验，但被后续环节使用。

### 4. 三层完整性校验，覆盖范围与执行主体各不相同

![三层完整性校验](diagrams/png/03-integrity-layers.png)

| 层 | 范围 | 形态 | 执行者 |
|---|---|---|---|
| L1 | 整包 | DEND 处的 CRC-32（覆盖 `[0, filesize-12)`，大端） | PC 升级器 |
| L2 | 分区内逐文件 | `NN01_X_sum` 目录下的 CSV 清单（ZV-E10 共 130 项） | 相机 |
| L3 | 每 1024 B 帧 | 16 bit 求和 | 引导 / 更新流程 |

改动任一字节，需要同时处理三层 —— 细节见 `docs/02-integrity-checks.md`。

### 5. 索尼私有分区表：校验条件出人意料的宽松

![分区表结构](diagrams/png/04-partition-table.png)

位于 eMMC 用户区偏移 0，长 4 KB。头部在 `+0x000` 与 `+0x200` 双写，30 个分区项各 16 B。

**校验只有三条**：

```
u32[0x000] == u32[0x200]
u32[0x004] == u32[0x204]
u32[0x008] <= u32[0x208]      （无符号）
```

**没有 magic 常量比对。** 也就是说，校验只检查"两份头是否一致 + 分区数是否落在容量范围内"。

`type` 语义：`0`=raw、`1`=normal、`2`=WBI（`wb_register_device`）、`3`=LZP 压缩（映射为 `dnflashaN`）。
与包内 `0300_partconf/partinf.conf` 的 30 条记录逐字段对应。

**这一项的价值**：有了它，就能在**没有真机**的情况下合成一张自洽的 eMMC 镜像。

### 6. 底层 ABI 几乎完全一致 —— 移植边界由此确定

对两机 `.so` 的导出符号做了逐项比对：

| 库 | ZV-1F | ZV-E10 | 重合度 |
|---|---|---|---|
| `libmintx.so` | 10785 | 10785 | **100%** |
| `libMWF.so` | 1544 | 1544 | **100%** |
| `libSysDef.so` | 24 | 24 | **100%** |
| `libIMDB.so` / `libptp.so` / `libosal_uipc.so` / `libBackupManager.so` / `libOnDemandLoader.so` / `libInfraAccessLog.so` | — | — | **全 100%** |
| `dataflowInfraFramework.so` | 1434 | 1416 | 98.7% |
| `libJiritsu.so` | 204 | 207 | 98.1% |
| `libInfraFsys.so` | 1492 | 1091 | 63.5% |
| **`libObj.so`** | 954 | 3386 | **24.1%** |

**结论：分辨率极高。** 底层（MWF / mintx / SysDef / IMDB / ptp / osal / Backup）一行不用动；
要换的只有 UI 层 —— 也就是 `libObj` 那 24.1%。

### 7. UI 架构是两种范式，但共用一个框架

![UI 架构对比](diagrams/png/05-ui-architecture.png)

| | ZV-E10 | ZV-1F |
|---|---|---|
| 视图定义 | **214 个 `view*.uxc`**（数据驱动） | **0 个**（内嵌在 `appFw.so` 51.9 MB） |
| 渲染库 | `viewUnified2..8.so` 共 19.6 MB | 无 |
| 框架 | `libObj.so` 20.8 MB | `libObj.so` 3.8 MB |
| 位图命名 | 语义式 `image_cmn_43` | 数字式 `image_00_43` |
| 竖屏支持 | 无 | **有**（`image_*_34` + `string_*_vertical/_horizontal`） |
| 资源注册 | `global.xdb` 313 条（含 230+ 视图） | 154 条（**0 视图**） |

**容器格式没变** —— `master_camera.uxc` 的头部 u16 索引表在两机上**逐字节相同**。
这意味着一方的解析工具可以直接用于另一方。

### 8. 触摸机制的真相：不是"被禁用"，而是"没有消费者"

> 这一节结论经过多轮修正。早期版本认为问题出在 `WidgetMode` 这个模式值上 ——
> **那个判断后来被推翻了**，原因记录在本节末尾，避免后来者重走。

![触摸调用链](diagrams/png/06-touch-callchain.png)

#### 8.0 现场观察先划掉了半边范围

在动任何代码之前，先收集了真实机型的使用表现。这三条观察在后续分析里起了
**排除法**作用，比符号扫描更早压缩了搜索空间。

**A. 同平台机型存在"触摸板"与"屏幕触摸"两套分开的机制**
（同 CXD90045 体系下 a6400 / a6100 / A7C / A7M3 等，排除 ZV-E10、ZV-1F 与无取景器机型）

> → 提示框架里可能同时有 pad 与 panel 两套概念，不能假设触摸是单一通路。
> → 据此去查 `getAbsoluteTouchPadPosition` / `setTouchPadEnabAreaForEvfOn/Off` /
> `isTouchPanelPadSupported`，**全部命中，且确认活跃通路正是 pad 这一条**。

**B. 三种场景的触摸表现不同：回放放大能拖、拍摄能对焦、菜单没反应**

> → 这是**价值最大**的一条。前两行都有响应 ⇒ **触摸输入通路本身是好的**；
> 只有菜单不响应 ⇒ **问题被限定在"菜单这一层"**。
> → **"输入通路故障"这个假设被现场事实直接划掉，不必再花时间查。**

**C. 回放放大的操作方式是"双击后拖动"**

> → "拖动"必然有一个"按方向移动"的实现，而菜单滚动也是"按方向移动"。
> → 据此去查 `CmnViewFocusMagnificationCoordinateMgr`，发现
> `move(int dir)` 是「方向 + 固定步长 + 边界钳制」——
> **与菜单的 `reqScroll(dir, beltId, n)` 完全同构**，成为适配层的直接范式来源。

> **⇒ 现场行为是最强的约束条件，应当先用它划掉可能性，再进符号扫描。**
> **完整复盘（含一次结论级误判的成因）见 [`docs/05-touch-callchain.md`](docs/05-touch-callchain.md)。**

#### 8.1 触摸默认是开的，全固件只有一条"关"命令

| 问题 | 结论 |
|---|---|
| 触摸默认状态 | **开启** |
| 唯一的"关"命令 | `requestModelExecute(11, 13, NULL)` |
| 唯一调用者 | `setTouchModeoff()`（无参函数） |
| 它被调用几处 | **15 处，全部位于录像类视图** |

15 处调用点的分布：

```
ViewMovieExpModeToInstance                       5 处
ViewStlrecToInstance                             8 处
CmnViewProcessDataElementCustomMovieRecording    1 处
CmnViewProcessDataElementCustomShutterSpeed      1 处
```

**没有一处来自菜单路径。**

> **⇒ 菜单在 model 层从来没有被"禁止触摸"。**
> **问题不是"权限被关掉了"，而是"没有任何代码去消费这个触摸"。**

这一点很重要：它意味着**不存在需要去"打开"的闸门**，
要做的是**新建一个消费者**。改动因此是纯增量的 —— 不触碰原厂任何既有行为。

#### 8.2 触摸事件根本到不了控件层

| 事实 | 证据 |
|---|---|
| `WidgetBase::onMouseEvent(MouseEvent const&)` 是 **6 字节空桩** | 反汇编只有一条 `bx lr` |
| **没有任何类重写它** | 全库符号扫描无 override |
| 全库**不存在任何命中测试** | `hitTest` / `pickWidget` / `findWidget` / `getWidgetAt` / `selectWidgetAt` 全部 0 命中 |
| `setTouchable(true)` 全固件从未被调用 | 仅有的调用点全部传 `false` |

**⇒ 触摸坐标从不进入控件系统。ZV-E10 的触摸走的是另一条完全独立的通路。**

#### 8.3 触摸坐标的真实来源

```
[global+0x191] == 2                        <- 触摸激活状态字节
        |
        v
ViewBase::GetEventParameter(view, 3)       <- 取触摸参数对象
        |
        v
obj->[0x10] = X     obj->[0x14] = Y        <- 原始触摸坐标
        |
        v
cmnViewTPOperationUtil::getAbsoluteTouchPadPosition(x, y, &ax, &ay)
        |   ax = (x - originX) * scaleX
        |   ay = (y - originY) * scaleY
        v
view[0x270] = ax    view[0x274] = ay        <- 屏幕绝对坐标缓存
```

**关键区分**：`getAbsoluteTouchPadPosition` **不是取值函数，是换算函数**
（减原点 + 乘缩放）。原始坐标在 `GetEventParameter` 返回的对象里。

该函数有 **15 个调用点**，全部属于拍摄类视图 ——
说明它是 ZV-E10 上**唯一活跃**的触摸通路。

#### 8.4 一个必须承认的边界：ZV-E10 没有多点触控原语

逐文件核对（不是全库汇总，是分别列出）：

| 符号 | ZV-1F `libObj.so` | ZV-E10 `libObj.so` |
|---|---|---|
| `GestureAnalyzer::getPinchOriginPos` | 有 | **无** |
| `GestureAnalyzer::isMultiTouch` | 有 | **无** |
| `WidgetBase::acceptMultiTouch(bool)` | 有 | **无** |

**⇒ ZV-E10 的框架里没有缩放手势的基础设施。
任何依赖 pinch 的交互都做不了，除非先补这套原语 —— 那是另一个量级的工程。**

#### 8.5 已排除的弯路：`WidgetMode` 不是菜单的开关

从代码看，`WidgetMode` 确实是一套模式机制：

```
布局/控件创建代码
   |  setDefWidgetMode(模式实参)          <- 虚调用
   v
LayoutableWidgetBase::setDefWidgetMode(m)
   |  this->defWidgetMode = m             (this + 0x180)
   v
ux::wgtsys::Widget::setMode(m)
   |  this->mode = m                      (this + 0x21, 1 字节)
```

- `LayoutableWidgetBase::C2Ev` 写 `movs r3,#2` ⇒ **默认 `defWidgetMode = 2`（非触摸）**
- 设置入口在 **vtable slot `0x10C`**；类型查询在 `vt[0x194]`
- `PAS_MenuSelectMode` 的类型 ID = **72**

**但这条线索最终没有指向"菜单被禁用"。**

原因就是 8.2 说的事：**菜单的输入根本不经过控件层**，
所以"控件是否 touchable"与"菜单能否触摸"之间**没有因果关系**。

> **⇒ 记录在此：`WidgetMode` 是一个真实存在的机制，但它不是菜单触摸的开关。
> 沿着它改，方向是错的。**

### 9. 菜单机制的整体结构

#### 9.1 核心模型：传送带（belt）

```
     [条目 N-2] [条目 N-1] [*条目 N*] [条目 N+1] [条目 N+2]
     -----------------------------------------------------  <- 带子在动
                              ^
                        游标固定在中间
```

**输入只有「方向」，没有「位置」。**

菜单不知道手指在哪，它只知道"往前一格 / 往后一格"。

> **⇒ 这就是为什么给菜单加触摸必须「算增量」，
> 而不能「把坐标映射成索引」。**
>
> 另外，belt 的几何量（`ItemPitch` / `ItemInterval` / `ItemSize` / `NumOfItem` /
> `getScrollPos` / `getScrollOffset`）在符号表里**全部为 0 命中** ——
> 绝对坐标→索引的映射在数据上也不可得。

#### 9.2 架构：每个视图一套 16 项动作派发表

**没有统一的"菜单输入入口"。** 每个视图在编译期生成自己的一套动作处理器。

```
ViewExpModeToInstance  @0x8CF63B (36 B)
        | 紧随其后的私有处理器簇
        v
0x8CF830 .. 0x8D005C
        |
        v
0x8CFFAE   <- 16 项动作派发器
            if (actionId > 0xF) return;
            tbh [pc, actionId, lsl #1];
```

跳表逐字节解出（表在 `0x8CFFBC`，表项 `10 4A 14 18 1C 20 24 28 2C 30 34 38 3C 40 44 48`）：

| action | 处理器 | 说明 |
|---|---|---|
| 0 | `0x8CF830` | |
| 1 | `0x8CFAAA` + `0x8CFF20` | 特殊（先处理再返回） |
| **2** | **`0x8CF838`** | **方向 0（上一项）** |
| **3** | **`0x8CF890`** | **方向 1（下一项）** |
| 4 | `0x8CFF14` | |
| 5 | `0x8CF8E8` | |
| 6 | `0x8CF978` | |
| 7 | `0x8CFD56` | 读 `[view+0x1D4]` 的后续处理 |
| 8–15 | `0x8CF98A` / `0x8CFD1C` / `0x8CF998` / `0x8CF9CC` / `0x8CFA00` / `0x8CFA94` / `0x8CFAAA` | 其它菜单动作 |

**表项从 action 2 起严格每 8 字节递增 —— 说明处理器是连续排列的生成代码。**

**⇒ 全树有 380 个 `*ToInstance` 视图工厂，每个都有自己的处理器簇。
⇒ 这就是为什么"找一个通用的菜单滚动函数"找不到。**

#### 9.3 方向处理器的完整代码模板

`0x8CF838`（方向 0，88 字节）：

```c
void handler_dir0(void* view) {
    Widget* w = Check_PAS_MenuSelectBeltZako(          // 类型检查 / 转换
                    ViewBase::getWidget(view, 0xDCAEB587));

    view->[0x1D0] = w;          // belt 控件缓存
    view->[0x1D4] = 0;          // 记录本次方向

    int state = CmnSettingNodeUtil::getProcVal(view + 0x15C);
    if (state == 0xD || state == 4 || state == 0xB)
        sub_89221A(w, 0);       // 前置处理（疑似清理旧高亮）

    PAS_MenuSelectBeltZako::reqScroll(w, /*dir*/0, /*beltId*/0, /*n*/1);
}
```

`0x8CF890`（方向 1）是它的镜像：写 `[+0x1D4] = 1`、状态码查 `{6, 2, 0xA}`、传 `dir = 1`。

四个辅助调用全部解析出来：

| 地址 | 符号 |
|---|---|
| `0x186DB0` | `ViewBase::getWidget(unsigned int)` |
| `0x18650C` | `Check_PAS_MenuSelectBeltZako(ux::wgtsys::Widget*)` |
| `0x1891A0` | `CmnSettingNodeUtil::getProcVal()` |
| `0x89221A` | thunk → `(w + 0x410)` 上的 `0x870F1C` |

注意：`0x89221A` 里对 `w` 加的是 `0x410` —— 与"belt 对象位于 `view+0x410`"这一布局一致。

#### 9.4 一个常量通吃所有菜单视图：widget ID `0xDCAEB587`

这个 32 位常量就是**菜单带控件的全局 ID**，出现在 **14 个以上视图的 `.uxc` 布局**里：

```
viewExpMode.uxc                      viewMovieExpMode.uxc
viewCommonShootingSetting.uxc        viewFocusArea.uxc / viewFocusArea_C.uxc
viewIsoSensitivity.uxc               viewLcdBrightness.uxc
viewFinderFrameRate.uxc              viewCustomZebra.uxc
viewSLogDispAssist.uxc               viewSelfTimerOnBracket.uxc
viewAutoDualRec.uxc                  viewIsoAutoMinSS.uxc
viewCommonDedicatedScreenFunc.uxc
```

也出现在多个代码库：`viewUnified2 / 3 / 4 / 7 / 8.so`。

> **⇒ 一个常量覆盖全部菜单视图 —— 不需要逐视图枚举 380 个 ID。**
> **⇒ 而且它在拍摄视图里也存在，只是不可见 / 未激活。**

#### 9.5 方向枚举：`WGT_MoveDirection` = {0, 1, 2, 3}

三重独立确认：三处 `cmp #3 / bhi` 范围检查 + 两个跳表逐字节解出 + 一处显式 ±1。

`GEN_CalcHltInfo::calcNextItemPos` 的跳表（原始表项 `02 11 1F 2E`）：

| dir | 跳转目标 | 动作 |
|---|---|---|
| **0** | `0x156B68` | `subs r6,#1` → **轴A −1** |
| **1** | `0x156B86` | `adds r6,#1` → **轴A +1** |
| **2** | `0x156BA2` | `subs r5,#1` → **轴B −1** |
| **3** | `0x156BC0` | `adds r5,#1` → **轴B +1** |

`GEN_EditBox::moveCursorPos` 独立印证：`dir == 2 → cur−1`，`dir == 3 → cur+1`。

> **⇒ 偶数 = 减，奇数 = 加；`{0,1}` 与 `{2,3}` 分属两根轴。**

#### 9.6 两族 belt 的约定不同（必须分别对待）

| | `PAS_MenuSelectBeltZako` | `PAS_MenuSelectBeltFn` |
|---|---|---|
| 接受的 `dir` | **只接受 `{0, 1}`** | **只接受 `{2, 3}`** |
| 判定位置 | worker `0x87139E`：`cmp.w sl,#1` / `bhi → return` | worker `0x8914A4`：`sub r2,sl,#2` / `cmp #1` / `bhi → return` |
| 条目总数偏移 | **`[belt + 0x1028]`** | **`[belt + 0xBE8]`** |
| 默认 `beltId` | `0` | `0` |

**⇒ 自己填 `dir` 去调 `reqScroll`，需要为每族各写一套映射。
⇒ 更稳的做法是调用 util 层入口，让固件自己完成映射。**

#### 9.7 `n` 参数不是"步数"

`reqScroll(dir, beltId, n)` 的 `n` 被直通到 worker，但 worker 里：

```c
// sub_890B64(this, n) —— 96 字节
if (this->[0xC0C] == 0) return 0;
if (this->[0xC04] == 0) return 0;
vt[0x5C](this->[0xC0C], 0x14, &local);
if (local != 0xE0) return 0;
return (n <= 1) ? 0 : 1;        // n 只用于这个阈值判断
```

> **⇒ `n` 是模式标志（行滚动 / 页滚动之类），不是"滚几格"。
> ⇒ 想快速滚动只能循环调用，不能加大 `n`。原厂恒传 `n = 1`。**

框架里并列存在的接口支持这个判断：
`reqLineScroll` / `reqPageScroll` / `reqContLineScroll` / `reqGroupScroll` /
`reqSingleGroupLineScroll` / `reqSingleGroupPageScroll` / `reqSingleGroupGroupScroll` …
—— "行 / 页 / 组"是多组并列的，所以用 `n` 区分它们完全合理。

#### 9.8 视图对象的布局

| 偏移 | 含义 | 依据 |
|---|---|---|
| `view + 0x15C` | `CmnSettingNodeUtil` 子对象 | `getProcVal(view+0x15C)` |
| `view + 0x1D0` | **belt 控件对象指针** | 两个方向处理器都写 |
| `view + 0x1D4` | **上一次移动的方向（0..3）** | dir0 写 0、dir1 写 1；action 7 读它 |
| `view + 0x270 / +0x274` | 触摸绝对坐标缓存 | `getAbsoluteTouchPadPosition` 调用点 |

#### 9.9 一条已排除的死路径

| 符号 | 直接 `bl` | PLT 调用点 | 指针槽 | 结论 |
|---|---|---|---|---|
| `CmnSettingNodeUtil::scrollingRequest` | 0 | **0** | GOT 有 | 导出但无人调用 |
| `CmnSettingNodeUtil::_updateOsdOnScrolling` | 0 | — | **无** | **死代码** |
| `CmnSettingNodeUtil::invalidatWidgets` | 0 | — | **无** | **死代码** |

全树 334 个 ELF 中，这些符号只出现在 `viewUnified2.so` 里，无任何外部库导入。

> **⇒ 早期几轮曾沿 `scrollingRequest` 分析，那条路是死的。
> 真正的入口是每视图的动作派发表（9.2）。**

#### 9.10 三族菜单工具类

| 类 | 数据结构 | 用途 |
|---|---|---|
| `CmnSettingNodeUtil` | 树（`CmnViewSettingNode`） | 设置菜单 |
| `CmnMenuTableUtil` | 平表（`CmnViewMenuData` + `CmnViewMenuItemStruct`） | 表格菜单 |
| `CmnZakoMenuUtil` | 平表 | 子项菜单 |

菜单项 API：`menuData->vt[0x10]()` 取条目数、`menuData->vt[0x24](idx, &out)` 按索引取条目。

#### 9.11 由此得到的适配层设计

把 8.3（坐标来源）和 9.3（方向处理器）接起来，就是完整的输入适配链路：

```
触摸坐标  GetEventParameter(view,3) 的 [0x10] / [0x14]
        |  差值 + 阈值 + 轴锁定 + 冷却
        v
方向  0 = 上一项, 1 = 下一项
        |
        v
ViewBase::getWidget(view, 0xDCAEB587)          <- 通用句柄（9.4）
        |
        v
Check_PAS_MenuSelectBeltZako(widget)            <- 类型转换
        |
        v
PAS_MenuSelectBeltZako::reqScroll(w, dir, 0, 1)
        |
        v
belt 滚动 + 高亮 + 重绘                          <- 原厂逻辑，一行不改
```

**不需要改动的东西**（均已验证无关）：
触摸开关、`WidgetMode`、`onMouseEvent`、`moveCursor`、`scrollingRequest`、
以及 ZV-1F 的任何 UI 资源。

#### 9.12 加载方式：符号插桩

适配层以 `.so` 形式实现，通过**导出同名 mangled 符号**接管
`ViewBase::getWidget(unsigned)`：

- 该函数**第 0 个参数就是 `view` 指针** —— 正是需要的东西
- 它被**全部 7 个视图库**（`viewUnified2` ~ `viewUnified8`）导入 ⇒ **插桩一次全覆盖**
- 而它的定义库 `libObj.so` 内部调用走直接跳转、不经 PLT
  ⇒ **不被插桩，只拦跨库调用**

四个目标符号已确认全部被导出（全树 156 个 `.so` 扫描）：

| 符号 | 定义库 | 地址 |
|---|---|---|
| `_ZN8ViewBase9getWidgetEj` | `libObj.so` | `0x1398E9` |
| `_Z28Check_PAS_MenuSelectBeltZakoPN2ux6wgtsys6WidgetE` | `viewUnified2.so` | `0x892135` |
| `_ZN22PAS_MenuSelectBeltZako9reqScrollE...` | `viewUnified2.so` | `0x892141` |
| `_ZN8ViewBase17GetEventParameterEmm` | `libObj.so` | `0x13A6EB` |

> **⚠️ 本仓库不发布该载荷的构建产物与端到端加载流程。
> 本节只说明机制层面的可行路径。**

#### 9.13 适配层算法的验证方式（不需要真机）

适配层的核心是一个纯状态机：输入 `(是否触摸中, x, y, 时间戳)`，
输出 `(是否滚一格, 方向)`。它不依赖任何固件符号 ——
固件那一侧全部收敛到一张函数指针表。因此**可以在 PC 上完整测试**。

已验证 26 项，其中三项是真实缺陷，值得单独记下：

| # | 发现 | 后果 |
|---|---|---|
| 1 | **轴锁定必须用「从触摸开始的累计位移」，不能用单帧** | 初版每帧只动 10px（阈值 12）时轴永远锁不上，**慢速拖动完全无响应**。这在实机上表现为"快速滑能用、慢慢拖没反应"，极难现场定位 |
| 2 | **冷却时间必须 ≥ belt 动画时长** | 冷却 50ms / 动画 80ms 时，15 次请求被拒 7 次，手感"丢步" |
| 3 | **冷却期应丢弃多余位移，而不是排队** | 一次 500px 甩动：丢弃 = 1 步；排队 = 7 步（过冲） |

### 10. 虚拟机复现：比预期乐观

参考了 [chocotan/a7c2-re](https://github.com/chocotan/a7c2-re)（A7C II，固件 2.01）的做法。
他们用 **「软件 GPE 模型 + QEMU ramfb（fw_cfg DMA）」** 把**原固件菜单真实跑起来并可操作**，
窗口画布与原应用快照**逐像素一致** —— 关键在于**不模拟 GPU**。

方法论可迁移的部分：ramfb 输出、Sony LZ77 用 CPU 同格式解码替代、原 Fsys 虚拟磁盘。
必须重写的部分：**GPE 绘图请求的编码**（ZV-E10 的图形栈不同代）。
**不适用**的部分：CP 启动（ZV-E10 是单 SoC，没有 CP）。

> 但要记住：QEMU 对移植工作的价值是**"能观察 UI 层的文件/ioctl/属性访问"**，
> 不是替代实机验证手感。**实机仍然需要。**

顺带一个实测发现：`init` 里有独立的 **`check_qemu()`** 函数，`im.elf` 里也有 `qemu` 参数字符串
—— **固件是主动检测 QEMU 环境的**。这解释了为什么某些环节必须打补丁才能推进。

---

## 完整解包链路

![解包流水线](diagrams/png/02-unpack-pipeline.png)

五层结构，每层只做一件事，反向即为打包顺序。
逐层说明见 `docs/01-format-dat-container.md`。

---

## 为什么暂时不推进移植

三条理由，从强到弱。

### ① 机制层面的结论让"移植"这个动作本身变得不必要

第 8、9 节合起来说明：ZV-E10 的菜单机制**是完整的**，
它缺的只是输入消费者，而不是"缺一套 UI"。

> **搬一套新 UI 进来，是拿最大的工程量换一个本来不需要的东西。**

所以方向改成"补输入层"。**这不是退让，是分析之后得到的更优解。**

### ② 只剩三件事必须实机才能定，静态分析已经到头

| # | 待定项 | 性质 |
|---|---|---|
| 1 | **菜单模式下 `ViewBase::getWidget` 是否真的被调用** | **最关键的不确定性** —— 决定插桩点是否落空 |
| 2 | 方向 `0` / `1` 与屏幕「上 / 下」的对应 | 1 比特，试一次即可确认 |
| 3 | belt 动画时长 | 决定冷却下限，按实际手感调 |

> **⇒ 项目停在：机制全部解清、适配层算法完成并交叉编译通过、
> PC 侧 26 项测试全过。再往前一步就需要硬件。但是评估上还得继续慢慢推进（其实还在准备买机子）**

---

## 仓库结构

```
.
├── README.md
├── DISCLAIMER.txt          使用前提，务必先读
├── LICENSE                 MIT 许可（逐字，无附加条款）
├── NOTICE                  许可适用范围声明（MIT 不管哪些东西）
├── ACKNOWLEDGEMENTS.md     致谢：本项目依据的公开工作
├── CONTRIBUTING.md         贡献规则：什么可以提交、什么绝对不行
├── .gitignore              第 1 层：安全排除（固件 / 二进制 / 密钥 / 资源）
├── .gitattributes          第 2 层：危险类型标记 binary
├── .githooks/
│   └── pre-commit          第 3 层：提交前硬失败检查
├── docs/
│   ├── 01-format-dat-container.md    容器 / 加密 / 分帧 格式
│   ├── 02-integrity-checks.md        三层校验与 .sum 规则
│   ├── 03-partition-table.md         私有分区表
│   ├── 04-ui-architecture-diff.md    两机 UI 架构差异
│   ├── 05-touch-callchain.md         触摸调用链与 WidgetMode
│   ├── 06-notes-and-pitfalls.md      教训与踩过的坑
│   └── 07-repack-design-analysis.md  重打包设计取舍与失效模式（不含端到端流程）
├── diagrams/               自制示意图
│   ├── *.svg               矢量源文件（可编辑、可 diff）
│   └── png/*.png           位图导出（2x，便于上传）
└── tools/
    ├── README.md           工具说明与用法
    └── src/                工具源码
```

---

## 现状

### 已完成

- [x] DAT 容器完整解析（记录流 + 终止符）
- [x] 两段解密流程确定（ECB 段 + CBC 段）
- [x] FDAT 分帧还原（帧格式 + 尾部处理）
- [x] ustar 归档提取（174 条目）
- [x] 三层完整性校验全部确定，可重算
- [x] 索尼私有分区表破解（含合成自校验样例）
- [x] 两机全树文件级差异对比（4,011 vs 973 文件）
- [x] 两机导出符号级 ABI 对比
- [x] `.uxc` / `.xdb` 容器格式识别
- [x] **触摸机制完整定性**：默认开启、唯一关闭命令只在录像视图、事件不进控件层、坐标来源定位
- [x] **多点触控能力边界确认**：ZV-E10 缺少 pinch / `isMultiTouch` / `acceptMultiTouch`
- [x] **菜单机制整体结构解出**：传送带模型、每视图 16 项动作派发表、方向处理器模板
- [x] **方向枚举 `WGT_MoveDirection` = {0,1,2,3} 三重确认**
- [x] **通用句柄 `0xDCAEB587` 定位**（跨 14+ 视图）
- [x] **两族 belt 的 `dir` 约定与条目数偏移分别确定**
- [x] **`n` 参数语义澄清**（不是步数）
- [x] **排除三条死路径**（`scrollingRequest` / `_updateOsdOnScrolling` / `invalidatWidgets`）
- [x] **输入适配层算法完成**，PC 侧 26 项测试全通过
- [x] **适配层 C 源码交叉编译为 ARM 目标文件**，`.so` 链接产物生成，插桩符号确认导出
- [x] 虚拟机复现可行性重新评估

### 未完成

- [ ] **UI 移植本身 —— 未实施**（且分析表明不再必要，见"为什么暂时不推进移植"）
- [ ] **适配层未在真机运行过**
- [ ] **菜单模式下 `getWidget` 是否被调用 —— 未验证**
- [ ] 方向 `0` / `1` 与屏幕「上 / 下」的对应 —— 未验证
- [ ] belt 动画时长 —— 未实测
- [ ] `sub_89221A`（→ `0x870F1C`）是否为必需步骤 —— 未确认
- [ ] **重打包工具不随仓库发布**（做过往返验证，但按设计取舍不公开）
- [ ] `.uxc` 内部偏移表的完整语义（值不是文件偏移，需加载器确认）
- [ ] `.uxb` 文件的作用
- [ ] QEMU 环境搭建与 GPE 软件模型
- [ ] 真机验证（从未在硬件上测试过任何修改）

### 探索过但没走通的（同样记录在案）

- **vtable 槽索引定位**：用 Itanium ABI 的 `_ZTI` 指针算出索引 177，
  但按该索引的偏移 `0x2C4` 搜调用点**零命中** —— 说明基址判定有误。
  自校验直方图把真实区间收敛到 **101-112**。方法、失败原因、
  以及为什么操作码扫描对虚调用无效，见 `docs/06-notes-and-pitfalls.md`。
- **`WidgetMode` 作为菜单触摸开关**：机制真实存在（默认值 2、入口 slot `0x10C`），
  但它与菜单触摸**没有因果关系** —— 菜单输入不经过控件层。方向错误，已放弃。
  完整复盘（错在哪、六个成因、留下的方法论）见
  `docs/05-touch-callchain.md` 第四、五节。
- **`scrollingRequest` 作为菜单滚动入口**：三种查法（直接 `bl` / PLT 调用点 / 指针槽）
  交叉验证均为零调用，属死代码。真正的入口是每视图的动作派发表。
- **belt 几何量**：`ItemPitch` / `ItemInterval` / `ItemSize` / `NumOfItem` /
  `getScrollPos` / `getScrollOffset` / `getBeltPos` 全部 0 符号命中
  ⇒ 绝对坐标→索引的映射在数据上不可得，只能改用相对增量。

---

## 致谢

本项目站在这些公开工作的肩膀上。**完整致谢见 [`ACKNOWLEDGEMENTS.md`](ACKNOWLEDGEMENTS.md)**，此处只列最直接相关的：

| 项目 / 作者 | 本项目用到的部分 |
|---|---|
| **[Cokee（cokeenet）— `zve10-qemu`](https://github.com/cokeenet/zve10-qemu)** | ZV-E10 的 QEMU 复现、16 分区映射、反砖设计。**虚拟机可行性判断几乎全部依据此工作** |
| **[chocotan — `a7c2-re`](https://github.com/chocotan/a7c2-re)** | A7C II 原生 QEMU 相机实验。"软件 GPE + ramfb"路线由此而来 |
| **[YuuLuo — `a7c2-service-preload`](https://github.com/YuuLuo/a7c2-service-preload)** | A7C II 服务模式 → root 执行链路：Jiritsu 路径越界 + `ld.so.preload` |
| **[DavidBuchanan314 — `ILCE-7M4-RE`](https://github.com/DavidBuchanan314/ILCE-7M4-RE)** | A7 IV 逆向笔记与 Linux 移植 |
| **[ma1co — `Sony-PMCA-RE`](https://github.com/ma1co/Sony-PMCA-RE) / [`qemu`](https://github.com/ma1co/qemu) / [`fwtool.py`](https://github.com/ma1co/fwtool.py)** | **解包链的全部基础**：容器格式、两段解密、公开密钥表 |
| **[QEMU 社区](https://www.qemu.org/)** | 全部离线实验环境 |
| 过往工具链的逆向人员（Ghidra 等） | 符号分析、反汇编、格式解析 |
| [Sony OSS](https://oss.sony.net/Products/Linux/) | 内核与驱动来源依据 |

方法论上从中学到的两条，本项目全程遵守：

1. **把"验证通过"和"发送成功"分开** —— 输入命令返回成功，不代表固件处理了对应语义
2. **不要在报告里把未验证的步骤写成已完成** —— 本 README 的"未完成"一节就是为此存在

---

## 许可

本项目自有的文档、工具源码与示意图采用 **MIT** 许可（见 `LICENSE`）。

**MIT 仅覆盖本项目原创内容**，不覆盖索尼固件、索尼二进制、索尼界面资源、
索尼商标或任何第三方内容 —— 详见 `NOTICE`。

> 说明：MIT 是**版权**许可，只处理"复制、修改、分发本作品"。
> 它不处理、也无法处理：规避技术保护措施的法律规定、专利权、
> 或使用者利用这些研究成果所实施的行为。
> 风险提示与行为约定见 `DISCLAIMER.txt`（那是使用前提，不是许可条款）。
