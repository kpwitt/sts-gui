namespace SchulDB;

public record struct Changes{
    public ChangeKind kind { get; set; }
    public ChangePerson person { get; set; }
    public Kurs kurs { get; set; }
    public int id { get; set; }
    public bool executed { get; set; }
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