# Peter Minecraft launcher（Beta）

离线优先的 Minecraft 启动器，当前为 **1.1.4.b1 Beta** 版本。

- 单片 exe、纯 WinForms 自绘界面、无第三方依赖（只要 .NET Framework 4.0+）
- 界面：Win32 经典控制面板风格——纯白背景、系统原生标题栏、灰色单线边框、Tahoma、全部直角
- 作者：Peter Brown
- 目标平台：Windows 7 SP1 / 8.1 / 10 / 11

## 当前版本

**1.1.4.b1**（Beta）：Win32 风格视觉改版，功能与 1.1.3 一致。

详见 [1.1.4.b1/](1.1.4.b1/) 目录。

## 功能

- 首次自动释放 `.minecraft`（zip 包或文件夹，带进度和标记文件）
- 按游戏版本自动选 Java（读版本清单 `javaVersion.majorVersion`，本机有就切，没有就从镜像下载 JRE）
- Java 自动探测（注册表 / Program Files / 官方启动器 runtime / PATH / JAVA_HOME）
- 多版本支持（`inheritsFrom` 合并父版本，兼容 Forge / Fabric / OptiFine）
- 微软正版登录（设备码登录 + 旧版弹窗登录两条链路）
- 离线昵称 + 自动离线 UUID
- 内存滑块（min/max 写 config.ini）
- 日志查看（游戏输出 / latest.log / 启动器日志三合一）
- 系统命令窗口（管理员 cmd）

## 编译

```bat
cd 1.1.4.b1
build.bat
```

用系统自带 `csc.exe`，不需要 Visual Studio。

## 许可证

MIT。
