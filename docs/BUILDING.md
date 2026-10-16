# 构建与发布

## 环境

- Windows x64；
- .NET 10 SDK；
- PowerShell。

仓库的 `global.json` 指定 .NET SDK 10.0.100，并允许使用同一功能带（feature band）的更高版本，因此安装任意 .NET 10 SDK 均可构建。

## 构建

```powershell
dotnet restore .\KeyStar.slnx
dotnet build .\KeyStar.slnx -c Release --no-restore
```

## 回归测试

测试项目是一个无第三方测试框架的控制台程序：

```powershell
dotnet run --project .\tests\KeyStar.Core.Tests -c Release --no-build
```

也可以在构建后直接运行生成的可执行文件，两种方式等价：

```powershell
.\tests\KeyStar.Core.Tests\bin\Release\net10.0\KeyStar.Core.Tests.exe
```

进程返回码为 0 表示全部通过（当前共 49 项）。

## 应用图标

图标由脚本生成，不依赖设计稿：

```powershell
python .\scripts\make-icon.py
```

脚本会写出 `src/KeyStar.App/Assets/KeyStar.ico`（16–256 多尺寸）和 `KeyStar-icon-final.png`（1024×1024 预览）。图标配色与 `App.xaml` 中的主题令牌保持一致，修改配色时请同步脚本顶部的常量。

## 发行包

```powershell
.\scripts\publish.ps1
```

脚本会生成：

- `KeyStar-V1.0-full-win-x64.zip`：包含 .NET 桌面运行时；
- `KeyStar-V1.0-lite-win-x64.zip`：依赖 x64 .NET 10 Desktop Runtime；
- `SHA256SUMS.txt`：两个 ZIP 的 SHA-256。

每个压缩包内都带有 `README.md`（使用指南）与 `images` 目录（界面截图），`Data` 目录已预建，解压后即可运行。

## 无网络环境构建

如果构建机无法访问 nuget.org，NuGet 漏洞审计会因为取不到数据而把警告升级为错误（`TreatWarningsAsErrors=true`），可以显式关闭审计：

```powershell
dotnet build .\KeyStar.slnx -c Release -p:NuGetAudit=false
```

包需要已存在于本地 NuGet 缓存中。
