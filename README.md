# CampusNet · 校园网自动拨号

一个 Windows 桌面小工具：**开机自动拨号 + 断线自动重连**，专为需要 PPPoE 拨号认证的校园网（宿舍网口）设计。

> 本工具为同学之间分享的便利软件，**与学校网络中心无关**。遇到网络本身故障请联系学校网络中心。

---

## 功能特性

| 功能 | 说明 |
|---|---|
| 开机自动拨号 | 登录 Windows 后自动连接，不用手动点"宽带连接" |
| 断线自动重连 | 息屏、休眠、断电恢复后网络中断会自动重拨 |
| 账号加密保存 | 密码通过 Windows DPAPI 加密，仅本机当前用户可解密 |
| 绿色便携 | 单文件 exe，无需安装、无需运行库，拷走即用 |
| 零网络上传 | 程序不联网收集任何信息，所有数据留在本机 |
| 免管理员权限 | 不改动系统关键设置，仅在当前用户范围注册自启 |

---

## 适用场景

**适合**：宿舍网口需要 **PPPoE 拨号**（用"学号 + 密码"那种）上网的环境。

**不适合**：
- 插上网线直接能上网（DHCP）
- 需要打开网页登录认证（Portal 认证）
- 纯无线 WiFi 环境

---

## 使用说明

### 快速开始（三步）

1. 从 [Releases](../../releases) 下载 `校园网拨号.exe`，放到一个**固定的独立文件夹**。
   > 程序会在同目录生成日志与设置文件，建议单独放一个文件夹，别丢在桌面根目录。
2. 双击运行，首次会自动弹出设置向导。
3. 跟着向导走：选择（或新建）拨号连接 → 输入账号密码 → 完成。
   建议勾选 **开机自启** 和 **断线自动重拨**。

### 常见问题

| 错误码 | 含义 | 处理 |
|---|---|---|
| **691** | 账号或密码不正确 | 检查密码是否改过、账号是否在别的设备登录 |
| **678** | 远程计算机无响应 | 多为网线/端口/交换机问题，重插网线或换网口 |
| **623** | 找不到拨号连接 | 打开程序 →「账号设置」重新选择或新建连接 |

其他：
- **改账号密码**：打开程序 → 左侧「账号设置」
- **临时关闭自动联网**：打开程序 → 左侧「自动重拨」→ 关掉开关
- **界面显示"未连接"但实际能上网**：更新到最新版本

### 卸载

1. 打开程序 →「自动重拨」→ 关闭"开机自动启动守护"
2. 直接删除整个文件夹即可，系统不会残留

---

## 从源码构建

### 环境要求

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows 10 / 11

### 构建命令

```bash
# 克隆
git clone https://github.com/<你的用户名>/campusnet.git
cd campusnet

# 编译
dotnet build CampusNet.sln -c Release

# 发布为单文件 exe（自包含，无需目标机器装 .NET）
dotnet publish src/CampusNet.Dialer/CampusNet.Dialer.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish
```

产物在 `./publish/校园网拨号.exe`。

---

## 项目结构

```
CampusNet.sln
└── src/
    ├── CampusNet.Core/               # 核心库（无 UI 依赖）
    │   ├── Ras/
    │   │   ├── NativeRas.cs          # rasapi32 的 P/Invoke 封装
    │   │   └── PhonebookManager.cs   # 拨号连接（PBK）管理
    │   ├── Network/
    │   │   ├── DialService.cs        # RasDial 拨号与结果判定
    │   │   └── NetworkMonitor.cs     # 联网状态探测
    │   ├── Credentials/              # DPAPI 凭据加密存取
    │   ├── Configuration/            # 设置读写与路径解析
    │   ├── Logging/                  # 日志
    │   ├── Models/                   # 数据模型
    │   └── Startup/                  # 守护进程与开机自启
    ├── CampusNet.Dialer/             # WPF 主程序（GUI + 守护模式）
    │   ├── Pages/                    # 状态 / 账号 / 自动重拨 / 日志 / 关于
    │   └── FirstRunWizard.xaml       # 首次运行向导
    └── CampusNet.Watchdog/           # （历史遗留，未进 sln，可忽略）
```

---

## 技术要点

### 关于 RAS 结构体内存布局（踩坑记录）

调用 `RasEnumConnections` / `RasEnumEntries` 时，如果结构体大小与系统期望不符，会返回
**错误码 632（`ERROR_INVALID_SIZE`）**，且**枚举结果为空** —— 表现为"拨号成功了但界面显示未连接"。

两个关键点：

1. **必须显式指定 `Pack = 4`**
   `ras.h` 使用 `#pragma pack(4)`，而 C# 默认按 8 字节对齐，会在结构体末尾多补 4 字节。

2. **本机 `RASENTRYNAMEW` 是不含 `guidEntry` 的旧版布局**
   直接照搬新版 SDK 头文件（含 `guidEntry`）会得到错误的大小。

诊断方法（不靠推算，直接问系统）：

```csharp
uint cb = 0, c = 0;
RasEnumConnections(IntPtr.Zero, ref cb, ref c);  // 首次传 null
int expected = (int)(cb / c);                    // 系统期望的单条大小
```

程序内部有 `WarnIfSizeMismatch()` 会在布局不符时写日志，避免以后再次出现这类难查的问题。

### 守护进程

守护模式**不是独立 exe**，而是同一个 exe 加 `--watchdog` 参数启动：

```
校园网拨号.exe --watchdog
```

`App.xaml.cs` 检测到该参数就进入后台守护逻辑（无窗口），通过全局 Mutex
（`Global\CampusNetWatchdog`）保证单实例。

### 数据文件位置

程序采用「绿色便携」策略：优先使用 **exe 所在目录**；若该目录不可写，回退到
`%LOCALAPPDATA%\CampusNetDialer`。

| 文件 | 内容 |
|---|---|
| `credential.bin` | 账号凭据（DPAPI 加密） |
| `settings.json` | 程序设置 |
| `watchdog.state` | 守护进程运行状态 |
| `logs/` | 拨号与守护日志 |

> ⚠️ **分享程序给别人时，请勿附带 `credential.bin` 和 `logs/`** —— 前者含你的账号凭据，
> 后者记录了连接名、账号掩码、IP 和拨号历史。

---

## 安全与隐私

- 账号密码通过 **Windows DPAPI（`ProtectedData`）** 加密，密钥绑定当前用户 + 机器，
  换机器无法解密。
- 程序**不进行任何网络通信**，不上传、不回传、不收集任何数据。
- 不需要管理员权限，不修改系统关键设置。
- 开机自启仅在 `HKCU\...\CurrentVersion\Run` 注册，可随时在程序内关闭。

---

## 许可证

[MIT License](LICENSE) —— 可自由使用、修改、分发。

---

## 免责声明

本工具为**非官方**的便利软件，与学校网络中心无关。
使用者需自行确保账号使用符合学校相关规定。
因使用本工具造成的任何问题，作者不承担责任。
