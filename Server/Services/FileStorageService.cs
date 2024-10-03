using MimeTypes;
using Server.Models;

namespace Server.Services;

public class FileStorageService : ApplicationService
{
  private static readonly string DefaultStorageFolder = "Default";
  private readonly IConfiguration _config;

  public FileStorageService(IConfiguration config)
  {
    _config = config;
  }

  public async Task<ResponseService<FileStorageResponseWrite>> WriteFileToStorageAsync(IFormFile formfile, string? storageFolderName)
  {
    var writeResponse = new ResponseService<FileStorageResponseWrite>();
    var randomFilename = Path.GetRandomFileName();
    var filePath = Path.Combine(storageFolderName ?? DefaultStorageFolder, randomFilename);
    var filePathInStorage = Path.Combine(GetMainPathStorage(_config), filePath);
    var mimeType = MimeTypeMap.GetMimeType(formfile.FileName);
    using (var stream = File.Create(filePathInStorage))
    {
      await formfile.CopyToAsync(stream);
    }
    writeResponse.SetSuccessful(new FileStorageResponseWrite
    {
      FilePath = filePath,
      OriginalFilename = formfile.FileName,
      MimeType = mimeType
    });
    return writeResponse;
  }

  public async Task<ResponseService<FileStorageResponseWrite>> WriteModelBinaryToStorageAsync(IFormFile formfile)
  {
    var writeResponse = new ResponseService<FileStorageResponseWrite>();
    var storageOptions = new ModelBinaryFileOptions();

    var responseFileOk = storageOptions.EnsureFileIsOk(formfile);
    if (responseFileOk.IsSuccess == false)
    {
      writeResponse.Errors = responseFileOk.Errors;
      return writeResponse;
    }
    var relativePath = storageOptions.GetRelativePath();
    return await WriteFileToStorageAsync(formfile, relativePath);
  }

  public async Task<ResponseService<FileStorageResponseWrite>> WriteModelImageToStorageAsync(IFormFile formfile)
  {
    var writeResponse = new ResponseService<FileStorageResponseWrite>();
    var storageOptions = new ModelImageFileOptions();

    var responseFileOk = storageOptions.EnsureFileIsOk(formfile);
    if (responseFileOk.IsSuccess == false)
    {
      writeResponse.Errors = responseFileOk.Errors;
      return writeResponse;
    }
    var relativePath = storageOptions.GetRelativePath();
    return await WriteFileToStorageAsync(formfile, relativePath);
  }

  public async Task<ResponseService<FileStorageResponseWrite>> WriteProjectFileToStorageAsync(IFormFile formfile, ProjectFile.EProjectFileType type)
  {
    var writeResponse = new ResponseService<FileStorageResponseWrite>();
    GenericFileOptions storageOptions;
    switch (type)
    {
      case ProjectFile.EProjectFileType.DEFAULT:
        storageOptions = new ProjectDefaultFileOptions();
        break;

      case ProjectFile.EProjectFileType.TRAIN:
        storageOptions = new ProjectTrainFileOptions();
        break;

      case ProjectFile.EProjectFileType.TEST:
        storageOptions = new ProjectTestFileOptions();
        break;

      default:
        storageOptions = new ProjectDefaultFileOptions();
        break;
    }
    var responseFileOk = storageOptions.EnsureFileIsOk(formfile);
    if (responseFileOk.IsSuccess == false)
    {
      writeResponse.Errors = responseFileOk.Errors;
      return writeResponse;
    }
    var relativePath = storageOptions.GetRelativePath();
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
    eraseResponse.SetSuccessful();
    return eraseResponse;
  }

  public async Task<ResponseService<FileStorageResponseGet>> GetFileFromStorageAsync(string fileRelativePath)
  {
    var getResponse = new ResponseService<FileStorageResponseGet>();
    var filePath = Path.Combine(GetMainPathStorage(_config), fileRelativePath);
    if (!File.Exists(filePath))
    {
      getResponse.AddError("File does not exist");
      return getResponse;
    }
    getResponse.SetSuccessful(new FileStorageResponseGet
    {
      FileBytes = await File.ReadAllBytesAsync(filePath)
    });
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
            new ModelBinaryFileOptions().GetRelativePath(),
            new ModelImageFileOptions().GetRelativePath(),
            new ProjectDefaultFileOptions().GetRelativePath(),
            DefaultStorageFolder
        };
    foreach (var folder in storageFolders)
    {
      Directory.CreateDirectory(Path.Combine(GetMainPathStorage(config), folder));
    }
  }



  // Service responses
  public class FileStorageResponseWrite
  {
    public string FilePath = string.Empty;
    public string OriginalFilename = string.Empty;
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

  public class FileStorageResponseGet
  {
    public byte[]? FileBytes;

    public bool FileBytesIsSet()
    {
      return FileBytes != null && FileBytes.Length > 0;
    }
  }



  // Options
  public class ProjectDefaultFileOptions : GenericFileOptions
  {
    protected override string[] FolderPath { get; set; } = ["ProjectFile", "Default"];
    protected override string[] PermittedExtensions { get; set; } = [".csv"];
    protected override long MaxFileLength { get; set; } = 100L * 1024L * 1024L; // 100Mb
  }

  public class ProjectTrainFileOptions : GenericFileOptions
  {
    protected override string[] FolderPath { get; set; } = ["ProjectFile", "Train"];
    protected override string[] PermittedExtensions { get; set; } = [".csv"];
    protected override long MaxFileLength { get; set; } = 100L * 1024L * 1024L; // 100Mb
  }

  public class ProjectTestFileOptions : GenericFileOptions
  {
    protected override string[] FolderPath { get; set; } = ["ProjectFile", "Test"];
    protected override string[] PermittedExtensions { get; set; } = [".csv"];
    protected override long MaxFileLength { get; set; } = 100L * 1024L * 1024L; // 100Mb
  }

  public class ModelBinaryFileOptions : GenericFileOptions
  {
    protected override string[] FolderPath { get; set; } = ["ModelFile", "Binary"];
    protected override string[] PermittedExtensions { get; set; } = [".pickle"];
    protected override long MaxFileLength { get; set; } = 100L * 1024L * 1024L; // 100Mb
  }

  public class ModelImageFileOptions : GenericFileOptions
  {
    protected override string[] FolderPath { get; set; } = ["ModelFile", "Images"];
    protected override string[] PermittedExtensions { get; set; } = [".png", ".jpeg"];
    protected override long MaxFileLength { get; set; } = 5L * 1024L * 1024L; // 5Mb
  }

  public abstract class GenericFileOptions
  {
    protected virtual string[] FolderPath { get; set; } = [];
    protected virtual string[] PermittedExtensions { get; set; } = [];
    protected virtual long MaxFileLength { get; set; }

    public virtual string GetRelativePath()
    {
      return Path.Combine(FolderPath);
    }

    public virtual ResponseService EnsureFileIsOk(IFormFile formfile)
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
      responseService.SetSuccessful();
      return responseService;
    }
  }
}