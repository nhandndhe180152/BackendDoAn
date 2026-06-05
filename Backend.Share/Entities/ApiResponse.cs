using System;
using System.Net;

namespace Backend.Share.Entities;

public class ApiResponse
{
    public int Status { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
    public object? Resources { get; set; }
    public object? Errors { get; set; }
    public bool IsSucceeded { get; set; }

    public static ApiResponse Success<T>(T? data = default, string message = "Success", int status = (int)HttpStatusCode.OK)
    {
        return new ApiResponse
        {
            IsSucceeded = true,
            Message = message,
            Resources = data,
            Status = status,
            Code = "CMN_200"
        };
    }
    public static ApiResponse Success(string message = "Success", int status = (int)HttpStatusCode.OK)
    {
        return new ApiResponse
        {
            IsSucceeded = true,
            Message = message,
            Resources = null,
            Status = status,
            Code = "CMN_200"
        };
    }

    public static ApiResponse Created<T>(T? data = default, string message = "Created")
    {
        return new ApiResponse
        {
            IsSucceeded = true,
            Message = message,
            Resources = data,
            Status = (int)HttpStatusCode.Created,
            Code = "CMN_201"
        };
    }
    public static ApiResponse Created(string message = "Created")
    {
        return new ApiResponse
        {
            IsSucceeded = true,
            Message = message,
            Status = (int)HttpStatusCode.Created,
            Code = "CMN_201"
        };
    }

    public static ApiResponse Error(string message = "Error", int status = (int)HttpStatusCode.BadRequest, string? code = null)
    {
        return new ApiResponse
        {
            IsSucceeded = false,
            Message = message,
            Errors = null,
            Status = status,
            Code = code
        };
    }

    public static ApiResponse Error<T>(T? errors = default, string message = "Error", int status = (int)HttpStatusCode.BadRequest, string? code = null)
    {
        return new ApiResponse
        {
            IsSucceeded = false,
            Message = message,
            Errors = errors,
            Status = status,
            Code = code
        };
    }

    public static ApiResponse NotFound<T>(T? errors = default, string message = "Not Found", string? code = null)
    {
        return Error<T>(errors, message, (int)HttpStatusCode.NotFound, code ?? "CMN_404");
    }

    public static ApiResponse NotFound(string message = "Not Found", string? code = null)
    {
        return Error(message, (int)HttpStatusCode.NotFound, code ?? "CMN_404");
    }

    public static ApiResponse Unauthorized<T>(T? errors = default, string message = "Unauthorized", string? code = null)
    {
        return Error<T>(errors, message, (int)HttpStatusCode.Unauthorized, code ?? "CMN_401");
    }
    public static ApiResponse Unauthorized(string message = "Unauthorized", string? code = null)
    {
        return Error(message, (int)HttpStatusCode.Unauthorized, code ?? "CMN_401");
    }

    public static ApiResponse Forbidden<T>(T? errors = default, string message = "Forbidden", string? code = null)
    {
        return Error<T>(errors, message, (int)HttpStatusCode.Forbidden, code ?? "CMN_403");
    }

    public static ApiResponse Forbidden(string message = "Forbidden", string? code = null)
    {
        return Error(message, (int)HttpStatusCode.Forbidden, code ?? "CMN_403");
    }

    public static ApiResponse UnprocessableEntity(List<FormErrorMessage> errors, string message = "Unprocessable entity", string? code = null)
    {
        return Error(errors, message, (int)HttpStatusCode.UnprocessableEntity, code ?? "CMN_422");
    }
    public static ApiResponse UnprocessableEntity(string message = "Unprocessable entity", string? code = null)
    {
        return Error(message, (int)HttpStatusCode.UnprocessableEntity, code ?? "CMN_422");
    }

    public static ApiResponse InternalServerError()
    {
        return new ApiResponse
        {
            IsSucceeded = false,
            Message = "Internal server error",
            Errors = null,
            Status = (int)HttpStatusCode.InternalServerError,
            Code = "CMN_500"
        };
    }

    public static ApiResponse BadRequest<T>(T? errors = default, string message = "Bad request", string? code = null)
    {
        return Error(errors, message, (int)HttpStatusCode.BadRequest, code ?? "CMN_400");
    }
    public static ApiResponse BadRequest(string message = "Bad request", string? code = null)
    {
        return Error(message, (int)HttpStatusCode.BadRequest, code ?? "CMN_400");
    }

}

public class FormErrorMessage
{
    public List<string> Message { get; set; } = new List<string>();
    public string Field { get; set; } = null!;
}
