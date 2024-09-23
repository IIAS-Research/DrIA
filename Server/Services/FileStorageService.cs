// using Server.DAL.Db;

// namespace Server.Services;

// public class FileStorageService : ApplicationService
// {
//   private static readonly string DefaultStorageFolder = "Default";
//   private readonly IConfiguration _config;

//   public FileStorageService(IConfiguration config, MainContext dbContext)
//   {
//     _config = config;
//   }

//   public async Task<FileStorageResponseWrite> WriteFileToStorageAsync(IFormFile formfile, string? storageFolderName)
//   {
//     var writeResponse = new FileStorageResponseWrite();
//     var randomFilename = Path.GetRandomFileName();
//     var filePath = Path.Combine(storageFolderName ?? DefaultStorageFolder, randomFilename);
//     var filePathInStorage = Path.Combine(GetMainPathStorage(_config), filePath);
//     var mimeType = MimeTypes.GetMimeType(formfile.FileName);
//     using (var stream = File.Create(filePathInStorage))
//     {
//       await formfile.CopyToAsync(stream);
//     }
//     writeResponse.SetSuccessTrue();
//     writeResponse.FilePath = filePath;
//     writeResponse.MimeType = mimeType;
//     return writeResponse;
//   }

//   public async Task<FileStorageResponseWrite> WriteUserAvatarFileToStorageAsync(IFormFile formfile)
//   {
//     var writeResponse = new FileStorageResponseWrite();
//     var responseFileOk = IaModelFileOptions.EnsureFileIsOk(formfile);
//     if (responseFileOk.IsSuccess == false)
//     {
//       writeResponse.Errors = responseFileOk.Errors;
//       return writeResponse;
//     }
//     var relativePath = IaModelFileOptions.GetRelativePath();
//     return await WriteFileToStorageAsync(formfile, relativePath);
//   }

//   public async Task<FileStorageResponseWrite> WriteQuotationFileToStorageAsync(IFormFile formfile)
//   {
//     var writeResponse = new FileStorageResponseWrite();
//     var responseFileOk = UserFileOptions.EnsureFileIsOk(formfile);
//     if (responseFileOk.IsSuccess == false)
//     {
//       writeResponse.Errors = responseFileOk.Errors;
//       return writeResponse;
//     }
//     var relativePath = UserFileOptions.GetRelativePath();
//     return await WriteFileToStorageAsync(formfile, relativePath);
//   }

//   public ResponseService EraseFileFromStorage(string fileRelativePath)
//   {
//     var eraseResponse = new ResponseService();
//     var filePath = Path.Combine(GetMainPathStorage(_config), fileRelativePath);
//     if (!File.Exists(filePath))
//     {
//       eraseResponse.AddError("File does not exist");
//       return eraseResponse;
//     }
//     File.Delete(filePath);
//     eraseResponse.IsSuccess = true;
//     return eraseResponse;
//   }

//   public async Task<FileStorageResponseGet> GetFileFromStorageAsync(string fileRelativePath)
//   {
//     var getResponse = new FileStorageResponseGet();
//     var filePath = Path.Combine(GetMainPathStorage(_config), fileRelativePath);
//     if (!File.Exists(filePath))
//     {
//       getResponse.AddError("File does not exist");
//       return getResponse;
//     }
//     getResponse.IsSuccess = true;
//     getResponse.FileBytes = await File.ReadAllBytesAsync(filePath);
//     return getResponse;
//   }



//   // Utils
//   private static string GetMainPathStorage(IConfiguration config)
//   {
//     var pathStorage = config[Configuration.PathMainStorage];
//     if (pathStorage == null)
//     {
//       throw new Exception($"'{Configuration.PathMainStorage}' is not set!");
//     }
//     return pathStorage;
//   }

//   public static void EnsureStorageDirectoryAreCreated(IConfiguration config)
//   {
//     var storageFolders = new List<string>() {
//             IaModelFileOptions.GetRelativePath(),
//             UserFileOptions.GetRelativePath(),
//             DefaultStorageFolder
//         };
//     foreach (var folder in storageFolders)
//     {
//       Directory.CreateDirectory(Path.Combine(GetMainPathStorage(config), folder));
//     }
//   }



//   // Service responses
//   public class FileStorageResponseWrite : ResponseService
//   {
//     public string? FilePath;
//     public string? MimeType;
//     public bool FilePathIsSet()
//     {
//       return !string.IsNullOrEmpty(FilePath);
//     }
//     public bool MiumeTypeIsSet()
//     {
//       return !string.IsNullOrEmpty(MimeType);
//     }
//   }

//   public class FileStorageResponseGet : ResponseService
//   {
//     public byte[]? FileBytes;

//     public bool FileBytesIsSet()
//     {
//       return FileBytes != null && FileBytes.Length > 0;
//     }
//   }



//   // Options
//   private class UserFileOptions
//   {
//     private readonly static string Folder = "UserFile";
//     private readonly static string[] PermittedExtensions = [".csv", ".xlsx"];
//     private readonly static long MaxFileLength = 100L * 1024L * 1024L; // 100Mb

//     public static string GetRelativePath()
//     {
//       return Folder;
//     }

//     public static ResponseService EnsureFileIsOk(IFormFile formfile)
//     {
//       var responseService = new ResponseService();
//       if (formfile.Length == 0)
//       {
//         responseService.AddError("File is tempty");
//       }
//       if (!PermittedExtensions.Contains(Path.GetExtension(formfile.FileName)))
//       {
//         responseService.AddError("File extension is not accepted");
//       }
//       if (formfile.Length > MaxFileLength)
//       {
//         responseService.AddError($"File is too big ( size > {MaxFileLength})");
//       }
//       return responseService;
//     }
//   }

//   private class IaModelFileOptions
//   {
//     private readonly static string Folder = "IaFile";
//     private readonly static string[] PermittedExtensions = [".pickle"];
//     private readonly static long MaxFileLength = 1L * 1024L * 1024L; // 100Mb

//     public static string GetRelativePath()
//     {
//       return Folder;
//     }

//     public static ResponseService EnsureFileIsOk(IFormFile formfile)
//     {
//       var responseService = new ResponseService();
//       if (formfile.Length == 0)
//       {
//         responseService.AddError("File is tempty");
//       }
//       if (!PermittedExtensions.Contains(Path.GetExtension(formfile.FileName)))
//       {
//         responseService.AddError("File extension is not accepted");
//       }
//       if (formfile.Length > MaxFileLength)
//       {
//         responseService.AddError($"File is too big ( size > {MaxFileLength})");
//       }
//       return responseService;
//     }
//   }
// }