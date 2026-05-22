from fastapi import FastAPI, UploadFile, File
import tempfile
import subprocess
import os
import json

app = FastAPI()

# =====================================
# НАДЁЖНЫЙ ПУТЬ
# =====================================

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

ANALYZER_PATH = os.path.join(
    BASE_DIR,
    "audioanaly","bin","Debug","net8.0","audioanaly.exe"
)

@app.get("/")
def home():
    return {"status": "server running"}

@app.post("/recognize")
async def recognize(file: UploadFile = File(...)):

    suffix = os.path.splitext(file.filename)[1]

    # =====================================
    # TEMP FILE
    # =====================================

    with tempfile.NamedTemporaryFile(
        delete=False,
        suffix=suffix
    ) as temp_audio:

        temp_audio.write(await file.read())
        temp_path = temp_audio.name

    try:

        # =====================================
        # RUN ANALYZER (SAFE)
        # =====================================

        result = subprocess.run(
            [ANALYZER_PATH, temp_path],
            capture_output=True,
            text=True,
            timeout=60  
        )

        # =====================================
        # ERROR CHECK
        # =====================================

        if result.returncode != 0:
            return {
                "status": "error",
                "stderr": result.stderr[-1000:]  # ограничение
            }

        stdout = result.stdout.strip()

        # =====================================
        # SAFETY PARSE
        # =====================================

        try:
            analysis = json.loads(stdout)
        except Exception:
            return {
                "status": "error",
                "message": "Invalid JSON from analyzer",
                "raw_output": stdout[-2000:]
            }

        # =====================================
        # RESPONSE
        # =====================================

        return {
            "status": "ok",
            "analysis": analysis
        }

    finally:
        if os.path.exists(temp_path):
            os.remove(temp_path)