using Microsoft.EntityFrameworkCore;
using TraineeManagement.api.CustomException;
using TraineeManagement.api.Data;
using TraineeManagement.api.DTO.TraineeDto;
using TraineeManagement.api.Enum;
using TraineeManagement.api.Helper;
using TraineeManagement.api.models;
using TraineeManagement.api.Redis.CacheKeys;
using TraineeManagement.api.Redis.Repository;
using TraineeManagement.api.Repository.Trainee;

namespace TraineeManagement.api.Services
{
    public class TraineeServices : ITraineeService
    {
        private AppDbContext _context;
        private readonly IRedisCacheRepo _redisCacheRepo;
        private readonly ILogger<SubmissionService> _logger;

        public TraineeServices(AppDbContext context, IRedisCacheRepo redisCacheRepo, ILogger<SubmissionService> logger)
        {
            _context = context;
            _redisCacheRepo = redisCacheRepo;
            _logger = logger;
        }

        private async Task<TraineeModel> FindTraineeById(int id)
        {

            TraineeModel? trainee = await _context.Trainees.FindAsync(id);
            if (trainee == null) throw new NotFoundException("Trainee Not Found");
            else
            {
                return trainee;
            }
        }

        public async Task<TraineeResponse> AddTrainee(CreateTraineeRequest trainee)
        {
            if (trainee.Status == null) throw new InvalidRequest("Enter Proper Trainee Details.");

            if (!trainee.FirstName.IsOnlyLetters())
            {
                throw new InvalidRequest("Enter Proper FirstName");
            }

            if (!trainee.LastName.IsOnlyLetters())
            {
                throw new InvalidRequest("Enter Proper LastName");
            }

            if (!trainee.Email.IsValidEmail())
            {
                throw new InvalidRequest("Enter Proper Email Address");
            }

            if (!trainee.TechStack.IsOnlyLetters())
            {
                throw new InvalidRequest("Enter Proper Tech Stack");
            }


            TraineeModel traineeModel = new TraineeModel(
                    trainee.FirstName.ToLower(),
                    trainee.LastName.ToLower(),
                    trainee.Email.ToLower(),
                    trainee.TechStack.ToLower(),
                    trainee.Status
                );
            traineeModel.CreatedAt = DateTime.UtcNow;
            traineeModel.UpdatedAt = DateTime.UtcNow;

            _context.Trainees.Add(traineeModel);
            await _context.SaveChangesAsync();

            // removing all trainee key from cache
            await _redisCacheRepo.RemoveByPatternAsync($"{TraineeCacheKey.AllTrainees}*");

            return TraineeModel.ToDto(traineeModel);
        }

        public async Task<bool> DeleteTraineeById(int id)
        {
            TraineeModel? trainee = await _context.Trainees.FindAsync(id);

            if (trainee == null) return false;

            _context.Trainees.Remove(trainee);

            await _context.SaveChangesAsync();

            // removing all trainee key from cache
            await _redisCacheRepo.RemoveByPatternAsync($"{TraineeCacheKey.AllTrainees}*");
            await _redisCacheRepo.RemoveItem($"{TraineeCacheKey.SingleTrainee}:{id}");

            return true;
        }

        public async Task<TraineeResponse> GetTraineeById(int id)
        {

            string cacheKey = $"{TraineeCacheKey.SingleTrainee}:{id}";

            TraineeResponse? cachedData = await _redisCacheRepo.GetItem<TraineeResponse>(cacheKey);

            if (cachedData != null)
            {
                return cachedData;
            }
            else
            {

                _logger.LogInformation(
                    "Trainee ID {TraineeId} not found in Redis cache using key {RedisKey}. Falling back to MySQL DB to fetch.",
                    id,
                    cacheKey
                );


                TraineeResponse? trainee = await _context.Trainees
                 .Where(p => p.Id == id)
                .Select(p => new TraineeResponse(p.Id, p.FirstName, p.LastName, p.Email, p.TechStack, p.Status, p.CreatedAt, p.UpdatedAt))
                 .FirstOrDefaultAsync();

                if (trainee == null) throw new NotFoundException("Trainee Not Found");
                else
                {

                    await _redisCacheRepo.SetItem<TraineeResponse>(cacheKey, trainee);

                    return trainee;

                }

            }

        }

        public async Task<TraineeResponse> UpdateTrainee(UpdateTraineeRequest updateTraineeRequest)
        {
            var trainee = await FindTraineeById(updateTraineeRequest.Id);

            if (updateTraineeRequest.FirstName != null)
            {

                if (!updateTraineeRequest.FirstName.IsOnlyLetters())
                {
                    throw new InvalidRequest("Enter Proper FirstName");
                }

                trainee.FirstName = updateTraineeRequest.FirstName;
            }

            if (updateTraineeRequest.LastName != null)
            {

                if (!updateTraineeRequest.LastName.IsOnlyLetters())
                {
                    throw new InvalidRequest("Enter Proper LastName");
                }

                trainee.LastName = updateTraineeRequest.LastName;
            }

            if (updateTraineeRequest.Email != null)
            {

                if (!updateTraineeRequest.Email.IsValidEmail())
                {
                    throw new InvalidRequest("Enter Proper Email Address");
                }

                trainee.Email = updateTraineeRequest.Email;
            }

            trainee.Status = (TraineeStatusEnum)updateTraineeRequest.Status;

            if (updateTraineeRequest.TechStack != null)
            {

                Console.WriteLine("Tech Stack: " + updateTraineeRequest.TechStack);
                
                if (!updateTraineeRequest.TechStack.IsOnlyLetters())
                {
                    throw new InvalidRequest("Enter Proper Tech Stack");
                }

                trainee.TechStack = updateTraineeRequest.TechStack;
            }

            trainee.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _redisCacheRepo.RemoveByPatternAsync($"{TraineeCacheKey.AllTrainees}*");
            await _redisCacheRepo.RemoveItem($"{TraineeCacheKey.SingleTrainee}:{updateTraineeRequest.Id}");

            return TraineeModel.ToDto(trainee);
        }

        public async Task<TraineeSearchResultDto> GetTraineesAsync(int pageNumber, int pageSize, string? search, TraineeStatusEnum? status)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 10;

            string cacheKey = $"{TraineeCacheKey.AllTrainees}:p{pageNumber}_s{pageSize}_q{search ?? "all"}_st{status?.ToString() ?? "all"}";

            var cachedData = await _redisCacheRepo.GetItem<TraineeSearchResultDto>(cacheKey);
            if (cachedData != null)
            {
                _logger.LogInformation("Trainee search/list found in Redis cache using key {RedisKey}.", cacheKey);
                return cachedData;
            }

            _logger.LogInformation("Trainee search/list not found in Redis. Falling back to DB.");

            var query = _context.Trainees.AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string lowerSearch = search.ToLower();
                query = query.Where(t =>
                    t.FirstName.ToLower().Contains(lowerSearch) ||
                    t.LastName.ToLower().Contains(lowerSearch) ||
                    t.Email.ToLower().Contains(lowerSearch) ||
                    t.TechStack.ToLower().Contains(lowerSearch)
                );
            }

            int totalRecords = await query.CountAsync();
            int skip = (pageNumber - 1) * pageSize;

            var traineeList = await query
                .Skip(skip)
                .Take(pageSize)
                .Select(p => new TraineeResponse(
                    p.Id,
                    p.FirstName,
                    p.LastName,
                    p.Email,
                    p.TechStack,
                    p.Status ?? TraineeStatusEnum.ACTIVE,
                    p.CreatedAt,
                    p.UpdatedAt
                ))
                .ToListAsync();

            var result = new TraineeSearchResultDto(pageNumber, pageSize, totalRecords, traineeList);

            await _redisCacheRepo.SetItem(cacheKey, result);

            return result;
        }
    }
}
