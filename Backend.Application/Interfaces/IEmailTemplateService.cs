using System;

namespace Backend.Application.Interfaces;

public interface IEmailTemplateService
{
    Task<string> GetEmailTemplateAsync(string templateName);
    Task<string> GetEmailTemplateAsync<T>(string templateName, T model);
}
