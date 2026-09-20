# Changelog · 架构细化批次（2026-09-20）

分支：`refactor/architecture-host-and-services`  
范围：可维护性 / 健壮性。不改课堂功能默认值（含自动杀进程仍默认开）。  
单测：40 通过（net472 MSTest；请用 VS MSBuild 编译后再 `dotnet test --no-build`）。

更细的分阶段记录在本机 `.ai/2026-09-20-1353-jiagou-refine/phase-A.md` … `phase-I.md`（未纳入版本库）。

---

## Added

- `Ink Canvas.Tests`：TimeMachine、SettingsStore、InkHistory、InkDocument、PowerPointSession、InkTool、ProcessWatchdog。
- `Domain/AppSurface`、`InkPage`、`InkDocument`：桌面 / 白板 / PPT 共用页模型。
- `History/InkHistoryController`：笔画监听、Apply、面积擦抬起提交。
- `Services/SettingsStore`：JSON 读写、500ms 防抖、关窗与未处理异常 Flush。
- `Services/PowerPointSession`：COM 单路 `TryAttach`、事件转发、Detach/Dispose。
- `Services/DispatcherGate`、`ProcessWatchdog`。
- `Input/InkTool` + `InkToolApplier`：工具栏/热键走 `ApplyTool`。
- `SettingsView.xaml`：设置面板从主窗剪出。
- `Settings.schemaVersion`（缺省 1，兼容旧 JSON）。

## Changed

- `LogHelper.NewLog(Exception)` 写入 Error；写日志加锁。
- 空栈 `TimeMachine.Undo` / `Redo` 返回 `null`，不再下标越界。
- `int currentMode` 改为 `AppSurface` + `IsPptShowActive`。
- PPT 笔画不再用 `MemoryStream[50]`，改走 `InkDocument`（跨页仍无撤销栈）。
- 设置开关仍调用 `SaveSettingsToFile()`，内部改为 `ScheduleSave()`。
- 浮动栏延时折叠等后台 hop 改为 `DispatcherTimer` / `Task`。
- 生产代码中约 61 处空 `catch` 改为分类日志（控制流仍吞掉）。

## Fixed

- 进白板时若当前白板页从未画过，桌面备份无法还原（`RestoreStrokes` 误看白板页是否为 null）。

## Removed

- 空壳 `SettingsPage.xaml`（设置只保留 `SettingsView`）。

## 未改（有意）

- 自动杀 PPTService / EasiNote 默认仍为开。
- `-o` 旧侧栏 UI 冻结，未删除。
- 形状橡皮筋、触点面积启发式仍直接写 `EditingMode`（不是用户点工具）。
- 墨迹回放仍有一处 `new Thread`。
- 未换 WPF `InkCanvas`，未做完整输入 Processor。

## 编译与测试

```
MSBuild.exe "Ink Canvas.Tests\Ink Canvas.Tests.csproj" /p:Configuration=Debug /p:Platform=AnyCPU /restore
dotnet test "Ink Canvas.Tests\Ink Canvas.Tests.csproj" --no-build -c Debug
```

`dotnet build` 编主工程会因 COM 互操作失败（MSB4803），需 Framework MSBuild。
