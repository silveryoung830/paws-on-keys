# Paws on Keys · 桌角猫

在 Windows 桌角养一只会跟你打字的猫。用照片取色，或从零捏猫，给它加上自家猫的脸斑和白爪。切回其他软件，猫仍会跟着你的鼠标推小鼠标、用爪子敲键，按下的键名会朝上飘。把光标放在它头顶，会看到猫眯起眼睛。

![桌角猫实际程序渲染预览](docs/preview.png)

[下载 Windows EXE](https://github.com/silveryoung830/paws-on-keys/releases/latest/download/PawsOnKeys.exe)

## 使用

1. 运行 `PawsOnKeys.exe`。首次启动会打开「我的猫」。
2. 选择照片或手动编辑。程序从照片中央估算底色，也可以点猫身或眼睛重新取色。花纹和局部标记由你手动校正。按预览效果调整花纹、眼睛、脸斑、白爪与体型。
3. 选择「放到桌面」。桌宠默认显示在右下角，鼠标点击可以穿过猫落到下面的应用。
4. 双击通知区域里的猫图标可以重新编辑。右键菜单可以隐藏上浮键名、暂停联动、改变位置或大小，以及退出。

字母和符号按 Windows 虚拟键映射到示意键盘，中文输入法组出的汉字不会成为上浮内容。用键盘输入敏感内容时，可以先关掉「显示上浮键名」或暂停联动。

## 从源码构建

Windows 10/11，.NET Framework 4.8，以及 Windows PowerShell 或 PowerShell 7。构建脚本使用系统的 C# 编译器和 WPF 程序集。无需管理员权限，也无需安装 NuGet 包。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\build\PawsOnKeys.exe
```

在具有桌面会话的 Windows 机器上运行测试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

## 实现

- `src/CatEditor.cs`、`src/PhotoPalette.cs` 提供本地照片取色、手动形象设定和预览。
- `src/PetView.cs` 绘制可缩放的猫与键鼠，并根据实时输入切换动作。
- `src/DesktopInput.cs` 使用 Windows Raw Input 接收物理输入。按键状态与短时动画只在内存中使用。
- `src/PetWindow.cs` 提供透明窗口、鼠标穿透、缩放和托盘控制。
- 用户设置储存在 `%LOCALAPPDATA%\PawsOnKeys\pet.xml`。设定文件保存外观参数和桌宠偏好，照片在本机读取。运行不需要账户或网络。

本版发布平台为 Windows。源码和程序内绘制的美术采用 [MIT 许可证](LICENSE)。
