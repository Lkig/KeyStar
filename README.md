# KeyStar

KeyStar 是一款面向 Windows 的本地键盘、鼠标与手柄使用频率统计工具。它按分钟保存聚合计数，并提供时间范围、十分钟时段分布、全尺寸键盘热力图、手柄热力图和按键明细列表。

界面采用 NASA 朋克（NASA punk）视觉风格：深色仪表盘底色、蓝图网格、等宽字体编号模块和红色仪表条点缀。

![总览](docs/images/overview.png)

<details>
<summary>更多界面截图（键盘热力图 / 手柄热力图 / 记录列表）</summary>

![键盘与鼠标热力图](docs/images/keyboard.png)

![手柄热力图](docs/images/gamepad.png)

![记录列表](docs/images/records.png)

</details>

## 隐私原则

- 不记录输入内容；
- 不记录按键顺序；
- 不记录正在使用的应用或窗口；
- 不记录鼠标位置；
- 不记录手柄摇杆位置或扳机数值；
- 不需要账号，不连接服务器，不上传数据；
- 所有数据只保存在程序旁的 `Data` 目录。

## 主要功能

- Windows Raw Input 后台采集；
- 键盘按键和鼠标按键分别统计，鼠标包含左键、右键、中键（滚轮按下）、前/后侧键和滚轮上滚/下滚；
- 手柄按键统计（XInput，支持 Xbox 手柄和兼容手柄），并提供独立的手柄热力图；
- 重置记录时把当前数据整份归档到 `Data\archives`，不删除任何历史文件；
- 左右 Ctrl、Shift、Alt 等物理键分开计数；
- 长按自动连发去重；
- 每分钟聚合，30 秒自动保存；
- 今天、最近 10 分钟、7 天、30 天、全部和自定义范围；
- 10 分钟时段分布、全尺寸键盘热力图和手柄热力图；
- CSV 导出；
- 系统托盘后台运行、暂停、立即保存和完整退出；
- 自动记住窗口大小和位置，重启后恢复；
- 单实例运行和可选的当前用户登录自启动；
- 绿色版数据目录，可直接随程序文件夹迁移。

## 界面说明

主窗口由顶栏、数据范围工具条和左侧导航组成，导航分为四个页面：

| 页面 | 内容 |
| --- | --- |
| 总览 | 本次运行输入量、历史总数量、采集控制按钮、当前按下的键与设备状态 |
| 键盘 | 键盘与鼠标热力图，鼠标按键区排在键盘最右侧 |
| 手柄 | 手柄按键热力图，按手柄实际形状排列 |
| 记录 | 所选范围内的按键明细列表，可按次数排序、可只看有记录的键 |

数据范围工具条在任意页面都可用：选择预设范围（今天 / 最近 10 分钟 / 最近 7 天 / 最近 30 天 / 全部历史 / 自定义范围）后，热力图、时段分布和记录列表会同步更新。

## 系统要求

- Windows 10 或更高版本；
- x64 处理器；
- 源码构建需要 .NET 10 SDK；
- 轻量发行版需要 x64 `.NET 10 Desktop Runtime`；完整自包含版无需预装运行时。

## 从源码构建

```powershell
dotnet restore .\KeyStar.slnx
dotnet build .\KeyStar.slnx -c Release --no-restore
dotnet run --project .\tests\KeyStar.Core.Tests -c Release --no-build
```

生成完整自包含版和轻量版：

```powershell
.\scripts\publish.ps1
```

输出位于 `artifacts` 目录。

## 目录结构

```text
src/KeyStar.Core/          键位模型、归一化和计数逻辑
src/KeyStar.Storage/       SQLite、分钟聚合、设置与导出
src/KeyStar.App/           WPF 界面、Raw Input 和托盘生命周期
tests/KeyStar.Core.Tests/  无第三方测试框架的回归测试
docs/                       使用、构建、隐私、测试说明与界面截图
scripts/                    图标生成与发行包构建脚本
```

## 数据与迁移

数据保存在可执行文件旁的 `Data` 目录。迁移时完整复制程序文件夹即可。完整版和轻量版使用相同的数据格式。

数据库只保存每一分钟内各按键或按钮的累计次数，不保存事件顺序。更多说明见 [隐私与数据格式](docs/PRIVACY.md)。

## 游戏兼容性

普通桌面程序和大多数游戏可以使用 Raw Input 计数。如果游戏以管理员权限运行，请尝试以相同权限启动 KeyStar。部分采用独占输入或反作弊隔离的游戏可能不会向普通桌面程序提供输入事件，本项目不会尝试绕过反作弊机制。

## 参与贡献

提交问题或代码前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。安全相关问题请参考 [SECURITY.md](SECURITY.md)。

## 第三方组件

项目使用 Microsoft.Data.Sqlite 和 SQLitePCLRaw。详情见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

## 许可证

正式公开前需要由项目所有者选择并添加 `LICENSE` 文件。
