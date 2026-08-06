using TraineeManagement.api.DTO.SubmissionDto;

namespace TraineeManagement.api.Repository.Submission
{
    public interface ISubmissionService
    {
        public Task<IEnumerable<SubmissionResponse>> GetSubmissionList();
        public Task<SubmissionResponse> GetSubmissionById(int id);
        public Task<List<SubmissionResponse>> GetSubmissionByTaskAssignmentId(int taskAssignmentId);
        public Task<SubmissionResponse> AddSubmission(CreateSubmissionRequest submissionRequest, List<IFormFile> files, string correlationId);

        public Task<(Stream FileStream, string ContentType, string FileName)> DownloadFileAsync(int fileMetadataId, CancellationToken cancellationToken);
        public Task<bool> DeleteSubmissionAsync(int id);
    }

}
