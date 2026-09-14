using System.Text.Json;

namespace GolfGrind.App.Services;

public static class AtomicJsonFileStore
{
    public static void Write<T>(string path, T value, JsonSerializerOptions options, Action<T>? validate = null)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("A storage directory is required.");
        Directory.CreateDirectory(directory);
        var temporaryPath = path + ".tmp";
        var goodPath = path + ".good";

        try
        {
            var json = JsonSerializer.Serialize(value, options);
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }

            var roundTrip = JsonSerializer.Deserialize<T>(File.ReadAllText(temporaryPath), options)
                ?? throw new InvalidDataException("The validated storage write deserialized to null.");
            validate?.Invoke(roundTrip);

            if (File.Exists(path))
            {
                File.Copy(path, goodPath, true);
                File.Move(temporaryPath, path, true);
            }
            else
            {
                File.Move(temporaryPath, path);
                File.Copy(path, goodPath, true);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static bool TryRead<T>(string path, JsonSerializerOptions options, out T? value)
    {
        if (TryReadPath(path, options, out value))
            return true;

        var goodPath = path + ".good";
        if (File.Exists(path))
        {
            var quarantinePath = path + $".damaged-{DateTime.UtcNow:yyyyMMddHHmmss}";
            try { File.Move(path, quarantinePath, true); } catch (IOException) { }
        }

        if (!TryReadPath(goodPath, options, out value))
            return false;

        try
        {
            Write(path, value!, options);
        }
        catch (IOException)
        {
            // The recovered value is still usable in memory if another process temporarily holds the path.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep the last-known-good value available without blocking app startup.
        }
        return true;
    }

    private static bool TryReadPath<T>(string path, JsonSerializerOptions options, out T? value)
    {
        value = default;
        if (!File.Exists(path))
            return false;
        try
        {
            value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), options);
            return value is not null;
        }
        catch (JsonException) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
