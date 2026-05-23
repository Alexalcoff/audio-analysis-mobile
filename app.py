from fastapi import FastAPI, UploadFile, File
import tempfile
import subprocess
import os
import json

app = FastAPI()

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

@app.get("/")
def home():
    return {"status": "server running"}


@app.post("/recognize")
async def recognize(file: UploadFile = File(...)):

    temp_path = None
    wav_path = None


    print(os.listdir(os.path.dirname(ANALYZER_PATH)))
    
    try:
        # 1. SAVE FILE
        suffix = os.path.splitext(file.filename)[1]

        with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as tmp:
            tmp.write(await file.read())
            temp_path = tmp.name

        temp_path = os.path.abspath(temp_path)
        wav_path = temp_path + ".wav"

        # 2. CONVERT
        ffmpeg = subprocess.run(
            ["ffmpeg", "-y", "-i", temp_path, wav_path],
            capture_output=True,
            text=True
        )

        if ffmpeg.returncode != 0:
            return {
                "status": "error",
                "stage": "ffmpeg",
                "stderr": ffmpeg.stderr[-2000:]
            }

        if not os.path.exists(wav_path):
            return {"status": "error", "stage": "wav_missing"}

        # 3. RUN ANALYZER
        result = subprocess.run(
            [ANALYZER_PATH, wav_path],
            capture_output=True,
            text=True,
            timeout=120
        )

        print("STDOUT:", result.stdout)
        print("STDERR:", result.stderr)

        if result.returncode != 0:
            return {
                "status": "error",
                "stage": "analyzer",
                "stderr": result.stderr[-2000:]
            }

        # 4. PARSE JSON
        try:
            analysis = json.loads(result.stdout.strip())
        except Exception:
            return {
                "status": "error",
                "stage": "json",
                "raw": result.stdout[-2000:]
            }

        return {
            "status": "ok",
            "analysis": analysis
        }

    finally:
        for p in [temp_path, wav_path]:
            if p and os.path.exists(p):
                try:
                    os.remove(p)
                except:
                    pass