using System;
using Backend.Share.Entities.Abstractions;

namespace Backend.Share.Entities;

public class GoogleMailRequest : IMailRequest
{
    public List<string> ToEmails { get; set; } = new List<string>();
    public List<string> CcEmails { get; set; } = new List<string>();
    public List<string> BccEmails { get; set; } = new List<string>();
    public string Subject { get; set; }
    public string Body { get; set; }
}
