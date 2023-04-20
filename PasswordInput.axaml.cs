using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace StS_GUI_Avalonia;

public partial class PasswordInput : Window
{
    private string pwd = "";
    
    public PasswordInput()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    private void ButtonOK_OnClick(object? sender, RoutedEventArgs e)
    {
        var upperTextBox = this.GetControl<TextBox>("pwInputUpper");
        var lowerTextBox = this.GetControl<TextBox>("pwInputLower");
        if (upperTextBox.Text != lowerTextBox.Text) return;
        pwd = upperTextBox.Text;
        Close();
    }

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        pwd = "";
        Close();
    }

    public async Task<string?> ShowPWDDialog(Window owner)
    {
        await ShowDialog(owner);
        return pwd;
    }
}