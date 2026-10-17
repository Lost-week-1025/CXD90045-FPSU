# 05 · 触摸调用链与 WidgetMode

> 示意图：`diagrams/06-touch-callchain.svg`

## 结论先行

ZV-E10 的触摸**不是被禁用**，而是由一个**控件模式值**决定是否被消费。
这个值在控件创建时写入。改它即可改行为 —— 不需要重写交互。

## 调用链

```
布局 / 控件创建代码（LG_*::Layoutlayout_XXX 构造函数，或布局引擎）
   │  setDefWidgetMode(模式实参)              ← 虚调用
   ▼
LayoutableWidgetBase::setDefWidgetMode(m)      libObj.so @0x161CC8, 34 B
   │  this->defWidgetMode = m                 （写入 this + 0x180）
   │  if (查询() != 3)  → 尾调用
   ▼
ux::wgtsys::Widget::setMode(m)                 libObj.so @0x613780, 64 B
   │  if (this->mode == m) return;            （幂等）
   │  this->mode = m                          （写入 this + 0x21，1 字节）
   │  if (m != 0) this->vt[0x48](m == 3 ? 0 : 1);
   ▼
this->member_0x30.someMethod(m == 3 ? 0 : 1)   （尾调用）
```

## WidgetMode 语义

| 值 | 含义 | 依据 |
|---|---|---|
| **0** | 可触摸 | `PAS_MenuSelectMode::setTouchable(true)` 映射到 0 |
| **2** | 不可触摸 | `setTouchable(false)` 映射到 2 |
| **3** | 特殊分支 | `setMode` / `setDefWidgetMode` / `FUN_0014934c` 三处都单独特判 3 |
| 1 | 未在 `setTouchable` 中出现 | 语义未定 |

`setMode` 里 `m == 3` 走单独分支（`vt[0x48](0)` 而非 `1`），说明 3 是一个"第三态"。

## 三个反直觉的实测事实

### 1. `setTouchable(true)` 从未被调用

全固件范围内，`PAS_MenuSelectMode::setTouchable` 与 `PAS_BarCtrlDial::setTouchable`
一共只有 5 个调用点，**全部传 `false`**：

```
PAS_MenuSelectMode::setTouchable(false)   × 1
PAS_BarCtrlDial::setTouchable(false)      × 4
```

**推论**：该 API 的作用是"在特定路径下把触摸关掉"（视图切换/关闭时的清理），
而**默认状态由控件构造时的模式决定** —— 也就是默认是模式 0（可触摸）。

### 2. `PAS_MenuSelectMode` 不处理任何事件

完整方法表只有 6 个成员：

```
setTouchable(bool) · getSelectItemNum() · openMenuSelectMode(CmnViewMenuDataI*, ushort)
update(ushort) · reqScroll(WGT_MoveDirection, uint) · cast(ux::wgtsys::Widget*)
```

**没有任何 `onMouseEvent` / `onKeyEvent` / `on*Event` 重写。**
它是菜单**数据持有者**，不是触摸实现者。触摸处理在 `ux::wgtsys` 框架基类里。

### 3. 框架能力是完整的

`viewUnified2.so` 与 `libObj.so` 中实测存在：

```
ux::wgtsys::WidgetSystem::getGestureAnalyzer()
ux::wgtsys::GestureAnalyzer::setConfig(GestureConfig, ushort) / setGestureYRatio(float)
ux::wgtsys::Widget::calcLocalFlickSpeed(MouseEvent const&)      ← flick 甩动
ux::wgtsys::WidgetBase::onMouseEvent / onMouseEventHook / setMouseCapture
GEN_GridList::setDragHoldPos(Vector2, Vector2)                  ← 网格拖动
ux::wgtlay::WidgetLayouter + AnimationMove / Alpha / Scale / Size / Trigger  ← 动画转场
ux::gfxeng::MultiLineTextSprite::setScrollable / setHorizontalScrollable
PAS_TouchPosition::cast(ux::wgtsys::Widget*)
```

**⇒ 手势、甩动、拖动、动画、可滚动文本全都在。缺的只是"让菜单用起来"。**

## 移植含义

| | 情况 |
|---|---|
| 要不要重写交互 | **不要** —— 框架能力完整 |
| 要改什么 | 控件创建时传入的模式实参 |
| 改动面 | 小，且不涉及逻辑重写 |
| 难点 | 定位调用点（虚调用，操作码匹配抓不到目标） |

## 定位调用点的尝试与结果

| 尝试 | 方法 | 结果 |
|---|---|---|
| 1 | 按"空槽间隙"回溯 vtable 基址 | 索引 0（明显错） |
| 2 | 放宽"槽"判据后回溯 | 索引 511（vtable 首尾相连，边界判错） |
| 3 | 用 Itanium ABI 的 `_ZTI` 指针定位 vtable 头 | 索引 **177** |
| 4 | 按索引 177 的偏移 `0x2C4` 搜调用点 | **零命中 ⇒ 177 是错的** |
| 5 | imm12 直方图自校验 | 真实区间收敛到 **101–112**（其中 0x1B4 命中 287，最接近 239 的引用数） |

**下一步**：在 101–112 区间内逐个验证，用"命中数与 239 同量级"作为收敛判据。

这次失败的方法论价值见 `06-notes-and-pitfalls.md`。