using Microsoft.EntityFrameworkCore;
using TraineeManagement.api.CustomException;
using TraineeManagement.api.Data;
using TraineeManagement.api.DTO.SubmissionDto;
using TraineeManagement.api.Helper;
using TraineeManagement.api.Models;
using TraineeManagement.api.Redis.CacheKeys;
using TraineeManagement.api.Redis.Repository;
using TraineeManagement.api.Repository.FileStorage;
using TraineeManagement.api.Repository.RabbitMQ;
using TraineeManagement.api.Repository.Submission;
using TraineeManagement.Shared.Contracts;
using TraineeManagement.Shared.RabbitMq;

namespace TraineeManagement.api.Services
{
    public class SubmissionService : ISubmissionService
    {
        private AppDbContext _context;
        private readonly IFileStorageService _storageService;
        private readonly SubmissionFileValidator _submissionFileValidator;
        private readonly IRedisCacheRepo _redisCacheRepo;
        private readonly ILogger<SubmissionService> _logger;
        private readonly IRabbitMqPublisher _rabbitMqPublisher;

        public SubmissionService(
            AppDbContext context,
            IFileStorageService storageService,
            SubmissionFileValidator submissionFileValidator,
            IRedisCacheRepo redisCacheRepo,
            ILogger<SubmissionService> logger,
            IRabbitMqPublisher rabbitMqPublisher
        )
        {
            _context = context;

            _storageService = storageService;

            _submissionFileValidator = submissionFileValidator;

            _redisCacheRepo = redisCacheRepo;

            _logger = logger;

            _rabbitMqPublisher = rabbitMqPublisher;
        }

        public async Task<SubmissionResponse> AddSubmission(CreateSubmissionRequest submissionRequest, List<IFormFile> files, string correlationId)
        {

            DateTime? dueDate = await _context.TaskAssignment
                .Where(t => t.Id == submissionRequest.TaskAssignmentId)
                .Select(t => t.DueDate).FirstOrDefaultAsync();

            if(dueDate == null)
            {
                throw new NotFoundException("Task Assignment not found");
            }
            

            if (submissionRequest.SubmittedDate > dueDate) throw new InvalidRequest("You cannot submit task after due date!");

            //var correlationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-ID"].ToString();
            if (string.IsNullOrEmpty(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
            }

            SubmissionModel submissionModel = new SubmissionModel(
                submissionRequest.TaskAssignmentId,
                submissionRequest.SubmissionUrl,
                submissionRequest.Notes,
                submissionRequest.SubmittedDate,
                Enum.SubmissionStatusEnum.QUEUED
            );

            var physicallyWrittenPaths = new List<string>();

            try
            {
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        if (file.Length == 0) throw new Exception("Invalid File Content");
                        
                        if (_submissionFileValidator.ValidateFileSize(file.Length))
                        {
                            throw new InvalidFileSubmission("File size exceeds the 2 MB limit.");
                        }

                        if (_submissionFileValidator.ValidateFileExtension(file.FileName) == false)
                        {
                            throw new InvalidFileSubmission("Only .txt, .pdf, or .docx files are allowed.");
                        }

                        string checksum = await _storageService.CalculateChecksumAsync(file);

                        if (submissionModel.SubmissionFiles.Any(f => f.Checksum == checksum))
                        {
                            throw new InvalidFileSubmission($"You cannot upload the duplicate file '{file.FileName}' in the same submission.");
                        }

                        string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";

                        string finalRelativePath = await _storageService.SaveFileAsync(file, uniqueFileName);

                        physicallyWrittenPaths.Add(finalRelativePath);

                        var fileModel = new SubmissionFileModel
                        {
                            FileName = file.FileName,
                            FilePath = finalRelativePath,
                            FileSizeInBytes = file.Length,
                            ContentType = file.ContentType,
                            Checksum = checksum,
                            Submission = submissionModel
                        };

                        submissionModel.SubmissionFiles.Add(fileModel);
                    }
                }

                _context.Submission.Add(submissionModel);
                await _context.SaveChangesAsync();

                try
                {
                    if (physicallyWrittenPaths.Any())
                    {
                        var integrationEvent = new SubmissionProcessingRequested
                        {
                            MessageId = Guid.NewGuid(),
                            CorrelationId = correlationId,
                            RequestedAt = DateTime.UtcNow,
                            ContractVersion = "1.0.0",
                            SubmissionId = submissionModel.Id
                        };

                        //Initiate a job
                        ProcessingJobModel job = new ProcessingJobModel(
                            integrationEvent.MessageId,
                            integrationEvent.CorrelationId,
                            0,
                            "",
                            Enum.ProcessingJobStatusEnum.QUEUED,
                            DateTime.UtcNow
                        );

                        _context.ProcessingJob.Add(job);

                        await _context.SaveChangesAsync();

                        await _rabbitMqPublisher.PublishAsync(
                            exchange: RabbitMqConfig.SubmissionExchangeName,
                            routingKey: RabbitMqConfig.SubmissionRoutingKey,
                            message: integrationEvent,
                            correlationId: correlationId
                        );
                    }
                    else
                    {
                        submissionModel.Status = Enum.SubmissionStatusEnum.SUBMITTED;
                        await _context.SaveChangesAsync();
                    }

                }
                catch (Exception ex)
                {

                    _logger.LogError(ex, "Infrastructure failure (RabbitMQ) for Submission ID {id}. Marking as FAILED.", submissionModel.Id);

                    submissionModel.Status = Enum.SubmissionStatusEnum.FAILED;
                    await _context.SaveChangesAsync();

                    throw;
                }


                // removing old cache entry
                await _redisCacheRepo.RemoveItem(SubmissionCacheKey.AllSubmissions);

                return SubmissionModel.ToDto(submissionModel);

            }
            catch (InvalidFileSubmission)
            {
                CleanUpPhysicalFiles(physicallyWrittenPaths);
                throw;
            }
            catch (Exception)
            {
                CleanUpPhysicalFiles(physicallyWrittenPaths);
                throw;
            }
        }

        private void CleanUpPhysicalFiles(List<string> paths)
        {
            foreach (var file in paths)
            {
                try
                {
                    _storageService.DeleteFile(file);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clean up file at path: {Path}", file);
                }
            }
        }

        public async Task<SubmissionResponse> GetSubmissionById(int id)
        {

            string cacheKey = $"{SubmissionCacheKey.SingleSubmission}:{id}";

            SubmissionResponse? cachedData = await _redisCacheRepo.GetItem<SubmissionResponse>(cacheKey);

            if (cachedData != null)
            {
                _logger.LogInformation(
                    "Submission ID {SubmissionId} found in Redis cache using key {RedisKey}.",
                    id,
                    cacheKey
                );

                return cachedData;
            }
            else
            {
                _logger.LogInformation(
                    "Submission ID {SubmissionId} not found in Redis cache using key {RedisKey}. Falling back to MySQL DB to fetch.",
                    id,
                    cacheKey
                );


                SubmissionResponse? submissionResponse = await _context.Submission
                    .Where(s => s.Id == id)
                    .Select(s => new SubmissionResponse(
                        s.Id, 
                        s.TaskAssignmentId, 
                        s.SubmissionUrl, 
                        s.Notes, 
                        s.SubmittedDate, 
                        s.Status
                    ))
                    .FirstOrDefaultAsync();

                if (submissionResponse == null) throw new NotFoundException("Submission Not Found");

                await _redisCacheRepo.SetItem<SubmissionResponse>(cacheKey, submissionResponse);

                return submissionResponse;

            }

        }

        public async Task<IEnumerable<SubmissionResponse>> GetSubmissionList()
        {

            string cacheKey = SubmissionCacheKey.AllSubmissions;

            IEnumerable<SubmissionResponse>? cachedData = await _redisCacheRepo.GetItem<IEnumerable<SubmissionResponse>>(cacheKey);

            if (cachedData != null)
            {

                _logger.LogInformation(
                   "Submission List found in Redis cache using key {RedisKey}.",
                   cacheKey
               );

                return cachedData;
            }
            else
            {
                _logger.LogInformation(
                    "Submission List not found in Redis cache using key {RedisKey}. Falling back to MySQL DB to fetch.",
                    cacheKey
                );


                List<SubmissionResponse> submissionResponse = await _context.Submission
                    .Select(s => new SubmissionResponse(
                        s.Id, 
                        s.TaskAssignmentId, 
                        s.SubmissionUrl, 
                        s.Notes, 
                        s.SubmittedDate, 
                        s.Status
                    ))
                    .ToListAsync();

                await _redisCacheRepo.SetItem<IEnumerable<SubmissionResponse>>(cacheKey, submissionResponse);

                return submissionResponse;

            }


        }

        public async Task<(Stream FileStream, string ContentType, string FileName)> DownloadFileAsync(int fileMetadataId, CancellationToken cancellationToken)
        {
            var fileRecord = await _context.SubmissionFile
                .FirstOrDefaultAsync(f => f.Id == fileMetadataId);

            if (fileRecord == null)
            {
                _logger.LogError(
                    "Submission File Metadata ID {fileMetaId} not found in database.",
                    fileMetadataId
                );

                throw new NotFoundException("The requested file record does not exist.");
            }

            //byte[] fileBytes = await _storageService.DownloadFile(fileRecord.FilePath);

            Stream fileStream = _storageService.DownloadFileStream(fileRecord.FilePath);

            return (fileStream, fileRecord.ContentType, fileRecord.FileName);
        }

        public async Task<bool> DeleteSubmissionAsync(int id)
        {
            SubmissionModel? submissionModel = await _context.Submission.FindAsync(id);

            if (submissionModel == null) throw new NotFoundException("Submission Not Found!. Please Try Again.");

            List<SubmissionFileModel> submissionFiles = await _context.SubmissionFile.Where(s => s.SubmissionId == id).ToListAsync();

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {

                foreach(var fileMetadata in submissionFiles)
                {

                    if (!_storageService.isFileExists(fileMetadata.FilePath)) throw new NotFoundException("Requested File does not exists on server.");

                    _storageService.DeleteFile(fileMetadata.FilePath);

                }

                _context.SubmissionFile.RemoveRange(submissionFiles);
                _context.Submission.Remove(submissionModel);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // remove submission data from cache
                await _redisCacheRepo.RemoveItem(SubmissionCacheKey.AllSubmissions);
                await _redisCacheRepo.RemoveItem($"{SubmissionCacheKey.SingleSubmission}:{id}");

                return true;

            }
            catch (Exception)
            {
                throw;
            }

        }

        public async Task<List<SubmissionResponse>> GetSubmissionByTaskAssignmentId(int taskAssignmentId)
        {

            List<SubmissionResponse> submissionResponse = await _context.Submission
                .Where(s => s.TaskAssignmentId == taskAssignmentId)
                .Select(s => new SubmissionResponse(
                    s.Id, 
                    s.TaskAssignmentId, 
                    s.SubmissionUrl, 
                    s.Notes, 
                    s.SubmittedDate, 
                    s.Status
                ))
                .ToListAsync();

            return submissionResponse;

        }
    }
}