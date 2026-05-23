using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.IO;
using MathNet.Numerics.IntegralTransforms;
using NAudio.Wave;

public class FileNames
{
    // =========================================================
    // CONFIG
    // =========================================================

    private const int FRAME_SIZE = 2048;
    private const int HOP_SIZE = 512;

    private const int PRESELECT_COUNT = 25;

    // =========================================================
    // AUDIO LOAD
    // =========================================================

    public static float[] LoadAudio(string path)
    {
        using var reader =
            new AudioFileReader(path);

        List<float> samples = new();

        float[] buffer = new float[4096];

        int read;

        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
                samples.Add(buffer[i]);
        }

        // stereo -> mono normalization
        NormalizeAudio(samples);

        return samples.ToArray();
    }

    private static void NormalizeAudio(List<float> samples)
    {
        float max = 0;

        foreach (float s in samples)
            max = Math.Max(max, Math.Abs(s));

        if (max < 1e-9f)
            return;

        for (int i = 0; i < samples.Count; i++)
            samples[i] /= max;
    }

    public static int GetSampleRate(string path)
    {
        using var reader =
            new AudioFileReader(path);

        return reader.WaveFormat.SampleRate;
    }

    // =========================================================
    // FRAMING
    // =========================================================

    public static List<float[]> Frame(
        float[] signal,
        int frameSize = FRAME_SIZE,
        int hop = HOP_SIZE)
    {
        var frames = new List<float[]>();

        for (int i = 0; i + frameSize < signal.Length; i += hop)
        {
            float[] frame = new float[frameSize];

            Array.Copy(signal, i, frame, 0, frameSize);

            ApplyHann(frame);

            frames.Add(frame);
        }

        return frames;
    }

    private static void ApplyHann(float[] frame)
    {
        int n = frame.Length;

        for (int i = 0; i < n; i++)
        {
            double w =
                0.5 *
                (1 - Math.Cos(2 * Math.PI * i / (n - 1)));

            frame[i] *= (float)w;
        }
    }

    // =========================================================
    // FFT
    // =========================================================

    public static Complex[] FFT(float[] frame)
    {
        Complex[] complex =
            new Complex[frame.Length];

        for (int i = 0; i < frame.Length; i++)
            complex[i] =
                new Complex(frame[i], 0);

        Fourier.Forward(
            complex,
            FourierOptions.Matlab);

        return complex;
    }

    // =========================================================
    // CHROMA
    // =========================================================

    public static float[] Chroma(
        Complex[] fft,
        int sampleRate)
    {
        float[] chroma = new float[12];

        for (int i = 1; i < fft.Length / 2; i++)
        {
            double freq =
                i * sampleRate /
                (double)fft.Length;

            if (freq < 40 || freq > 5000)
                continue;

            int note =
                (int)(
                    12 *
                    Math.Log(freq / 440.0, 2)
                    + 69);

            note %= 12;

            if (note < 0)
                note += 12;

            double mag =
                fft[i].Magnitude;

            // лог-компрессия
            mag = Math.Log10(1 + mag);

            chroma[note] +=
                (float)mag;
        }

        Normalize(chroma);

        return chroma;
    }

    // =========================================================
    // CHROMA SEQUENCE
    // =========================================================

    public static List<float[]> ExtractChromaSequence(
        float[] samples,
        int sampleRate)
    {
        var frames =
            Frame(samples);

        var result =
            new List<float[]>(frames.Count);

        foreach (var frame in frames)
        {
            var fft =
                FFT(frame);

            var chroma =
                Chroma(fft, sampleRate);

            result.Add(chroma);
        }

        return result;
    }

    // =========================================================
    // DISTANCE
    // =========================================================

    public static double Distance(
        float[] a,
        float[] b)
    {
        double dot = 0;
        double na = 0;
        double nb = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        if (na < 1e-12 || nb < 1e-12)
            return 1;

        double cosine =
            dot /
            (Math.Sqrt(na) * Math.Sqrt(nb));

        cosine =
            Math.Clamp(cosine, -1, 1);

        return 1.0 - cosine;
    }

    // =========================================================
    // DTW
    // =========================================================

    public static double DTW(
        List<float[]> A,
        List<float[]> B)
    {
        int n = A.Count;
        int m = B.Count;

        if (n == 0 || m == 0)
            return double.MaxValue;

        int w =
            Math.Max(
                50,
                Math.Abs(n - m));

        double[] prev =
            new double[m];

        double[] curr =
            new double[m];

        int[] prevLen =
            new int[m];

        int[] currLen =
            new int[m];

        for (int j = 0; j < m; j++)
        {
            prev[j] = double.MaxValue;
            prevLen[j] = 0;
        }

        prev[0] =
            Distance(A[0], B[0]);

        prevLen[0] = 1;

        for (int i = 1; i < n; i++)
        {
            Array.Fill(curr, double.MaxValue);
            Array.Fill(currLen, 0);

            int start =
                Math.Max(1, i - w);

            int end =
                Math.Min(m - 1, i + w);

            for (int j = start; j <= end; j++)
            {
                double cost =
                    Distance(A[i], B[j]);

                double best =
                    prev[j];

                int bestLen =
                    prevLen[j];

                if (curr[j - 1] < best)
                {
                    best = curr[j - 1];
                    bestLen = currLen[j - 1];
                }

                if (prev[j - 1] < best)
                {
                    best = prev[j - 1];
                    bestLen = prevLen[j - 1];
                }

                curr[j] =
                    cost + best;

                currLen[j] =
                    bestLen + 1;
            }

            (prev, curr) =
                (curr, prev);

            (prevLen, currLen) =
                (currLen, prevLen);
        }

        return
            prev[m - 1] /
            Math.Max(prevLen[m - 1], 1);
    }

    // =========================================================
    // VECTOR HELPERS
    // =========================================================

    private static void Normalize(float[] v)
    {
        double norm = 0;

        for (int i = 0; i < v.Length; i++)
            norm += v[i] * v[i];

        norm = Math.Sqrt(norm);

        if (norm < 1e-12)
            return;

        for (int i = 0; i < v.Length; i++)
            v[i] /= (float)norm;
    }

    private static double[] Normalize(double[] v)
    {
        double norm = 0;

        for (int i = 0; i < v.Length; i++)
            norm += v[i] * v[i];

        norm = Math.Sqrt(norm);

        if (norm < 1e-12)
            return v;

        for (int i = 0; i < v.Length; i++)
            v[i] /= norm;

        return v;
    }

    // =========================================================
    // FEATURE VECTOR
    // =========================================================

    public static double[] MergeFeatures(
        TrackFeatureS t)
    {
        List<double> result = new();

        // chroma
        if (t.MeanChroma != null)
        {
            double[] c =
                t.MeanChroma
                .Select(x => (double)x)
                .ToArray();

            Normalize(c);

            foreach (double x in c)
                result.Add(x * 2.0);
        }

        // mfcc
        if (t.MeanMFCC != null)
        {
            double[] mfcc =
                t.MeanMFCC.ToArray();

            Normalize(mfcc);

            foreach (double x in mfcc)
                result.Add(x);
        }

        // delta
        if (t.DeltaMFCCMean != null)
        {
            double[] d =
                t.DeltaMFCCMean.ToArray();

            Normalize(d);

            foreach (double x in d)
                result.Add(x * 0.5);
        }

        // energy
        result.Add(
            Math.Log10(
                1 + Math.Abs(t.Energy)));

        return Normalize(
            result.ToArray());
    }

    // =========================================================
    // COSINE
    // =========================================================

    public static double CosineSimilarity(
        double[] a,
        double[] b)
    {
        double dot = 0, dotA = 0, dotB = 0;

        for (int i = 0; i < a.Length; i++)
            dot += a[i] * b[i];
        for (int i = 0; i < a.Length; i++)
            dotA += a[i] * a[i];
        for (int i = 0; i < a.Length; i++)
            dotB += b[i] * b[i];

        return dot/dotA/dotB;
    }

    // =========================================================
    // BIN LOAD
    // =========================================================

    public static List<float[]> LoadChromaFromBin(
        string path)
    {
        using var br =
            new BinaryReader(
                File.OpenRead(path));

        int count =
            br.ReadInt32();

        List<float[]> result =
            new(count);

        for (int i = 0; i < count; i++)
        {
            float[] frame =
                new float[12];

            for (int j = 0; j < 12; j++)
                frame[j] =
                    br.ReadByte() / 255f;

            Normalize(frame);

            result.Add(frame);
        }

        return result;
    }

    // =========================================================
    // FEATURE EXTRACTION
    // =========================================================

    public static TrackFeatureS BuildFeatures(
        string file)
    {
        float[] samples =
            LoadAudio(file);

        int sampleRate =
            GetSampleRate(file);

        List<float[]> chroma =
            ExtractChromaSequence(
                samples,
                sampleRate);

        float[] meanChroma =
            new float[12];

        foreach (var c in chroma)
        {
            for (int i = 0; i < 12; i++)
                meanChroma[i] += c[i];
        }

        if (chroma.Count > 0)
        {
            for (int i = 0; i < 12; i++)
                meanChroma[i] /= chroma.Count;
        }

        Normalize(meanChroma);

        return new TrackFeatureS
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

    // =========================================================
    // FAST SEARCH
    // =========================================================

    public static List<CandidatE> FindTopCandidates(
        TrackFeatureS query,
        string jsonFolder,
        int top = PRESELECT_COUNT)
    {
        List<CandidatE> result =
            new();

        double[] q =
            MergeFeatures(query);

        foreach (string json in Directory.GetFiles(jsonFolder, "*.json"))
        {
            try
            {
                TrackFeatureS track =
                    JsonSerializer.Deserialize<TrackFeatureS>(
                        File.ReadAllText(json));

                if (track == null)
                    continue;

                double[] v =
                    MergeFeatures(track);

                double sim =
                    CosineSimilarity(q, v);

                result.Add(
                    new CandidatE
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
    }

    // =========================================================
    // MAIN SEARCH
    // =========================================================

    public static string FindBestMatches(
        string queryFile,
        string jsonFolder,
        string binFolder)
    {
        List<SearchResulT> results =
            new();

        TrackFeatureS query =
            BuildFeatures(queryFile);

        int sr =
            GetSampleRate(queryFile);

        List<float[]> queryChroma =
            ExtractChromaSequence(
                LoadAudio(queryFile),
                sr);

        // =========================
        // PRESELECT
        // =========================

        List<CandidatE> top =
            FindTopCandidates(
                query,
                jsonFolder,
                PRESELECT_COUNT);

        // =========================
        // DTW FINAL
        // =========================

        foreach (CandidatE c in top)
        {
            try
            {
                string binPath =
                    Path.Combine(
                        binFolder,
                        c.Track.BinFile);

                if (!File.Exists(binPath))
                    continue;

                List<float[]> other =
                    LoadChromaFromBin(binPath);

                double dtw =
                    DTW(queryChroma, other);

                double similarity =
                    Math.Exp(-dtw * 3.0);

                // бонус если почти одинаковы
                similarity *=
                    c.Score;

                results.Add(
                    new SearchResulT
                    {
                        Title = c.Track.Title,
                        Similarity = similarity,
                        StreamUrl = c.Track.StreamUrl
                    });
            }
            catch
            {

            }
        }

        // remove duplicates
        results =
            results
            .GroupBy(x => x.Title)
            .Select(g => g
                .OrderByDescending(x => x.Similarity)
                .First())
            .OrderByDescending(x => x.Similarity)
            .Take(10)
            .ToList();

        return JsonSerializer.Serialize(
            results,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }
}

// =========================================================
// DATA
// =========================================================

public class TrackFeatureS
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

public class SearchResulT
{
    public string Title { get; set; }

    public double Similarity { get; set; }

    public string StreamUrl { get; set; }
}

public class CandidatE
{
    public TrackFeatureS Track { get; set; }

    public double Score { get; set; }
}

// =========================================================
// MAIN
// =========================================================

class FileName
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

            string queryFile =
                args[0];

            string jsonFolder =
                "/root/audio-analysis-mobile/audioanaly/bin/Debug/net8.0/json";

            string binFolder =
                "/root/audio-analysis-mobile/audioanaly/bin/Debug/net8.0/bbin";

            string result =
                FileNames
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
}
