using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PayFlow.Application.Common.Exceptions;

namespace PayFlow.Api.Filters;

public sealed class GlobalExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        context.Result = context.Exception switch
        {
            TenantAlreadyExistsException exception => new ObjectResult(new { error = exception.Message })
            {
                StatusCode = StatusCodes.Status409Conflict
            },
            UnauthorizedException exception => new ObjectResult(new { error = exception.Message })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            },
            NotFoundException exception => new ObjectResult(new { error = exception.Message })
            {
                StatusCode = StatusCodes.Status404NotFound
            },
            ValidationException exception => new BadRequestObjectResult(new
            {
                errors = exception.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.ErrorMessage).ToArray())
            }),
            _ => new ObjectResult(new { error = "An unexpected error occurred." })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            }
        };

        context.ExceptionHandled = true;
    }
}
