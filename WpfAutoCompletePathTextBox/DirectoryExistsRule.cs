using System.Windows.Controls;
using System.IO;
using System.Globalization;

namespace WpfAutoCompletePathTextBox;

public sealed class DirectoryExistsRule : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        string? message;

        try
        {
            if (value is string path)
            {
                message = Directory.Exists(path) ? null : "Directory not found.";
            }
            else
            {
                message = "Invalid path.";
            }
        }
        catch (Exception)
        {
            message = "Invalid path.";
        }

        bool isValid = message is null;
        return new(isValid, message);
    }
}
