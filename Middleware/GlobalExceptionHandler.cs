using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using System.Text.Json;
using TraineeManagement.api.CustomException;

namespace TraineeManagement.api.Middleware
{
    public class GlobalExceptionHandler
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);

                if (!context.Response.HasStarted)
                {
                    switch (context.Response.StatusCode)
                    {
                        case StatusCodes.Status401Unauthorized:
                            _logger.LogCritical("User tried to log in but failed (Unauthorized).");
                            await WriteErrorResponse(context, "Unauthorized Action", StatusCodes.Status401Unauthorized, "You are unauthorized to access.");
                            break;

                        case StatusCodes.Status403Forbidden:
                            _logger.LogCritical("ERROR: User tried to log in but failed (Forbidden).");
                            await WriteErrorResponse(context, "Forbidden Action", StatusCodes.Status403Forbidden, "You are forbidden to access the resource.");
                            break;

                        case StatusCodes.Status404NotFound:
                            _logger.LogCritical("ERROR: Invalid Resource Requested");
                            await WriteErrorResponse(context, "Resource Not Found", StatusCodes.Status404NotFound, "Invalid Resource Found.");
                            break;
                    }
                }

            }
            catch (BadHttpRequestException ex)
            {
                _logger.LogWarning(ex, "A bad JSON or model binding request occurred.");

                string technicalMessage = ex.InnerException?.Message ?? ex.Message;
                string clearMessage = $"Invalid input data format: {technicalMessage}";

                await WriteErrorResponse(context, "Bad Request Exception", StatusCodes.Status400BadRequest, clearMessage);
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlException mysqlEx)
            {
                // 1452 is the MySQL error code for a Foreign Key constraint violation
                if (mysqlEx.Number == 1452)
                {
                    _logger.LogWarning("Foreign key violation occurred: {Message}", mysqlEx.Message);
                    await WriteErrorResponse(context, "Bad Request", StatusCodes.Status400BadRequest, "The provided Trainee, Mentor, or Task ID does not exist.");
                }
                else
                {
                    // Handling other general MySQL database update errors
                    _logger.LogError(ex, "A database update error occurred.");
                    await WriteErrorResponse(context, "Database Error", StatusCodes.Status500InternalServerError, "A database processing error occurred.");
                }
            }
            catch (NotFoundException ex)
            {
                _logger.LogError(ex, ex.Message);
                await WriteErrorResponse(context, "Resource Not Found", StatusCodes.Status404NotFound, ex.Message);
            }
            catch(DuplicateUsernameException ex)
            {
                //When user tries to create username that already exists
                _logger.LogError(ex, ex.Message);
                await WriteErrorResponse(context, "Duplicate Username Exception", StatusCodes.Status400BadRequest, ex.Message);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid Request Properties: {Message}", ex.Message);
                await WriteErrorResponse(context, "Bad Request", StatusCodes.Status400BadRequest, "Invalid Request Properties");
            }
            catch(InvalidRequest ex)
            {
                _logger.LogError(ex, ex.Message);
                await WriteErrorResponse(context, "Invalid Request", StatusCodes.Status400BadRequest, ex.Message);
            }
            catch(InvalidFileSubmission ex)
            {
                _logger.LogError(ex, ex.Message);
                await WriteErrorResponse(context, "Invalid Submission File Request", StatusCodes.Status413RequestEntityTooLarge, ex.Message);
            }
            catch(RabbitMqException ex)
            {
                _logger.LogError(ex, ex.Message);
                await WriteErrorResponse(context, "Internal Server Error", StatusCodes.Status500InternalServerError, "Internal Server Error");
            }
            catch(OperationCanceledException ex)
            {
                _logger.LogWarning("Download aborted: User network is gone");

                await WriteErrorResponse(context, "Client Closed Request", StatusCodes.Status499ClientClosedRequest, ex.Message);

            }
            catch (ArgumentException ex)
            {
                // Validation errors instantly returned back to user without looping retries
                _logger.LogWarning($"Validation Error: {ex.Message}");

                await WriteErrorResponse(context, "Validation Error", StatusCodes.Status400BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception escaped the pipeline: {Message}", ex.Message);

                await WriteErrorResponse(context, "Internal Server Error", StatusCodes.Status500InternalServerError, "Internal Server Error");
            }
        }

        private static async Task WriteErrorResponse(HttpContext context, string errorType, int statusCode, string message)
        {
            if (context.Response.HasStarted) return;

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var payload = new
            {
                ErrorType = errorType,
                StatusCode = statusCode,
                Message = message
            };

            var json = JsonSerializer.Serialize(payload);
            await context.Response.WriteAsync(json);
        }
    }
}
