using Avalonia.Platform.Storage;

namespace StS_GUI_Avalonia.services
{
    internal class StSFileTypes
    {
        public static FilePickerFileType DataBaseFile { get; } = new("Database")
        {
            Patterns = new[] { "*.sqlite" },
            AppleUniformTypeIdentifiers = new[] { "public.database" },
            MimeTypes = new[] { "application/vnd.sqlite3" }
        };

        public static FilePickerFileType CSVFile { get; } = new("CSV")
        {
            Patterns = new[] { "*.csv" },
            AppleUniformTypeIdentifiers = new[] { "public.text" },
            MimeTypes = new[] { "text/csv" }
        };

        public static FilePickerFileType EncryptedFile { get; } = new("Aes-File")
        {
            Patterns = new[] { "*.aes" },
            AppleUniformTypeIdentifiers = new[] { "public.data" },
            MimeTypes = new[] { "application/aes" }
        };
    }
}
