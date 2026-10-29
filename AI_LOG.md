# AI log

记录搭建本仓库时实际使用过的提示，以及采用、修改或拒绝的结果。没有 API key 或密钥。

## P-001
- 目标：选定能在 Unity LTS 里打开的 AR Foundation 版本，而不是随便写一个包版本号。
### Prompt 原文
> Which Unity LTS in October 2026 matches AR Foundation, and what exact package versions does the official arfoundation-samples 6.3 branch pin in Packages/manifest.json?
### 结果
- 采用
- 对照 Unity 文档和 `arfoundation-samples` 的 `6.3` 分支 manifest。编辑器写为 6000.3.27f1（6.3 LTS 当前补丁），AR Foundation / ARCore / ARKit 都是 6.3.5，Input System 1.19.0，XR Plug-in Management 4.6.1。这组版本写进 `unity/Packages/manifest.json` 和 README。

## P-002
- 目标：决定 `GET /api/issues` 的响应形状，让 Unity 和 React 都能解析。
### Prompt 原文
> Return a raw JSON array from GET /api/issues so the API is more RESTful. Unity can parse it with JsonUtility, and React can use the array directly.
### 结果
- 修改后采用
- 裸数组对 React 没问题，但 Unity 的 `JsonUtility` 不能解析顶层数组。改成 `{"issues":[...]}`，空列表是 `{"issues":[]}`。验证：`server/internal/httpapi/handler_test.go` 的 `TestEmptyList` 和 `TestCreateListUpdateDelete` 断言这个形状；Unity 客户端只反序列化单条 issue 对象。

## P-003
- 目标：运行时创建 AR 相机的 Tracked Pose Driver，让真机跟踪头部姿态。
### Prompt 原文
> AddComponent&lt;TrackedPoseDriver&gt;() and rely on Reset() to bind &lt;XRHMD&gt;/centerEyePosition. That is enough for an AR Foundation camera built in code.
### 结果
- 未采用
- 核对了 Input System 1.19.0 的 `TrackedPoseDriver.cs`。`Reset()` 创建的是没有 binding 的空 `InputAction`，而且 `Reset()` 只在编辑器里调用，不会在 Player 里执行。只 `AddComponent` 时 `OnEnable` 绑不上姿态，真机画面不会跟着手机走。`ArRigBuilder` 改为显式绑定 `<XRHMD>/centerEyePosition`、`<XRHMD>/centerEyeRotation` 和 `<XRHMD>/trackingState`。这里没有 Unity，不能在真机上再跑一遍，所以 README 把真机跟踪列为必须由设备验证的部分。

## P-004
- 目标：给 Go 选一个进程重启后数据还在的 SQLite 驱动。
### Prompt 原文
> Use github.com/mattn/go-sqlite3 for the issues database so the API matches common Go examples.
### 结果
- 未采用
- `mattn/go-sqlite3` 需要 CGO。作业要求 `go test` 能过，并且可选 Docker 镜像最好是静态二进制。改用 `modernc.org/sqlite` v1.34.5。验证：在本机 Go 1.22.2 上 `go get` 成功，`go test ./...` 通过，其中 `TestIssueSurvivesReopen` 关掉数据库再打开，读回同一条问题。

## P-005
- 目标：点击表单按钮时不要同时在平面上放一个标记。
### Prompt 原文
> Ignore taps when EventSystem.current.IsPointerOverGameObject(touch.fingerId) is true. That is the usual AR Foundation sample check.
### 结果
- 修改后采用
- `fingerId` 配新 Input System 时经常对不上，按钮点击仍会落到 `ARRaycastManager`。`MarkerPlacer` 改为用 `EventSystem.RaycastAll` 检测屏幕坐标下的 UI，输入框聚焦或软键盘打开时也不放置。设备上新的触摸和鼠标模拟不会在同一次按下里放两个标记。这个判断写在 `Tick()` 里，不依赖别的脚本记得调用。真机点击仍需在 Unity 里确认，逻辑和 README 的说明一致。
