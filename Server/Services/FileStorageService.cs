using MimeTypes;

namespace Server.Services;

public class FileStorageService : ApplicationService
{
    private static readonly string DefaultStorageFolder = "Default";
    private readonly IConfiguration _config;

    public FileStorageService(IConfiguration config)
    {
        _config = config;
    }

    public async Task<FileStorageResponseWrite> WriteFileToStorageAsync(IFormFile formfile, string? storageFolderName)
    {
        var writeResponse = new FileStorageResponseWrite();
        var randomFilename = Path.GetRandomFileName();
        var filePath = Path.Combine(storageFolderName ?? DefaultStorageFolder, randomFilename);
        var filePathInStorage = Path.Combine(GetMainPathStorage(_config), filePath);
        var mimeType = MimeTypeMap.GetMimeType(formfile.FileName);
        using (var stream = File.Create(filePathInStorage))
        {
            await formfile.CopyToAsync(stream);
        }
        writeResponse.SetSuccessful();
        writeResponse.FilePath = filePath;
        writeResponse.MimeType = mimeType;
        return writeResponse;
    }

    public async Task<FileStorageResponseWrite> WriteModelBinaryToStorageAsync(IFormFile formfile)
    {
        var writeResponse = new FileStorageResponseWrite();
        var responseFileOk = ModelBinaryFileOptions.EnsureFileIsOk(formfile);
        if (responseFileOk.IsSuccess == false)
        {
            writeResponse.Errors = responseFileOk.Errors;
            return writeResponse;
        }
        var relativePath = ModelBinaryFileOptions.GetRelativePath();
        return await WriteFileToStorageAsync(formfile, relativePath);
    }

    public async Task<FileStorageResponseWrite> WriteModelImageToStorageAsync(IFormFile formfile)
    {
        var writeResponse = new FileStorageResponseWrite();
        var responseFileOk = ModelImageFileOptions.EnsureFileIsOk(formfile);
        if (responseFileOk.IsSuccess == false)
        {
            writeResponse.Errors = responseFileOk.Errors;
            return writeResponse;
        }
        var relativePath = ModelImageFileOptions.GetRelativePath();
        return await WriteFileToStorageAsync(formfile, relativePath);
    }

    public async Task<FileStorageResponseWrite> WriteProjectFileToStorageAsync(IFormFile formfile)
    {
        var writeResponse = new FileStorageResponseWrite();
        var responseFileOk = ProjectFileOptions.EnsureFileIsOk(formfile);
        if (responseFileOk.IsSuccess == false)
        {
            writeResponse.Errors = responseFileOk.Errors;
            return writeResponse;
        }
        var relativePath = ProjectFileOptions.GetRelativePath();
        return await WriteFileToStorageAsync(formfile, relativePath);
    }

    public ResponseService EraseFileFromStorage(string fileRelativePath)
    {
        var eraseResponse = new ResponseService();
        var filePath = Path.Combine(GetMainPathStorage(_config), fileRelativePath);
        if (!File.Exists(filePath))
        {
            eraseResponse.AddError("File does not exist");
            return eraseResponse;
        }
        File.Delete(filePath);
        eraseResponse.IsSuccess = true;
        return eraseResponse;
    }

    public async Task<FileStorageResponseGet> GetFileFromStorageAsync(string fileRelativePath)
    {
        var getResponse = new FileStorageResponseGet();
        var filePath = Path.Combine(GetMainPathStorage(_config), fileRelativePath);
        if (!File.Exists(filePath))
        {
            getResponse.AddError("File does not exist");
            return getResponse;
        }
        getResponse.IsSuccess = true;
        getResponse.FileBytes = await File.ReadAllBytesAsync(filePath);
        return getResponse;
    }



    // Utils
    private static string GetMainPathStorage(IConfiguration config)
    {
        var pathStorage = config[Configuration.PathMainStorage];
        if (pathStorage == null)
        {
            throw new Exception($"'{Configuration.PathMainStorage}' is not set!");
        }
        return pathStorage;
    }

    public static void EnsureStorageDirectoryAreCreated(IConfiguration config)
    {
        var storageFolders = new List<string>() {
            ModelBinaryFileOptions.GetRelativePath(),
            ModelImageFileOptions.GetRelativePath(),
            ProjectFileOptions.GetRelativePath(),
            DefaultStorageFolder
        };
        foreach (var folder in storageFolders)
        {
            Directory.CreateDirectory(Path.Combine(GetMainPathStorage(config), folder));
        }
    }



    // Service responses
    public class FileStorageResponseWrite : ResponseService
    {
        public string FilePath = string.Empty;
        public string MimeType = string.Empty;
        public bool FilePathIsSet()
        {
            return !string.IsNullOrEmpty(FilePath);
        }
        public bool MimeTypeIsSet()
        {
            return !string.IsNullOrEmpty(MimeType);
        }
    }

    public class FileStorageResponseGet : ResponseService
    {
        public byte[]? FileBytes;

        public bool FileBytesIsSet()
        {
            return FileBytes != null && FileBytes.Length > 0;
        }
    }



    // Options
    private static class ProjectFileOptions
    {
        private readonly static string Folder = "UserFile";
        private readonly static string[] PermittedExtensions = [".csv", ".xlsx"];
        private readonly static long MaxFileLength = 100L * 1024L * 1024L; // 100Mb

        public static string GetRelativePath()
        {
            return Folder;
        }

        public static ResponseService EnsureFileIsOk(IFormFile formfile)
        {
            var responseService = new ResponseService();
            if (formfile.Length == 0)
            {
                responseService.AddError("File is tempty");
            }
            if (!PermittedExtensions.Contains(Path.GetExtension(formfile.FileName)))
            {
                responseService.AddError("File extension is not accepted");
            }
            if (formfile.Length > MaxFileLength)
            {
                responseService.AddError($"File is too big ( size > {MaxFileLength})");
            }
            return responseService;
        }
    }

    private static class ModelBinaryFileOptions
    {
        public readonly static string Folder = "ModelFile";
        private readonly static string SubFolderBinary = "Binary";
        private readonly static string[] PermittedExtensions = [".pickle"];
        private readonly static long MaxFileLength = 100L * 1024L * 1024L; // 100Mb

        public static string GetRelativePath()
        {
            return Path.Combine(Folder, SubFolderBinary);
        }

        public static ResponseService EnsureFileIsOk(IFormFile formfile)
        {
            var responseService = new ResponseService();
            if (formfile.Length == 0)
            {
                responseService.AddError("File is tempty");
            }
            if (!PermittedExtensions.Contains(Path.GetExtension(formfile.FileName)))
            {
                responseService.AddError("File extension is not accepted");
            }
            if (formfile.Length > MaxFileLength)
            {
                responseService.AddError($"File is too big ( size > {MaxFileLength})");
            }
            return responseService;
        }
    }

    private static class ModelImageFileOptions
    {
        private readonly static string SubFolder = "Images";
        private readonly static string[] PermittedExtensions = [".png", ".jpeg"];
        private readonly static long MaxFileLength = 5L * 1024L * 1024L; // 5Mb

        public static string GetRelativePath()
        {
            return Path.Combine(ModelBinaryFileOptions.Folder, SubFolder);
        }

        public static ResponseService EnsureFileIsOk(IFormFile formfile)
        {
            var responseService = new ResponseService();
            if (formfile.Length == 0)
            {
                responseService.AddError("File is tempty");
            }
            if (!PermittedExtensions.Contains(Path.GetExtension(formfile.FileName)))
            {
                responseService.AddError("File extension is not accepted");
            }
            if (formfile.Length > MaxFileLength)
            {
                responseService.AddError($"File is too big ( size > {MaxFileLength})");
            }
            return responseService;
        }
    }

}