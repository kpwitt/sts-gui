using Avalonia.Platform.Storage;

namespace StS_GUI_Avalonia.services;

internal static class StSFileTypes {
    public static FilePickerFileType DataBaseFile { get; } = new("sqlite") {
        Patterns = ["*.sqlite"],
        AppleUniformTypeIdentifiers = ["public.database"],
        MimeTypes = ["application/vnd.sqlite3"]
    };

    public static FilePickerFileType CSVFile { get; } = new("csv") {
        Patterns = ["*.csv"],
        AppleUniformTypeIdentifiers = ["public.text"],
        MimeTypes = ["text/csv"]
    };

    public static FilePickerFileType EncryptedFile { get; } = new("aes") {
        Patterns = ["*.aes"],
        AppleUniformTypeIdentifiers = ["public.data"],
        MimeTypes = ["application/aes"]
    };

    public static FilePickerFileType JSONFile { get; } = new("json") {
        Patterns = ["*.json"],
        AppleUniformTypeIdentifiers = ["public.json"],
        MimeTypes = ["application/json"]
    };
}