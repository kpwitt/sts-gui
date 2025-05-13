using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace StS_GUI_Avalonia;

public partial class PasswordInputDec : Window
{
    private string _pwd = "";

    public PasswordInputDec()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ButtonOK_OnClick(object? sender, RoutedEventArgs e)
    {
        var upperTextBox = this.GetControl<TextBox>("pwInputUpper");
        if (upperTextBox.Text != null) _pwd = upperTextBox.Text;
        Close();
    }

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        _pwd = "";
        Close();
    }

    public async Task<string?> ShowPwdDialog(Window owner)
    {
        await ShowDialog(owner);
        return _pwd;
    }
}