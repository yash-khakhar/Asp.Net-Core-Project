using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TraineeManagement.api.CustomException;
using TraineeManagement.api.Data;
using TraineeManagement.api.DTO.UserDto;
using TraineeManagement.api.Enum;
using TraineeManagement.api.Enum.Mentor;
using TraineeManagement.api.Helper;
using TraineeManagement.api.models;
using TraineeManagement.api.Models;
using TraineeManagement.api.Redis.CacheKeys;
using TraineeManagement.api.Redis.Repository;
using TraineeManagement.api.Repository.Password;
using TraineeManagement.api.Repository.User;

namespace TraineeManagement.api.Services
{
    public class UserService : IUserService
    {

        private AppDbContext _context;
        private IPasswordService _passwordService;
        private readonly IConfiguration _config;

        private readonly IRedisCacheRepo _redisCacheRepo;

        public UserService(
            AppDbContext context, 
            IPasswordService passwordService, 
            IConfiguration config,
            IRedisCacheRepo redisCacheRepo
        )
        {
            _context = context;
            _passwordService = passwordService;
            _config = config;
            _redisCacheRepo = redisCacheRepo;
           
        }

        private async Task<UserModel> FindByUsername(string userName)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName.Equals(userName));
            if (user == null) throw new NotFoundException("Invalid username or password");
            return user;
        }

        private async Task<bool> isUserPresent(string username)
        {
            return _context.Users.Any(u => u.UserName.Equals(username));
            
        }

        public async Task<UserLoginResponse> Login(UserLoginRequestDto userDto)
        {

            if (!userDto.UserName.IsOnlyLetters())
            {
                throw new InvalidRequest("Invalid Username");
            }

            UserModel user = await FindByUsername(userDto.UserName);

            if (!await _passwordService.VerifyPassword(userDto.Password, user))
            {
                throw new NotFoundException("Invalid username or password");
            }

            string jwtToken = JwtHelper.GenerateJwtToken(user, _config);

            var jwtSettings = _config.GetSection("JwtSettings");

            var expiry = jwtSettings["ExpiryMinutes"];

            return new UserLoginResponse(jwtToken, Convert.ToInt32(expiry) * 60, new UserResponse(user.Id, user.UserName, user.Role));

        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            return JwtHelper.ValidateToken(token, _config);
        }

        public async Task<UserResponse> RegisterUser(CreateUserRequest newUser)
        {

            if (!newUser.Username.IsOnlyLetters())
            {
                throw new InvalidRequest("Invalid Username");
            }

            if (!newUser.FirstName.IsOnlyLetters())
            {
                throw new InvalidRequest("Enter Proper FirstName");
            }

            if (!newUser.LastName.IsOnlyLetters())
            {
                throw new InvalidRequest("Enter Proper LastName");
            }

            if (!newUser.Email.IsValidEmail())
            {
                throw new InvalidRequest("Invalid Email Address");
            }

            if (!newUser.TechStack.IsOnlyLetters())
            {
                throw new InvalidRequest("Enter Proper Tech Stack");
            }

            if (await isUserPresent(newUser.Username))
            {
                throw new DuplicateUsernameException($"The username '{newUser.Username}' is already taken.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                UserModel userModel = new UserModel(
                    newUser.Username,
                    newUser.Email,
                    _passwordService.HashPassword(newUser.Password),
                    newUser.Role
                )
                {
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(userModel);
                await _context.SaveChangesAsync();

                switch (newUser.Role)
                {
                    case UserRolesEnum.TRAINEE:
                        TraineeModel trainee = new TraineeModel(
                            userModel.Id,
                            newUser.FirstName,
                            newUser.LastName,
                            newUser.Email,
                            newUser.TechStack,
                            newUser.Status == UserStatusEnum.ACTIVE ? TraineeStatusEnum.ACTIVE : TraineeStatusEnum.INACTIVE
                        );

                        _context.Trainees.Add(trainee);
                        // remove from redis if user is trainee
                        await _redisCacheRepo.RemoveByPatternAsync($"{TraineeCacheKey.AllTrainees}*");
                       
                        break;

                    case UserRolesEnum.MENTOR:
                        MentorModel mentor = new MentorModel(
                            userModel.Id,
                            newUser.FirstName,
                            newUser.LastName,
                            newUser.Email,
                            newUser.TechStack,
                            newUser.Status == UserStatusEnum.ACTIVE ? MentorStatusEnum.ACTIVE : MentorStatusEnum.INACTIVE
                        );
                        
                        _context.Mentor.Add(mentor);
                        
                        break;

                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return UserModel.ToDto(userModel);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<UserResponse> GetUserById(int userId)
        {
            var user = await _context.Users.FindAsync(userId);

            if(user == null) throw new InvalidRequest("You are unauthoried to access");

            return UserModel.ToDto(user);
        }
    }
}
