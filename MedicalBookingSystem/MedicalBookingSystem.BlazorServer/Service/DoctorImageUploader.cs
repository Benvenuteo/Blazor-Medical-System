namespace MedicalBookingSystem.BlazorServer.Service
{
    public class DoctorImageUploader
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public DoctorImageUploader(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
        {
            _env = env;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<string?> SaveDoctorImageAsync(Stream stream, string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                return null;

            var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "doctors");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var newFileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(uploadsFolder, newFileName);

            using (var fileStream = new FileStream(fullPath, FileMode.Create))
            {
                await stream.CopyToAsync(fileStream);
            }

            var request = _httpContextAccessor.HttpContext?.Request
                ?? throw new InvalidOperationException("The current request is unavailable. Configure a public media URL before uploading outside an HTTP request.");
            var baseUrl = $"{request.Scheme}://{request.Host}";
            return $"{baseUrl}/images/doctors/{newFileName}";
        }
    }

}
