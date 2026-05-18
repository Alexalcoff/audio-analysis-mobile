/*using System.IO;
using System.Text.Json;

class Duplicatore
{
    static void Main()
    {
        var data = new
        {
            title = "Song",
            score = 0.95,
            features = new
            {
                tempo = 120,
                key = "C Major",
                mode = "Major",
                time_signature = "4/4"
            },

        };

        string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

        Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, "result.json");

        File.WriteAllText(path, json);
    }
}*/