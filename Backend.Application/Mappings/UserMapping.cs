using System;
using System.Globalization;
using Backend.Application.DTOs.Users;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Share.Helpers;

namespace Backend.Application.Mappings;

public static class UserMapping
{
    public static User ToEntity(this CreateUserDto obj)
    {
        return new User
        {
            CreatedBy = obj.CreatedBy,
            Username = obj.Email.ToLower(),
            PasswordHash = obj.PasswordHash,
            FirstName = obj.FirstName,
            LastName = obj.LastName,
            Email = obj.Email.ToLower(),
            PhoneNumber = obj.PhoneNumber,
            IdentityNumber = obj.IdentityNumber,
            Gender = obj.Gender,
            AddresDetail = obj.AddresDetail,
            UserStatusId = (int)Enums.UserStatus.Actived,
            AccessFailedCount = 0,
            LockEnabled = false,
            CreatedDate = DateTime.Now
        };
    }

    public static User ToEntity(this UpdateUserDto obj, User existData)
    {
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        existData.UserStatusId = obj.UserStatusId;
        existData.LockEnabled = obj.LockEnabled;
        existData.LockEndDate = obj.LockEndDate;

        return existData;
    }

    public static UserDetailDto ToDto(this User entity)
    {
        return new UserDetailDto
        {
            Id = entity.Id,
            Username = entity.Username,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            Email = entity.Email,
            PhoneNumber = entity.PhoneNumber,
            AccessFailedCount = entity.AccessFailedCount,
            Gender = entity.Gender,
            LockEndDate = entity.LockEndDate,
            LockEnabled = entity.LockEnabled,
            CreatedDate = entity.CreatedDate,
        };
    }

    //Admin Register User
    public static User ToEntity(this AdminRegisterDto obj)
    {
        return new User
        {
            Username = obj.Username.Trim(),
            PasswordHash = PasswordHelper.HashPassword(obj.Password.Trim()),
            FirstName = obj.FirstName.Trim(),
            LastName = obj.LastName.Trim(),
            Email = obj.Email.Trim(),
            UserStatusId = (int)Enums.UserStatus.NotActivated,
            AccessFailedCount = 0,
            LockEnabled = false,
            //OfficeId = obj.OfficeId,
            CreatedDate = DateTime.Now
        };
    }

    //Register User
    public static User ToEntity(this UserSignUpDto obj)
    {
        return new User
        {
            Username = obj.UserName.Trim(),
            PasswordHash = PasswordHelper.HashPassword(obj.Password.Trim()),
            FirstName = obj.FirstName.Trim(),
            LastName = obj.LastName.Trim(),
            Email = obj.Email.Trim(),
            PhoneNumber = obj.PhoneNumber.Trim(),
            IdentityNumber = obj.IdentityNumber.Trim(),
            UserStatusId = (int)Enums.UserStatus.NotActivated,
            AccessFailedCount = 0,
            LockEnabled = false,
            CreatedDate = DateTime.Now
        };
    }

    //Admin create user
    //Register User
    public static User ToEntity(this CreateEndUserDto obj)
    {
        return new User
        {
            Username = obj.Username.Trim(),
            PasswordHash = PasswordHelper.HashPassword(obj.Password.Trim()),
            FirstName = obj.FirstName.Trim(),
            LastName = obj.LastName.Trim(),
            Email = obj.Email.Trim(),
            PhoneNumber = obj.PhoneNumber != null ? obj.PhoneNumber.Trim() : null,
            IdentityNumber = obj.IdentityNumber.Trim(),
            UserStatusId = (int)Enums.UserStatus.Actived,
            AccessFailedCount = 0,
            AddresDetail = obj.AddressDetail,
            LockEnabled = false,
            CreatedDate = DateTime.Now,
            CreatedBy = obj.CreatedBy,
            //truyền thêm
            DateOfBirth = DateTime.ParseExact(obj.DateOfBirth, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal),
            Gender = obj.Gender,
        };
    }
}
