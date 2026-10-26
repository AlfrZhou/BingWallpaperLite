# 构建说明

## 环境

- Windows 10 / 11，64 位。
- Windows PowerShell 或 PowerShell 7。
- Windows 自带的 .NET Framework C# 编译器。
- [w64devkit](https://github.com/skeeto/w64devkit)，本地验证版本为 2.10.1。
- [Inno Setup](https://jrsoftware.org/isinfo.php)，本地验证版本为 6.7.3。

项目没有 NuGet 或 npm 依赖。编译工具不包含在仓库中。

## 工具位置

将 w64devkit 解压到 `build/tools/w64devkit`，确认 `build/tools/w64devkit/bin/gcc.exe` 存在。

Inno Setup 可放在 `build/tools/InnoSetup`，也可正常安装在系统默认位置。自定义路径可通过参数指定：

```powershell
.\Package.ps1 -IsccPath 'D:\Tools\Inno Setup\ISCC.exe'
```

## 编译与打包

在项目根目录执行：

```powershell
.\Build.ps1
.\Package.ps1
.\Verify-Release.ps1
```

`Package.ps1` 会先构建再生成安装包，因此只需要安装包时可以直接执行它。版本统一在 `Version.txt` 中定义。

| 目录 | 内容 |
| --- | --- |
| `build/release` | 当前程序与图标 |
| `build/obj` | 版本资源等中间文件 |
| `build/tests` | 测试程序 |
| `dist` | 安装包、SHA-256 校验与构建检查结果 |

## 测试

```powershell
.\Test.ps1
.\Test-Paint.ps1 -SkipBuild
.\Test-Popup.ps1 -SkipBuild
.\Test-UI.ps1 -SkipBuild
```

`Test.ps1` 构建程序，并检查配置、图片校验、旧图清理、状态写入、原生配置解析和定时计算。

其余检查分别覆盖自绘控件重绘、下拉菜单生命周期，以及设置窗口关闭、最小化、重复启动和托盘内存。菜单测试创建独立测试桌面，不切换当前桌面；界面测试使用临时数据目录并在结束后清理，不下载或应用壁纸。

`Verify-Release.ps1` 检查版本、校验文件、原生依赖和 ASLR / DEP 标志。它不是杀毒扫描，不提供安全软件检测结论。

## 代码结构

| 位置 | 职责 |
| --- | --- |
| `native/app.c` | 原生托盘、定时、辅助进程管理 |
| `native/config.h` | 托盘需要的配置字段解析 |
| `src/BingWallpaperLite.cs` | 配置、状态、下载、桌面与锁屏设置、图片清理 |
| `src/HelperEntry.cs` | 按参数进入设置或更新流程 |
| `ui/Settings.cs` | 设置窗口及托盘通信 |
| `ui/Controls.cs` | 自绘控件 |
| `installer/BingWallpaperLite.iss` | 当前用户安装、自启与卸载 |

托盘不加载 .NET。设置和更新共用一个程序文件，分别在按需启动的进程中执行。
