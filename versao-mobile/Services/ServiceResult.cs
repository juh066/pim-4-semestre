namespace AuroraGaleria.Services;

public record ServiceResult<T>(bool Success, string Code, string Message, T? Value = default)
{
    public static ServiceResult<T> Ok(T value, string message = "") => new(true, "ok", message, value);
    public static ServiceResult<T> Fail(string code, string message, T? value = default) => new(false, code, message, value);
}
