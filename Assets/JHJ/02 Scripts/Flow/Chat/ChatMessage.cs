
public enum ChatKind
{
    User,
    System
}

public readonly struct ChatMessage
{
    public readonly ChatKind Kind;
    public readonly string Nickname;
    public readonly string Text;
    public readonly bool IsMine;

    private ChatMessage(ChatKind kind, string nickname, string text, bool isMine)
    {
        Kind = kind;
        Nickname = nickname;
        Text = text;
        IsMine = isMine;
    }

    public static ChatMessage CreateUser(string nickname, string text, bool isMine)
    {
        return new ChatMessage(ChatKind.User, nickname, text, isMine);
    }

    public static ChatMessage CreateSystem(string text)
    {
        return new ChatMessage(ChatKind.System, string.Empty, text, false);
    }
}
