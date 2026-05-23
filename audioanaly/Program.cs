/*using System;
using System.Collections.Generic;
using System.Numerics;
using NAudio.Wave;
using MathNet.Numerics.IntegralTransforms;
using System.Text.Json;
using System.IO;
using System.Linq;
using NAudio.Utils;
using NAudio.Wave.SampleProviders;
using static System.Net.WebRequestMethods;
using System.Diagnostics;


//заметки на будущее
//звук передается в виде массива амплитуд сигналов в разные промежутки времени (номеры положения амплитуды по порядку)
//sample rate - измерение сигнала в секунду (Герцы)
//

public class AudioSimilarityAnalyzer
{*/
    // =========================
    // 1. LOAD AUDIO (PCM)
    //функция открывает аудио файл, декодирует его из предыдущего формата в массив чисел
    // =========================
    /*public static float[] LoadAudio(string path)
    {
        WaveStream reader;

        string ext =
            Path.GetExtension(path).ToLower();

        if (ext == ".mp3")
        {
            reader = new Mp3FileReader(path);
        }
        else if (ext == ".wav")
        {
            reader = new WaveFileReader(path);
        }
        else
        {
            throw new Exception(
                "Unsupported format: " + ext);
        }

        using (reader)
        {
            ISampleProvider sampleProvider =
                reader.ToSampleProvider();

            List<float> samples =
                new List<float>();

            float[] buffer = new float[4096];

            int read;

            while ((read =
                sampleProvider.Read(
                    buffer,
                    0,
                    buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                    samples.Add(buffer[i]);
            }

            return samples.ToArray();
        }
    }*/
    /*public static float[] LoadAudio(string path)
    {
        string wavPath =
            Path.ChangeExtension(
                Path.GetTempFileName(),
                ".wav"
            );

        var ffmpeg =
            new ProcessStartInfo
            {
                FileName = "ffmpeg",

                Arguments =
                    $"-i \"{path}\" -ac 1 -ar 44100 \"{wavPath}\" -y",

                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

        using var process =
            Process.Start(ffmpeg);

        process.WaitForExit();

        using var reader =
            new WaveFileReader(wavPath);

        var samples = new List<float>();

        var provider =
            reader.ToSampleProvider();

        float[] buffer = new float[4096];

        int read;

        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
                samples.Add(buffer[i]);
        }

        System.IO.File.Delete(wavPath);

        return samples.ToArray();
    }*/

    /*
    public static float[] LoadAudio(string path)
    {
        using var reader =
            new WaveFileReader(path);

        var samples = new List<float>();

        var provider =
            reader.ToSampleProvider();

        float[] buffer = new float[4096];

        int read;

        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
                samples.Add(buffer[i]);
        }

        return samples.ToArray();
    }

    public static int GetSampleRate(string path)
    {
        string ext =
            Path.GetExtension(path).ToLower();

        WaveStream reader;

        if (ext == ".mp3")
        {
            reader = new Mp3FileReader(path);
        }
        else if (ext == ".wav")
        {
            reader = new WaveFileReader(path);
        }
        else
        {
            throw new Exception(
                "Unsupported format");
        }

        using (reader)
        {
            return reader.WaveFormat.SampleRate;
        }
    }

    // =========================
    // 2. FRAMING //музыка меняется со временем а следовательно режем эту музыку на маленькие промежутки, так как преобразованеи фурье не может все вместе считать и определить что - где
    // =========================
    public static List<float[]> Frame(float[] signal, int frameSize = 2048, int hop = 512) //стандартные; длина окна в 4 раза больше шага смещения начала следующего окна,
                                                                                           //шаг нужен иначе теряется инфа
    {
        var frames = new List<float[]>();

        for (int i = 0; i + frameSize < signal.Length; i += hop)
        {
            var frame = new float[frameSize];
            Array.Copy(signal, i, frame, 0, frameSize); //копирует кусок сигнала
            frames.Add(frame);
        }

        return frames;
    }

    // =========================
    // 3. FFT
    //преобразование амплитуды по времени в амплитуды по частотам
    // =========================
    public static Complex[] FFT(float[] frame)
    {
        var complex = new Complex[frame.Length];

        for (int i = 0; i < frame.Length; i++)
            complex[i] = new Complex(frame[i], 0);

        Fourier.Forward(complex, FourierOptions.Matlab); //дап
        return complex; //соответственно каждый элемент хранит амплитуду и фазу
    }

    //позже воспользуемся fft[i].Magnitude (это модуль одного из значений выше типо, модуль комплексного числа)

    // =========================
    // 4. CHROMA FEATURE (melody proxy)
    //теперь исследуем частотность появления различных нот, благодаря этому задаем еще 12 параметров для отсечения лишних звуковых дорожек
    // =========================
    public static float[] Chroma(Complex[] fft, int sampleRate)
    {
        float[] chroma = new float[12];

        for (int i = 1; i < fft.Length / 2; i++) //только до половины, ибо вторая половина копирует перву, FFT симметрична
        {
            double freq = i * sampleRate / (double)fft.Length; //f_i = i * F_s / N; F_s - sample rate, N - размер FFT
            if (freq < 50) continue;

            int note = (int)(12 * Math.Log(freq / 440.0, 2) + 69) % 12; //формула нот, плюс убираем разницу в октавах, может ктото перепевает в другом формате песню;
            if (note < 0) note += 12;

            chroma[note] += (float)fft[i].Magnitude;
        }

        float norm = 0;

        for (int i = 0; i < 12; i++)
            norm += chroma[i] * chroma[i];

        norm = (float)Math.Sqrt(norm);

        if (norm > 0)
        {
            for (int i = 0; i < 12; i++)
                chroma[i] /= norm;
        }

        return chroma;
    }

    // =========================
    // 5. CHROMA SEQUENCE
    //здесь проделывание предыдущего для каждого окна
    // =========================
    public static List<float[]> ExtractChromaSequence(float[] samples, int sampleRate)
    {
        var frames = Frame(samples);
        var result = new List<float[]>();

        foreach (var frame in frames)
        {
            var fft = FFT(frame);
            var chroma = Chroma(fft, sampleRate);
            result.Add(chroma);
        }

        return result;
    }

    // =========================
    // 6. DISTANCE BETWEEN FRAMES
    // пишем чтобы понять насколько два chroma вектора отличаются
    // =========================
    public static double Distance(float[] a, float[] b)
    {
        double sum = 0;

        for (int i = 0; i < a.Length; i++)
            sum += (a[i] - b[i]) * (a[i] - b[i]);

        return Math.Sqrt(sum); //норма по евклиду, ну соответствено если ноты похожи то и расстояние маленькое
    }
    */
    // =========================
    // 7. DTW (Dynamic Time Warping) растяжение по времени в простонаречье
    //ЕСЛИ:
    //два трека играют с разной скоростью
    //имеют разные сдвиги
    //имеют так же какие-то паузы
    //ТО это теоретически решает нашу проблему

    //каждая клетка хранит минимальную стоимость пути
    // =========================
    /*public static double DTW(List<float[]> A, List<float[]> B)
    {
        int n = A.Count;
        int m = B.Count;



        double[,] dp = new double[n, m]; //Dynamic Programmic matrix

        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
                dp[i, j] = double.MaxValue; //для поиска минимума

        dp[0, 0] = Distance(A[0], B[0]);

        for (int i = 1; i < n; i++)
            dp[i, 0] = dp[i - 1, 0] + Distance(A[i], B[0]);

        for (int j = 1; j < m; j++)
            dp[0, j] = dp[0, j - 1] + Distance(A[0], B[j]);

        for (int i = 1; i < n; i++)
        {
            for (int j = 1; j < m; j++)
            {
                double cost = Distance(A[i], B[j]);

                dp[i, j] = cost + Math.Min(
                    dp[i - 1, j],
                    Math.Min(dp[i, j - 1], dp[i - 1, j - 1]) //пути прохода по матрице, справа, снизу по диагонали;
                );
            }
        }

        return dp[n - 1, m - 1]/(m+n); //ищет лучший путь сопоставления двух последовательностей
    }*/

    /*public static double DTW(List<float[]> A, List<float[]> B)
    {
        int n = A.Count;
        int m = B.Count;

        int w = Math.Max(10, Math.Abs(n - m) + 50); // Sakoe-Chiba window

        double[] prev = new double[m];
        double[] curr = new double[m];

        for (int j = 0; j < m; j++)
            prev[j] = double.MaxValue;

        prev[0] = Distance(A[0], B[0]);

        for (int i = 1; i < n; i++)
        {
            for (int j = 0; j < m; j++)
                curr[j] = double.MaxValue;

            int start = Math.Max(0, i - w);
            int end = Math.Min(m - 1, i + w);

            for (int j = start; j <= end; j++)
            {
                double cost = Distance(A[i], B[j]);

                double minPrev = prev[j];

                if (j > 0)
                    minPrev = Math.Min(minPrev, curr[j - 1]);

                if (j > 0)
                    minPrev = Math.Min(minPrev, prev[j - 1]);

                curr[j] = cost + minPrev;
            }

            var temp = prev;
            prev = curr;
            curr = temp;
        }

        return prev[m - 1]/m;
    }*/

    /*
    public static double DTW(List<float[]> A, List<float[]> B)
    {
        int n = A.Count;
        int m = B.Count;

        int w = Math.Max(10, Math.Abs(n - m) + 50);

        double[] prev = new double[m];
        double[] curr = new double[m];

        int[] prevLen = new int[m];
        int[] currLen = new int[m];

        // init
        for (int j = 0; j < m; j++)
        {
            prev[j] = double.MaxValue;
            prevLen[j] = 0;
        }

        prev[0] = Distance(A[0], B[0]);
        prevLen[0] = 1;

        for (int i = 1; i < n; i++)
        {
            for (int j = 0; j < m; j++)
            {
                curr[j] = double.MaxValue;
                currLen[j] = 0;
            }

            int start = Math.Max(0, i - w);
            int end = Math.Min(m - 1, i + w);

            for (int j = start; j <= end; j++)
            {
                double cost = Distance(A[i], B[j]);

                double bestCost = prev[j];
                int bestLen = prevLen[j];

                if (j > 0 && curr[j - 1] < bestCost)
                {
                    bestCost = curr[j - 1];
                    bestLen = currLen[j - 1];
                }

                if (j > 0 && prev[j - 1] < bestCost)
                {
                    bestCost = prev[j - 1];
                    bestLen = prevLen[j - 1];
                }

                curr[j] = cost + bestCost;
                currLen[j] = bestLen + 1;
            }

            var tmp = prev; prev = curr; curr = tmp;
            var tmpL = prevLen; prevLen = currLen; currLen = tmpL;
        }

        double finalCost = prev[m - 1];
        int finalLen = Math.Max(1, prevLen[m - 1]);

        return finalCost / finalLen;
    }

    public static double CosineSimilarity( //Сравнивание по векторам, скалярное проищведение поделенное на две нормы
    double[] a,
    double[] b)
    {
        double dot = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
            return 0;

        return dot /
            (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    public static double[] MergeFeatures(    //Обьединение нескольких векторов в один
    TrackFeatures t)
    {
        List<double> v1 = new(), v2 = new(), v3 = new();


        // MeanChroma
        foreach (var x in t.MeanChroma)
            v1.Add(x);

        // MFCC
        foreach (var x in t.MeanMFCC)
            v2.Add(x);

        // Delta MFCC
        foreach (var x in t.DeltaMFCCMean)
            v3.Add(x);

        double[] v11 = v1.ToArray();
        double[] v22 = v2.ToArray();
        double[] v33 = v3.ToArray();

        v11 = Normalize(v11);
        v22 = Normalize(v22);
        v33 = Normalize(v33);

        List<double> combined = new();

        combined.AddRange(v11);
        combined.AddRange(v22);
        combined.AddRange(v33);

        double[] finalVector = combined.ToArray();

        combined.Add(t.Energy);

        return combined.ToArray();
    }

    public static List<float[]> LoadChromaFromBin(         //Получение информации из бинарников трека
    string path)
    {
        using var br =
            new BinaryReader(
                System.IO.File.OpenRead(path));

        int count = br.ReadInt32();

        var result =
            new List<float[]>();

        for (int i = 0; i < count; i++)
        {
            float[] frame = new float[12];

            for (int j = 0; j < 12; j++)
            {
                frame[j] =
                    br.ReadByte() / 255f;
            }

            result.Add(frame);
        }

        return result;
    }*/

    /*public static List<Candidate>
FindTopCandidates(             //поиск топ 10  по косиносному преобразовани.
    TrackFeatures query,
    string jsonFolder,
    int top = 10)
    {
        var result =
            new List<Candidate>();

        string[] jsons =
            Directory.GetFiles(
                jsonFolder,
                "*.json");

        double[] q =
            MergeFeatures(query);

        foreach (string json in jsons)
        {
            try
            {
                var track =
                    JsonSerializer.Deserialize<TrackFeatures>(
                        System.IO.File.ReadAllText(json));

                double[] v =
                    MergeFeatures(track);

                double sim =
                    CosineSimilarity(q, v);

                result.Add(
                    new Candidate
                    {
                        Track = track,
                        Score = sim
                    });
            }
            catch
            {

            }
        }

        return result
            .OrderByDescending(x => x.Score)
            .Take(top)
            .ToList();
    }*/
/*
    private static double[] Normalize(double[] v)
    {
        double mean = v.Average();
        double std = Math.Sqrt(v.Select(x => (x - mean) * (x - mean)).Average());

        if (std < 1e-12)
            return v;

        for (int i = 0; i < v.Length; i++)
            v[i] = (v[i] - mean) / std;

        return v;
    }

    private static double[] NormalizeParts(double[] vector)
    {
        if (vector.Length != 39)
            throw new ArgumentException("Вектор должен иметь длину 39");

        // Разделение
        double[] part1 = vector.Take(12).ToArray();
        double[] part2 = vector.Skip(12).Take(13).ToArray();
        double[] part3 = vector.Skip(25).Take(13).ToArray();
        double[] part4 = vector.Skip(38).Take(1).ToArray();

        // Нормализация первых трёх частей
        part1 = Normalize(part1);
        part2 = Normalize(part2);
        part3 = Normalize(part3);

        // Объединение обратно
        return part1
            .Concat(part2)
            .Concat(part3)
            .Concat(part4)
            .ToArray();
    }

    public static List<Candidate> FindTopCandidates(
    TrackFeatures query,
    string jsonFolder,
    int top = 10)
    {
        var result = new List<Candidate>();

        string[] jsons = Directory.GetFiles(jsonFolder, "*.json");

        double[] q = Normalize(NormalizeParts(MergeFeatures(query)));

        foreach (string json in jsons)
        {
            try
            {
                string text = System.IO.File.ReadAllText(json);

                var track = JsonSerializer.Deserialize<TrackFeatures>(text);

                if (track == null)
                    continue;

                double[] v = (MergeFeatures(track));

                double sim = CosineSimilarity(q, v);

                result.Add(new Candidate
                {
                    Track = track,
                    Score = sim
                });
            }
            catch (Exception ex)
            {
                // лучше логировать, а не молча игнорировать
                Console.Error.WriteLine($"JSON error: {json} -> {ex.Message}");
            }
        }

        return result
            .OrderByDescending(x => x.Score)
            .ThenBy(x => Guid.NewGuid()) // ломает "залипание" при равных score
            .Take(top)
            .ToList();
    }*/

    /*public static string FindBestMatches(
    string queryFile,
    string jsonFolder, string binFolder)
    {
        var results =
            new List<SearchResult>();

        // ============================
        // QUERY
        // ============================

        var samples =
            LoadAudio(queryFile);

        //using var reader =
        //  new AudioFileReader(queryFile);

        //int sr =
        //  reader.WaveFormat.SampleRate;

        //var chroma =
        //  ExtractChromaSequence(samples, sr);

        int sampleRate =
    GetSampleRate(queryFile);

        var chroma =
            ExtractChromaSequence(
                samples,
                sampleRate);

        // ============================
        // MEAN CHROMA
        // ============================

        float[] meanChroma =
            new float[12];

        foreach (var c in chroma)
        {
            for (int i = 0; i < 12; i++)
                meanChroma[i] += c[i];
        }

        for (int i = 0; i < 12; i++)
            meanChroma[i] /= chroma.Count;

        var query =
            new TrackFeatures
            {
                MeanChroma = meanChroma,
                MeanMFCC = new double[13],
                DeltaMFCCMean = new double[13],
                Energy =
                    samples
                    .Select(x => x * x)
                    .Average()
            };

        // ============================
        // FAST SEARCH
        // ============================

        var top =
            FindTopCandidates(
                query,
                jsonFolder,
                5);

        // ============================
        // DTW
        // ============================

        foreach (var x in top)
        {
            try
            {
                string binPath =
                    Path.Combine(
                        binFolder,
                        x.Track.BinFile);

                var other =
                    LoadChromaFromBin(binPath);

                double dtw =
                    DTW(chroma, other);

                results.Add(
                    new SearchResult
                    {
                        Title =
                            x.Track.Title,

                        Similarity =
                            1.0 / (1.0 + dtw),

                        StreamUrl =
                            x.Track.StreamUrl
                    });
            }
            catch
            {

            }
        }

        // ============================
        // SORT
        // ============================

        results =
            results
            .OrderByDescending(x => x.Similarity)
            .ToList();

        return JsonSerializer.Serialize(
            results,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }*/
/*
    public static string FindBestMatches(
    string queryFile,
    string jsonFolder,
    string binFolder)
    {
        var results =
            new List<SearchResult>();

        // ============================
        // TRY FIND SAME TRACK IN BASE
        // ============================

        TrackFeatures existingTrack = null;
        List<float[]> existingChroma = null;

        string queryName =
            Path.GetFileNameWithoutExtension(queryFile)
            .ToLower()
            .Trim();

        string[] jsons =
            Directory.GetFiles(jsonFolder, "*.json");

        foreach (string json in jsons)
        {
            try
            {
                var track =
                    JsonSerializer.Deserialize<TrackFeatures>(
                        System.IO.File.ReadAllText(json));

                if (track == null)
                    continue;

                string dbName =
                    Path.GetFileNameWithoutExtension(track.Title ?? "")
                    .ToLower()
                    .Trim();

                string audioName =
                    Path.GetFileNameWithoutExtension(track.AudioPath ?? "")
                    .ToLower()
                    .Trim();

                if (dbName == queryName ||
                    audioName == queryName)
                {
                    existingTrack = track;

                    string binPath =
                        Path.Combine(
                            binFolder,
                            track.BinFile);

                    existingChroma =
                        LoadChromaFromBin(binPath);

                    break;
                }
            }
            catch
            {

            }
        }

        // ============================
        // QUERY
        // ============================

        float[] samples;
        int sampleRate;
        List<float[]> chroma;
        TrackFeatures query;

        // =========================================
        // USE EXISTING FEATURES IF TRACK FOUND
        // =========================================

        if (existingTrack != null &&
            existingChroma != null)
        {
            query = existingTrack;

            chroma = existingChroma;
        }
        else
        {
            samples =
                LoadAudio(queryFile);

            sampleRate =
                GetSampleRate(queryFile);

            chroma =
                ExtractChromaSequence(
                    samples,
                    sampleRate);

            // ============================
            // MEAN CHROMA
            // ============================

            float[] meanChroma =
                new float[12];

            foreach (var c in chroma)
            {
                for (int i = 0; i < 12; i++)
                    meanChroma[i] += c[i];
            }

            for (int i = 0; i < 12; i++)
                meanChroma[i] /= chroma.Count;

            query =
                new TrackFeatures
                {
                    MeanChroma = meanChroma,
                    MeanMFCC = new double[13],
                    DeltaMFCCMean = new double[13],
                    Energy =
                        samples
                        .Select(x => x * x)
                        .Average()
                };
        }

        // ============================
        // FAST SEARCH
        // ============================

        var top =
            FindTopCandidates(
                query,
                jsonFolder,
                5);

        // ============================
        // FORCE SELF MATCH TO TOP
        // ============================

        if (existingTrack != null)
        {
            results.Add(
                new SearchResult
                {
                    Title = existingTrack.Title,
                    Similarity = 1.0,
                    StreamUrl = existingTrack.StreamUrl
                });
        }

        // ============================
        // DTW
        // ============================

        foreach (var x in top)
        {
            try
            {
                // skip duplicate self-match
                if (existingTrack != null &&
                    x.Track.Title == existingTrack.Title)
                {
                    continue;
                }

                string binPath =
                    Path.Combine(
                        binFolder,
                        x.Track.BinFile);

                var other =
                    LoadChromaFromBin(binPath);

                double dtw =
                    DTW(chroma, other);

                results.Add(
                    new SearchResult
                    {
                        Title =
                            x.Track.Title,

                        Similarity =
                            1.0 / (1.0 + dtw),

                        StreamUrl =
                            x.Track.StreamUrl
                    });
            }
            catch
            {

            }
        }

        // ============================
        // SORT
        // ============================

        results =
            results
            .OrderByDescending(x => x.Similarity)
            .ToList();

        return JsonSerializer.Serialize(
            results,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }
    // =========================
    // 8. MAIN COMPARISON API
    // =========================
    public static double Compare(string fileA, string fileB)
    {
        var samplesA = LoadAudio(fileA);
        var samplesB = LoadAudio(fileB);

        //using var r1 = new AudioFileReader(fileA);
        //using var r2 = new AudioFileReader(fileB);

        int sampleRateA =
    GetSampleRate(fileA);

        var chromaA =
            ExtractChromaSequence(
                samplesA,
                sampleRateA);

        int sampleRateB =
    GetSampleRate(fileB);

        var chromaB =
            ExtractChromaSequence(
                samplesB,
                sampleRateB);

        //var chromaA = ExtractChromaSequence(samplesA, r1.WaveFormat.SampleRate);
        //var chromaB = ExtractChromaSequence(samplesB, r2.WaveFormat.SampleRate);

        return DTW(chromaA, chromaB);
    }*/

/*
    public static double[] Merging(string fileA)
    {
        bool alreadyProcessed = false;

        string[] existingJsons =
            Directory.GetFiles("json/", "*.json");

        double[] d = new double[5];

        foreach (string json in existingJsons)
        {
            try
            {
                var existing =
                    JsonSerializer.Deserialize<TrackFeatures>(
                        System.IO.File.ReadAllText(json));

                //if (existing.Title == Path.GetFileName(file))
                if (!string.IsNullOrWhiteSpace(existing?.AudioPath))
                {
                    if (
                        Path.GetFileNameWithoutExtension(existing.AudioPath)
                        ==
                        Path.GetFileNameWithoutExtension(fileA)
                    )
                    {
                        alreadyProcessed = true;
                        break;
                    }
                }
            }
            catch
            {

            }
        }
        return d;
    }

    public static double[] PowerSpectrum(Complex[] fft)
    {
        int half = fft.Length / 2;
        double[] power = new double[half];

        for (int i = 0; i < half; i++)
        {
            double mag = fft[i].Magnitude;
            power[i] = mag * mag;
        }

        return power;
    }

    public static List<float[]> Extraction(string fileA)
    {
        var samplesA = LoadAudio(fileA);

        //using var r1 = new AudioFileReader(fileA);

        int sampleRate =
    GetSampleRate(fileA);

        var chromaA =
            ExtractChromaSequence(
                samplesA,
                sampleRate);

        //var chromaA = ExtractChromaSequence(samplesA, r1.WaveFormat.SampleRate);

        return chromaA;
    }
}

public class TrackFeatures
{
    public int Id { get; set; }

    public string Title { get; set; }

    public string Artist { get; set; }

    public string AudioPath { get; set; }

    public string StreamUrl { get; set; }

    public int SampleRate { get; set; }

    public int FrameSize { get; set; }

    public int HopSize { get; set; }

    public float[] MeanChroma { get; set; }

    public double[] MeanMFCC { get; set; }

    public double[] DeltaMFCCMean { get; set; }

    public double Energy { get; set; }

    public string BinFile { get; set; }
}

public class SearchResult
{
    public string Title { get; set; }

    public double Similarity { get; set; }

    public string StreamUrl { get; set; }
}

public class Candidate
{
    public TrackFeatures Track { get; set; }

    public double Score { get; set; }
}*/

// =========================
// 9. CONSOLE ENTRY POINT
// =========================
/*class Program 
{
        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.WriteLine(
                        JsonSerializer.Serialize(
                            new
                            {
                                status = "error",
                                message = "No input file"
                            }));

                    return;
                }

                string queryFile = args[0];

            string jsonFolder = "/root/audio-analysis-mobile/audioanaly/bin/Debug/net8.0/json";
            /*
            Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "json");*/

           /* string binFolder = "/root/audio-analysis-mobile/audioanaly/bin/Debug/net8.0/bbin";
            /*
                                Path.Combine(
                                    AppDomain.CurrentDomain.BaseDirectory,
                                    "bin");*/

          /*  string result = 
                    AudioSimilarityAnalyzer
                    .FindBestMatches(
                        queryFile,
                        jsonFolder,
                        binFolder);

                Console.WriteLine(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    JsonSerializer.Serialize(
                        new
                        {
                            status = "error",
                            message = ex.ToString()
                        }));
            }
        }
    
} */
