using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PropertySystem.Services
{
    // 1. 定义抽象接口 (不管底层是本地还是阿里云，只管调接口)
    public interface IFileStorageService
    {
        Task<string> UploadFileAsync(IFormFile file, string folderName);
    }

    // 2. 本地实现类 (以后换阿里云，只需建个 AliOssService 替换注入即可)
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;

        public LocalFileStorageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0) return null;

            // 模拟 OSS 的分片上传与唯一命名机制
            string uploadsFolder = Path.Combine(_env.WebRootPath, "oss_bucket", folderName);
            Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 返回虚拟的 CDN 路径
            return $"/oss_bucket/{folderName}/{uniqueFileName}";
        }
    }
}
