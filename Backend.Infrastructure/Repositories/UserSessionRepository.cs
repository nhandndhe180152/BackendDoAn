using System;
using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;

namespace Backend.Infrastructure.Repositories;

public class UserSessionRepository : RepositoryBase<UserSession, int>, IUserSessionRepository
{
    public UserSessionRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
    }
}
