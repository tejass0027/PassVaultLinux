namespace PassVaultLinux.Auth;

public enum LoginEventType
{
    Pattern,
    Recovery
}

public class LoginEvent
{
    public long Timestamp { get; set; }
    public LoginEventType Type { get; set; }
    public bool Success { get; set; }
}
