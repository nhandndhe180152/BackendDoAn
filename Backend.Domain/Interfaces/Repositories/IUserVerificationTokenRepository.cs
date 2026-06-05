using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IUserVerificationTokenRepository : IRepositoryBase<UserVerificationToken, int>
{
    Task<DTResult<UserVerificationTokenAggregate>> GetPagedAsync(DTParameter parameters);
}
