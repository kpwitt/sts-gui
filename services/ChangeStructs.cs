namespace SchulDB;

public record struct Changes {
    public ChangeKind kind { get; set; }
    public ChangePerson person { get; set; }
    public Kurs kurs { get; set; }
    public int id { get; set; }

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