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
namespace SchulDB;

// ReSharper disable InconsistentNaming
public readonly record struct Changes(ChangeKind kind, ChangePerson person, Kurs kurs, int id) {
    public override string ToString() {
        var action = kind switch {
            ChangeKind.add => "Add",
            ChangeKind.del => "Del",
            _ => "<?>"
        };

        var who = person switch {
            ChangePerson.LuL => "LuL",
            ChangePerson.SuS => "SuS",
            _ => "<?>"
        };
        var bezeichnung = kurs.Bezeichnung;

        return $"{action} {who} mit der ID {id} {bezeichnung}";
    }
}

public enum ChangeKind {
    add,
    del
}

public enum ChangePerson {
    SuS,
    LuL
}