using System;
using System.Collections.Generic;
using System.Numerics;
using NAudio.Wave;
using MathNet.Numerics.IntegralTransforms;
using System.Text.Json;


//заметки на будущее
//звук передается в виде массива амплитуд сигналов в разные промежутки времени (номеры положения амплитуды по порядку)
//sample rate - измерение сигнала в секунду (Герцы)
//

public class AudioSimilarityAnalyzer
{
    // =========================
    // 1. LOAD AUDIO (PCM)
    //функция открывает аудио файл, декодирует его из предыдущего формата в массив чисел
    // =========================
    public static float[] LoadAudio(string path)
    {
        using var reader = new AudioFileReader(path); //открывает файл и декодирует сжатый звук выдавая данные PCM (Pulse Code Modulation)

        var samples = new List<float>();
        float[] buffer = new float[reader.WaveFormat.SampleRate]; //логичное продолжение

        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
                samples.Add(buffer[i]);
        }

        return samples.ToArray(); //на выходе цифровой сигнал
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

    public static double DTW(List<float[]> A, List<float[]> B)
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

        return prev[m - 1] / (m + n);
    }

    // =========================
    // 8. MAIN COMPARISON API
    // =========================
    public static double Compare(string fileA, string fileB)
    {
        var samplesA = LoadAudio(fileA);
        var samplesB = LoadAudio(fileB);

        using var r1 = new AudioFileReader(fileA);
        using var r2 = new AudioFileReader(fileB);

        var chromaA = ExtractChromaSequence(samplesA, r1.WaveFormat.SampleRate);
        var chromaB = ExtractChromaSequence(samplesB, r2.WaveFormat.SampleRate);

        return DTW(chromaA, chromaB);
    }

    public static List<float[]> Extraction(string fileA)
    {
        var samplesA = LoadAudio(fileA);

        using var r1 = new AudioFileReader(fileA);

        var chromaA = ExtractChromaSequence(samplesA, r1.WaveFormat.SampleRate);

        return chromaA;
    }
}

public class TrackFeatures
{
    public string Title { get; set; }

    public string Artist { get; set; }

    public string FilePath { get; set; }

    public int SampleRate { get; set; }

    public int FrameSize { get; set; }

    public int HopSize { get; set; }

    public List<float[]> ChromaSequence { get; set; }
}

// =========================
// 9. CONSOLE ENTRY POINT
// =========================
class Program 
{ 
    static void Main(string[] args) 
    {
        /*try 
        { 
            string filePath = args[0]; 
            // Пока заглушка:
            // позже здесь будет реальный анализ
            var result = new { title = "Unknown", similarity = 0.82, frames = 1240 }; 
            Console.WriteLine( JsonSerializer.Serialize(result) ); 
        } catch (Exception ex) { Console.Error.WriteLine(ex.Message); 
            Environment.Exit(1); 
        } */
        string fileA = "C:\\Users\\User\\source\\repos\\audio-analysis-mobile\\audioanaly\\Music_data\\Fallen down on my handmade piano! [ZjipSUkV2l4].mp3";
        //string fileB = "C:\\Users\\User\\source\\repos\\audio-analysis-mobile\\audioanaly\\Music_data\\Fallen down on my handmade piano! [ZjipSUkV2l4].mp3";
        string fileB = "C:\\Users\\User\\source\\repos\\audio-analysis-mobile\\audioanaly\\Music_data\\kris_piano_waitingroom - Deltarune Chapter 4 [MEXiaOHeYEU].mp3";
        Console.WriteLine(AudioSimilarityAnalyzer.Compare(fileA, fileB));
    } 
}