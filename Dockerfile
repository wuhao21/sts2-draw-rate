FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender1 \
    libgomp1 \
    && rm -rf /var/lib/apt/lists/*

RUN python -m pip install --no-cache-dir --upgrade pip && \
    python -m pip install --no-cache-dir \
    paddlepaddle==3.1.1 \
    paddleocr==3.4.0 \
    pandas \
    pillow \
    opencv-python-headless

COPY . .

RUN mkdir -p /app/screenshots

CMD ["python", "ocr.py"]
