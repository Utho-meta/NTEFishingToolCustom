# NTEFishingToolCustom

这是一个基于 OpenCV 图像识别的自动钓鱼辅助工具，适用于 Windows x64。

## 下载后怎么用

1. 下载 [v1.0.10 Windows x64 压缩包](https://github.com/Utho-meta/NTEFishingToolCustom/releases/download/v1.0.10/NTEFishingToolCustom-v1.0.10-win-x64.zip)。
2. 解压整个文件夹，不要只单独拿出 exe。
3. 运行 `启动工具.bat`。
4. 弹出管理员权限确认时选择“是”。
5. 游戏进程名默认是 `HTGame`，一般不用改。
6. 打开钓鱼点界面后，按下 `CapsLock` 开始自动运行；关闭 `CapsLock` 停止。

## 用户需要额外安装什么

推荐发布 `win-x64 self-contained` 版本。这个版本自带 .NET 运行时和 OpenCV 依赖，普通用户不需要安装 Visual Studio、.NET SDK 或 OpenCV。

注意事项：

- 只支持 Windows x64。
- 需要管理员权限运行，否则可能无法向游戏窗口发送按键和鼠标操作。请优先使用 `启动工具.bat`。
- 需要解压后运行，不能直接在压缩包里打开。
- 游戏分辨率、UI 缩放、画质或语言变化可能影响识别效果。

## 当前自动逻辑

- 自动点击“开始钓鱼”进入钓鱼界面。
- 结算后等待下一次咬钩时，点击“开始钓鱼”按钮附近并按 `F`，避免因延迟卡在结算页。
- 连续两次识别到鱼饵用完提示后，才打开商店购买并装备 5 块钱的“万能鱼饵”。
- 溜鱼时仅识别鱼条和黄针，用 `A/D` 让黄针追踪绿条中心。
- 钓鱼结算或失败提示出现后，点击空白区域关闭，再继续钓鱼循环。
- `CapsLock` 开启时运行，关闭时停止。

## 自己构建发布包

需要安装 .NET SDK 8 或更新版本，然后运行：

```powershell
.\build-release.ps1
```

构建完成后，发布包会出现在：

```text
..\github-release\
```

把生成的 zip 上传到 GitHub Releases 即可。

## 风险提示

这个工具通过图像识别和模拟输入操作游戏窗口。使用自动化工具可能违反游戏规则或触发反作弊风险，请自行判断并承担后果。
