import csv
import os
from pathlib import Path
from paddleocr import PaddleOCR
import re

IMG_FOLDER = Path("screenshots")
OUTPUT_FILE = Path("../result_cleaned.csv")

def main():
    if not IMG_FOLDER.exists():
        print("文件夹不存在")
        return

    # 初始化 OCR
    ocr = PaddleOCR(use_angle_cls=True, lang="ch", use_gpu=False)
    
    all_rows = []
    image_files = [p for p in IMG_FOLDER.iterdir() if p.suffix.lower() in {".png", ".jpg", ".jpeg"}]

    print(f"开始深度解析 {len(image_files)} 张图片...")

    for img_path in image_files:
        try:
            result = ocr.ocr(str(img_path), cls=True)
            if not result or not result[0]: continue

            # 1. 按行分组数据
            lines = {} # 格式: {y_coordinate: [item1, item2, ...]}
            for line in result[0]:
                box = line[0]
                text = line[1][0]
                # 计算该文字块的纵向中心点
                y_center = (box[0][1] + box[2][1]) / 2
                
                # 寻找是否属于已有的行（上下误差15像素以内视为同一行）
                placed = False
                for line_y in lines.keys():
                    if abs(line_y - y_center) < 15:
                        lines[line_y].append({'x': box[0][0], 'text': text})
                        placed = True
                        break
                if not placed:
                    lines[y_center] = [{'x': box[0][0], 'text': text}]

            # 2. 解析每一行
            for y in sorted(lines.keys()):
                # 按横坐标 X 从左到右排序
                row_items = sorted(lines[y], key=lambda i: i['x'])
                row_texts = [i['text'] for i in row_items]

                # 判定条件：这一行必须包含至少一个百分比数字，且长度超过2
                if any('%' in t for t in row_texts) and len(row_texts) >= 2:
                    # 提取逻辑：
                    # 通常第一个是名字，后面跟着几个数字
                    name = row_texts[0]
                    # 提取所有看起来像百分比的数字
                    stats = []
                    for t in row_texts:
                        nums = re.findall(r"\d+\.?\d*", t)
                        if nums: stats.extend(nums)
                    
                    # 只要拿到了名字和至少一个数据就存入
                    if len(stats) >= 1:
                        # 补齐位，确保 CSV 格式整齐 [名字, 胜率, 选取率, 略过率]
                        data_row = [name] + stats[:3]
                        while len(data_row) < 4: data_row.append("0.0")
                        all_rows.append(data_row)
                        print(f"解析成功: {data_row}")

        except Exception as e:
            print(f"解析 {img_path.name} 出错: {e}")

    # 3. 保存并去重
    import pandas as pd
    df = pd.DataFrame(all_rows, columns=['卡牌名称', '胜率', '选取率', '略过率'])
    # 去掉重复识别的卡牌（因为截图可能有重叠）
    df.drop_duplicates(subset=['卡牌名称'], keep='first', inplace=True)
    df.to_csv(OUTPUT_FILE, index=False, encoding='utf_8_sig')
    
    print(f"\n🎉 整理完成！最终数据已存入: {OUTPUT_FILE}")

if __name__ == "__main__":
    main()