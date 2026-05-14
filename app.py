"""from fastapi import FastAPI, UploadFile, File

app = FastAPI()

@app.get("/")
def home():
    return {"status": "server running"}

@app.post("/recognize")
async def recognize(file: UploadFile = File(...)):
    audio = await file.read()

    print(f"Received file size: {len(audio)} bytes")

    return {
        "title": "Test Song",
        "artist": "Test Artist",
        "youtube_link": "https://music.youtube.com/watch?v=test"
    }
"""
from fastapi import FastAPI, UploadFile, File
import tempfile
import subprocess
import os
import json

app = FastAPI()

# Путь до C# анализатора
ANALYZER_PATH = "./audio_analyzer/AudioAnalyzer.exe"

@app.get("/")
def home():
    return {"status": "server running"}

@app.post("/recognize")
async def recognize(file: UploadFile = File(...)):

    # =====================================
    # 1. СОЗДАЕМ ВРЕМЕННЫЙ ФАЙЛ
    # =====================================

    suffix = os.path.splitext(file.filename)[1]

    with tempfile.NamedTemporaryFile(
        delete=False,
        suffix=suffix
    ) as temp_audio:

        content = await file.read()
        temp_audio.write(content)

        temp_path = temp_audio.name

    print(f"Saved temp file: {temp_path}")

    try:

        # =====================================
        # 2. ЗАПУСКАЕМ C# АНАЛИЗАТОР
        # =====================================

        result = subprocess.run(
            [
                ANALYZER_PATH,
                temp_path
            ],
            capture_output=True,
            text=True
        )

        # =====================================
        # 3. ПРОВЕРКА ОШИБОК
        # =====================================

        if result.returncode != 0:
            return {
                "error": "Analyzer crashed",
                "details": result.stderr
            }

        print("Analyzer output:")
        print(result.stdout)

        # =====================================
        # 4. ПАРСИНГ JSON
        # =====================================

        analysis = json.loads(result.stdout)

        return {
            "status": "ok",
            "analysis": analysis
        }

    finally:

        # =====================================
        # 5. УДАЛЕНИЕ ВРЕМЕННОГО ФАЙЛА
        # =====================================

        if os.path.exists(temp_path):
            os.remove(temp_path)