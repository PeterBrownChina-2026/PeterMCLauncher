# Peter Minecraft launcher

一个**离线优先**的 Minecraft 启动器：单片 exe、纯 WinForms 自绘界面、没有任何第三方依赖（只要系统自带 .NET Framework 4.0+）。

- 当前版本：**1.1.1**
- 作者：Peter Brown
- 目标平台：Windows 7 SP1 / 8.1 / 10 / 11（32 位与 64 位系统都能跑）
- 1.16.5 上实测通过；1.17 及以后的标准版本 JSON 走同一套逻辑，`inheritsFrom`（Forge / Fabric / OptiFine）也做了合并

## 功能

| 功能 | 说明 |
| --- | --- |
| 首次自动释放 `.minecraft` | 第一次启动自动把游戏目录"释放"出来，之后用标记文件记账，不会重复释放 |
| **按游戏版本自动选 Java** | 一个按钮：读版本清单里的 `javaVersion.majorVersion`（没有就按版本号映射），本机有匹配的就直接切过去，没有就从镜像站下载对应 JRE 装到 `Java\jre{主版本}` |
| Java 自动探测 | 注册表、Program Files、Eclipse Temurin/Adoptium、Microsoft、Zulu、Corretto、官方启动器 runtime、PATH、JAVA_HOME 全扫一遍，逐个跑 `java -version` 验证 |
| 多版本支持 | 版本列表读 `versions\*` 的清单；`inheritsFrom` 会自动向上合并父版本（mainClass、libraries、arguments、assetIndex、jar、javaVersion），老格式 `minecraftArguments` 也兼容 |
| 微软正版登录 | 设备码登录（OAuth 2.0）与旧版弹窗登录两条链路：微软 → Xbox Live → XSTS → Minecraft |
| 离线昵称 | 自定义游戏内显示名，自动生成与 Java `UUID.nameUUIDFromBytes` 一致的离线 UUID |
| 内存分配 | 最小 / 最大内存滑块，写进 `config.ini`，启动时变成 `-Xms` / `-Xmx` |
| 日志查看 | 游戏输出、`latest.log`、启动器日志三合一查看（游戏运行中也能读） |
| 系统命令窗口 | 以管理员身份打开 cmd，等同旧版 bat 的「进入系统级命令窗口」 |
| 兼容旧配置 | `config.ini` 里 `username` / `maxmem` 键名与旧版批处理启动器一致 |

### Java 版本对照

| 游戏版本 | 需要的 Java | 说明 |
| --- | --- | --- |
| 1.16.5 及更早 | Java 8 | 只能 Java 8，换 11+ 会直接崩 |
| 1.17.x | Java 16（官方）→ 实际下 17 | Temurin 没有 Windows 版 JRE 16（官方已 EOL），17 实测可跑 |
| 1.18 ~ 1.20.4 | Java 17 | 版本清单里会写 `javaVersion` |
| 1.20.5 及以后 / 1.21 | Java 21 | 版本清单里会写 `javaVersion` |

判断顺序：**版本清单里的 `javaVersion.majorVersion`** → 读不到才按版本号映射表兜底。
只要本机已有不低于要求的 Java（1.16 那种只能 8 的除外），点按钮就只切换、不下载。

> **关于联网**：只做两件事——① 微软正版登录；② 点「根据我的游戏版本自动选择 Java」时从镜像站下载 JRE。
> 不下载游戏文件、不下载资源、不检查启动器更新、不联机。
> 下载源优先级：清华 TUNA 镜像（`mirrors.tuna.tsinghua.edu.cn/Adoptium`）→ Adoptium 官方 API 兜底。

## 目录结构（便携模式）

```
PeterLauncher\
├─ PeterMinecraftLauncher.exe     编译产物
├─ config.ini                     配置（自动生成）
├─ launcher.log                   启动器日志（自动生成）
├─ 更新日志.txt
├─ Games\
│  ├─ .minecraft\                 游戏目录（首次启动自动释放）
│  └─ mc_log.txt                  游戏输出
├─ Java\
│  ├─ jre8\                       自带便携 JRE 8（离线兜底，可直接用）
│  ├─ jre17\ / jre21\ / jre25\    自动下载的 JRE（点按钮后生成）
│  └─ cache\                      下载临时目录（下载完自动删）
└─ data\                          可选的释放源
   ├─ minecraft.zip               ← 有它就优先解包
   └─ .minecraft\                 ← 没有压缩包就整个复制
```

首次启动的释放优先级：`data\minecraft.zip` → `data\.minecraft` → `Games\.minecraft` → 空目录骨架。
释放完成后会在游戏目录写 `.pmcl_release` 标记，之后不再重复释放；「释放 / 修复 .minecraft」按钮可强制重来（不动存档）。

## 编译

不需要 Visual Studio，用系统自带的 C# 编译器即可：

```bat
build.bat
```

脚本会自动找 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（找不到就用 32 位的），产物在 `dist\PeterMinecraftLauncher.exe`。

也可以手动编译：

```bat
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /optimize+ ^
  /out:"dist\PeterMinecraftLauncher.exe" /win32icon:"assets\icon.ico" ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /recurse:src\*.cs
```

> 说明：csc 不会生成 Win32 版本资源块，所以资源管理器「属性 → 详细信息」里看不到文件版本；启动器界面内显示的是 1.1.1。

## 配置项（config.ini）

```ini
username=Player         # 离线昵称
minmem=1024M            # 最小内存，支持 1G / 1024M
maxmem=2048M            # 最大内存
java=                   # 选用的 java.exe 路径，留空自动探测并按版本匹配
gamedir=                # 留空使用 <启动器目录>\Games\.minecraft
clientid=               # 微软登录用的 Azure 应用客户端 ID
account=offline         # offline / ms
msname= / msuuid= / mstoken= / msrefresh= / msexpire=
lastversion=1.16.5
keepopen=0
```

## 微软正版登录

两条链路，任选一条：

1. **设备码登录（推荐）**：需要一个 Azure 应用的客户端 ID。
   - 打开 Azure 门户 → Microsoft Entra ID → 应用注册 → 新注册；
   - 账户类型选「仅此组织目录中的账户」之外的任意类型，平台选「移动和桌面应用程序」，勾选「启用公共客户端流」；
   - 把「应用程序(客户端) ID」填到「账户」页的客户端 ID 输入框；
   - 点「设备码登录」，浏览器打开微软页面后输入界面上的代码即可。
2. **旧版弹窗登录**：使用微软自家的公共客户端 ID（`00000000402b5328`），不需要自己注册 Azure 应用，
   但该链路较老，微软随时可能收紧，失败时请改用设备码登录。

登录成功后，`--accessToken` / `--uuid` / `--userType msa` 都会使用真实档案，单机与正版服务器都按正版身份进入。

**注意**：登录只验证「这个微软账户有没有购买 Java 版」，不会修改你的账户资料。

## 源码结构

```
src\
├─ Program.cs            入口、版本信息、全局异常兜底
├─ Core\
│  ├─ Config.cs          AppInfo 常量 + config.ini 读写
│  ├─ Json.cs            自研 JSON 解析器 + UTF-8 文本读写
│  ├─ Http.cs            极简 HTTP 客户端（登录 + 下载 JRE）
│  ├─ Zip.cs             自研 ZIP/JAR 解包（只用 DeflateStream）
│  ├─ Log.cs             线程安全文件日志
│  ├─ JavaManager.cs     Java 探测（多发行版、多主版本）
│  ├─ JavaProvisioner.cs 按游戏版本算 Java 需求 + 从镜像站下载/解压 JRE
│  ├─ MicrosoftAuth.cs   微软 + Xbox Live + Minecraft 登录链路
│  ├─ GameLauncher.cs    VersionMeta（inheritsFrom 合并）→ 命令行，natives 解压，进程启动
│  └─ ReleaseManager.cs  首次释放 / 修复 .minecraft
└─ UI\
   ├─ Theme.cs           配色、字体、圆角绘制
   ├─ Widgets.cs         自绘按钮 / 卡片 / 滑块 / 输入框 / 进度条
   ├─ Pages.cs           六个页面（启动、账户、Java、设置、日志、关于）
   └─ MainForm.cs        无边框窗口、拖拽、导航、异步任务编排
```

## 已知限制

- ZIP 解包不支持 zip64（单个文件超过 4GB 或条目数超 65535 的包会报错；JRE 包远达不到）。
- Temurin 没有 Windows 版 JRE 16，1.17 一律按 Java 17 处理。
- 版本清单要求 Java 8、本机却只有更高版本时只提示、不硬拦（1.16 用 Java 17 会崩，这个提示会写明）。
- 微软登录依赖微软公开接口，接口或策略调整时可能失效。

## 已验证 / 未验证

实测过的部分（1.1.1）：

- 自动下载 Java：从清华镜像取 `OpenJDK17U-jre_x64_windows_hotspot_17.0.21_9.zip`，**9 秒**下载完并解压到 `Java\jre17`，探测到 `17.0.21（64 位）`。
- 版本需求判断：1.16.5 → Java 8（读的是清单里的 `javaVersion`），1.17 → 17，1.18.2 / 1.20.4 → 17，1.21 → 21。
- 重新拼装的启动链路：真实 1.16.5（`versions\1.16.5` 完整 + `libraries` + `assets` + Temurin JRE 1.8.0_512）启动，**14 秒**出现游戏窗口。
- 界面：启动页显示「1.8.0_512 64 位（匹配 Java 8）」，Java 页显示「1.16.5 需要 Java 8，已经匹配」。
- 首次释放：从 `data\.minecraft` 复制 980MB，95 秒完成并写 `.pmcl_release`。

**没有实测的部分**（交付时请自己确认）：

- 微软正版登录整条链路（微软账户 → Xbox Live → XSTS → Minecraft）。我没有购买了 Java 版的微软账户可用，
  Azure 应用客户端 ID 也需要你自己注册。
- `inheritsFrom` 合并（Forge / Fabric / OptiFine）：代码已实现（父版本向上递归合并 mainClass / libraries / arguments / assetIndex / jar / javaVersion），
  但我手上没有 Forge 实例，只验证了原版清单这条路径正常。
- 1.17 及以后版本的完整启动（本地只有 1.16.5 的游戏文件，没有 1.18 / 1.21 的 libraries 与 assets）。


## 更新日志

见 [更新日志.txt](更新日志.txt)。

## 许可证

MIT，见 [LICENSE](LICENSE)。
