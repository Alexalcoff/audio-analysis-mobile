from fastapi import FastAPI, UploadFile

app = FastAPI()

@app.get("/")
def home():
    return {"status": "server running"}

@app.post("/recognize")
async def recognize(file: UploadFile):

    audio = await file.read()

    return {
        "title": "Test Song",
        "artist": "Test Artist",
        "youtube_link": "https://music.youtube.com/watch?v=test"
    }

