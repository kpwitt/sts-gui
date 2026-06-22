/*SchildToSchule (StS) - Programm zur Verwaltung von Schüler:innen/Lehrkräfte und Kursdaten
   Copyright (C) 2026 Kay-Patrick Wittbold

   This program is free software: you can redistribute it and/or modify
   it under the terms of the GNU General Public License as published by
   the Free Software Foundation, either version 3 of the License, or
   (at your option) any later version.

   This program is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU General Public License for more details.

   You should have received a copy of the GNU General Public License
   along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using Avalonia.Platform.Storage;

// ReSharper disable InconsistentNaming

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