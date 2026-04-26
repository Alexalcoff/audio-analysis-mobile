from fastapi import FastAPI, UploadFile, File

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