# Devin Bots 接入计划：EditMode 测试骨架自动化

> 本文与 [issue #1](https://github.com/rico-cell/unity-ar-inspection/issues/1) 对应，记录 Devin Bots 在本仓库的接入架构与工作流。

## 目标
让 Devin Bot 在每个 PR 上自动运行 EditMode 测试骨架，把追踪丢失（tracking loss）相关的回归全部挡在合并之前。

## 骨架架构
- **IARSessionEventBus 适配器**：所有 ARSession 事件经由可注入的轻量事件总线，EditMode 测试手动触发事件，不依赖真实 ARKit 子系统；FSM 只通过该总线订阅，teardown 因此可断言。
- **注入时钟**：追踪丢失超时用注入时钟而非 `Time.deltaTime`，恢复迁移无需真实帧驱动即可测试。

## 五组 teardown / 回归断言
1. **幂等销毁** — dispose 两次，无 NRE，状态机停在 `Disposed`
2. **零泄漏监听** — 反射遍历事件总线每个委托的调用列表，teardown 后计数为 0
3. **销毁后事件死寂** — dispose 后再触发 `sessionDidUpdate`，断言无迁移、无异常
4. **乱序中断/恢复** — `[TestCase]` 遍历 `(interrupted, relocalize, ended)` / `(interrupted, ended, relocalize)` / `(ended, 无 interrupt)` 等序列，断言回到 `Tracking` 且 `focusState` 复位，永不卡死在 `LossPending`
5. **注入时钟超时** — 追踪丢失超时迁移可脱离真实帧测试

## Bot 工作流（每个 PR）
1. Devin session 在每个 PR 上运行骨架套件
2. 抓到回归 → 自动写补丁、开修复 PR（附失败断言）并推送 trace 供 review
3. 解析 review 意见、推送直到绿灯
4. 夜间全量排列组合回归；新增会话事件时自动扩展乱序测试用例

## 首个 PR
分支 `devin/editmode-harness`：事件总线适配器 + 五组断言，今晚发出。
