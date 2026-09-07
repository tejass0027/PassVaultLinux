using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PassVaultLinux.Views;

public partial class PasswordGeneratorWindow : Window
{
    private const string Lower = "abcdefghijklmnopqrstuvwxyz";
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{}?";

    public PasswordGeneratorWindow()
    {
        InitializeComponent();
        Opened += (_, _) => Regenerate();
    }

    private void Option_Changed(object? sender, RoutedEventArgs e) => Regenerate();

    private void Regenerate()
    {
        // The checkboxes fire their Checked event while this window is still being built,
        // before every named field below is assigned yet - guard against that instead of
        // just the two fields used first (that alone crashes on DigitsCheck/SymbolsCheck).
        if (LengthText == null || GeneratedText == null || UpperCheck == null || DigitsCheck == null || SymbolsCheck == null)
        {
            return;
        }
        int length = (int)LengthSlider.Value;
        LengthText.Text = length.ToString();

        var pool = new StringBuilder(Lower);
        if (UpperCheck.IsChecked == true)
        {
            pool.Append(Upper);
        }
        if (DigitsCheck.IsChecked == true)
        {
            pool.Append(Digits);
        }
        if (SymbolsCheck.IsChecked == true)
        {
            pool.Append(Symbols);
        }

        var poolString = pool.ToString();
        var result = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            result.Append(poolString[RandomNumberGenerator.GetInt32(poolString.Length)]);
        }
        GeneratedText.Text = result.ToString();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);

    private void Use_Click(object? sender, RoutedEventArgs e) => Close(GeneratedText.Text);
}
