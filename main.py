import sys
import os
import pandas as pd
import keyboard
import numpy as np
import signal
import ctypes
import re
from PIL import ImageGrab
from paddleocr import PaddleOCR
from fuzzywuzzy import process
from PyQt6.QtWidgets import QApplication, QMainWindow, QLabel
from PyQt6.QtCore import Qt, QThread, pyqtSignal, QTimer

# --- 1. 环境修复与 DPI 意识 ---
try:
    # 强制 Windows 使用原始分辨率截图，解决缩放导致的错位
    ctypes.windll.shcore.SetProcessDpiAwareness(1)
except Exception:
    pass

# --- 2. 坐标校准 (0-1000 归一化比例) ---
# X: 增加避开能量豆, Y: 对准名字条
RELATIVE_REGIONS = [
    (230, 420, 180, 50),  # 左侧卡牌
    (415, 420, 180, 50),  # 中间卡牌
    (600, 420, 180, 50)   # 右侧卡牌
]

# --- 3. OCR 识别线程 ---
class OCRWorker(QThread):
    finished = pyqtSignal(list)

    def __init__(self, db_keys):
        super().__init__()
        os.environ["PADDLE_WITH_MKLDNN"] = "OFF"
        # 针对卡牌名字，关闭方向分类能稍微提速
        self.ocr = PaddleOCR(lang="ch", use_gpu=False, show_log=False, use_angle_cls=False)
        self.db_keys = db_keys

    def run(self):
        screen = QApplication.primaryScreen().size()
        sw, sh = screen.width(), screen.height()
        
        results = []
        for i, (rx, ry, rw, rh) in enumerate(RELATIVE_REGIONS):
            x, y = int(rx * sw / 1000), int(ry * sh / 1000)
            w, h = int(rw * sw / 1000), int(rh * sh / 1000)
            
            img = ImageGrab.grab(bbox=(x, y, x + w, y + h))
            debug_path = f"debug_{i}.png"
            img.save(debug_path)
            
            ocr_res = self.ocr.ocr(debug_path, cls=False)
            if ocr_res and ocr_res[0]:
                full_text = "".join([line[1][0] for line in ocr_res[0]])
                
                # 【核心修改】只提取识别结果中的中文字符
                # 这会自动过滤掉能量数字、升级加号以及可能出现的杂质符号
                clean_text = "".join(re.findall(r'[\u4e00-\u9fa5]', full_text))
                
                if clean_text:
                    match, score = process.extractOne(clean_text, self.db_keys)
                    # 因为过滤了 "+"，fuzzywuzzy 的匹配得分会更高
                    results.append(match if score > 45 else f"识别不清({clean_text})")
                else:
                    results.append("未检出文字")
            else:
                results.append("未检出内容")
        
        self.finished.emit(results)

# --- 4. 透明置顶窗口 ---
class OverlayWindow(QMainWindow):
    # 【修复】必须在这里定义信号
    toggle_signal = pyqtSignal()

    def __init__(self):
        super().__init__()
        self.card_db = {}
        self.load_data()
        self.init_ui()
        
        # 绑定 OCR 逻辑
        self.worker = OCRWorker(list(self.card_db.keys()))
        self.worker.finished.connect(self.display_results)

        # 绑定快捷键逻辑
        self.toggle_signal.connect(self.toggle_window)
        keyboard.add_hotkey('ctrl+q', self.start_analysis)
        keyboard.add_hotkey('ctrl+h', self.toggle_signal.emit) # 隐藏/显示
        keyboard.add_hotkey('ctrl+shift+x', self.close_app)    # 强行关闭

        print(">>> 插件全功能已就绪！")
        print("    [Ctrl+Q] 分析  [Ctrl+H] 隐藏/显示  [Ctrl+Shift+X] 退出")

    def load_data(self):
        if os.path.exists('result_cleaned.csv'):
            df = pd.read_csv('result_cleaned.csv')
            df['卡牌名称'] = df['卡牌名称'].astype(str).str.strip()
            self.card_db = df.set_index('卡牌名称').to_dict('index')
        else:
            print("错误：未找到数据库 result_cleaned.csv")

    def init_ui(self):
        screen = QApplication.primaryScreen().size()
        self.setWindowFlags(Qt.WindowType.WindowStaysOnTopHint | Qt.WindowType.FramelessWindowHint | Qt.WindowType.Tool)
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        self.setGeometry(0, 0, screen.width(), screen.height())

        self.labels = []
        for i, (rx, ry, rw, rh) in enumerate(RELATIVE_REGIONS):
            label = QLabel("待机中...", self)
            label.setStyleSheet("""
                color: #00FF00; font-family: 'Microsoft YaHei'; font-size: 16px; font-weight: bold;
                background-color: rgba(0, 0, 0, 200); border: 2px solid #555; border-radius: 8px; padding: 8px;
            """)
            lx = int(rx * screen.width() / 1000) + 100
            ly = int((ry + 310) * screen.height() / 1000) # 下调后的位置
            label.move(lx, ly)
            label.setFixedWidth(220)
            label.setFixedHeight(130)
            label.setAlignment(Qt.AlignmentFlag.AlignTop | Qt.AlignmentFlag.AlignLeft)
            label.setWordWrap(True)
            self.labels.append(label)

    def start_analysis(self):
        # 触发时自动确保窗口可见
        self.setVisible(True)
        for l in self.labels: l.setText("分析数据中...")
        self.worker.start()

    def toggle_window(self):
        is_now_visible = self.isVisible()
        self.setVisible(not is_now_visible)
        print(f"[!] 插件显示状态已切换: {'显示' if not is_now_visible else '隐藏'}")

    def display_results(self, names):
        for i, name in enumerate(names):
            data = self.card_db.get(name)
            if data:
                # 数据提取
                wr = data.get('胜率', 0)
                pr = data.get('选取率', 0)
                try:
                    wr_v = float(str(wr).replace('%', ''))
                    pr_v = float(str(pr).replace('%', ''))
                except: wr_v, pr_v = 0.0, 0.0

                # --- 核心改进：极其宽松的评价公式 ---
                # 选取率权重提升至 0.4，因为“群众的选择”很重要
                final_score = (wr_v * 0.6) + (pr_v * 0.4)
                
                # --- 阶梯大幅下调 ---
                # 在 StS2 目前的数据环境下，综合分过 48 绝对是顶级强卡了
                if final_score > 48: 
                    r, c = "S (必拿)", "#FFD700"
                elif final_score > 42: 
                    r, c = "A (优质)", "#E0E0E0"
                elif final_score > 35: 
                    r, c = "B (可用)", "#CD7F32"
                elif final_score > 28:
                    r, c = "C (平庸)", "#A0A0A0"
                else: 
                    r, c = "D (陷阱)", "#707070"

                self.labels[i].setText(
                    f"【{name}】\n"
                    f"胜率: {wr_v}%\n"
                    f"⭐ 综合: {final_score:.1f}\n"
                    f"评级: {r}"
                )
                self.labels[i].setStyleSheet(f"""
                    color: {c}; font-family: 'Microsoft YaHei'; font-size: 15px; font-weight: bold;
                    background-color: rgba(0, 0, 0, 230); border: 2px solid {c}; border-radius: 10px; padding: 10px;
                """)
            else:
                self.labels[i].setText(f"{name}\n暂无匹配数据")
                self.labels[i].setStyleSheet("color: #666; font-size: 14px; background-color: rgba(0, 0, 0, 150);")

    def close_app(self):
        """核弹级退出：不走 Qt 的温情清理，直接杀掉进程"""
        print("\n[!] 收到强制关闭指令，正在抹除进程...")
        try:
            # 1. 尝试正常停止线程（温和手段）
            if hasattr(self, 'worker') and self.worker.isRunning():
                self.worker.terminate()
        except:
            pass
        
        # 2. 暴力退出（强硬手段）
        # os._exit(0) 会立即停止 Python 解释器，不会触发任何清理钩子或 try/finally
        os._exit(0)

if __name__ == "__main__":
    # 修复 Ctrl+C 退出补丁
    signal.signal(signal.SIGINT, signal.SIG_DFL)
    app = QApplication(sys.argv)
    
    # 强制 Python 检查信号
    timer = QTimer()
    timer.start(500)
    timer.timeout.connect(lambda: None)

    window = OverlayWindow()
    window.show()
    sys.exit(app.exec())