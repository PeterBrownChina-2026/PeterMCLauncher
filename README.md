# PeterMCLauncher

一个**离线优先**的 Minecraft 启动器：单片 exe、纯 WinForms 自绘界面，C# / .NET Framework 4.0+，不依赖任何第三方库（系统自带 .NET Framework 即可编译运行）。

本仓库按版本归档各版的**源代码与启动器产物**，不含游戏文件与 Java 运行时。

- 作者：Peter Brown
- 目标平台：Windows 7 SP1 / 8.1 / 10 / 11（32 位与 64 位系统）
- 许可证：MIT（见各版本目录内 LICENSE）

## 版本目录

| 版本 | 目录 | Git 标签 | 要点 |
| --- | --- | --- | --- |
| **1.1.4**（当前最新） | [1.1.4/](1.1.4/) | `v1.1.4` | JRE 直接内置进 exe 单文件分发——四个 Temurin JRE（8/17/21/25）作为资源编译进程序，首次启动自动从 exe 内部解压到 `JRE\jreX`，不再需要外挂 `data\jreX.zip`；兼容旧外挂包 |
| 1.1.3 | [1.1.3/](1.1.3/) | `v1.1.3` | 内置便携 JRE 自动释放——启动时检查 `JRE\jre8/17/21/25` 是否完整，缺失从 `data\jreX.zip` 解压，已存在则跳过；随程序分发四个 Temurin JRE |
| 1.1.2 | [1.1.2/](1.1.2/) | `v1.1.2` | Java 扫描兼容 `JRE\` 目录名——之前只认 `Java\`，现在两个目录下的 `jre*` / `jdk*` 都能自动识别 |
| 1.1.1 | [1.1.1/](1.1.1/) | `v1.1.1` | 按游戏版本自动匹配 Java（8 / 11 / 17 / 21 / 25），缺 JRE 自动从镜像站下载；多版本扫描，`inheritsFrom`（Forge / Fabric / OptiFine）向上合并；兼容老格式 `minecraftArguments` |
| 1.1.0 | [1.1.0/](1.1.0/) | `v1.1.0` | 首个图形界面版本；首次启动自动释放 `.minecraft`；Java 8 一键安装 / 便携部署；微软正版登录（设备码 / 旧版弹窗两条链路）；离线昵称与离线 UUID；内存滑块；日志查看页 |

各版本的完整功能说明、配置项、源码结构与已知限制，见对应目录内的 README；历史改动见各目录下的「更新日志.txt」。

## 仓库里有什么

**已入库：**

- `src/` — 全部 C# 源代码（Core 业务逻辑 + UI 自绘界面）
- `build.bat` — 一键编译脚本
- `更新日志.txt` — 历史记录

**未入库（体积大 / 个人内容，不入库）：**

| 排除项 | 原因 |
| --- | --- |
| `Games/` | 游戏目录：存档、assets、libraries、versions、截图等游戏文件 |
| `Java/` | 便携 JRE 与安装包，可由系统或镜像提供；1.1.1 起启动器按版本自动下载 |
| `data/` | 首次启动的游戏释放源；1.1.3 起曾放内置 JRE zip 包，1.1.4 起 JRE 直接编译进 exe，外挂 zip 不再需要 |
| `assets/jre*.zip` | 四个 JRE 压缩包（共约 185MB），编译时通过 `build.bat` 的 `/resource` 打进 exe；体积大不入库 |
| `dist/` | 编译产物 exe |
| `config.ini` | 个人配置，可能包含微软登录令牌、账号与本地绝对路径 |
| `*.log` / `mc_log.txt` | 运行时生成的日志 |

## 编译

不需要 Visual Studio，用系统自带的 C# 编译器即可。进入对应版本目录执行：

```bat
build.bat
```

脚本会自动查找 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（找不到再退到 32 位），产物输出到 `dist\PeterMinecraftLauncher.exe`。

**1.1.4 起：** 想得到单文件分发版，把四个 JRE 压缩包放到版本目录下的 `assets\jre8.zip`、`jre17.zip`、`jre21.zip`、`jre25.zip`，再跑 `build.bat`，脚本会用 `/resource` 把它们编译进 exe；没有这几个包也能编译，只是 exe 不内置 JRE（运行时会回退到 `data\jreX.zip` 或联网下载）。

## 配置

复制对应版本目录下的 `config.example.ini` 为 `config.ini`，再按需要修改；`config.ini` 不会被提交到仓库。

## 功能概览

- 首次启动自动释放 `.minecraft`（zip 包 / 文件夹两种释放源，标记文件记账，不重复释放）
- 微软正版登录：设备码登录（OAuth 2.0）与旧版弹窗登录，链路为 微软 → Xbox Live → XSTS → Minecraft
- 离线昵称 + 与 Java `UUID.nameUUIDFromBytes` 一致的离线 UUID
- 最小 / 最大内存分配，写入 `config.ini`，启动时转为 `-Xms` / `-Xmx`
- 游戏输出、`latest.log`、启动器日志三合一查看
- 1.1.1：按版本清单 `javaVersion.majorVersion`（读不到再按版本号映射）自动选 Java，TUNA 镜像优先、Adoptium 官方 API 兜底下载 JRE
- 1.1.2：Java 扫描兼容 `JRE\` 目录，手动放置的便携 JRE 放在 `JRE\jre8`、`JRE\jre17` 等位置也能自动识别
- 1.1.3：内置 JRE 自动释放，启动时检查 `JRE\` 下四个版本是否完整，缺失从 `data\jreX.zip` 解压
- 1.1.4：JRE 直接内置进 exe，单文件分发，首次启动自动从 exe 内解压 `JRE\jre8/17/21/25`（已存在跳过）

## 免责声明

本项目与 Mojang Studios、Microsoft 无任何隶属或背书关系，Minecraft 商标归其所有者所有。本仓库仅分发第三方启动器的源代码与编译产物，不包含任何游戏本体文件。
