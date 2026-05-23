from fastapi import FastAPI, UploadFile, File
import tempfile
import subprocess
import os
import json
import traceback

app = FastAPI()

# =========================================
# PATHS
# =========================================

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

# =========================================
# HEALTHCHECK
# =========================================

@app.get("/")
def home():
    return {
        "status": "server running"
    }

# =========================================
# MAIN ENDPOINT
# =========================================

@app.post("/recognize")
async def recognize(file: UploadFile = File(...)):

    temp_path = None
    wav_path = None

    try:

        # =====================================
        # DEBUG INFO
        # =====================================

        print("=== REQUEST START ===")
        print("FILE NAME:", file.filename)
        print("ANALYZER PATH:", ANALYZER_PATH)
        print("ANALYZER EXISTS:", os.path.exists(ANALYZER_PATH))

        if os.path.exists(os.path.dirname(ANALYZER_PATH)):
            print(
                "ANALYZER DIR CONTENT:",
                os.listdir(os.path.dirname(ANALYZER_PATH))
            )

        # =====================================
        # SAVE INPUT FILE
        # =====================================

        suffix = os.path.splitext(file.filename)[1]

        with tempfile.NamedTemporaryFile(
            delete=False,
            suffix=suffix
        ) as tmp:

            content = await file.read()
            tmp.write(content)

            temp_path = os.path.abspath(tmp.name)

        print("TEMP PATH:", temp_path)
        print("TEMP EXISTS:", os.path.exists(temp_path))

        # =====================================
        # WAV OUTPUT
        # =====================================

        wav_path = temp_path + ".wav"

        # =====================================
        # FFMPEG CONVERT
        # =====================================

        ffmpeg_result = subprocess.run(
            [
                "ffmpeg",
                "-y",
                "-i",
                temp_path,
                wav_path
            ],
            capture_output=True,
            text=True
        )

        print("FFMPEG RETURN:", ffmpeg_result.returncode)
        print("FFMPEG STDERR:", ffmpeg_result.stderr)

        if ffmpeg_result.returncode != 0:
            return {
                "status": "error",
                "stage": "ffmpeg",
                "stderr": ffmpeg_result.stderr[-3000:]
            }

        print("WAV EXISTS:", os.path.exists(wav_path))
        print("WAV PATH:", wav_path)

        if not os.path.exists(wav_path):
            return {
                "status": "error",
                "stage": "wav_missing"
            }

        # =====================================
        # MAKE ANALYZER EXECUTABLE
        # =====================================

        try:
            os.chmod(ANALYZER_PATH, 0o755)
        except Exception as chmod_error:
            print("CHMOD ERROR:", str(chmod_error))

        # =====================================
        # RUN ANALYZER
        # =====================================

        result = subprocess.run(
            [
                ANALYZER_PATH,
                wav_path
            ],
            capture_output=True,
            text=True,
            timeout=3000
        )

        print("ANALYZER RETURN:", result.returncode)
        print("ANALYZER STDOUT:", result.stdout)
        print("ANALYZER STDERR:", result.stderr)

        if result.returncode != 0:
            return {
                "status": "error",
                "stage": "analyzer",
                "stderr": result.stderr[-3000:]
            }

        # =====================================
        # JSON PARSE
        # =====================================

        stdout = result.stdout.strip()

        try:
            analysis = json.loads(stdout)

        except Exception as json_error:

            return {
                "status": "error",
                "stage": "json_parse",
                "json_error": str(json_error),
                "raw_output": stdout[-3000:]
            }

        # =====================================
        # SUCCESS
        # =====================================

        return {
            "status": "ok",
            "analysis": analysis
        }

    # =========================================
    # GLOBAL FATAL ERROR
    # =========================================

    except Exception as e:

        trace = traceback.format_exc()

        print("=== FATAL SERVER ERROR ===")
        print(trace)

        return {
            "status": "fatal",
            "error": str(e),
            "trace": trace[-5000:]
        }

    # =========================================
    # CLEANUP
    # =========================================

    finally:

        for p in [temp_path, wav_path]:

            try:

                if p and os.path.exists(p):
                    os.remove(p)

            except Exception as cleanup_error:

                print(
                    "CLEANUP ERROR:",
                    str(cleanup_error)
                )