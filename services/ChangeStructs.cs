namespace SchulDB;

public record struct Changes{
    public ChangeKind kind { get; set; }
    public ChangePerson person { get; set; }
    public Kurs kurs { get; set; }
    public int id { get; set; }
    public bool executed { get; set; }
    
    public override string ToString()
    {
        var action = kind switch
        {
            ChangeKind.add => "Add",
            ChangeKind.del => "Del",
            _ => "<?>"
        };

        var who = person switch
        {
            ChangePerson.LuL => "LuL",
            ChangePerson.SuS => "SuS",
            _ => "<?>"
        };

        var status = executed ? "bereits ausgeführt" : "noch nicht ausgeführt";
        var bezeichnung = kurs.Bezeichnung ?? "<ohne Bezeichnung>";

        return $"{action} {who} mit der ID {id} {bezeichnung} {status}";
    }

}

public enum ChangeKind
{
    add,
    del
}

public enum ChangePerson
{
    SuS,
    LuL
}