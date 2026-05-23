from fastapi import FastAPI, UploadFile, File
import tempfile
import subprocess
import os
import json

app = FastAPI()

# ==============================
# PATH TO ANALYZER
# ==============================
BASE_DIR = os.path.dirname(os.path.abspath(__file__))

ANALYZER_PATH = os.path.join(
    BASE_DIR,
    "audioanaly",
    "bin",
    "Release",
    "net8.0",
    "linux-x64",
    "publish",
    "audioanaly"
)

# ==============================
# HEALTH CHECK
# ==============================
@app.get("/")
def home():
    return {"status": "server running"}


# ==============================
# MAIN ENDPOINT
# ==============================
@app.post("/recognize")
async def recognize(file: UploadFile = File(...)):

    temp_path = None
    wav_path = None

    try:
        # =====================================
        # 1. SAVE TEMP FILE
        # =====================================
        suffix = os.path.splitext(file.filename)[1]

        with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as temp_audio:
            temp_audio.write(await file.read())
            temp_path = os.path.abspath(temp_audio.name)

        # WAV OUTPUT PATH
        wav_path = temp_path + ".wav"

        # =====================================
        # 2. CONVERT TO WAV (FFMPEG)
        # =====================================
        ffmpeg_result = subprocess.run(
            [
                "ffmpeg",
                "-y",
                "-i", temp_path,
                wav_path
            ],
            capture_output=True,
            text=True
        )

        if ffmpeg_result.returncode != 0:
            return {
                "status": "error",
                "stage": "ffmpeg",
                "stderr": ffmpeg_result.stderr[-2000:]
            }

        # =====================================
        # 3. RUN C# ANALYZER
        # =====================================
        result = subprocess.run(
            [ANALYZER_PATH, wav_path],
            capture_output=True,
            text=True,
            timeout=120
        )

        if result.returncode != 0:
            return {
                "status": "error",
                "stage": "analyzer",
                "stderr": result.stderr[-2000:]
            }

        # =====================================
        # 4. PARSE JSON OUTPUT
        # =====================================
        stdout = result.stdout.strip()

        try:
            analysis = json.loads(stdout)
        except Exception:
            return {
                "status": "error",
                "stage": "json_parse",
                "raw_output": stdout[-3000:]
            }

        # =====================================
        # 5. RESPONSE
        # =====================================
        return {
            "status": "ok",
            "analysis": analysis
        }

    finally:
        # =====================================
        # 6. CLEANUP
        # =====================================
        for p in [temp_path, wav_path]:
            if p and os.path.exists(p):
                try:
                    os.remove(p)
                except:
                    pass