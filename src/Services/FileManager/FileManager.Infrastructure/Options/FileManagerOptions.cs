namespace FileManager.Infrastructure.Options;

public class FileManagerOptions
{
    public string RootStoragePath { get; set; } = "FileStorage";
    public string FilesSavePath { get; set; } = "Files";
    public long MaxFileSizeBytes { get; set; } = 104857600; // 100 MB default
    public List<string> AllowedExtensions { get; set; } = new();
    public Dictionary<string, string> ExtensionToTypeMapping { get; set; } = new();
}
