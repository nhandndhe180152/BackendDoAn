using System;

namespace Backend.Application.Interfaces;

public interface IEmailService<in T> where T : class
{
    Task SendMailAsync(T request);
}
