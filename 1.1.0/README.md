# Peter Minecraft launcher

一个**离线优先**的 Minecraft 启动器，单片 exe、纯 WinForms 自绘界面、没有任何第三方依赖（只要系统自带 .NET Framework 4.0+）。

- 当前版本：**1.1.0**
- 作者：Peter Brown
- 目标平台：Windows 7 SP1 / 8.1 / 10 / 11（32 位与 64 位系统都能跑）
- 专为 **Minecraft 1.16.5** 的便携环境打磨（其他版本只要是标准版本 JSON 也能启动）

## 功能

| 功能 | 说明 |
| --- | --- |
| 首次自动释放 `.minecraft` | 第一次启动自动把游戏目录"释放"出来，之后用标记文件记账，不会重复释放 |
| 一键安装 Java | 64 位安装包、32 位安装包各一个按钮，另有便携 JRE 一键部署 |
| Java 自动探测 | 注册表、Program Files、官方启动器 runtime、PATH、JAVA_HOME 全扫一遍，逐个跑 `java -version` 验证 |
| 微软正版登录 | 设备码登录（OAuth 2.0）与旧版弹窗登录两条链路：微软 → Xbox Live → XSTS → Minecraft |
| 离线昵称 | 自定义游戏内显示名，自动生成与 Java `UUID.nameUUIDFromBytes` 一致的离线 UUID |
| 内存分配 | 最小 / 最大内存滑块，写进 `config.ini`，启动时变成 `-Xms` / `-Xmx` |
| 日志查看 | 游戏输出、`latest.log`、启动器日志三合一查看 |
| 系统命令窗口 | 以管理员身份打开 cmd，等同旧版 bat 的「进入系统级命令窗口」 |
| 兼容旧配置 | `config.ini` 里 `username` / `maxmem` 键名与旧版批处理启动器一致 |

> **关于联网**：启动器只有「微软正版登录」会访问网络（登录微软、Xbox Live、Minecraft 服务）。
> 不下载游戏文件、不检查更新、不联机、不读写任何服务器列表。

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
│  ├─ Setup.exe                   Java 8 64 位安装包
│  ├─ Setup-x86.exe               Java 8 32 位安装包
│  ├─ jre8\                       便携 JRE 源（解压好的 JRE 8）
│  └─ portable_jre8\              部署后的便携 JRE
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

> 说明：csc 不会生成 Win32 版本资源块，所以资源管理器「属性 → 详细信息」里看不到文件版本；启动器界面内显示的是 1.1.0。

## 配置项（config.ini）

```ini
username=Player         # 离线昵称
minmem=1024M            # 最小内存，支持 1G / 1024M
maxmem=2048M            # 最大内存
java=                   # 选用的 java.exe 路径，留空自动探测
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
│  ├─ Http.cs            极简 HTTP 客户端（只给登录用）
│  ├─ Zip.cs             自研 ZIP/JAR 解包（只用 DeflateStream）
│  ├─ Log.cs             线程安全文件日志
│  ├─ JavaManager.cs     Java 探测 / 便携部署 / 安装包调起
│  ├─ MicrosoftAuth.cs   微软 + Xbox Live + Minecraft 登录链路
│  ├─ GameLauncher.cs    版本 JSON → 命令行，natives 解压，进程启动
│  └─ ReleaseManager.cs  首次释放 / 修复 .minecraft
└─ UI\
   ├─ Theme.cs           配色、字体、圆角绘制
   ├─ Widgets.cs         自绘按钮 / 卡片 / 滑块 / 输入框 / 进度条
   ├─ Pages.cs           六个页面（启动、账户、Java、设置、日志、关于）
   └─ MainForm.cs        无边框窗口、拖拽、导航、异步任务编排
```

## 已知限制

- 只支持标准版本 JSON（`versions\<id>\<id>.json` + `jar`），`inheritsFrom` 合并版（Forge / Fabric）暂未实现。
- ZIP 解包不支持 zip64（单个文件超过 4GB 的包会直接报错）。
- 1.16.5 需要 Java 8；用 Java 11+ 启动会崩，启动器会提示但不阻止。
- 微软登录依赖微软公开接口，接口或策略调整时可能失效。

## 已验证 / 未验证

已经实测过的部分：

- `build.bat` + 系统自带 csc 编译通过，exe 双击可启动，六个页面都能正常渲染。
- 用一份真实的 1.16.5（`versions\1.16.5` 完整 + `libraries` + `assets` + Temurin JRE 1.8.0_512）离线启动，
  14 秒后游戏窗口出现，`mc_log.txt` 里出现 `Setting user: Player`，说明 classpath、natives、离线 UUID 都是对的。

**没有实测的部分**（交付时请自己确认）：

- 微软正版登录整条链路（微软账户 → Xbox Live → XSTS → Minecraft 档案）。我没有购买了 Java 版的微软账户可用，
  Azure 应用客户端 ID 也需要你自己注册，这两条登录方式是否一次成功，我不敢替你打包票。
- Java 安装包按钮（`Java\Setup.exe` / `Setup-x86.exe`）会弹 UAC 装 Oracle JRE，这条只在有安装包的目录里才有意义。


## 更新日志

见 [更新日志.txt](更新日志.txt)。

## 许可证

MIT，见 [LICENSE](LICENSE)。
