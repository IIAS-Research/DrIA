namespace Server.Services;

public abstract class ApplicationService
{
  public abstract class ResponseServiceAbstract
  {
    public bool IsSuccess { get; set; } = false;
    public List<string> Errors { get; set; } = [];

    public void AddError(string error)
    {
      Errors.Add(error);
    }

    public bool EnsureOtherServiceIsSuccess(ResponseServiceAbstract anotherResponseService)
    {
      if (!anotherResponseService.IsSuccess)
      {
        Errors = anotherResponseService.Errors;
        return false;
      }
      return true;
    }

    // Not used but maybe for another project
    async public Task<ResponseServiceAbstract> TryActionAsync(Func<ResponseServiceAbstract, Task<ResponseServiceAbstract>> action)
    {
      try
      {
        return await action(this);
      }
      catch (Exception exception)
      {
        AddError(exception.Message);
        return this;
      }
    }
  }
  public class ResponseService : ResponseServiceAbstract
  {
    public void SetSuccessful()
    {
      IsSuccess = true;
    }
  }

  public class ResponseService<T> : ResponseServiceAbstract
  {
    public T Data { get; set; }

    public void SetSuccessful(T data)
    {
      Data = data;
      IsSuccess = true;
    }
  }
}

