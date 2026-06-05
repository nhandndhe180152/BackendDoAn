using System;

namespace Backend.Share.Entities.Abstractions;

public interface IMailRequest
{
    List<string> ToEmails { get; set; }
    List<string> CcEmails { get; set; }
    List<string> BccEmails { get; set; }
    string Subject { get; set; }
    string Body { get; set; }
}
