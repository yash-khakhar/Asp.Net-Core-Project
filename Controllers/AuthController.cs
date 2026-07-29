using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TraineeManagement.api.CustomException;
using TraineeManagement.api.DTO.UserDto;
using TraineeManagement.api.Repository.User;

namespace TraineeManagement.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IUserService userService, ILogger<AuthController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserResponse>> RegisterUser([FromBody] CreateUserRequest user)
        {
            if (user == null)
            {
                throw new InvalidRequest("Please Provide Correct Input Data");
            }

            UserResponse userResponse = await _userService.RegisterUser(user);

            _logger.LogInformation($"Register: {userResponse.UserName} is registered!!");

            return userResponse;
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserResponse>> Login([FromBody] UserLoginRequestDto user)
        {
            if (user == null)
            {
                _logger.LogInformation($"ERROR: Exception in User Login: Invalid Credentials");
                throw new InvalidRequest("Invalid Credentails");
            }

            var userLoginResponse = await _userService.Login(user);
            
            Response.Cookies.Append(
                "access_token", 
                userLoginResponse.Token,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = DateTimeOffset.UtcNow.AddHours(1)
                }
            );

            _logger.LogInformation($"Login: {userLoginResponse.User.UserName} is logged in!!");

            return Ok(userLoginResponse.User);

        }

        [HttpGet("me")]
        public async Task<ActionResult<UserResponse>> Me()
        {
            
            var token = Request.Cookies["access_token"];
            Console.WriteLine("Token: " + token);

            if (String.IsNullOrEmpty(token))
            {
                return Unauthorized();
            }

            var principal = _userService.ValidateToken(token);
            Console.WriteLine(principal);
            
            if(principal == null)
            {
                return Unauthorized();
            }

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            Console.WriteLine("UserId: " + userId);

            if (String.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = await _userService.GetUserById(int.Parse(userId));

            if(user == null)
            {
                return Unauthorized();
            }

            return Ok(user);

        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("access_token");

            return NoContent();
        }
    }
}
