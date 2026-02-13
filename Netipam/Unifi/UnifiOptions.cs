namespace Netipam.Unifi;

public class UnifiOptions
{
    public string BaseUrl { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Site { get; set; } = "default";
    public bool AllowInsecureTls { get; set; }
}

