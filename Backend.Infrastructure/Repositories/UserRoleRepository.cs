using System;
using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;

namespace Backend.Infrastructure.Repositories;

public class UserRoleRepository : RepositoryBase<UserRole, int>, IUserRoleRepository
{
    public UserRoleRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
    }
}
