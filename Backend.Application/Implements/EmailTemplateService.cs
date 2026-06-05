using System;
using Backend.Application.Interfaces;
using HandlebarsDotNet;

namespace Backend.Application.Implements;

public class EmailTemplateService : IEmailTemplateService
{
    public async Task<string> GetEmailTemplateAsync(string templateName)
    {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "EmailTemplates", $"{templateName}.html");

        if (!File.Exists(path))
            throw new FileNotFoundException($"Template '{templateName}' not found at {path}");

        return await File.ReadAllTextAsync(path);
    }

    public async Task<string> GetEmailTemplateAsync<T>(string templateName, T model)
    {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "EmailTemplates", $"{templateName}.html");

        if (!File.Exists(path))
            throw new FileNotFoundException($"Template '{templateName}' not found at {path}");

        var templateContent = await File.ReadAllTextAsync(path);
        var template = Handlebars.Compile(templateContent);
        var body = template(model);

        return body;
    }
}
