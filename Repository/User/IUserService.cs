using System.Security.Claims;
using TraineeManagement.api.DTO.UserDto;

namespace TraineeManagement.api.Repository.User
{
    public interface IUserService
    {
        public Task<UserResponse> RegisterUser(CreateUserRequest newUser);
        public Task<UserLoginResponse> Login(UserLoginRequestDto userDto);

        public ClaimsPrincipal? ValidateToken(string token);

        public Task<UserResponse> GetUserById(int userId);
    }
}
