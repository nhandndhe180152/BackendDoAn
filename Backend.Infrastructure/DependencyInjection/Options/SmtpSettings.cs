using System;

namespace Backend.Infrastructure.DependencyInjection.Options;

public class SmtpSettings
{
    public string Server { get; set; }
    public int Port { get; set; }
    public bool UseSsl { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public string SenderName { get; set; }
    public string TargetName { get; set; }
}
