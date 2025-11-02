namespace SchulDB;

public record struct Changes(
    ChangeKind kind,
    ChangePerson person,
    Kurs kurs,
    int id,
    bool executed = false
);

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