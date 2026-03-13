# 使用说明（main.py）

![demo](docs/demo.png)

这是一个桌面悬浮层工具：读取 `result_cleaned.csv`，按快捷键触发识别，并在屏幕上显示结果。

## 0. 傻瓜式教程（给电脑小白）

按下面做，不用懂代码。

1. 打开项目文件夹：`sts2 db`
2. 在文件夹空白处右键，选择“在终端中打开”
3. 复制并执行下面 4 行（一次执行一行）：

```bash
python -m venv .venv
.venv\Scripts\activate
python -m pip install -U pip
python -m pip install -r requirements.txt
```

4. 启动程序：

```bash
python main.py
```

5. 进入游戏画面后按：
- `Ctrl+Q` 开始分析
- `Ctrl+H` 隐藏/显示窗口
- `Ctrl+Shift+X` 退出程序

如果第 3 步报错“`python` 不是内部或外部命令”，先安装 Python 3.12，并勾选 “Add Python to PATH”。

## 1. 常规安装（推荐 Python 3.12）

```bash
python -m venv .venv
.venv\Scripts\activate
python -m pip install -U pip
python -m pip install -r requirements.txt
```

确保项目目录里有 `result_cleaned.csv`，否则 `main.py` 没有数据可读。

## 2. 启动

```bash
python main.py
```

## 3. 快捷键

- `Ctrl+Q`：开始分析
- `Ctrl+H`：显示/隐藏悬浮窗
- `Ctrl+Shift+X`：退出程序

## 4. Demo

最小演示流程：

1. 启动：`python main.py`
2. 打开游戏或目标画面，保证三张卡区域可见
3. 按 `Ctrl+Q`
4. 悬浮窗显示卡牌名称、胜率和评级

示例终端输出（参考）：

```text
>>> 插件全功能已就绪
    [Ctrl+Q] 分析  [Ctrl+H] 隐藏/显示  [Ctrl+Shift+X] 退出
```

## 5. 常见问题

### 启动后没数据

- 检查 `result_cleaned.csv` 是否存在
- 检查 CSV 列名是否与代码一致（当前代码读取“卡牌名称”等列）

### 热键无响应

- 用管理员权限打开终端再运行 `main.py`
- 避免和其他软件全局热键冲突

### OCR 报错（Paddle 相关）

- 确认依赖版本和 `requirements.txt` 一致
- 若有旧版本残留，重装依赖后再试
