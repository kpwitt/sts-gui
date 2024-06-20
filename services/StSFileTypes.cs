using Avalonia.Platform.Storage;

namespace StS_GUI_Avalonia.services;

internal class StSFileTypes
{
    public static FilePickerFileType DataBaseFile { get; } = new("sqlite")
    {
        Patterns = new[] { "*.sqlite" },
        AppleUniformTypeIdentifiers = new[] { "public.database" },
        MimeTypes = new[] { "application/vnd.sqlite3" }
    };

    public static FilePickerFileType CSVFile { get; } = new("csv")
    {
        Patterns = new[] { "*.csv" },
        AppleUniformTypeIdentifiers = new[] { "public.text" },
        MimeTypes = new[] { "text/csv" }
    };

    public static FilePickerFileType EncryptedFile { get; } = new("aes")
    {
        Patterns = new[] { "*.aes" },
        AppleUniformTypeIdentifiers = new[] { "public.data" },
        MimeTypes = new[] { "application/aes" }
    };
        
    public static FilePickerFileType JSONFile { get; } = new("json")
    {
        Patterns = new[] { "*.config" },
        AppleUniformTypeIdentifiers = new[] { "public.json" },
        MimeTypes = new[] { "application/json" }
    };
}