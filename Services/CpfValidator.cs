namespace AuroraGaleria.Services;

public static class CpfValidator
{
    public static bool IsValid(string? value)
    {
        var cpf = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (cpf.Length != 11 || cpf.Distinct().Count() == 1)
        {
            return false;
        }

        return CalculateDigit(cpf, 9) == cpf[9] - '0' && CalculateDigit(cpf, 10) == cpf[10] - '0';
    }

    private static int CalculateDigit(string cpf, int length)
    {
        var sum = 0;
        for (var index = 0; index < length; index++)
        {
            sum += (cpf[index] - '0') * (length + 1 - index);
        }

        var result = 11 - sum % 11;
        return result >= 10 ? 0 : result;
    }
}
